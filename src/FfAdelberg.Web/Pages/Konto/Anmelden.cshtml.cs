using System.ComponentModel.DataAnnotations;
using FfAdelberg.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FfAdelberg.Web.Pages.Konto;

[AllowAnonymous]
public class AnmeldenModel(SignInManager<Benutzer> anmeldung, ILogger<AnmeldenModel> log) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public sealed class Eingabe
    {
        [Required(ErrorMessage = "E-Mail-Adresse fehlt."), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Passwort fehlt."), DataType(DataType.Password), Display(Name = "Passwort")]
        public string Passwort { get; set; } = "";

        public bool Merken { get; set; }
    }

    public async Task OnGetAsync()
    {
        // Reste einer abgebrochenen Zwei-Faktor-Anmeldung entfernen.
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ergebnis = await anmeldung.PasswordSignInAsync(Formular.Email.Trim(), Formular.Passwort, Formular.Merken, lockoutOnFailure: true);
        if (ergebnis.Succeeded)
        {
            log.LogInformation("Anmeldung {Email}", Formular.Email);
            return LocalRedirect(Ziel(Url, ReturnUrl));
        }

        if (ergebnis.RequiresTwoFactor)
        {
            return RedirectToPage("AnmeldenCode", new { ReturnUrl, Formular.Merken });
        }

        if (ergebnis.IsLockedOut)
        {
            return RedirectToPage("Gesperrt");
        }

        ModelState.AddModelError(string.Empty, ergebnis.IsNotAllowed
            ? "Dieses Konto ist deaktiviert. Bitte wende dich an den Administrator."
            : "E-Mail-Adresse oder Passwort ist falsch.");
        return Page();
    }

    /// <summary>Nur lokale Rücksprungadressen, sonst die Startseite.</summary>
    internal static string Ziel(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
