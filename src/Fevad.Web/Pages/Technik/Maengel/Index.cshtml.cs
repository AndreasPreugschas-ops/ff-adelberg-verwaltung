using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Maengel;

/// <summary>Mängelliste, standardmäßig ohne behobene.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    /// <summary>Standard: nur offene und in Arbeit.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    /// <summary>Die angezeigten Mängel, höchstens 500.</summary>
    public IReadOnlyList<MangelZeile> Zeilen { get; private set; } = [];

    /// <summary>Lädt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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
