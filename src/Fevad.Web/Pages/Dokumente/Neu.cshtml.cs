using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dokumente;

/// <summary>Neues Dokument mit erster Version hochladen.</summary>
[Authorize(Policy = Berechtigungen.DokumenteVerwalten)]
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class NeuModel(AppDbContext db, Dokumentablage ablage) : PageModel
{
    [BindProperty] public Eingabe Formular { get; set; } = new();
    [BindProperty, Display(Name = "Datei")] public IFormFile? Datei { get; set; }

    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];

    public sealed class Eingabe
    {
        [Required(ErrorMessage = "Titel fehlt."), StringLength(200), Display(Name = "Titel")]
        public string Titel { get; set; } = "";

        [StringLength(2000), Display(Name = "Beschreibung")]
        public string? Beschreibung { get; set; }

        [Required(ErrorMessage = "Kategorie fehlt."), Display(Name = "Kategorie")]
        public Guid? KategorieId { get; set; }

        [Display(Name = "Sichtbar für")]
        public DokumentSichtbarkeit Sichtbarkeit { get; set; }

        [Display(Name = "Lesebestätigung verlangen")]
        public bool LesebestaetigungErforderlich { get; set; }

        [Display(Name = "Gültig ab")]
        public DateOnly? GueltigAb { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Datei is null)
        {
            ModelState.AddModelError(nameof(Datei), "Bitte eine Datei auswählen.");
        }
        else if (await ablage.PruefenAsync(Datei.FileName, Datei.Length, ct) is { } fehler)
        {
            ModelState.AddModelError(nameof(Datei), fehler);
        }

        if (Formular.KategorieId is { } k && !await db.Dokumentkategorien.AnyAsync(x => x.Id == k, ct))
        {
            ModelState.AddModelError("Formular.KategorieId", "Unbekannte Kategorie.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var dokument = new Dokument
        {
            Titel = Formular.Titel.Trim(),
            Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim(),
            KategorieId = Formular.KategorieId!.Value,
            Sichtbarkeit = Formular.Sichtbarkeit,
            LesebestaetigungErforderlich = Formular.LesebestaetigungErforderlich,
            GueltigAb = Formular.GueltigAb,
        };
        db.Dokumente.Add(dokument);
        await using (var inhalt = Datei!.OpenReadStream())
        {
            await ablage.VersionHinzufuegenAsync(dokument, Datei.FileName, inhalt, null, ct);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{dokument.Titel}“ hochgeladen.";
        return RedirectToPage("Detail", new { id = dokument.Id });
    }

    private async Task LadenAsync(CancellationToken ct) =>
        Kategorien = await db.Dokumentkategorien.AsNoTracking().Where(k => k.Aktiv).OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);
}
