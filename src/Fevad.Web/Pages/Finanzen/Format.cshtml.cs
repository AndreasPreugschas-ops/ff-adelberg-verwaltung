using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Finanzen;

/// <summary>Spaltenzuordnung einer Exportart einrichten (V23); bis zur Abstimmung mit der Kämmerei gilt das Standardformat.</summary>
[Authorize(Policy = Berechtigungen.FinanzexportEinrichten)]
public class FormatModel(AppDbContext db) : PageModel
{
    /// <summary>Die Exportart.</summary>
    public ExportArt Art { get; private set; }

    /// <summary>Eingaben.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Auswahl der Kodierungen.</summary>
    public static IReadOnlyList<SelectListItem> Kodierungen { get; } =
        Enum.GetValues<Exportkodierung>().Select(k => new SelectListItem(Anzeige.Text(k), k.ToString())).ToList();

    /// <summary>Eingaben des Formats.</summary>
    public sealed class Eingabe
    {
        /// <summary>Trennzeichen.</summary>
        [Required(ErrorMessage = "Trennzeichen fehlt."), StringLength(3), Display(Name = "Trennzeichen (\\t für Tabulator)")] public string Trennzeichen { get; set; } = ";";
        /// <summary>Kodierung.</summary>
        [Display(Name = "Zeichenkodierung")] public Exportkodierung Kodierung { get; set; }
        /// <summary>Kopfzeile.</summary>
        [Display(Name = "Erste Zeile mit Überschriften")] public bool Kopfzeile { get; set; } = true;
        /// <summary>Dezimalkomma.</summary>
        [Display(Name = "Komma als Dezimaltrennzeichen")] public bool DezimalKomma { get; set; } = true;
        /// <summary>Datumsformat.</summary>
        [Required(ErrorMessage = "Datumsformat fehlt."), StringLength(20), Display(Name = "Datumsformat (z. B. dd.MM.yyyy oder yyyyMMdd)")] public string Datumsformat { get; set; } = "dd.MM.yyyy";
        /// <summary>Spalten.</summary>
        [Required(ErrorMessage = "Spalten fehlen."), StringLength(2000), Display(Name = "Spalten, eine je Zeile: Feld oder Überschrift=Feld; \"Text\" für einen festen Wert")] public string Spalten { get; set; } = "";
        /// <summary>Buchungsstelle.</summary>
        [StringLength(50), Display(Name = "Buchungsstelle (z. B. Produkt 12.60.01)")] public string? Buchungsstelle { get; set; }
        /// <summary>Sachkonto.</summary>
        [StringLength(50), Display(Name = "Sachkonto")] public string? Sachkonto { get; set; }
    }

    /// <summary>Zeigt das Format.</summary>
    /// <param name="art">Exportart.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(ExportArt art, CancellationToken ct)
    {
        Art = art;
        var f = await db.Exportformate.AsNoTracking().FirstOrDefaultAsync(x => x.Art == art, ct) ?? Finanzexport.Standard(art);
        Formular = new Eingabe
        {
            Trennzeichen = f.Trennzeichen, Kodierung = f.Kodierung, Kopfzeile = f.Kopfzeile, DezimalKomma = f.DezimalKomma, Datumsformat = f.Datumsformat,
            Spalten = f.Spalten, Buchungsstelle = f.Buchungsstelle, Sachkonto = f.Sachkonto,
        };
    }

    /// <summary>Speichert das Format nach Prüfung der Felder und des Datumsformats.</summary>
    /// <param name="art">Exportart.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(ExportArt art, CancellationToken ct)
    {
        Art = art;
        var f = await db.Exportformate.FirstOrDefaultAsync(x => x.Art == art, ct);
        var neu = f ?? new Exportformat { Art = art };
        neu.Trennzeichen = Formular.Trennzeichen;
        neu.Kodierung = Formular.Kodierung;
        neu.Kopfzeile = Formular.Kopfzeile;
        neu.DezimalKomma = Formular.DezimalKomma;
        neu.Datumsformat = Formular.Datumsformat.Trim();
        neu.Spalten = Formular.Spalten.Replace("\r", "").Trim();
        neu.Buchungsstelle = string.IsNullOrWhiteSpace(Formular.Buchungsstelle) ? null : Formular.Buchungsstelle.Trim();
        neu.Sachkonto = string.IsNullOrWhiteSpace(Formular.Sachkonto) ? null : Formular.Sachkonto.Trim();
        Finanzexport.SpaltenLesen(neu, out var fehler);
        if (fehler is not null)
        {
            ModelState.AddModelError("Formular.Spalten", fehler);
        }

        try
        {
            _ = new DateOnly(2026, 12, 31).ToString(neu.Datumsformat, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            ModelState.AddModelError("Formular.Datumsformat", "Kein gültiges Datumsformat.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (f is null)
        {
            db.Exportformate.Add(neu);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Spaltenzuordnung „{Anzeige.Text(art)}“ gespeichert.";
        return RedirectToPage(new { art });
    }
}
