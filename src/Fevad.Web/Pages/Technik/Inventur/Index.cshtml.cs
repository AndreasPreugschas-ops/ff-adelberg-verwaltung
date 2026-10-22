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
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Bezeichnung, DateOnly Stichtag, string? Fahrzeug, int Positionen, int Erfasst, int Fehlt, int Beschaedigt, DateTimeOffset? AbgeschlossenAm);

    public sealed class Eingabe
    {
        [StringLength(200), Display(Name = "Bezeichnung (leer = automatisch)")]
        public string? Bezeichnung { get; set; }

        [Display(Name = "Umfang")]
        public Guid? FahrzeugId { get; set; }

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
        string? fahrzeug = null;
        if (Formular.FahrzeugId is not null)
        {
            fahrzeug = await db.Fahrzeuge.Where(f => f.Id == Formular.FahrzeugId).Select(f => f.Funkrufname).FirstOrDefaultAsync(ct);
            if (fahrzeug is null)
            {
                ModelState.AddModelError("Formular.FahrzeugId", "Unbekanntes Fahrzeug.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var geraete = await db.Geraete
            .Where(g => g.AusgesondertAm == null && (Formular.FahrzeugId == null || g.FahrzeugId == Formular.FahrzeugId))
            .Select(g => new { g.Id, Fahrzeug = g.Fahrzeug!.Funkrufname, g.Lagerort })
            .ToListAsync(ct);

        var inventur = new Domain.Technik.Inventur
        {
            Bezeichnung = string.IsNullOrWhiteSpace(Formular.Bezeichnung)
                ? $"Inventur {fahrzeug ?? "Gesamtbestand"} {Anzeige.Datum(Formular.Stichtag)}"
                : Formular.Bezeichnung.Trim(),
            Stichtag = Formular.Stichtag!.Value,
            FahrzeugId = Formular.FahrzeugId,
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
            .Select(i => new Zeile(i.Id, i.Bezeichnung, i.Stichtag, i.Fahrzeug!.Funkrufname,
                i.Positionen.Count,
                i.Positionen.Count(p => p.Befund != null),
                i.Positionen.Count(p => p.Befund == InventurBefund.Fehlt),
                i.Positionen.Count(p => p.Befund == InventurBefund.Beschaedigt),
                i.AbgeschlossenAm))
            .ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.Where(f => f.AusserDienstAm == null).OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString())).ToListAsync(ct);
    }
}
