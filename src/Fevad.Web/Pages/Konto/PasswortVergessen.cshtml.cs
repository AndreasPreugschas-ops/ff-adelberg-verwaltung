using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Link zum Zurücksetzen des Passworts per E-Mail anfordern.</summary>
[AllowAnonymous]
public class PasswortVergessenModel(UserManager<Benutzer> benutzer, Kontolinks links, ILogger<PasswortVergessenModel> log) : PageModel
{
    /// <summary>E-Mail-Adresse des Kontos.</summary>
    [BindProperty, Required(ErrorMessage = "E-Mail-Adresse fehlt."), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse")]
    public string Email { get; set; } = "";

    /// <summary>Der Mailversand ist eingerichtet.</summary>
    public bool MailAktiv => links.MailAktiv;
    /// <summary>Die Anfrage wurde verarbeitet; die Seite verrät nicht, ob es das Konto gibt.</summary>
    public bool Gesendet { get; private set; }

    /// <summary>Zeigt das Formular.</summary>
    public void OnGet()
    {
    }

    /// <summary>Verschickt den Link, wenn es ein Konto mit dieser Adresse gibt.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
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
