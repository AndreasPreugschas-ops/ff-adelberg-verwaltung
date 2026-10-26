using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Notfallliste der Jugend- und Kinderfeuerwehr (V46): Heimweg, Abholberechtigte, Erziehungsberechtigte, Notfallkontakt und Stand der
/// Notfallmappe, als Seite und als PDF für Dienste, Fahrten und Lager. Jugendwart und Betreuer sehen ihre Abteilungen, Kommandant und
/// Stellvertreter alle. Gesundheitsangaben gibt es hier nicht, sie stehen nur in der Mappe auf Papier (E3).
/// </summary>
[Authorize(Policy = Berechtigungen.JugendNotfalllisteSehen)]
public class NotfalllisteModel(AppDbContext db, Datenbereich datenbereich, Kassenberichte berichte, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Gewählte Abteilung; leer für alle sichtbaren.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Abteilung { get; set; }

    /// <summary>Die Zeilen der Liste.</summary>
    public IReadOnlyList<NotfalllistePdf.Zeile> Zeilen { get; private set; } = [];

    /// <summary>Abteilungen zur Auswahl: Jugend- und Kinderfeuerwehren mit sichtbaren Mitgliedern.</summary>
    public IReadOnlyList<(Guid Id, string Name)> Abteilungen { get; private set; } = [];

    /// <summary>Anzahl ohne aktuelle Notfallmappe.</summary>
    public int OhneMappe { get; private set; }

    /// <summary>Nach so vielen Monaten ist die Mappe zu erneuern.</summary>
    public int ErneuernMonate { get; private set; }

    /// <summary>Zeigt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Die Liste als PDF.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnGetPdfAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var titel = Abteilung is { } a && Abteilungen.FirstOrDefault(x => x.Id == a) is { Name: { } name } ? $"Notfallliste {name}" : "Notfallliste Jugend";
        var pdf = NotfalllistePdf.Erzeugen(new(await berichte.KopfAsync(ct), titel, Anzeige.DatumZeit(zeit.GetUtcNow()) + " Uhr", Zeilen));
        return File(pdf, "application/pdf", $"notfallliste-{Anzeige.Heute(zeit):yyyy-MM-dd}.pdf");
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        ErneuernMonate = await parameter.ZahlAsync(ParameterSchluessel.NotfallmappeErneuernMonate, heute, 12, ct);
        var sichtbar = Datenbereich.NurJugend(
            await datenbereich.FilternAsync(db.Mitglieder.AsNoTracking(), User, Berechtigungen.JugendNotfallliste, heute, ct), heute);
        var mitglieder = await sichtbar
            .Include(m => m.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .ThenInclude(s => s.Abteilung)
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .ToListAsync(ct);

        Abteilungen = mitglieder
            .SelectMany(m => m.Mitgliedschaften.Select(s => s.Abteilung!))
            .Where(a => a.Art is AbteilungArt.Jugend or AbteilungArt.Kinder)
            .DistinctBy(a => a.Id).OrderBy(a => a.Sortierung).Select(a => (a.Id, a.Bezeichnung)).ToList();
        if (Abteilung is { } gewaehlt)
        {
            mitglieder = mitglieder.Where(m => m.Mitgliedschaften.Any(s => s.AbteilungId == gewaehlt)).ToList();
        }

        var ids = mitglieder.Select(m => m.Id).ToList();
        var eltern = (await db.Erziehungsberechtigte.AsNoTracking().Where(e => ids.Contains(e.MitgliedId)).OrderBy(e => e.Nummer).ToListAsync(ct))
            .ToLookup(e => e.MitgliedId);

        Zeilen = mitglieder.Select(m => new NotfalllistePdf.Zeile(
            $"{m.Nachname}, {m.Vorname}",
            m.Geburtsdatum is { } g ? $"{Anzeige.Datum(g)} ({m.AlterAm(heute)})" : "",
            string.Join(", ", m.Mitgliedschaften.Select(s => s.Abteilung!).Where(a => a.Art is AbteilungArt.Jugend or AbteilungArt.Kinder).Select(a => a.Bezeichnung)),
            m.Abholregelung is null ? "nicht angegeben" : Anzeige.Text(m.Abholregelung),
            m.Abholberechtigte,
            eltern[m.Id].Select(Elternzeile).ToList(),
            m.NotfallkontaktName is null ? null : $"{m.NotfallkontaktName} {m.NotfallkontaktTelefon}".Trim(),
            Mappe(m, heute, ErneuernMonate))).ToList();
        OhneMappe = mitglieder.Count(m => !m.NotfallmappeAktuell(heute, ErneuernMonate));
    }

    /// <summary>Stand der Notfallmappe als Text.</summary>
    /// <param name="m">Das Mitglied.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="erneuernMonate">Frist zum Erneuern.</param>
    /// <returns>„fehlt“, „liegt vor (…)“ oder „erneuern (vom …)“.</returns>
    public static string Mappe(Mitglied m, DateOnly heute, int erneuernMonate) =>
        m.NotfallmappeAm is not { } am ? "fehlt"
        : m.NotfallmappeAktuell(heute, erneuernMonate) ? $"liegt vor ({Anzeige.Datum(am)})"
        : $"erneuern (vom {Anzeige.Datum(am)})";

    private static string Elternzeile(Erziehungsberechtigter e)
    {
        var telefon = string.Join(" / ", new[] { e.Mobil, e.Telefon }.Where(t => !string.IsNullOrWhiteSpace(t)));
        return $"{e.Vorname} {e.Nachname}{(e.Verhaeltnis is null ? "" : $" ({Anzeige.Text(e.Verhaeltnis)})")}{(telefon.Length > 0 ? ": " + telefon : "")}";
    }
}
