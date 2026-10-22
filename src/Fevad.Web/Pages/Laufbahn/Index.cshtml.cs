using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Anstehende Übertritte, fällige Ehrungen und mögliche Beförderungen aller Mitglieder.</summary>
public class IndexModel(Laufbahnabfrage laufbahn, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public DateOnly Heute { get; private set; }
    public int VorlaufMonate { get; private set; }
    public bool DarfRegeln { get; private set; }
    public IReadOnlyList<Laufbahnstand> Staende { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        VorlaufMonate = (await laufbahn.RegelnAsync(Heute, ct)).VorlaufMonate;
        DarfRegeln = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnRegeln)).Succeeded;
        Staende = await laufbahn.LadenAsync(Heute, ct: ct);
    }
}
