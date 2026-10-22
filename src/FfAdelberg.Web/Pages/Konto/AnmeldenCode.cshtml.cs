using System.ComponentModel.DataAnnotations;
using FfAdelberg.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FfAdelberg.Web.Pages.Konto;

[AllowAnonymous]
public class AnmeldenCodeModel(SignInManager<Benutzer> anmeldung) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Code fehlt."), Display(Name = "Code")]
    public string Code { get; set; } = "";

    [BindProperty]
    public bool GeraetMerken { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Merken { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await anmeldung.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("Anmelden") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await anmeldung.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("Anmelden");
        }

        var code = Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var ergebnis = await anmeldung.TwoFactorAuthenticatorSignInAsync(code, Merken, GeraetMerken);
        if (ergebnis.Succeeded)
        {
            return LocalRedirect(AnmeldenModel.Ziel(Url, ReturnUrl));
        }

        if (ergebnis.IsLockedOut)
        {
            return RedirectToPage("Gesperrt");
        }

        ModelState.AddModelError(string.Empty, "Der Code stimmt nicht. Bitte den aktuellen Code aus der App eingeben.");
        return Page();
    }
}
