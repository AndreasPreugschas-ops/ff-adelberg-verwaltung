using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Inventur;

/// <summary>Inventuren anlegen und auflisten.</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben für eine neue Inventur.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Alle Inventuren.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Auswahl der Inventurgruppen.</summary>
    public IReadOnlyList<SelectListItem> Inventurgruppen { get; private set; } = [];

    /// <summary>Eine Zeile der Inventurliste.</summary>
    /// <param name="Id">Kennung der Inventur.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Stichtag">Stichtag.</param>
    /// <param name="Gruppe">Inventurgruppe; leer beim ganzen Bestand.</param>
    /// <param name="Positionen">Zahl der Positionen.</param>
    /// <param name="Erfasst">Zahl der erfassten Positionen.</param>
    /// <param name="Fehlt">Zahl der fehlenden Geräte.</param>
    /// <param name="Beschaedigt">Zahl der beschädigten Geräte.</param>
    /// <param name="AbgeschlossenAm">Zeitpunkt des Abschlusses.</param>
    public sealed record Zeile(Guid Id, string Bezeichnung, DateOnly Stichtag, string? Gruppe, int Positionen, int Erfasst, int Fehlt, int Beschaedigt, DateTimeOffset? AbgeschlossenAm);

    /// <summary>Formular einer neuen Inventur.</summary>
    public sealed class Eingabe
    {
        /// <summary>Bezeichnung; leer wird sie automatisch gebildet.</summary>
        [StringLength(200), Display(Name = "Bezeichnung (leer = automatisch)")]
        public string? Bezeichnung { get; set; }

        /// <summary>Inventurgruppe; leer für den ganzen Bestand.</summary>
        [Display(Name = "Inventurgruppe")]
        public Guid? InventurgruppeId { get; set; }

        /// <summary>Stichtag.</summary>
        [Required(ErrorMessage = "Stichtag fehlt."), Display(Name = "Stichtag")]
        public DateOnly? Stichtag { get; set; }
    }

    /// <summary>Zeigt die Liste mit heutigem Stichtag im Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Formular.Stichtag = Anzeige.Heute(zeit);
        await LadenAsync(ct);
    }

    /// <summary>Legt die Inventur mit einer Position je Gerät an.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Erfassung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        string? gruppe = null;
        if (Formular.InventurgruppeId is not null)
        {
            gruppe = await db.Inventurgruppen.Where(g => g.Id == Formular.InventurgruppeId).Select(g => g.Bezeichnung).FirstOrDefaultAsync(ct);
            if (gruppe is null)
            {
                ModelState.AddModelError("Formular.InventurgruppeId", "Unbekannte Inventurgruppe.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        // Gezählt wird, was inventurpflichtig ist; ausgegebene persönliche Ausrüstung führt die Kleiderkammer.
        var geraete = await db.Geraete
            .Where(g => g.AusgesondertAm == null && g.Material!.Inventurpflichtig
                && (Formular.InventurgruppeId == null || g.InventurgruppeId == Formular.InventurgruppeId))
            .Select(g => new { g.Id, Fahrzeug = g.Fahrzeug!.Funkrufname, g.Lagerort })
            .ToListAsync(ct);

        var inventur = new Domain.Technik.Inventur
        {
            Bezeichnung = string.IsNullOrWhiteSpace(Formular.Bezeichnung)
                ? $"Inventur {gruppe ?? "Gesamtbestand"} {Anzeige.Datum(Formular.Stichtag)}"
                : Formular.Bezeichnung.Trim(),
            Stichtag = Formular.Stichtag!.Value,
            InventurgruppeId = Formular.InventurgruppeId,
            Positionen = geraete.Select(g => new InventurPosition
            {
                GeraetId = g.Id,
                SollStandort = g.Fahrzeug is null ? g.Lagerort ?? "Lager" : $"{g.Fahrzeug} {g.Lagerort}".Trim(),
            }).ToList(),
        };
        db.Inventuren.Add(inventur);
        await db.SaveChangesAsync(ct);
        return RedirectToPage("Erfassen", new { id = inventur.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Zeilen = await db.Inventuren.AsNoTracking()
            .OrderByDescending(i => i.Stichtag).ThenByDescending(i => i.ErstelltAm)
            .Select(i => new Zeile(i.Id, i.Bezeichnung, i.Stichtag, i.Inventurgruppe!.Bezeichnung,
                i.Positionen.Count,
                i.Positionen.Count(p => p.Befund != null),
                i.Positionen.Count(p => p.Befund == InventurBefund.Fehlt),
                i.Positionen.Count(p => p.Befund == InventurBefund.Beschaedigt),
                i.AbgeschlossenAm))
            .ToListAsync(ct);
        Inventurgruppen = await db.Inventurgruppen.Where(g => g.Aktiv).OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
    }
}
