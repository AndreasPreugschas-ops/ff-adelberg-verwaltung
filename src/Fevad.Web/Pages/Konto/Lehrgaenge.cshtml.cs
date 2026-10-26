using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Ausbildung;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>Meine Lehrgangswünsche (V54): Lehrgang wünschen, Voraussetzungen sehen, Stand verfolgen, Wunsch zurückziehen.</summary>
public class LehrgaengeModel(AppDbContext db, Lehrgangswuensche wuensche, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Wünsche, neueste zuerst.</summary>
    public IReadOnlyList<Lehrgangswunsch> Wuensche { get; private set; } = [];

    /// <summary>Lehrgänge mit Prüfergebnis.</summary>
    public IReadOnlyList<Lehrgangswuensche.Angebot> Angebote { get; private set; } = [];

    /// <summary>Das Mitglied gehört zur Einsatzabteilung und kann Lehrgänge wünschen.</summary>
    public bool Berechtigt { get; private set; }

    /// <summary>Laufendes Jahr.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Gewählter Lehrgang.</summary>
    [BindProperty] public Guid QualifikationId { get; set; }

    /// <summary>Wunschjahr.</summary>
    [BindProperty] public int Jahr { get; set; }

    /// <summary>Begründung oder Hinweis.</summary>
    [BindProperty] public string? Begruendung { get; set; }

    /// <summary>Zeigt Wünsche und Lehrgänge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        Jahr = AktuellesJahr + 1;
        return Page();
    }

    /// <summary>Trägt einen Wunsch ein.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehler.</returns>
    public async Task<IActionResult> OnPostEintragenAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        if (await wuensche.EintragenAsync(mitgliedId, QualifikationId, Jahr, Begruendung, Anzeige.Heute(zeit), ct) is { } fehler)
        {
            ModelState.AddModelError(nameof(QualifikationId), fehler);
            await LadenAsync(mitgliedId, ct);
            return Page();
        }

        TempData["Meldung"] = "Ihr Wunsch ist eingetragen. Das Kommando entscheidet und meldet den Bedarf an den Kreisbrandmeister.";
        return RedirectToPage();
    }

    /// <summary>Zieht einen eigenen Wunsch zurück, solange er nicht angemeldet ist.</summary>
    /// <param name="id">Kennung des Wunsches.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var wunsch = await db.Lehrgangswuensche.FirstOrDefaultAsync(w => w.Id == id && w.MitgliedId == mitgliedId, ct);
        if (wunsch is null || !wunsch.Zurueckziehbar)
        {
            return NotFound();
        }

        wunsch.Status = WunschStatus.Zurueckgezogen;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Der Wunsch ist zurückgezogen.";
        return RedirectToPage();
    }

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        AktuellesJahr = heute.Year;
        Berechtigt = await wuensche.Berechtigte(heute).AnyAsync(m => m.Id == mitgliedId, ct);
        Wuensche = await db.Lehrgangswuensche.AsNoTracking().Include(w => w.Qualifikation)
            .Where(w => w.MitgliedId == mitgliedId).OrderByDescending(w => w.Jahr).ThenByDescending(w => w.EingetragenAm).ToListAsync(ct);
        Angebote = Berechtigt ? await wuensche.AngeboteAsync(mitgliedId, heute, ct) : [];
    }
}
