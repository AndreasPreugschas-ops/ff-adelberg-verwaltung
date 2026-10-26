using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// „Meine Einsatzleitungen“ (V19): Einsätze, die das Mitglied geleitet hat, je Jahr, als Erfahrungsnachweis für die Laufbahn (VwV Laufbahn).
/// Gezählt wird der Verlauf der Einsatzleitung und, für ältere Berichte, der eingetragene Einsatzleiter.
/// </summary>
public class EinsatzleitungenModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Ein geleiteter Einsatz.</summary>
    /// <param name="Einsatznummer">Nummer des Einsatzes.</param>
    /// <param name="Alarmiert">Alarmzeit.</param>
    /// <param name="Stichwort">Stichwort.</param>
    /// <param name="Fuehrungsstufe">Führungsstufe, soweit eingetragen.</param>
    /// <param name="Ab">Übernahme der Leitung; leer, wenn nur als Einsatzleiter im Bericht eingetragen.</param>
    public sealed record Zeile(string Einsatznummer, DateTimeOffset Alarmiert, string? Stichwort, Fuehrungsstufe? Fuehrungsstufe, DateTimeOffset? Ab);

    /// <summary>Geleitete Einsätze je Jahr, neueste zuerst.</summary>
    public IReadOnlyList<IGrouping<int, Zeile>> Jahre { get; private set; } = [];

    /// <summary>Zeigt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var zeilen = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Status == EinsatzStatus.Freigegeben
                && (e.EinsatzleiterId == mitgliedId || e.Einsatzleitungen.Any(l => l.MitgliedId == mitgliedId)))
            .OrderByDescending(e => e.Alarmiert)
            .Select(e => new Zeile(e.Einsatznummer, e.Alarmiert, e.Stichwort, e.Fuehrungsstufe,
                e.Einsatzleitungen.Where(l => l.MitgliedId == mitgliedId).OrderBy(l => l.Ab).Select(l => (DateTimeOffset?)l.Ab).FirstOrDefault()))
            .ToListAsync(ct);
        Jahre = zeilen.GroupBy(z => Anzeige.LokalesDatum(z.Alarmiert).Year).ToList();
        return Page();
    }
}
