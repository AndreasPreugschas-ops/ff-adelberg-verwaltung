using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Anstehende Übertritte, fällige Ehrungen und mögliche Beförderungen aller Mitglieder.</summary>
public class IndexModel(Laufbahnabfrage laufbahn, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Vorlauf in Monaten, ab dem anstehende Übertritte und Ehrungen gemeldet werden.</summary>
    public int VorlaufMonate { get; private set; }
    /// <summary>Der Benutzer darf die Laufbahnregeln bearbeiten.</summary>
    public bool DarfRegeln { get; private set; }
    /// <summary>Laufbahnstand aller sichtbaren Mitglieder.</summary>
    public IReadOnlyList<Laufbahnstand> Staende { get; private set; } = [];

    /// <summary>Lädt den Laufbahnstand aller sichtbaren Mitglieder.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        VorlaufMonate = (await laufbahn.RegelnAsync(Heute, ct)).VorlaufMonate;
        DarfRegeln = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnRegeln)).Succeeded;
        Staende = await laufbahn.LadenAsync(Heute, ct: ct);
    }
}
