using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

[Authorize(Policy = Berechtigungen.StatistikLesen)]
public class JahresstatistikModel(Jahresstatistik statistik, TimeProvider zeit) : PageModel
{
    public Jahresstatistik.Bericht Bericht { get; private set; } = null!;
    public int AktuellesJahr { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        Bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        var pdf = JahresberichtPdf.Erzeugen(bericht, "Freiwillige Feuerwehr Adelberg", zeit.GetUtcNow());
        return File(pdf, "application/pdf", $"Jahresbericht_{jahr}.pdf");
    }

    public async Task<IActionResult> OnGetCsvAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        return File(JahresmeldungCsv.Erzeugen(bericht), "text/csv; charset=utf-8", $"Jahresmeldung_{jahr}.csv");
    }

    /// <summary>Ohne Angabe: im Januar und Februar das Vorjahr (Hauptversammlung), sonst das laufende Jahr.</summary>
    private bool JahrPruefen(ref int? jahr)
    {
        var heute = Anzeige.Heute(zeit);
        AktuellesJahr = heute.Year;
        jahr ??= heute.Month <= 2 ? heute.Year - 1 : heute.Year;
        return jahr is >= 1990 and <= 2100;
    }
}
