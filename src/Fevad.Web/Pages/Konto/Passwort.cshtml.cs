using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

public class PasswortModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public sealed class Eingabe
    {
        [Required(ErrorMessage = "Aktuelles Passwort fehlt."), DataType(DataType.Password), Display(Name = "Aktuelles Passwort")]
        public string Aktuell { get; set; } = "";

        [Required(ErrorMessage = "Neues Passwort fehlt."), DataType(DataType.Password), Display(Name = "Neues Passwort (mind. 10 Zeichen)")]
        public string Neu { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Neues Passwort wiederholen"), Compare(nameof(Neu), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string Wiederholung { get; set; } = "";
    }

    public void OnGet()
    {
    }

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
