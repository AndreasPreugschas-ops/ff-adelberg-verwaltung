using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Pruefungen;

public class DetailsModel(AppDbContext db) : PageModel
{
    public Pruefung Pruefung { get; private set; } = null!;
    public string Gegenstand { get; private set; } = "";
    public IReadOnlyList<PruefpunktErgebnis> Ergebnisse { get; private set; } = [];
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var pruefung = await db.Pruefungen.AsNoTracking()
            .Include(p => p.Geraet)
            .Include(p => p.Fahrzeug)
            .Include(p => p.Pruefer)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pruefung is null)
        {
            return NotFound();
        }

        Pruefung = pruefung;
        Gegenstand = pruefung.Geraet is { } g ? $"{g.Inventarnummer} {g.Bezeichnung}" : pruefung.Fahrzeug?.Funkrufname ?? "";
        Ergebnisse = PruefpunktErgebnis.AusJson(pruefung.Ergebnisse);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.PruefungId == id)).ToListAsync(ct);
        return Page();
    }
}
