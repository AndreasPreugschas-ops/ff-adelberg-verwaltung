using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Übungsplanung eines Ausbildungsdienstes (V53): Thema aus dem Katalog, Lernziele, Fahrzeuge und Material, Bericht. Bearbeiten darf, wer den
/// Dienst bearbeiten darf, und der eingetragene Übungsleiter, dieser nur seine eigenen Dienste.
/// </summary>
public class UebungsplanungModel(AppDbContext db, Datenbereich bereich) : PageModel
{
    /// <summary>Der Dienst.</summary>
    public Dienst Dienst { get; private set; } = null!;
    /// <summary>Auswahl der Ausbildungsthemen.</summary>
    public IReadOnlyList<SelectListItem> Themen { get; private set; } = [];

    /// <summary>Thema der Standortausbildung.</summary>
    [BindProperty]
    public Guid? AusbildungsthemaId { get; set; }

    /// <summary>Lernziele.</summary>
    [BindProperty, StringLength(2000)]
    public string? Lernziele { get; set; }

    /// <summary>Fahrzeuge und Material.</summary>
    [BindProperty, StringLength(1000)]
    public string? FahrzeugeUndMaterial { get; set; }

    /// <summary>Bericht nach dem Dienst.</summary>
    [BindProperty, StringLength(4000)]
    public string? Bericht { get; set; }

    /// <summary>Zeigt die Planung.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 404 oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        AusbildungsthemaId = Dienst.AusbildungsthemaId;
        Lernziele = Dienst.Lernziele;
        FahrzeugeUndMaterial = Dienst.FahrzeugeUndMaterial;
        Bericht = Dienst.Bericht;
        return Page();
    }

    /// <summary>Speichert die Planung.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, bei Fehlern die Seite, 404 oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var dienst = await db.Dienste.FirstAsync(d => d.Id == id, ct);
        dienst.AusbildungsthemaId = AusbildungsthemaId;
        dienst.Lernziele = Leer(Lernziele);
        dienst.FahrzeugeUndMaterial = Leer(FahrzeugeUndMaterial);
        dienst.Bericht = Leer(Bericht);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Übungsplanung gespeichert.";
        return RedirectToPage(new { id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<IActionResult?> LadenAsync(Guid id, CancellationToken ct)
    {
        var dienst = await db.Dienste.AsNoTracking().Include(d => d.Abteilungen).Include(d => d.Ausbildungsthema).Include(d => d.Verantwortlicher)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dienst is null || !Ausbildungsplanung.IstAusbildung(dienst.Art))
        {
            return NotFound();
        }

        if (!await bereich.DarfMeldungenBearbeitenAsync(User, dienst, ct))
        {
            return Forbid();
        }

        Dienst = dienst;
        Themen = await db.Ausbildungsthemen.AsNoTracking().Where(t => t.Aktiv || t.Id == dienst.AusbildungsthemaId)
            .OrderBy(t => t.Sortierung).ThenBy(t => t.Bezeichnung)
            .Select(t => new SelectListItem(t.Bezeichnung + (t.Grundlage == null ? "" : " (" + t.Grundlage + ")"), t.Id.ToString())).ToListAsync(ct);
        return null;
    }
}
