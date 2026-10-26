using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

/// <summary>Führungskennzahlen (V52): Ist/Soll, Zeiten gegen das Schutzziel, Beteiligung und Abgangsprognose; CSV ohne Namen.</summary>
[Authorize(Policy = Berechtigungen.KennzahlenLesen)]
public class FuehrungModel(Fuehrungskennzahlen kennzahlen, TimeProvider zeit) : PageModel
{
    /// <summary>Der berechnete Bericht.</summary>
    public Fuehrungskennzahlen.Bericht Bericht { get; private set; } = null!;

    /// <summary>Laufendes Jahr, als Obergrenze der Auswahl.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Zeigt die Kennzahlen; ohne Jahr das laufende Jahr.</summary>
    /// <param name="jahr">Auswertungsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        Bericht = await kennzahlen.BerichtAsync(jahr!.Value, Anzeige.Heute(zeit), ct);
        return Page();
    }

    /// <summary>Kennzahlen ohne Namen als CSV, z. B. für die Fortschreibung des Bedarfsplans.</summary>
    /// <param name="jahr">Auswertungsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetCsvAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var b = await kennzahlen.BerichtAsync(jahr!.Value, Anzeige.Heute(zeit), ct);
        return File(Fuehrungskennzahlen.Csv(b), "text/csv; charset=utf-8", $"Fuehrungskennzahlen-{b.Jahr}.csv");
    }

    /// <summary>Zahl mit einer Nachkommastelle oder Strich.</summary>
    /// <param name="wert">Der Wert.</param>
    /// <returns>Der Text.</returns>
    public static string Zahl(double? wert) => wert is { } w ? w.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) : "–";

    private bool JahrPruefen(ref int? jahr)
    {
        AktuellesJahr = Anzeige.Heute(zeit).Year;
        jahr ??= AktuellesJahr;
        return jahr >= 2000 && jahr <= AktuellesJahr;
    }
}
