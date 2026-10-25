using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Abmelden; die Seite bestätigt danach die Abmeldung.</summary>
[AllowAnonymous]
public class AbmeldenModel(SignInManager<Benutzer> anmeldung) : PageModel
{
    /// <summary>Zeigt die Seite.</summary>
    public void OnGet()
    {
    }

    /// <summary>Meldet ab.</summary>
    /// <returns>Weiterleitung auf diese Seite.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        await anmeldung.SignOutAsync();
        return RedirectToPage("Abmelden");
    }
}
