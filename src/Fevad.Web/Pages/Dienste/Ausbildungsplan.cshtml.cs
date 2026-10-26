using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Jahresausbildungsplan (V53): Ausbildungsdienste des Jahres nach Monaten, Ampel je Pflichtthema der Standortausbildung, Stundenkonten
/// (Standortfortbildung und Truppmann Teil 2) und das Dienstbuch als PDF.
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungLesen)]
public class AusbildungsplanModel(Ausbildungsplaene plaene, Kassenberichte berichte, TimeProvider zeit) : PageModel
{
    /// <summary>Kalenderjahr.</summary>
    public int Jahr { get; private set; }
    /// <summary>Heutiges Datum.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Dienste und Pflichtthemen.</summary>
    public Ausbildungsplaene.Plan Plan { get; private set; } = new([], []);
    /// <summary>Stundenkonten der Einsatzabteilung.</summary>
    public IReadOnlyList<Ausbildungsplaene.Konto> Konten { get; private set; } = [];

    /// <summary>Lädt den Plan eines Jahres.</summary>
    /// <param name="jahr">Kalenderjahr; leer = laufendes Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Jahr = jahr is >= 2000 and <= 2100 ? jahr.Value : Heute.Year;
        Plan = await plaene.PlanAsync(Jahr, ct);
        Konten = await plaene.StundenkontenAsync(Jahr < Heute.Year ? new DateOnly(Jahr, 12, 31) : Heute, ct);
    }

    /// <summary>Dienstbuch des Jahres als PDF.</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnGetDienstbuchAsync(int jahr, CancellationToken ct)
    {
        var dienste = await plaene.DienstbuchAsync(jahr, ct);
        var pdf = DienstbuchPdf.Erzeugen(await berichte.KopfAsync(ct), jahr, dienste, Anzeige.DatumZeit(zeit.GetUtcNow()));
        return File(pdf, "application/pdf", $"dienstbuch-{jahr}.pdf");
    }
}
