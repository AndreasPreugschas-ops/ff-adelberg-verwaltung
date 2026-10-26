using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Meine Tagesverfügbarkeit (V43): freiwillig angeben, an welchen Werktagen ich tagsüber im Ort bin. Ohne Standort und ohne Uhrzeiten;
/// die Führung sieht nur Summen je Funktion (Entscheidung E4). Die Angabe lässt sich jederzeit löschen.
/// </summary>
public class VerfuegbarkeitModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Gespeicherte Angabe; leer ohne Angabe.</summary>
    public Werktage? Angabe { get; private set; }

    /// <summary>Tag der letzten Angabe.</summary>
    public DateOnly? Stand { get; private set; }

    /// <summary>Angekreuzte Tage.</summary>
    [BindProperty] public List<Werktage> Tage { get; set; } = [];

    /// <summary>Zeigt die eigene Angabe.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await MitgliedAsync(ct) is not { } m)
        {
            return NotFound();
        }

        Angabe = m.Tagesverfuegbarkeit;
        Stand = m.TagesverfuegbarkeitStand;
        return Page();
    }

    /// <summary>Speichert das Wochenmuster; ohne Häkchen heißt „an keinem Werktag“.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await MitgliedAsync(ct) is not { } m)
        {
            return NotFound();
        }

        m.Tagesverfuegbarkeit = Tage.Where(Tagesverfuegbarkeit.Tage.Contains).Aggregate(Werktage.Keine, (summe, tag) => summe | tag);
        m.TagesverfuegbarkeitStand = Anzeige.Heute(zeit);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Ihre Angabe ist gespeichert. Danke!";
        return RedirectToPage();
    }

    /// <summary>Löscht die Angabe ganz.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(CancellationToken ct)
    {
        if (await MitgliedAsync(ct) is not { } m)
        {
            return NotFound();
        }

        m.Tagesverfuegbarkeit = null;
        m.TagesverfuegbarkeitStand = null;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Ihre Angabe ist gelöscht.";
        return RedirectToPage();
    }

    private async Task<Mitglied?> MitgliedAsync(CancellationToken ct) =>
        await planung.MitgliedIdAsync(benutzer.Id, ct) is { } id ? await db.Mitglieder.FirstOrDefaultAsync(m => m.Id == id, ct) : null;
}
