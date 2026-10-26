using Fevad.Domain.Technik;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Wartungsvorschau (V61): was in den nächsten Jahren an Grundüberholungen, Flaschenprüfungen, Bauteiltausch und Ersatz ansteht,
/// als Grundlage für die Haushaltsanmeldung.
/// </summary>
public class VorschauModel(Wartungsvorschau vorschau, TimeProvider zeit) : PageModel
{
    /// <summary>Filter auf einen Rücklaufweg; leer zeigt alle Geräte.</summary>
    [BindProperty(SupportsGet = true)]
    public Ruecklauf? Werkstatt { get; set; }

    /// <summary>Die Zeilen nach Jahr.</summary>
    public IReadOnlyList<IGrouping<int, Vorschauzeile>> Jahre { get; private set; } = [];

    /// <summary>Berechnet die Vorschau.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Jahre = (await vorschau.BerechnenAsync(Anzeige.Heute(zeit), Werkstatt, ct)).GroupBy(z => z.Jahr).ToList();
    }
}
