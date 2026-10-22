using System.ComponentModel.DataAnnotations;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FfAdelberg.Web.Pages.Konto;

[AllowAnonymous]
public class PasswortVergessenModel(UserManager<Benutzer> benutzer, Kontolinks links, ILogger<PasswortVergessenModel> log) : PageModel
{
    [BindProperty, Required(ErrorMessage = "E-Mail-Adresse fehlt."), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse")]
    public string Email { get; set; } = "";

    public bool MailAktiv => links.MailAktiv;
    public bool Gesendet { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!MailAktiv || !ModelState.IsValid)
        {
            return Page();
        }

        // Immer dieselbe Antwort, damit niemand ausprobieren kann, welche Adressen ein Konto haben.
        Gesendet = true;
        var konto = await benutzer.FindByEmailAsync(Email.Trim());
        if (konto is null || konto.IstDeaktiviert)
        {
            return Page();
        }

        try
        {
            await links.ZuruecksetzenSendenAsync(konto, await links.PasswortLinkAsync(HttpContext, konto), ct);
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            log.LogError(fehler, "Mail zum Zurücksetzen an {Email} nicht versendet", konto.Email);
        }

        return Page();
    }
}
