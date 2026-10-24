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

[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Inventurgruppen { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Bezeichnung, DateOnly Stichtag, string? Gruppe, int Positionen, int Erfasst, int Fehlt, int Beschaedigt, DateTimeOffset? AbgeschlossenAm);

    public sealed class Eingabe
    {
        [StringLength(200), Display(Name = "Bezeichnung (leer = automatisch)")]
        public string? Bezeichnung { get; set; }

        [Display(Name = "Inventurgruppe")]
        public Guid? InventurgruppeId { get; set; }

        [Required(ErrorMessage = "Stichtag fehlt."), Display(Name = "Stichtag")]
        public DateOnly? Stichtag { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Formular.Stichtag = Anzeige.Heute(zeit);
        await LadenAsync(ct);
    }

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
