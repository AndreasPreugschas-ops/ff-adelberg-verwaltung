using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Lager;

/// <summary>Verbrauchsmaterial (V16): Bestand, Mindestmenge und nächster Verfall je Artikel; neue Artikel anlegen.</summary>
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Artikel mit Chargen, nach Kategorie und Bezeichnung.</summary>
    public IReadOnlyList<Lagerartikel> Artikel { get; private set; } = [];

    /// <summary>Stichtag.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Darf Artikel anlegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Kategorien zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Kategorien { get; } = Enum.GetValues<Lagerkategorie>().Select(k => new SelectListItem(Anzeige.Text(k), k.ToString())).ToList();

    /// <summary>Neuer Artikel.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Stammdaten eines Lagerartikels (auch für das Ändern).</summary>
    public sealed class Eingabe
    {
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")] public string? Bezeichnung { get; set; }

        /// <summary>Gruppe.</summary>
        [Display(Name = "Kategorie")] public Lagerkategorie Kategorie { get; set; }

        /// <summary>Einheit.</summary>
        [Required(ErrorMessage = "Einheit fehlt."), StringLength(20), Display(Name = "Einheit")] public string? Einheit { get; set; } = "Stück";

        /// <summary>Mindestmenge als Text (Komma erlaubt).</summary>
        [Display(Name = "Mindestmenge")] public string? Mindestmenge { get; set; }

        /// <summary>Preis je Einheit als Text (Komma erlaubt).</summary>
        [Display(Name = "Preis je Einheit (€, Wiederbeschaffung)")] public string? Stueckpreis { get; set; }

        /// <summary>Lagerort.</summary>
        [StringLength(100), Display(Name = "Lagerort")] public string? Lagerort { get; set; }
    }

    /// <summary>Zeigt die Liste.</summary>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Legt einen Artikel an.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var (mindestmenge, preis) = Zahlen(Formular, ModelState);
        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var artikel = new Lagerartikel
        {
            Bezeichnung = Formular.Bezeichnung!.Trim(),
            Kategorie = Formular.Kategorie,
            Einheit = Formular.Einheit!.Trim(),
            Mindestmenge = mindestmenge ?? 0,
            Stueckpreis = preis,
            Lagerort = string.IsNullOrWhiteSpace(Formular.Lagerort) ? null : Formular.Lagerort.Trim(),
        };
        db.Lagerartikel.Add(artikel);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{artikel.Bezeichnung}“ angelegt.";
        return RedirectToPage("Artikel", new { id = artikel.Id });
    }

    /// <summary>Liest Mindestmenge und Preis aus dem Formular; Fehler landen im ModelState.</summary>
    /// <param name="formular">Eingaben.</param>
    /// <param name="modelState">Fehlerablage.</param>
    /// <returns>Mindestmenge und Preis.</returns>
    public static (decimal? Mindestmenge, decimal? Preis) Zahlen(Eingabe formular, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        var mindestmenge = Entschaedigungssaetze.Lesen(formular.Mindestmenge);
        if (!string.IsNullOrWhiteSpace(formular.Mindestmenge) && mindestmenge is null or < 0)
        {
            modelState.AddModelError("Formular.Mindestmenge", "Bitte eine Zahl ab 0 eingeben.");
        }

        var preis = Entschaedigungssaetze.Lesen(formular.Stueckpreis);
        if (!string.IsNullOrWhiteSpace(formular.Stueckpreis) && preis is null or < 0)
        {
            modelState.AddModelError("Formular.Stueckpreis", "Bitte einen Betrag ab 0 eingeben, z. B. 12,50.");
        }

        return (mindestmenge, preis);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
        Artikel = await db.Lagerartikel.AsNoTracking().Include(a => a.Chargen)
            .OrderBy(a => !a.Aktiv).ThenBy(a => a.Kategorie).ThenBy(a => a.Bezeichnung).ToListAsync(ct);
    }
}
