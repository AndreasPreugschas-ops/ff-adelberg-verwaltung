using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>
/// Prüfplaketten als PDF (V58) für Etikettendrucker oder A4-Bogen. Auswahl über IDs (kommagetrennt, z. B. aus der Sammelprüfung)
/// oder dieselben Filter wie die Geräteliste; Maße aus den Parametern <c>Technik.Etikett.*</c>.
/// </summary>
public class PlakettenModel(AppDbContext db, Geraetecode geraetecode, Pruefstaende pruefstaende, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Höchstzahl Plaketten je PDF.</summary>
    public const int HoechstZahl = 500;

    /// <summary>Geräte-IDs, durch Komma getrennt; hat Vorrang vor den Filtern.</summary>
    [BindProperty(SupportsGet = true)] public string? Ids { get; set; }

    /// <summary>Suchtext wie in der Geräteliste.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }

    /// <summary>Nur Geräte dieses Fahrzeugs.</summary>
    [BindProperty(SupportsGet = true)] public Guid? FahrzeugId { get; set; }

    /// <summary>Nur Geräte dieser Geräteart.</summary>
    [BindProperty(SupportsGet = true)] public Guid? GeraeteartId { get; set; }

    /// <summary>Etikettendrucker oder A4-Bogen.</summary>
    [BindProperty(SupportsGet = true)] public PlakettenPdf.Format Format { get; set; } = PlakettenPdf.Format.Bogen;

    /// <summary>Liefert das PDF.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var ids = (Ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var abfrage = ids.Count > 0
            ? db.Geraete.Where(g => ids.Contains(g.Id))
            : IndexModel.Filtern(db.Geraete, Suche, FahrzeugId, GeraeteartId);
        var geraete = await abfrage.AsNoTracking()
            .Where(g => g.AusgesondertAm == null)
            .OrderBy(g => g.Inventarnummer)
            .Take(HoechstZahl)
            .Select(g => new { g.Id, g.Inventarnummer, g.Bezeichnung })
            .ToListAsync(ct);

        var heute = Anzeige.Heute(zeit);
        var auswahl = geraete.Select(g => g.Id).ToHashSet();
        var termine = (await pruefstaende.GeraeteAsync(heute, ct: ct))
            .Where(s => auswahl.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.NaechsterTermin);

        var plaketten = geraete
            .Select(g => new PlakettenPdf.Plakette(g.Inventarnummer, g.Bezeichnung, termine.GetValueOrDefault(g.Id),
                Geraetecode.Svg(geraetecode.Adresse(Request, g.Inventarnummer))))
            .ToList();
        var masse = new PlakettenPdf.Masse(
            await parameter.ZahlAsync(ParameterSchluessel.EtikettBreiteMm, heute, 62, ct),
            await parameter.ZahlAsync(ParameterSchluessel.EtikettHoeheMm, heute, 29, ct),
            await parameter.ZahlAsync(ParameterSchluessel.EtikettBogenSpalten, heute, 3, ct),
            await parameter.ZahlAsync(ParameterSchluessel.EtikettBogenZeilen, heute, 8, ct));
        var feuerwehr = (await db.OrganisationAsync(ct)).FeuerwehrName;
        return File(PlakettenPdf.Erzeugen(plaketten, Format, masse, feuerwehr), "application/pdf",
            Format == PlakettenPdf.Format.Drucker ? "Pruefplaketten-Etikettendrucker.pdf" : "Pruefplaketten-A4.pdf");
    }
}
