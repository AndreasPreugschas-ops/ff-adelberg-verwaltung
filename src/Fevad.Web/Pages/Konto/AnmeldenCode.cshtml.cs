using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Zweiter Faktor der Anmeldung: Code aus der Authenticator-App.</summary>
[AllowAnonymous]
public class AnmeldenCodeModel(SignInManager<Benutzer> anmeldung) : PageModel
{
    /// <summary>Code aus der App.</summary>
    [BindProperty, Required(ErrorMessage = "Code fehlt."), Display(Name = "Code")]
    public string Code { get; set; } = "";

    /// <summary>Dieses Gerät merken und hier keinen Code mehr verlangen.</summary>
    [BindProperty]
    public bool GeraetMerken { get; set; }

    /// <summary>Angemeldet bleiben, aus dem ersten Schritt übernommen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Merken { get; set; }

    /// <summary>Lokale Adresse, zu der nach der Anmeldung weitergeleitet wird.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Zeigt das Formular, ohne begonnene Anmeldung zurück zur Anmeldung.</summary>
    /// <returns>Die Seite oder Weiterleitung zur Anmeldung.</returns>
    public async Task<IActionResult> OnGetAsync() =>
        await anmeldung.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("Anmelden") : Page();

    /// <summary>Prüft den Code und meldet an.</summary>
    /// <returns>Weiterleitung zum Ziel, zur Anmeldung oder zur Sperrseite; bei falschem Code die Seite.</returns>
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
