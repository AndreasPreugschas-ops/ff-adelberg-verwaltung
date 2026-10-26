using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Chronik;

/// <summary>Jubiläumsvorschau (V56): Mitgliedsjubiläen und runde Geburtstage eines Jahres, nur für Kommandant und Schriftführung.</summary>
[Authorize(Policy = Berechtigungen.Jubilaeen)]
public class JubilaeenModel(Jubilaeen jubilaeen, TimeProvider zeit) : PageModel
{
    /// <summary>Das Jahr; ohne Angabe das laufende.</summary>
    [BindProperty(SupportsGet = true)] public int? Jahr { get; set; }

    /// <summary>Die Jubiläen.</summary>
    public IReadOnlyList<Jubilaeen.Eintrag> Eintraege { get; private set; } = [];

    /// <summary>Laufendes Jahr.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Zeigt die Jubiläen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        AktuellesJahr = heute.Year;
        Jahr ??= heute.Year;
        if (Jahr < heute.Year - 1 || Jahr > heute.Year + 2)
        {
            return BadRequest();
        }

        Eintraege = await jubilaeen.ImJahrAsync(Jahr.Value, heute, mitGeburtstagen: true, ct);
        return Page();
    }
}
