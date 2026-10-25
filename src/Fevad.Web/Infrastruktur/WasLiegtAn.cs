using System.Security.Claims;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Pages.Termine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Startseite „Was liegt an“: je Bereich nur Anzahlen, und nur aus Bereichen, für die das Konto die Erinnerungsmail bekäme
/// bzw. die Liste lesen darf. Namen stehen erst auf den verlinkten Seiten. Eigene Fristen und Rückmeldungen sieht jeder.
/// </summary>
public sealed class WasLiegtAn(
    AppDbContext db,
    IEnumerable<IFristQuelle> quellen,
    Personalfristenabfrage personalfristen,
    Atemschutzabfrage atemschutz,
    Terminlader termine,
    ParameterLeser parameter,
    IAuthorizationService autorisierung,
    TimeProvider zeit)
{
    /// <summary>Ein Punkt der Liste; <paramref name="Rot"/> ist überfällig, <paramref name="Gelb"/> steht bald an.</summary>
    public sealed record Punkt(string Titel, string Seite, int Rot, int Gelb, string? Zusatz = null);

    /// <summary>Eine eigene Frist des angemeldeten Mitglieds.</summary>
    public sealed record EigeneFrist(string Bezeichnung, DateOnly? FaelligAm, Fristampel Ampel);

    /// <summary>Alles, was der Kasten „Was liegt an“ zeigt.</summary>
    /// <param name="Punkte">Punkte aus den Bereichen.</param>
    /// <param name="EigeneFristen">Eigene Fristen des Mitglieds.</param>
    /// <param name="OffeneRueckmeldungen">Termine, zu denen die eigene Rückmeldung fehlt.</param>
    /// <param name="VorschauTage">Wie viele Tage voraus „steht bald an“ reicht.</param>
    public sealed record Stand(IReadOnlyList<Punkt> Punkte, IReadOnlyList<EigeneFrist> EigeneFristen, int OffeneRueckmeldungen, int VorschauTage)
    {
        /// <summary>Es liegt nichts an.</summary>
        public bool Leer => Punkte.Count == 0 && EigeneFristen.Count == 0 && OffeneRueckmeldungen == 0;
    }

    private static readonly Dictionary<string, string> Seiten = new()
    {
        ["Personal"] = "/Personal/Index",
        ["Mitgliedschaft"] = "/Laufbahn/Index",
        ["Ehrungen"] = "/Laufbahn/Index",
        ["Beförderungen"] = "/Laufbahn/Index",
        ["Technik"] = "/Technik/Index",
        ["Kleiderkammer"] = "/Kleiderkammer/Fristen",
    };

    /// <summary>Lädt den Stand für den Benutzer.</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Stand.</returns>
    public async Task<Stand> LadenAsync(ClaimsPrincipal benutzer, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var rechte = new Dictionary<string, bool>();
        async Task<bool> Darf(string policy)
        {
            if (!rechte.TryGetValue(policy, out var darf))
            {
                rechte[policy] = darf = (await autorisierung.AuthorizeAsync(benutzer, policy)).Succeeded;
            }

            return darf;
        }

        var punkte = new List<Punkt>();
        var hinweise = new List<Fristhinweis>();
        foreach (var quelle in quellen)
        {
            foreach (var h in await quelle.FaelligeAsync(heute, ct))
            {
                if (await Darf(h.Empfaenger))
                {
                    hinweise.Add(h);
                }
            }
        }

        punkte.AddRange(hinweise
            .GroupBy(h => h.Bereich)
            .OrderBy(g => g.Key)
            .Select(g => new Punkt($"Fristen {g.Key}", Seiten.GetValueOrDefault(g.Key, "/Index"),
                g.Count(h => h.Ampel == Fristampel.Rot), g.Count(h => h.Ampel != Fristampel.Rot))));

        if (await Darf(Berechtigungen.AntraegeBearbeiten)
            && await db.Antraege.CountAsync(a => a.Status == AntragStatus.Eingegangen, ct) is > 0 and var antraege)
        {
            punkte.Add(new("Anträge zu entscheiden", "/Antraege/Index", 0, antraege));
        }

        if (await Darf(Berechtigungen.UnfaelleLesen))
        {
            var frist = await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, heute, 3, ct);
            var offen = await db.Unfaelle.AsNoTracking().Where(u => u.Status != UnfallStatus.Abgeschlossen).ToListAsync(ct);
            if (offen.Count > 0)
            {
                var ueberfaellig = offen.Count(u => u.AnzeigeAmpel(heute, frist) == Fristampel.Rot);
                punkte.Add(new("Dienstunfälle offen", "/Unfaelle/Index", ueberfaellig, offen.Count - ueberfaellig,
                    ueberfaellig > 0 ? "Unfallanzeige überfällig" : null));
            }
        }

        var alsKommando = await Darf(Berechtigungen.AtemschutzBestaetigenKommando);
        var alsWart = await Darf(Berechtigungen.AtemschutzBestaetigenWart);
        if ((alsKommando || alsWart) && await atemschutz.Unbestaetigt(alsKommando, alsWart).CountAsync(ct) is > 0 and var unbestaetigt)
        {
            punkte.Add(new("Atemschutznachweise zu bestätigen", "/Atemschutz/Index", 0, unbestaetigt));
        }

        if (await Darf(Berechtigungen.AtemschutzBearbeiten) && await atemschutz.GenehmigungenOffenAsync(heute, ct) is > 0 and var genehmigen)
        {
            punkte.Add(new("Atemschutz-Abmeldungen zu genehmigen", "/Atemschutz/Index", 0, genehmigen));
        }

        if (await Darf(Berechtigungen.TechnikLesen)
            && await db.Maengel.CountAsync(m => m.Status != MangelStatus.Behoben, ct) is > 0 and var maengel)
        {
            punkte.Add(new("Mängel offen", "/Technik/Maengel/Index", 0, maengel));
        }

        var eigene = new List<EigeneFrist>();
        if (await termine.MitgliedIdAsync(ct) is { } mitgliedId)
        {
            eigene.AddRange((await personalfristen.LadenAsync(heute, mitgliedId, ct))
                .Where(f => f.Ampel != Fristampel.Gruen)
                .OrderByDescending(f => f.Ampel).ThenBy(f => f.FaelligAm ?? DateOnly.MinValue)
                .Select(f => new EigeneFrist(f.Bezeichnung, f.FaelligAm, f.Ampel)));
        }

        var tage = await parameter.ZahlAsync(ParameterSchluessel.StartseiteVorschauTage, heute, 28, ct);
        var offeneRueckmeldungen = 0;
        if (await termine.HatZielgruppeAsync(ct))
        {
            var jetzt = zeit.GetUtcNow();
            offeneRueckmeldungen = (await termine.KartenAsync(jetzt, jetzt.AddDays(tage), alle: false, ct)).Count(k => k.Offen);
        }

        return new(punkte, eigene, offeneRueckmeldungen, tage);
    }
}
