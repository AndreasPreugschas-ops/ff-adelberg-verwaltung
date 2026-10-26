using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Technik;

/// <summary>Jahresbericht Gerätewart (V64): Kennzahlen, Kosten je Fahrzeug, Bestand und Ausblick, auch als PDF für die Hauptversammlung.</summary>
public class JahresberichtModel(Geraetewartberichte berichte, Kassenberichte kopf, TimeProvider zeit) : PageModel
{
    /// <summary>Der Bericht.</summary>
    public Geraetewartbericht Bericht { get; private set; } = null!;

    /// <summary>Zeigt den Bericht.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe bis Ende März das Vorjahr (Hauptversammlung), danach das laufende Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Bericht = await berichte.ErstellenAsync(jahr ?? Standardjahr(heute), heute, ct);
    }

    /// <summary>Erzeugt den Bericht als PDF.</summary>
    /// <param name="jahr">Berichtsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnGetPdfAsync(int? jahr, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var bericht = await berichte.ErstellenAsync(jahr ?? Standardjahr(heute), heute, ct);
        var pdf = GeraetewartberichtPdf.Erzeugen(await kopf.KopfAsync(ct), bericht, Anzeige.DatumZeit(zeit.GetUtcNow()));
        return File(pdf, "application/pdf", $"Jahresbericht-Geraetewart-{bericht.Jahr}.pdf");
    }

    private static int Standardjahr(DateOnly heute) => heute.Month <= 3 ? heute.Year - 1 : heute.Year;
}
