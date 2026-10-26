using System.Security.Claims;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Dienstbetrieb;
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
    Dienstplanung planung,
    Datenbereich datenbereich,
    ParameterLeser parameter,
    IAuthorizationService autorisierung,
    Fevad.Infrastructure.Mitglieder.Einwilligungsregister einwilligungen,
    Fevad.Infrastructure.Mitglieder.Betreuerregister betreuer,
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

        if (await Darf(Berechtigungen.MitgliederBearbeiten))
        {
            var mitglieder = await datenbereich.FilternBearbeitenAsync(db.Mitglieder, benutzer, heute, ct);
            if (await db.Aenderungsantraege.CountAsync(a => a.Status == AenderungsantragStatus.Offen && mitglieder.Any(m => m.Id == a.MitgliedId), ct) is > 0 and var aenderungen)
            {
                punkte.Add(new("Änderungsanträge von Mitgliedern", "/Mitglieder/Aenderungen", 0, aenderungen));
            }

            if (await einwilligungen.OffeneFolgeaufgaben(mitglieder).CountAsync(ct) is > 0 and var widerrufe)
            {
                punkte.Add(new("Widerrufe erledigen (WhatsApp, Fotos)", "/Mitglieder/Widerrufe", 0, widerrufe));
            }
        }

        if (await Darf(Berechtigungen.JugendBetreuerSehen))
        {
            // Führungszeugnisse der Betreuer (V47): fehlend, überfällig oder mit Eintragung rot, bald zu erneuern gelb.
            var freigabe = await datenbereich.FreigabeAsync(benutzer, Berechtigungen.JugendBetreuer);
            var zeilen = (await betreuer.ListeAsync(freigabe.Alle ? null : freigabe.Abteilungen, heute, true, ct)).Where(z => z.Betreuer.TaetigAm(heute)).ToList();
            var rot = zeilen.Count(z => z.Status is Zeugnisstatus.Fehlt or Zeugnisstatus.Faellig or Zeugnisstatus.Eintrag);
            var gelb = zeilen.Count(z => z.Status == Zeugnisstatus.Bald);
            if (rot + gelb > 0)
            {
                punkte.Add(new("Führungszeugnisse der Betreuer", "/Jugend/Betreuer", rot, gelb));
            }
        }

        if (await Darf(Berechtigungen.JugendBearbeiten))
        {
            // Warteliste der Jugend (V50): Kinder, die das Mindestalter erreicht haben.
            var freigabe = await datenbereich.FreigabeAsync(benutzer, Berechtigungen.MitgliederBearbeiten);
            var freigegeben = freigabe.Abteilungen.ToList();
            if (await db.Interessenten.CountAsync(i => i.ErledigtAm == null && i.Wiedervorlage <= heute
                    && (freigabe.Alle || freigegeben.Contains(i.AbteilungId)), ct) is > 0 and var warteliste)
            {
                punkte.Add(new("Warteliste: Kinder melden", "/Jugend/Warteliste", 0, warteliste));
            }
        }

        if (await Darf(Berechtigungen.JugendVeranstaltungenSehen))
        {
            // Zuschussanträge der Jugend (V48): nach der Veranstaltung offen gelb, nach der Antragsfrist rot.
            var abteilungen = await Pages.Jugend.VeranstaltungenModel.JugendabteilungenAsync(db, datenbereich, benutzer, ct);
            var jetzt = zeit.GetUtcNow();
            var offen = await db.Jugendveranstaltungen.AsNoTracking()
                .Where(v => v.ZuschussgeberId != null && v.ZuschussBeantragtAm == null && v.Dienst!.Ende < jetzt
                    && v.Dienst.Status != DienstStatus.Abgesagt && v.Dienst.Abteilungen.Any(a => abteilungen.Contains(a.Id)))
                .Select(v => new { v.Dienst!.Beginn, v.Dienst.Ende, v.Zuschussgeber!.AntragsfristTage })
                .ToListAsync(ct);
            var rot = offen.Count(v => v.AntragsfristTage is { } frist
                && Ortszeit.Datum(v.Beginn).AddDays(Ortszeit.Kalendertage(v.Beginn, v.Ende) - 1 + frist) < heute);
            if (offen.Count > 0)
            {
                punkte.Add(new("Zuschüsse für Jugendveranstaltungen beantragen", "/Jugend/Veranstaltungen", rot, offen.Count - rot));
            }
        }

        if (await Darf(Berechtigungen.AbwesenheitenLesen) && (await planung.OffeneAnzeigenAsync(ct)).Count is > 0 and var abwesenheiten)
        {
            punkte.Add(new("Abwesenheiten zur Kenntnis", "/Dienste/Abwesenheiten", 0, abwesenheiten));
        }

        if (await Darf(Berechtigungen.UnfaelleLesen))
        {
            var frist = await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, heute, 3, ct);
            var offen = await db.Unfaelle.AsNoTracking().Where(u => u.Status != UnfallStatus.Abgeschlossen).ToListAsync(ct);
            if (offen.Count > 0)
            {
                var ueberfaellig = offen.Count(u => u.AnzeigeAmpel(heute, frist) == Fristampel.Rot);
                punkte.Add(new("Unfallmeldungen offen", "/Unfaelle/Index", ueberfaellig, offen.Count - ueberfaellig,
                    ueberfaellig > 0 ? "Unfallanzeige überfällig" : null));
            }
        }

        if (await Darf(Berechtigungen.SachschaedenBearbeiten)
            && await db.Sachschaeden.CountAsync(s => s.Status != SachschadenStatus.Erstattet && s.Status != SachschadenStatus.Abgelehnt, ct) is > 0 and var schaeden)
        {
            punkte.Add(new("Sachschäden offen", "/Unfaelle/Sachschaeden", 0, schaeden));
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
