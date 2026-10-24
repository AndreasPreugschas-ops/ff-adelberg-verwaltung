using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Inventur;

/// <summary>Inventurgruppen pflegen: Fahrzeuge, Räume, Regale, Orte. Inventurpflichtige Geräte ohne Gruppe werden angezeigt.</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class GruppenModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];
    public IReadOnlyList<(Guid Id, string Inventarnummer, string Bezeichnung)> OhneGruppe { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Bezeichnung, InventurgruppenArt Art, string? Fahrzeug, string? Beschreibung, int Geraete, bool Aktiv);

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
        public string? Bezeichnung { get; set; }

        [Display(Name = "Art")] public InventurgruppenArt Art { get; set; } = InventurgruppenArt.Raum;
        [Display(Name = "Fahrzeug")] public Guid? FahrzeugId { get; set; }
        [StringLength(500), Display(Name = "Beschreibung")] public string? Beschreibung { get; set; }
        [Display(Name = "Sortierung")] public int Sortierung { get; set; }
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var g = await db.Inventurgruppen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (g is null)
            {
                return NotFound();
            }

            Formular = new Eingabe { Id = g.Id, Bezeichnung = g.Bezeichnung, Art = g.Art, FahrzeugId = g.FahrzeugId, Beschreibung = g.Beschreibung, Sortierung = g.Sortierung, Aktiv = g.Aktiv };
        }

        await LadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var bezeichnung = (Formular.Bezeichnung ?? "").Trim();
        if (await db.Inventurgruppen.AnyAsync(g => g.Bezeichnung == bezeichnung && g.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Bezeichnung", "Diese Inventurgruppe gibt es schon.");
        }

        if (Formular.Art == InventurgruppenArt.Fahrzeug)
        {
            if (Formular.FahrzeugId is null || !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
            {
                ModelState.AddModelError("Formular.FahrzeugId", "Bitte das Fahrzeug wählen.");
            }
            else if (await db.Inventurgruppen.AnyAsync(g => g.FahrzeugId == Formular.FahrzeugId && g.Id != Formular.Id, ct))
            {
                ModelState.AddModelError("Formular.FahrzeugId", "Für dieses Fahrzeug gibt es schon eine Inventurgruppe.");
            }
        }
        else
        {
            Formular.FahrzeugId = null;
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        Inventurgruppe gruppe;
        if (Formular.Id is null)
        {
            gruppe = new Inventurgruppe { Bezeichnung = bezeichnung };
            db.Inventurgruppen.Add(gruppe);
        }
        else
        {
            gruppe = await db.Inventurgruppen.FirstOrDefaultAsync(g => g.Id == Formular.Id, ct) ?? throw new InvalidOperationException();
        }

        gruppe.Bezeichnung = bezeichnung;
        gruppe.Art = Formular.Art;
        gruppe.FahrzeugId = Formular.FahrzeugId;
        gruppe.Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim();
        gruppe.Sortierung = Formular.Sortierung;
        gruppe.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"Inventurgruppe {gruppe.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Zeilen = await db.Inventurgruppen.AsNoTracking()
            .OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new Zeile(g.Id, g.Bezeichnung, g.Art, g.Fahrzeug!.Funkrufname, g.Beschreibung,
                db.Geraete.Count(x => x.InventurgruppeId == g.Id && x.AusgesondertAm == null), g.Aktiv))
            .ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.Where(f => f.AusserDienstAm == null || f.Id == Formular.FahrzeugId).OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString())).ToListAsync(ct);
        OhneGruppe = (await db.Geraete.AsNoTracking()
                .Where(g => g.AusgesondertAm == null && g.InventurgruppeId == null && g.Material!.Inventurpflichtig)
                .OrderBy(g => g.Inventarnummer).Take(100)
                .Select(g => new { g.Id, g.Inventarnummer, g.Bezeichnung })
                .ToListAsync(ct))
            .Select(g => (g.Id, g.Inventarnummer, g.Bezeichnung)).ToList();
    }
}
