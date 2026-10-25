using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Beschaffung;

/// <summary>Anlagennachweis für die Kämmerei (V60): Fahrzeuge und inventarisierungspflichtige Geräte mit Abgängen des Jahres, als Liste und CSV.</summary>
public class AnlagenModel(Anlagennachweis nachweis, TimeProvider zeit) : PageModel
{
    /// <summary>Jahr der Abgänge; Standard ist das laufende Jahr.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    /// <summary>Zeilen des Nachweises.</summary>
    public IReadOnlyList<Anlagennachweis.Zeile> Zeilen { get; private set; } = [];

    /// <summary>Zeigt den Nachweis.</summary>
    public async Task OnGetAsync(CancellationToken ct) => Zeilen = await LadenAsync(ct);

    /// <summary>Liefert den Nachweis als CSV.</summary>
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct) =>
        File(Anlagennachweis.Csv(await LadenAsync(ct)), "text/csv; charset=utf-8", $"Anlagennachweis_{Jahr}.csv");

    private async Task<IReadOnlyList<Anlagennachweis.Zeile>> LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Jahr = Jahr is >= 2000 and <= 2100 ? Jahr : heute.Year;
        return await nachweis.ErstellenAsync(Jahr.Value, heute, ct);
    }
}
