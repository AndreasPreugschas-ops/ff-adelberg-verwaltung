using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Eigenes Passwort ändern.</summary>
public class PasswortModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Formular der Passwortänderung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Aktuelles Passwort.</summary>
        [Required(ErrorMessage = "Aktuelles Passwort fehlt."), DataType(DataType.Password), Display(Name = "Aktuelles Passwort")]
        public string Aktuell { get; set; } = "";

        /// <summary>Neues Passwort.</summary>
        [Required(ErrorMessage = "Neues Passwort fehlt."), DataType(DataType.Password), Display(Name = "Neues Passwort (mind. 10 Zeichen)")]
        public string Neu { get; set; } = "";

        /// <summary>Wiederholung des neuen Passworts.</summary>
        [DataType(DataType.Password), Display(Name = "Neues Passwort wiederholen"), Compare(nameof(Neu), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string Wiederholung { get; set; } = "";
    }

    /// <summary>Zeigt das Formular.</summary>
    public void OnGet()
    {
    }

    /// <summary>Ändert das Passwort.</summary>
    /// <returns>Weiterleitung zum Konto, bei Fehlern die Seite.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        var ergebnis = await benutzer.ChangePasswordAsync(konto, Formular.Aktuell, Formular.Neu);
        if (!ergebnis.Succeeded)
        {
            foreach (var fehler in ergebnis.Errors)
            {
                ModelState.AddModelError(fehler.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "Formular.Aktuell" : "Formular.Neu", fehler.Description);
            }

            return Page();
        }

        await anmeldung.RefreshSignInAsync(konto);
        TempData["Meldung"] = "Passwort geändert.";
        return RedirectToPage("Index");
    }
}
