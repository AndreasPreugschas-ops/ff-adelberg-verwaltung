using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>Fahrzeug mit Prüfstand, Beladung, Prüfbuch und offenen Mängeln.</summary>
public class DetailsModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug Fahrzeug { get; private set; } = null!;
    /// <summary>Prüfstand des Fahrzeugs.</summary>
    public Pruefstand Stand { get; private set; } = null!;
    /// <summary>Geräte auf dem Fahrzeug mit Prüfstand.</summary>
    public Seite<Pruefstand> Beladung { get; private set; } = Seite<Pruefstand>.Leer;
    /// <summary>Prüfbuch des Fahrzeugs.</summary>
    public IReadOnlyList<PruefbuchZeile> Pruefbuch { get; private set; } = [];
    /// <summary>Offene Mängel am Fahrzeug.</summary>
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];

    /// <summary>Lädt das Fahrzeug.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var fahrzeug = await db.Fahrzeuge.AsNoTracking().Include(f => f.Pruefvorlage).FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fahrzeug is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        Fahrzeug = fahrzeug;
        Stand = (await pruefstaende.FahrzeugeAsync(heute, id, ct)).Single();
        Beladung = (await pruefstaende.GeraeteAsync(heute, fahrzeugId: id, ct: ct)).Ganz();
        Pruefbuch = await Ansichten.PruefbuchAsync(db, null, id, ct);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.FahrzeugId == id && m.Status != MangelStatus.Behoben)).ToListAsync(ct);
        return Page();
    }
}
