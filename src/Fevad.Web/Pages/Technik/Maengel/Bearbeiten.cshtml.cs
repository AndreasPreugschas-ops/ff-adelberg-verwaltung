using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Maengel;

public class BearbeitenModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public MangelZeile Mangel { get; private set; } = null!;
    public Guid? GeraetId { get; private set; }
    public Guid? FahrzeugId { get; private set; }
    public bool DarfBearbeiten { get; private set; }
    public Guid? BeschaffungId { get; private set; }

    public sealed class Eingabe
    {
        public uint Version { get; set; }
        public MangelStatus Status { get; set; }
        public MangelSchwere Schwere { get; set; }

        [StringLength(2000), Display(Name = "Maßnahme")]
        public string? Massnahme { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mangel is null)
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Version = db.Entry(mangel).Property<uint>("Version").CurrentValue,
            Status = mangel.Status,
            Schwere = mangel.Schwere,
            Massnahme = mangel.Massnahme,
        };
        await LadenAsync(id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mangel is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        db.Entry(mangel).Property<uint>("Version").OriginalValue = Formular.Version;
        mangel.Schwere = Formular.Schwere;
        mangel.Massnahme = Formular.Massnahme;
        if (mangel.Status != Formular.Status)
        {
            mangel.Status = Formular.Status;
            mangel.BehobenAm = Formular.Status == MangelStatus.Behoben ? zeit.GetUtcNow() : null;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Mangel wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            await LadenAsync(id, ct);
            return Page();
        }

        TempData["Meldung"] = "Mangel gespeichert.";
        return RedirectToPage("Index");
    }

    private async Task LadenAsync(Guid id, CancellationToken ct)
    {
        Mangel = await Ansichten.Maengel(db.Maengel.Where(m => m.Id == id)).SingleAsync(ct);
        var ziel = await db.Maengel.Where(m => m.Id == id).Select(m => new { m.GeraetId, m.FahrzeugId }).SingleAsync(ct);
        GeraetId = ziel.GeraetId;
        FahrzeugId = ziel.FahrzeugId;
        BeschaffungId = await db.Beschaffungen.Where(b => b.MangelId == id).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
    }
}
