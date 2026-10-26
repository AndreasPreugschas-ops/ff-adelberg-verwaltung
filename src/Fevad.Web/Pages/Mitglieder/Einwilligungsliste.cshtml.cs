using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Übersicht des Einwilligungsregisters (V45): je aktives Mitglied der Stand aller Arten, wahlweise nur, wo eine Einwilligung fehlt.
/// Vor dem Veröffentlichen eines Fotos zeigt sie, wer auf welchem Kanal erscheinen darf. Jugend und Einsatzabteilung getrennt wie in der Mitgliederliste.
/// </summary>
public class EinwilligungslisteModel(
    AppDbContext db, Datenbereich datenbereich, Einwilligungsregister register, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Mitglied mit dem Stand je Art in der Reihenfolge von <see cref="Arten"/>.</summary>
    /// <param name="Id">Kennung des Mitglieds.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Alter">Alter heute.</param>
    /// <param name="Status">Stand je Art.</param>
    public sealed record Zeile(Guid Id, string Name, int Alter, IReadOnlyList<Einwilligungsstatus> Status);

    /// <summary>Jugend- und Kinderfeuerwehr statt übrigem Personal.</summary>
    [BindProperty(SupportsGet = true)] public bool Jugend { get; set; }
    /// <summary>Code der Abteilung als Filter.</summary>
    [BindProperty(SupportsGet = true)] public string? Abteilung { get; set; }
    /// <summary>Nur Mitglieder zeigen, bei denen mindestens eine Einwilligung nicht wirksam ist.</summary>
    [BindProperty(SupportsGet = true)] public bool NurFehlende { get; set; }

    /// <summary>Beide Bereiche sind sichtbar; dann gibt es die Umschaltung.</summary>
    public bool BeideSichtbar { get; private set; }
    /// <summary>Spalten der Übersicht.</summary>
    public IReadOnlyList<Einwilligungsart> Arten { get; private set; } = [];
    /// <summary>Zeilen der Übersicht.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Abteilungen des Bereichs für den Filter.</summary>
    public IReadOnlyList<(string Code, string Bezeichnung)> Abteilungen { get; private set; } = [];

    /// <summary>Zeigt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var jugendSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendLesen)).Succeeded;
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.MitgliederLesen);
        var jugendabteilungen = await db.Abteilungen.Where(a => a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder).Select(a => a.Id).ToListAsync(ct);
        var feuerwehrSichtbar = freigabe.Alle || freigabe.OhneJugend is not null || freigabe.Abteilungen.Except(jugendabteilungen).Any();
        if (!feuerwehrSichtbar)
        {
            Jugend = true;
        }

        if (Jugend && !jugendSichtbar)
        {
            return Forbid();
        }

        BeideSichtbar = jugendSichtbar && feuerwehrSichtbar;
        var jugend = Jugend;
        Abteilungen = (await db.Abteilungen.AsNoTracking()
                .Where(a => a.Aktiv && (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder) == jugend)
                .OrderBy(a => a.Sortierung).Select(a => new { a.Code, a.Bezeichnung }).ToListAsync(ct))
            .Select(a => (a.Code, a.Bezeichnung)).ToList();
        Arten = await register.ArtenAsync(Jugend, ct);

        var abteilung = string.IsNullOrWhiteSpace(Abteilung) ? null : Abteilung;
        var sichtbar = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute);
        var abfrage = (Jugend ? Datenbereich.NurJugend(sichtbar, heute) : Datenbereich.OhneJugendpersonal(sichtbar, heute))
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (abteilung == null || s.Abteilung!.Code == abteilung)));
        var mitglieder = await abfrage.OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Geburtsdatum }).ToListAsync(ct);
        var ids = mitglieder.Select(m => m.Id).ToList();
        var eintraege = (await db.Einwilligungen.AsNoTracking().Where(e => ids.Contains(e.MitgliedId)).ToListAsync(ct))
            .ToLookup(e => (e.MitgliedId, e.EinwilligungsartId));
        var grenzen = await register.GrenzenAsync(heute, ct);

        Zeilen = mitglieder
            .Select(m =>
            {
                var alter = Mitglied.Alter(m.Geburtsdatum, heute);
                return new Zeile(m.Id, $"{m.Nachname}, {m.Vorname}", alter,
                    Arten.Select(a => Einwilligungsregeln.Status(eintraege[(m.Id, a.Id)], alter, grenzen.VolljaehrigAb, grenzen.MitentscheidungAb)).ToList());
            })
            .Where(z => !NurFehlende || z.Status.Any(s => s != Einwilligungsstatus.Erteilt))
            .ToList();
        return Page();
    }
}
