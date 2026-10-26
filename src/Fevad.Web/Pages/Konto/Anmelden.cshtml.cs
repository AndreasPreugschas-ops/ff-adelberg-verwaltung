using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Anmeldung mit E-Mail-Adresse und Passwort; danach folgt bei Bedarf der zweite Faktor.</summary>
[AllowAnonymous]
public class AnmeldenModel(SignInManager<Benutzer> anmeldung, UserManager<Benutzer> benutzer, ILogger<AnmeldenModel> log) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Lokale Adresse, zu der nach der Anmeldung weitergeleitet wird.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Formular der Anmeldung.</summary>
    public sealed class Eingabe
    {
        /// <summary>E-Mail-Adresse.</summary>
        [Required(ErrorMessage = "E-Mail-Adresse fehlt."), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse")]
        public string Email { get; set; } = "";

        /// <summary>Passwort.</summary>
        [Required(ErrorMessage = "Passwort fehlt."), DataType(DataType.Password), Display(Name = "Passwort")]
        public string Passwort { get; set; } = "";

        /// <summary>Angemeldet bleiben.</summary>
        public bool Merken { get; set; }
    }

    /// <summary>Zeigt das Formular und beendet einen halb begonnenen Zwei-Faktor-Vorgang.</summary>
    public async Task OnGetAsync()
    {
        // Reste einer abgebrochenen Zwei-Faktor-Anmeldung entfernen.
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
    }

    /// <summary>Meldet mit Passwort an.</summary>
    /// <returns>Weiterleitung zum Ziel, zum zweiten Faktor oder zur Sperrseite; bei Fehlern die Seite.</returns>
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

    /// <summary>Liefert die Optionen für die Anmeldung mit Passkey (V41); ohne Benutzername wählt der Browser den Passkey.</summary>
    /// <returns>Die Optionen als JSON.</returns>
    public async Task<IActionResult> OnPostPasskeyOptionenAsync() =>
        Content(await anmeldung.MakePasskeyRequestOptionsAsync(null), "application/json");

    /// <summary>
    /// Meldet mit einem Passkey an. Ein Passkey mit Benutzerprüfung (Fingerabdruck, Gesicht, PIN) ersetzt Passwort und zweiten Faktor.
    /// Sperre und deaktivierte Konten werden wie bei der Anmeldung mit Passwort geprüft.
    /// </summary>
    /// <param name="credentialJson">Antwort des Browsers.</param>
    /// <param name="merken">Angemeldet bleiben.</param>
    /// <returns>Weiterleitung zum Ziel, zur Sperrseite oder die Seite mit Fehler.</returns>
    public async Task<IActionResult> OnPostPasskeyAsync(string? credentialJson, bool merken)
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(credentialJson))
        {
            ModelState.AddModelError(string.Empty, "Die Anmeldung mit Passkey wurde abgebrochen.");
            return Page();
        }

        var pruefung = await anmeldung.PerformPasskeyAssertionAsync(credentialJson);
        if (!pruefung.Succeeded)
        {
            log.LogInformation("Passkey-Anmeldung abgelehnt: {Grund}", pruefung.Failure.Message);
            ModelState.AddModelError(string.Empty, "Dieser Passkey ist hier nicht bekannt. Bitte mit Passwort anmelden.");
            return Page();
        }

        var konto = pruefung.User;
        if (await benutzer.IsLockedOutAsync(konto))
        {
            return RedirectToPage("Gesperrt");
        }

        if (!await anmeldung.CanSignInAsync(konto))
        {
            ModelState.AddModelError(string.Empty, "Dieses Konto ist deaktiviert. Bitte wende dich an den Administrator.");
            return Page();
        }

        await benutzer.AddOrUpdatePasskeyAsync(konto, pruefung.Passkey);
        await benutzer.ResetAccessFailedCountAsync(konto);
        await anmeldung.SignInWithClaimsAsync(konto, merken, [new System.Security.Claims.Claim("amr", "pop")]);
        log.LogInformation("Anmeldung mit Passkey {Email}", konto.Email);
        return LocalRedirect(Ziel(Url, ReturnUrl));
    }

    /// <summary>Nur lokale Rücksprungadressen, sonst die Startseite.</summary>
    internal static string Ziel(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
