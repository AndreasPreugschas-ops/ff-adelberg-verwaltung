using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

/// <summary>Einstieg zu Entschädigung und Jahresstatistik; zeigt nur, was die Rolle sehen darf.</summary>
public class IndexModel(IAuthorizationService autorisierung) : PageModel
{
    public bool Entschaedigung { get; private set; }
    public bool Statistik { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Entschaedigung = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungLesen)).Succeeded;
        Statistik = (await autorisierung.AuthorizeAsync(User, Berechtigungen.StatistikLesen)).Succeeded;
        return Entschaedigung || Statistik ? Page() : Forbid();
    }
}
