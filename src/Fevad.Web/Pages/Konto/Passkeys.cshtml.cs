using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Passkeys des eigenen Kontos (V41): einrichten, umbenennen und entfernen. Ein Passkey mit Benutzerprüfung (Fingerabdruck,
/// Gesicht, Geräte-PIN) ersetzt bei der Anmeldung Passwort und Authenticator-Code; gespeichert wird nur der öffentliche Schlüssel.
/// </summary>
public class PasskeysModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung, TimeProvider zeit, ILogger<PasskeysModel> log) : PageModel
{
    /// <summary>Ein eingerichteter Passkey.</summary>
    /// <param name="Id">Kennung des Schlüssels (Base64url).</param>
    /// <param name="Name">Selbst vergebener Name.</param>
    /// <param name="Eingerichtet">Zeitpunkt der Einrichtung.</param>
    public sealed record Zeile(string Id, string? Name, DateTimeOffset Eingerichtet);

    /// <summary>Die Passkeys des Kontos.</summary>
    public IReadOnlyList<Zeile> Passkeys { get; private set; } = [];

    /// <summary>Zeigt die Passkeys.</summary>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        Passkeys = (await benutzer.GetPasskeysAsync(konto))
            .OrderBy(p => p.CreatedAt)
            .Select(p => new Zeile(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(p.CredentialId), p.Name, p.CreatedAt))
            .ToList();
        return Page();
    }

    /// <summary>Liefert die Optionen zum Einrichten eines Passkeys.</summary>
    /// <returns>Die Optionen als JSON.</returns>
    public async Task<IActionResult> OnPostOptionenAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        var name = konto.Email ?? konto.UserName ?? "Konto";
        return Content(await anmeldung.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = konto.Id.ToString(), Name = name, DisplayName = name,
        }), "application/json");
    }

    /// <summary>Speichert einen neu eingerichteten Passkey.</summary>
    /// <param name="credentialJson">Antwort des Browsers.</param>
    /// <param name="name">Name, z. B. „Handy“.</param>
    /// <returns>Zurück zur Seite.</returns>
    public async Task<IActionResult> OnPostAsync(string? credentialJson, [StringLength(100)] string? name)
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(credentialJson))
        {
            TempData["Fehler"] = "Die Einrichtung wurde abgebrochen.";
            return RedirectToPage();
        }

        var ergebnis = await anmeldung.PerformPasskeyAttestationAsync(credentialJson);
        if (!ergebnis.Succeeded)
        {
            log.LogInformation("Passkey-Einrichtung abgelehnt: {Grund}", ergebnis.Failure.Message);
            TempData["Fehler"] = "Der Passkey konnte nicht eingerichtet werden. Bitte noch einmal versuchen.";
            return RedirectToPage();
        }

        var passkey = ergebnis.Passkey;
        passkey.Name = string.IsNullOrWhiteSpace(name) ? $"Passkey vom {Fevad.Web.Infrastruktur.Anzeige.DatumZeit(zeit.GetUtcNow())}" : name.Trim()[..Math.Min(100, name.Trim().Length)];
        var speichern = await benutzer.AddOrUpdatePasskeyAsync(konto, passkey);
        TempData[speichern.Succeeded ? "Meldung" : "Fehler"] = speichern.Succeeded
            ? $"Passkey „{passkey.Name}“ eingerichtet. Ab jetzt geht die Anmeldung auf diesem Gerät auch ohne Passwort."
            : "Der Passkey konnte nicht gespeichert werden.";
        return RedirectToPage();
    }

    /// <summary>Entfernt einen Passkey, z. B. bei einem verlorenen Handy.</summary>
    /// <param name="id">Kennung des Schlüssels (Base64url).</param>
    /// <returns>Zurück zur Seite.</returns>
    public async Task<IActionResult> OnPostEntfernenAsync(string id)
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        byte[] schluessel;
        try
        {
            schluessel = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(id);
        }
        catch (FormatException)
        {
            return NotFound();
        }

        var ergebnis = await benutzer.RemovePasskeyAsync(konto, schluessel);
        TempData[ergebnis.Succeeded ? "Meldung" : "Fehler"] = ergebnis.Succeeded ? "Passkey entfernt." : "Passkey nicht gefunden.";
        return RedirectToPage();
    }
}
