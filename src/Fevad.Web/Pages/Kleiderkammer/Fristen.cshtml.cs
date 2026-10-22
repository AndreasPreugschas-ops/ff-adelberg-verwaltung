using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Prüffristen und Lebensdauer aller PSA-Einzelstücke, das Dringendste zuerst.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class FristenModel(Kleiderkammerstand stand, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<PsaStand> Zeilen { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Zeilen = (await stand.PsaAsync(Anzeige.Heute(zeit), ct))
            .OrderByDescending(p => p.Stand.Ampel)
            .ThenBy(p => new[] { p.Stand.NaechsterTermin, p.Stand.AussonderungFaellig }.Where(d => d is not null).DefaultIfEmpty(DateOnly.MaxValue).Min())
            .ThenBy(p => p.Stand.Nummer)
            .ToList();
}
