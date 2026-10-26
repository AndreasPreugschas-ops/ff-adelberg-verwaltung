using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Abteilungen;

/// <summary>Jahresstatistik der Jugend- und Kinderfeuerwehr zum 31.12. mit CSV für die Meldungen und Jugendbericht als PDF (V49).</summary>
[Authorize(Policy = Berechtigungen.JugendLesen)]
public class JugendstatistikModel(Jugendstatistik statistik, AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Der berechnete Bericht.</summary>
    public Jugendstatistik.Bericht Bericht { get; private set; } = null!;
    /// <summary>Laufendes Jahr, als Obergrenze der Auswahl.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Zeigt die Statistik eines Jahres.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe im Januar und Februar das Vorjahr, sonst das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        Bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        return Page();
    }

    /// <summary>Liefert den Jugendbericht als PDF.</summary>
    /// <param name="jahr">Berichtsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die PDF-Datei oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetPdfAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        var pdf = JugendberichtPdf.Erzeugen(bericht, (await db.OrganisationAsync(ct)).FeuerwehrName, zeit.GetUtcNow());
        return File(pdf, "application/pdf", JugendberichtPdf.Dateiname(jahr.Value, "pdf"));
    }

    /// <summary>Liefert die Jugendstatistik als CSV.</summary>
    /// <param name="jahr">Berichtsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die CSV-Datei oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetCsvAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        return File(Jugendstatistik.Csv(bericht), "text/csv; charset=utf-8", $"Jugendstatistik_{jahr}.csv");
    }

    private bool JahrPruefen(ref int? jahr)
    {
        var heute = Anzeige.Heute(zeit);
        AktuellesJahr = heute.Year;
        jahr ??= heute.Month <= 2 ? heute.Year - 1 : heute.Year;
        return jahr is >= 1990 and <= 2100;
    }
}
