using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

[AllowAnonymous]
public class AnmeldenWiederherstellungModel(SignInManager<Benutzer> anmeldung, ILogger<AnmeldenWiederherstellungModel> log) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Code fehlt."), Display(Name = "Wiederherstellungscode")]
    public string Code { get; set; } = "";

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

        if (await anmeldung.GetTwoFactorAuthenticationUserAsync() is not { } konto)
        {
            return RedirectToPage("Anmelden");
        }

        var ergebnis = await anmeldung.TwoFactorRecoveryCodeSignInAsync(Code.Replace(" ", string.Empty));
        if (ergebnis.Succeeded)
        {
            log.LogWarning("Anmeldung {Email} mit Wiederherstellungscode", konto.Email);
            return LocalRedirect(AnmeldenModel.Ziel(Url, ReturnUrl));
        }

        if (ergebnis.IsLockedOut)
        {
            return RedirectToPage("Gesperrt");
        }

        ModelState.AddModelError(string.Empty, "Der Wiederherstellungscode ist ungültig oder schon verbraucht.");
        return Page();
    }
}
