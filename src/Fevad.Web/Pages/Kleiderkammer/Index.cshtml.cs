using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Lagerbestand der Kleiderkammer je Artikel und Größe.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class IndexModel(Kleiderkammerstand stand) : PageModel
{
    /// <summary>Bestandszeilen.</summary>
    public IReadOnlyList<BestandZeile> Zeilen { get; private set; } = [];

    /// <summary>Lädt den Bestand.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => Zeilen = await stand.BestandAsync(ct);
}
