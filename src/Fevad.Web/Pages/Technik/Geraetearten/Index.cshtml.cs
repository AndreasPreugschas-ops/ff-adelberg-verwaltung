using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraetearten;

public class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Geraeteart> Arten { get; private set; } = [];
    public IReadOnlyDictionary<Guid, int> Anzahl { get; private set; } = new Dictionary<Guid, int>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Arten = await db.Geraetearten.AsNoTracking()
            .Include(g => g.Pruefplan)
            .OrderBy(g => g.FuerFahrzeuge).ThenBy(g => g.Sortierung)
            .ToListAsync(ct);
        Anzahl = await db.Geraete
            .Where(g => g.AusgesondertAm == null)
            .GroupBy(g => g.GeraeteartId)
            .Select(g => new { g.Key, Anzahl = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Anzahl, ct);
    }
}
