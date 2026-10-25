using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

/// <summary>Stichtagsnachweis der Einsatzabteilung zum 31.12. mit Pauschalförderung und Zuschüssen (V28), als Liste, PDF und CSV.</summary>
[Authorize(Policy = Berechtigungen.StatistikLesen)]
public class StichtagslisteModel(Stichtagsliste liste, AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Der berechnete Nachweis.</summary>
    public Stichtagsliste.Nachweis Nachweis { get; private set; } = null!;

    /// <summary>Laufendes Jahr, als Obergrenze der Auswahl.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Der Stichtag liegt noch in der Zukunft; die Liste ist vorläufig.</summary>
    public bool Vorlaeufig { get; private set; }

    /// <summary>Zeigt den Nachweis; ohne Jahr das Vorjahr.</summary>
    public async Task<IActionResult> OnGetAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        Nachweis = await liste.ErstellenAsync(jahr!.Value, ct);
        Vorlaeufig = Nachweis.Stichtag > Anzeige.Heute(zeit);
        return Page();
    }

    /// <summary>Nachweis als PDF; mit <paramref name="nurAnzahlen"/> ohne Namen (Datenminimierung).</summary>
    public async Task<IActionResult> OnGetPdfAsync(int? jahr, bool nurAnzahlen, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var n = await liste.ErstellenAsync(jahr!.Value, ct);
        var o = await db.OrganisationAsync(ct);
        var kopf = new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp);
        return File(StichtagslistePdf.Erzeugen(kopf, n, Anzeige.Lokal(zeit.GetUtcNow()).DateTime, nurAnzahlen), "application/pdf", StichtagslistePdf.Dateiname(n, "pdf", nurAnzahlen));
    }

    /// <summary>Nachweis als CSV.</summary>
    public async Task<IActionResult> OnGetCsvAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var n = await liste.ErstellenAsync(jahr!.Value, ct);
        return File(Stichtagsliste.Csv(n), "text/csv; charset=utf-8", StichtagslistePdf.Dateiname(n, "csv"));
    }

    private bool JahrPruefen(ref int? jahr)
    {
        AktuellesJahr = Anzeige.Heute(zeit).Year;
        jahr ??= AktuellesJahr - 1;
        return jahr >= 2000 && jahr <= AktuellesJahr;
    }
}
