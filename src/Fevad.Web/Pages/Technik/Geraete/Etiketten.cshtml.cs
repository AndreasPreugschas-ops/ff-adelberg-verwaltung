using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>Druckbogen mit QR-Etiketten; Auswahl über einzelne IDs oder dieselben Filter wie die Geräteliste.</summary>
public class EtikettenModel(AppDbContext db, Geraetecode geraetecode) : PageModel
{
    /// <summary>Ausgewählte Geräte; leer heißt Auswahl über die Filter.</summary>
    [BindProperty(SupportsGet = true)] public List<Guid> Ids { get; set; } = [];
    /// <summary>Suchtext wie in der Geräteliste.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    /// <summary>Nur Geräte auf diesem Fahrzeug.</summary>
    [BindProperty(SupportsGet = true)] public Guid? FahrzeugId { get; set; }
    /// <summary>Nur Geräte dieser Art.</summary>
    [BindProperty(SupportsGet = true)] public Guid? GeraeteartId { get; set; }

    /// <summary>Die Etiketten, höchstens 500.</summary>
    public IReadOnlyList<Etikett> Etiketten { get; private set; } = [];

    /// <summary>Ein Etikett.</summary>
    /// <param name="Inventarnummer">Inventarnummer.</param>
    /// <param name="Bezeichnung">Bezeichnung des Geräts.</param>
    /// <param name="Standort">Fahrzeug und Fach oder Lagerort.</param>
    /// <param name="Svg">QR-Code als SVG.</param>
    public sealed record Etikett(string Inventarnummer, string Bezeichnung, string? Standort, string Svg);

    /// <summary>Lädt die Etiketten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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
