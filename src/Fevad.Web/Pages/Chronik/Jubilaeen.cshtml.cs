using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Chronik;

/// <summary>
/// Jubiläumsvorschau (V56): Mitgliedsjubiläen und runde Geburtstage eines Jahres für alle mit Zugang zur Personalverwaltung, jeweils nur für die
/// Mitglieder, die sie dort sehen dürfen.
/// </summary>
[Authorize(Policy = Berechtigungen.MitgliederSehen)]
public class JubilaeenModel(Jubilaeen jubilaeen, AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
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

        Eintraege = await jubilaeen.ImJahrAsync(await datenbereich.Filtern(db.Mitglieder, User, heute), Jahr.Value, heute, ct);
        return Page();
    }
}
