using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Meine Einsätze (V18): eigene Teilnahmen mit Fahrzeug, Funktion und Zeiten. Solange der Bericht nicht freigegeben ist, bestätigt das
/// Mitglied seine Zeiten selbst oder meldet eine Korrektur an die Einsatzleitung; geändert werden die Zeiten nur im Bericht.
/// </summary>
public class EinsaetzeModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Pfad dieser Seite, Ziel der Bitte um Bestätigung.</summary>
    public const string Link = "/Konto/Einsaetze";

    /// <summary>Eine eigene Teilnahme.</summary>
    /// <param name="TeilnahmeId">Schlüssel der Teilnahme.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    /// <param name="Stichwort">Stichwort.</param>
    /// <param name="Alarmiert">Alarmierung.</param>
    /// <param name="Fahrzeug">Fahrzeug.</param>
    /// <param name="Funktion">Funktion.</param>
    /// <param name="Von">Von (leer: ab Alarmierung).</param>
    /// <param name="Bis">Bis (leer: bis Einsatzbereitschaft).</param>
    /// <param name="Atemschutz">Unter Atemschutz.</param>
    /// <param name="Offen">Bericht noch nicht freigegeben, Rückmeldung möglich.</param>
    /// <param name="Bestaetigt">Zeiten bestätigt.</param>
    /// <param name="Korrekturhinweis">Gemeldete Korrektur.</param>
    public sealed record Zeile(Guid TeilnahmeId, string Einsatznummer, string? Stichwort, DateTimeOffset Alarmiert, string? Fahrzeug, string? Funktion,
        DateTimeOffset? Von, DateTimeOffset? Bis, bool Atemschutz, bool Offen, bool Bestaetigt, string? Korrekturhinweis);

    /// <summary>Eigene Teilnahmen, neueste zuerst.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Zeigt die eigenen Einsätze.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        if (benutzer.Id is { } konto)
        {
            // Die Bitte um Bestätigung ist mit dem Öffnen dieser Seite erledigt.
            var jetzt = zeit.GetUtcNow();
            await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto && b.GelesenAm == null && b.Link == Link)
                .ExecuteUpdateAsync(x => x.SetProperty(b => b.GelesenAm, jetzt), ct);
        }

        Zeilen = await db.EinsatzTeilnahmen.AsNoTracking().Where(t => t.MitgliedId == mitgliedId)
            .OrderByDescending(t => t.Einsatz!.Alarmiert).Take(50)
            .Select(t => new Zeile(t.Id, t.Einsatz!.Einsatznummer, t.Einsatz.Stichwort, t.Einsatz.Alarmiert, t.EinsatzFahrzeug!.Fahrzeug!.Funkrufname, t.Funktion,
                t.Von, t.Bis, t.Atemschutz, t.Einsatz.Status != EinsatzStatus.Freigegeben, t.Bestaetigt, t.Korrekturhinweis))
            .ToListAsync(ct);
        return Page();
    }

    /// <summary>Bestätigt die eigenen Zeiten oder meldet eine Korrektur.</summary>
    /// <param name="teilnahmeId">Die eigene Teilnahme.</param>
    /// <param name="korrektur">Korrekturhinweis; leer heißt „stimmt so“.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Liste, 404 bei fremder Teilnahme.</returns>
    public async Task<IActionResult> OnPostAsync(Guid teilnahmeId, [StringLength(200)] string? korrektur, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var t = await db.EinsatzTeilnahmen.Include(x => x.Einsatz).FirstOrDefaultAsync(x => x.Id == teilnahmeId && x.MitgliedId == mitgliedId, ct);
        if (t is null || mitgliedId is null)
        {
            return NotFound();
        }

        if (t.Einsatz!.Status == EinsatzStatus.Freigegeben)
        {
            TempData["Fehler"] = "Der Bericht ist schon freigegeben. Korrekturen bitte direkt an die Einsatzleitung.";
            return RedirectToPage();
        }

        korrektur = string.IsNullOrWhiteSpace(korrektur) ? null : korrektur.Trim();
        if (korrektur?.Length > 200)
        {
            TempData["Fehler"] = "Der Hinweis darf höchstens 200 Zeichen haben.";
            return RedirectToPage();
        }

        t.Bestaetigt = korrektur is null;
        t.Korrekturhinweis = korrektur;
        t.RueckmeldungAm = zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = korrektur is null ? "Danke, deine Zeiten sind bestätigt." : "Danke, dein Hinweis steht jetzt im Einsatzbericht.";
        return RedirectToPage();
    }
}
