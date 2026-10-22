using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

[AllowAnonymous]
public class AbmeldenModel(SignInManager<Benutzer> anmeldung) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await anmeldung.SignOutAsync();
        return RedirectToPage("Abmelden");
    }
}
