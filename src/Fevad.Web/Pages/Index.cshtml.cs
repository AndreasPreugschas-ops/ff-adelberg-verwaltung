using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages;

/// <summary>
/// Einstieg: zuerst den Bereich wählen, dann die Seite. Darüber „Was liegt an“ mit Anzahlen je Bereich, nur aus Bereichen,
/// die das Konto sehen darf; Namen stehen erst in den Bereichen. Wird nachgeladen, damit die Auswahl sofort da ist.
/// </summary>
public class IndexModel(Hauptmenue hauptmenue, WasLiegtAn wasLiegtAn) : PageModel
{
    public IReadOnlyList<Hauptmenue.Bereich> Bereiche { get; private set; } = [];

    /// <summary>Gewählter Bereich; bei nur einem Bereich gleich dieser.</summary>
    public Hauptmenue.Bereich? Gewaehlt { get; private set; }

    public async Task OnGetAsync(string? bereich)
    {
        Bereiche = await hauptmenue.LadenAsync(User);
        Gewaehlt = Bereiche.Count == 1 ? Bereiche[0] : Bereiche.FirstOrDefault(b => string.Equals(b.Titel, bereich, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>HTMX: der Kasten „Was liegt an“.</summary>
    public async Task<IActionResult> OnGetAnliegenAsync(CancellationToken ct) =>
        User.Identity?.IsAuthenticated == true ? Partial("_WasLiegtAn", await wasLiegtAn.LadenAsync(User, ct)) : Unauthorized();
}
