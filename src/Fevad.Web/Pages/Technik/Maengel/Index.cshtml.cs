using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Maengel;

public class IndexModel(AppDbContext db) : PageModel
{
    /// <summary>Standard: nur offene und in Arbeit.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    public IReadOnlyList<MangelZeile> Zeilen { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var abfrage = db.Maengel.AsQueryable();
        if (!Alle)
        {
            abfrage = abfrage.Where(m => m.Status != MangelStatus.Behoben);
        }

        Zeilen = await Ansichten.Maengel(abfrage).Take(500).ToListAsync(ct);
    }
}
