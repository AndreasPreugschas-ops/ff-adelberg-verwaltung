using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Was nachbestellt werden muss: Mindestbestand, fehlende Grundausstattung und ablaufende Lebensdauer.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class BedarfModel(Kleiderkammerstand stand, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<BedarfZeile> Zeilen { get; private set; } = [];
    public int VorlaufMonate { get; private set; }
    public bool DarfBeschaffen { get; private set; }

    [BindProperty]
    public List<string> Auswahl { get; set; } = [];

    public static string Schluessel(BedarfZeile z) => $"{z.ArtikelId}|{z.Groesse}";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Zeilen = await stand.BedarfAsync(heute, ct);
        VorlaufMonate = await parameter.ZahlAsync(ParameterSchluessel.KleiderkammerAussonderungVorlaufMonate, heute, 6, ct);
        DarfBeschaffen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.BeschaffungBearbeiten)).Succeeded;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.BeschaffungBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var auswahl = Auswahl
            .Select(s => s.Split('|', 2))
            .Where(t => t.Length == 2 && Guid.TryParse(t[0], out _))
            .Select(t => (Guid.Parse(t[0]), t[1]))
            .ToList();
        var anzahl = await stand.NachbestellungenAnlegenAsync(Anzeige.Heute(zeit), auswahl, ct);
        TempData["Meldung"] = anzahl == 0 ? "Nichts ausgewählt." : $"{anzahl} Beschaffung(en) mit Status „Bedarf“ angelegt.";
        return RedirectToPage();
    }
}
