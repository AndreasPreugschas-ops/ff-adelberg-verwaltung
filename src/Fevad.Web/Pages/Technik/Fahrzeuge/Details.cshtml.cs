using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

public class DetailsModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    public Fahrzeug Fahrzeug { get; private set; } = null!;
    public Pruefstand Stand { get; private set; } = null!;
    public Seite<Pruefstand> Beladung { get; private set; } = Seite<Pruefstand>.Leer;
    public IReadOnlyList<PruefbuchZeile> Pruefbuch { get; private set; } = [];
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];

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
