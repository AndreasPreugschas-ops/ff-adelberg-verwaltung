using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages;

/// <summary>
/// Einstieg: zuerst den Bereich wählen, dann die Seite. Bewusst ohne Fristen oder Kennzahlen, denn das sind
/// Personaldaten, die nicht jeder sehen darf; die stehen in den Bereichen.
/// </summary>
public class IndexModel(Hauptmenue hauptmenue) : PageModel
{
    public IReadOnlyList<Hauptmenue.Bereich> Bereiche { get; private set; } = [];

    /// <summary>Gewählter Bereich; bei nur einem Bereich gleich dieser.</summary>
    public Hauptmenue.Bereich? Gewaehlt { get; private set; }

    public async Task OnGetAsync(string? bereich)
    {
        Bereiche = await hauptmenue.LadenAsync(User);
        Gewaehlt = Bereiche.Count == 1 ? Bereiche[0] : Bereiche.FirstOrDefault(b => string.Equals(b.Titel, bereich, StringComparison.OrdinalIgnoreCase));
    }
}
