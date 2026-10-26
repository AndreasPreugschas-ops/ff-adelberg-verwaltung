using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Datenschutz;

/// <summary>
/// Löschanfragen der Mitglieder (V39, Art. 17 DSGVO) beantworten: prüfen, was ohne Aufbewahrungspflicht gelöscht werden kann, das auf den
/// jeweiligen Seiten löschen und hier die Antwort eintragen. Erledigte Anfragen löscht L52 nach der Frist.
/// </summary>
public class LoeschanfragenModel(AppDbContext db, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Anfragen, älteste zuerst.</summary>
    public IReadOnlyList<Loeschanfrage> Offene { get; private set; } = [];
    /// <summary>Erledigte Anfragen, neueste zuerst.</summary>
    public IReadOnlyList<Loeschanfrage> Erledigte { get; private set; } = [];

    /// <summary>Zeigt die Anfragen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Offene = await db.Loeschanfragen.AsNoTracking().Where(l => l.ErledigtAm == null).OrderBy(l => l.GestelltAm).ToListAsync(ct);
        Erledigte = await db.Loeschanfragen.AsNoTracking().Where(l => l.ErledigtAm != null).OrderByDescending(l => l.ErledigtAm).Take(50).ToListAsync(ct);
    }

    /// <summary>Beantwortet eine Anfrage und schließt sie ab.</summary>
    /// <param name="id">Kennung der Anfrage.</param>
    /// <param name="antwort">Antwort an das Mitglied.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErledigenAsync(Guid id, string? antwort, CancellationToken ct)
    {
        if (await db.Loeschanfragen.FirstOrDefaultAsync(l => l.Id == id && l.ErledigtAm == null, ct) is not { } anfrage)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(antwort))
        {
            TempData["Fehler"] = "Bitte eine Antwort eintragen: was gelöscht wurde und was wegen Aufbewahrungspflichten bleibt.";
            return RedirectToPage();
        }

        anfrage.Antwort = antwort.Trim()[..Math.Min(antwort.Trim().Length, 4000)];
        anfrage.ErledigtAm = zeit.GetUtcNow();
        anfrage.ErledigtVon = benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Anfrage von {anfrage.Name} beantwortet.";
        return RedirectToPage();
    }
}
