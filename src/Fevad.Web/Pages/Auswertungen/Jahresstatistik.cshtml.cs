using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

/// <summary>Jahresstatistik der Feuerwehr mit Ausgabe als PDF-Jahresbericht und CSV-Jahresmeldung.</summary>
[Authorize(Policy = Berechtigungen.StatistikLesen)]
public class JahresstatistikModel(Jahresstatistik statistik, AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Der berechnete Bericht.</summary>
    public Jahresstatistik.Bericht Bericht { get; private set; } = null!;
    /// <summary>Laufendes Jahr, als Obergrenze der Auswahl.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Zeigt die Statistik eines Jahres.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe das laufende.</param>
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

    /// <summary>Liefert den Quartalsbericht für den Bürgermeister als PDF (V56), nur Summen.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe das laufende.</param>
    /// <param name="quartal">Quartal 1 bis 4.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die PDF-Datei oder 400 bei ungültigem Jahr oder Quartal.</returns>
    public async Task<IActionResult> OnGetQuartalAsync(int? jahr, int quartal, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr) || quartal is < 1 or > 4)
        {
            return BadRequest();
        }

        var (erster, letzter) = Jahresstatistik.Quartal(jahr!.Value, quartal);
        var bericht = await statistik.ZeitraumAsync(erster, letzter, ct);
        var pdf = QuartalsberichtPdf.Erzeugen(bericht, quartal, (await db.OrganisationAsync(ct)).FeuerwehrName, zeit.GetUtcNow());
        return File(pdf, "application/pdf", $"Quartalsbericht_{jahr}_Q{quartal}.pdf");
    }

    /// <summary>Liefert den Jahresbericht als PDF.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die PDF-Datei oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetPdfAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        var pdf = JahresberichtPdf.Erzeugen(bericht, (await db.OrganisationAsync(ct)).FeuerwehrName, zeit.GetUtcNow());
        return File(pdf, "application/pdf", $"Jahresbericht_{jahr}.pdf");
    }

    /// <summary>Liefert den Jahresbericht als Word-Datei zum Weiterschreiben (F23).</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die DOCX-Datei oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetDocxAsync(int? jahr, CancellationToken ct)
    {
        if (!JahrPruefen(ref jahr))
        {
            return BadRequest();
        }

        var bericht = await statistik.ErstellenAsync(jahr!.Value, ct);
        var docx = JahresberichtDocx.Erzeugen(bericht, (await db.OrganisationAsync(ct)).FeuerwehrName, zeit.GetUtcNow());
        return File(docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Jahresbericht_{jahr}.docx");
    }

    /// <summary>Liefert die Jahresmeldung als CSV.</summary>
    /// <param name="jahr">Berichtsjahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die CSV-Datei oder 400 bei ungültigem Jahr.</returns>
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
