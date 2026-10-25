using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Ziel der Einrichtungs- und Zurücksetzen-Links. Funktioniert ohne Anmeldung.</summary>
[AllowAnonymous]
public class PasswortSetzenModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung) : PageModel
{
    /// <summary>Kennung des Kontos aus dem Link.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    /// <summary>Code aus dem Link.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Code { get; set; }

    /// <summary>Neues Passwort.</summary>
    [BindProperty, Required(ErrorMessage = "Passwort fehlt."), DataType(DataType.Password), Display(Name = "Neues Passwort")]
    public string Passwort { get; set; } = "";

    /// <summary>Wiederholung des Passworts.</summary>
    [BindProperty, DataType(DataType.Password), Display(Name = "Passwort wiederholen"), Compare(nameof(Passwort), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
    public string Wiederholung { get; set; } = "";

    /// <summary>E-Mail-Adresse des Kontos.</summary>
    public string? Email { get; private set; }
    /// <summary>Der Link ist ungültig oder abgelaufen.</summary>
    public bool Ungueltig { get; private set; }

    /// <summary>Zeigt das Formular.</summary>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        var konto = await PruefenAsync();
        Ungueltig = konto is null;
        Email = konto?.Email;
        return Page();
    }

    /// <summary>Setzt das Passwort.</summary>
    /// <returns>Weiterleitung zur Anmeldung, bei Fehlern die Seite.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        var konto = await PruefenAsync();
        if (konto is null)
        {
            Ungueltig = true;
            return Page();
        }

        Email = konto.Email;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ergebnis = await benutzer.ResetPasswordAsync(konto, Kontolinks.CodeLesen(Code)!, Passwort);
        if (!ergebnis.Succeeded)
        {
            foreach (var fehler in ergebnis.Errors)
            {
                ModelState.AddModelError(nameof(Passwort), fehler.Description);
            }

            return Page();
        }

        // Wer den Link bekommen hat, hat Zugriff auf das Postfach oder den Link vom Administrator.
        if (!konto.EmailConfirmed)
        {
            konto.EmailConfirmed = true;
            await benutzer.UpdateAsync(konto);
        }

        await benutzer.ResetAccessFailedCountAsync(konto);
        await benutzer.SetLockoutEndDateAsync(konto, null);
        await anmeldung.SignOutAsync();
        TempData["Meldung"] = "Passwort gespeichert. Du kannst dich jetzt anmelden.";
        return RedirectToPage("Anmelden");
    }

    private async Task<Benutzer?> PruefenAsync()
    {
        var token = Kontolinks.CodeLesen(Code);
        if (token is null || await benutzer.FindByIdAsync(Id.ToString()) is not { IstDeaktiviert: false } konto)
        {
            return null;
        }

        var gueltig = await benutzer.VerifyUserTokenAsync(konto, benutzer.Options.Tokens.PasswordResetTokenProvider, UserManager<Benutzer>.ResetPasswordTokenPurpose, token);
        return gueltig ? konto : null;
    }
}
