using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Auswertungen;

/// <summary>Einstieg zu Entschädigung, Jahresstatistik und Führungskennzahlen; zeigt nur, was die Rolle sehen darf.</summary>
public class IndexModel(IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Der Benutzer darf die Entschädigung sehen.</summary>
    public bool Entschaedigung { get; private set; }
    /// <summary>Der Benutzer darf die Jahresstatistik sehen.</summary>
    public bool Statistik { get; private set; }
    /// <summary>Der Benutzer darf die Führungskennzahlen sehen.</summary>
    public bool Kennzahlen { get; private set; }

    /// <summary>Zeigt die Auswertungen, für die der Benutzer Rechte hat.</summary>
    /// <returns>Die Seite oder 403 ohne jedes Recht.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        Entschaedigung = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungLesen)).Succeeded;
        Statistik = (await autorisierung.AuthorizeAsync(User, Berechtigungen.StatistikLesen)).Succeeded;
        Kennzahlen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KennzahlenLesen)).Succeeded;
        return Entschaedigung || Statistik || Kennzahlen ? Page() : Forbid();
    }
}
