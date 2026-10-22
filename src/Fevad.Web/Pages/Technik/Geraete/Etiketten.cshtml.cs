using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>Druckbogen mit QR-Etiketten; Auswahl über einzelne IDs oder dieselben Filter wie die Geräteliste.</summary>
public class EtikettenModel(AppDbContext db, Geraetecode geraetecode) : PageModel
{
    [BindProperty(SupportsGet = true)] public List<Guid> Ids { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? FahrzeugId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? GeraeteartId { get; set; }

    public IReadOnlyList<Etikett> Etiketten { get; private set; } = [];

    public sealed record Etikett(string Inventarnummer, string Bezeichnung, string? Standort, string Svg);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var abfrage = Ids.Count > 0
            ? db.Geraete.Where(g => Ids.Contains(g.Id))
            : IndexModel.Filtern(db.Geraete, Suche, FahrzeugId, GeraeteartId).Where(g => g.AusgesondertAm == null);

        var geraete = await abfrage.AsNoTracking()
            .OrderBy(g => g.Inventarnummer)
            .Take(500)
            .Select(g => new { g.Inventarnummer, g.Bezeichnung, Fahrzeug = g.Fahrzeug!.Funkrufname, g.Lagerort })
            .ToListAsync(ct);

        Etiketten = geraete
            .Select(g => new Etikett(g.Inventarnummer, g.Bezeichnung, g.Fahrzeug is null ? g.Lagerort : $"{g.Fahrzeug} {g.Lagerort}".Trim(),
                Geraetecode.Svg(geraetecode.Adresse(Request, g.Inventarnummer))))
            .ToList();
    }
}
