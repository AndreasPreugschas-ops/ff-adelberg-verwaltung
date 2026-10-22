using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class IndexModel(Kleiderkammerstand stand) : PageModel
{
    public IReadOnlyList<BestandZeile> Zeilen { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Zeilen = await stand.BestandAsync(ct);
}
