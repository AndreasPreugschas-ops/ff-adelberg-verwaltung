using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace Fevad.Web.Pages.Konto;

/// <summary>Einrichten oder Wechseln der Authenticator-App (TOTP). Danach gibt es neue Wiederherstellungscodes.</summary>
public class AuthenticatorModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung, ILogger<AuthenticatorModel> log) : PageModel
{
    private const string Aussteller = "FEVAD";

    /// <summary>Code aus der App zur Bestätigung.</summary>
    [BindProperty, Required(ErrorMessage = "Code fehlt."), Display(Name = "Code aus der App")]
    public string Code { get; set; } = "";

    /// <summary>Schlüssel zum Eintippen in die App.</summary>
    public string Schluessel { get; private set; } = "";
    /// <summary>QR-Code mit dem Schlüssel als SVG.</summary>
    public string QrCodeSvg { get; private set; } = "";
    /// <summary>Zwei-Faktor-Anmeldung ist für dieses Konto Pflicht und fehlt noch.</summary>
    public bool Pflicht => ZweiFaktorPflicht.Fehlt(User);

    /// <summary>Zeigt Schlüssel und QR-Code.</summary>
    /// <returns>Die Seite oder die Anmeldung.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        if (!konto.TwoFactorEnabled)
        {
            await AnzeigeVorbereitenAsync(konto);
        }

        Aktiv = konto.TwoFactorEnabled;
        return Page();
    }

    /// <summary>Die Zwei-Faktor-Anmeldung ist bereits eingeschaltet.</summary>
    public bool Aktiv { get; private set; }

    /// <summary>Neues Handy oder Handy verloren: alter Schlüssel wird ungültig, die App muss neu eingerichtet werden.</summary>
    public async Task<IActionResult> OnPostErsetzenAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        await benutzer.SetTwoFactorEnabledAsync(konto, false);
        await benutzer.ResetAuthenticatorKeyAsync(konto);
        await anmeldung.RefreshSignInAsync(konto);
        log.LogInformation("Authenticator-Schlüssel für {Email} ersetzt", konto.Email);
        return RedirectToPage();
    }

    /// <summary>Prüft den Code, schaltet die Zwei-Faktor-Anmeldung ein und erzeugt neue Wiederherstellungscodes.</summary>
    /// <returns>Weiterleitung zu den Wiederherstellungscodes, bei falschem Code die Seite.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        if (konto.TwoFactorEnabled)
        {
            return RedirectToPage("Index");
        }

        if (!ModelState.IsValid)
        {
            await AnzeigeVorbereitenAsync(konto);
            return Page();
        }

        var code = Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var gueltig = await benutzer.VerifyTwoFactorTokenAsync(konto, benutzer.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!gueltig)
        {
            ModelState.AddModelError(nameof(Code), "Der Code stimmt nicht. Uhrzeit am Handy prüfen und den aktuellen Code eingeben.");
            await AnzeigeVorbereitenAsync(konto);
            return Page();
        }

        await benutzer.SetTwoFactorEnabledAsync(konto, true);
        var codes = await benutzer.GenerateNewTwoFactorRecoveryCodesAsync(konto, 10);
        await anmeldung.RefreshSignInAsync(konto);
        log.LogInformation("Zwei-Faktor-Anmeldung für {Email} eingerichtet", konto.Email);

        TempData[WiederherstellungscodesModel.TempDataSchluessel] = string.Join(';', codes ?? []);
        return RedirectToPage("Wiederherstellungscodes");
    }

    private async Task AnzeigeVorbereitenAsync(Benutzer konto)
    {
        var schluessel = await benutzer.GetAuthenticatorKeyAsync(konto);
        if (string.IsNullOrEmpty(schluessel))
        {
            // Ändert den Sicherheitsstempel, deshalb die eigene Sitzung erneuern.
            await benutzer.ResetAuthenticatorKeyAsync(konto);
            await anmeldung.RefreshSignInAsync(konto);
            schluessel = await benutzer.GetAuthenticatorKeyAsync(konto);
        }

        Schluessel = string.Join(' ', schluessel!.ToLowerInvariant().Chunk(4).Select(t => new string(t)));
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Aussteller)}:{Uri.EscapeDataString(konto.Email!)}?secret={schluessel}&issuer={Uri.EscapeDataString(Aussteller)}&digits=6";

        using var generator = new QRCodeGenerator();
        using var daten = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        QrCodeSvg = new SvgQRCode(daten).GetGraphic(4);
    }
}
