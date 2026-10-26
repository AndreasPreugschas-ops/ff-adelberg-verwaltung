using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Gesamtexport (V70): alle Daten als ZIP mit JSON je Tabelle und den gespeicherten Dateien, als Ausstiegsweg neben dem Datenbank-Dump.
/// Nur für Administratoren und nur nach erneuter Bestätigung mit Authenticator-Code oder Passkey; jeder Export steht im Änderungsprotokoll
/// und wird den übrigen Administratoren gemeldet.
/// </summary>
[Authorize(Policy = Berechtigungen.Gesamtexport)]
public class ExportModel(
    Gesamtexport export, Benachrichtigungsdienst benachrichtigungen, IAktuellerBenutzer benutzer, UserManager<Fevad.Infrastructure.Identitaet.Benutzer> konten,
    SignInManager<Fevad.Infrastructure.Identitaet.Benutzer> anmeldung, TimeProvider zeit) : PageModel
{
    /// <summary>Bisherige Exporte, neueste zuerst.</summary>
    public IReadOnlyList<(DateTimeOffset Zeitpunkt, string? Konto)> Bisherige { get; private set; } = [];

    /// <summary>Zeigt Hinweise und bisherige Exporte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => Bisherige = await export.BisherigeAsync(ct);

    /// <summary>Prüft den Code aus der Authenticator-App und erstellt dann den Export.</summary>
    /// <param name="code">Sechsstelliger Code.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das ZIP oder die Seite mit Fehler.</returns>
    public async Task<IActionResult> OnPostAsync(string? code, CancellationToken ct)
    {
        var konto = benutzer.Id is { } id ? await konten.FindByIdAsync(id.ToString()) : null;
        var eingabe = (code ?? "").Replace(" ", "", StringComparison.Ordinal);
        if (konto is null || eingabe.Length == 0
            || !await konten.VerifyTwoFactorTokenAsync(konto, konten.Options.Tokens.AuthenticatorTokenProvider, eingabe))
        {
            return await FehlerAsync("Der Code stimmt nicht. Bitte den aktuellen Code aus der Authenticator-App eingeben.", ct);
        }

        return await ExportierenAsync(ct);
    }

    /// <summary>Optionen für die Bestätigung mit Passkey, nur mit den Passkeys des eigenen Kontos.</summary>
    /// <returns>JSON für den Browser.</returns>
    public async Task<IActionResult> OnPostPasskeyOptionenAsync()
    {
        var konto = benutzer.Id is { } id ? await konten.FindByIdAsync(id.ToString()) : null;
        return konto is null ? Forbid() : Content(await anmeldung.MakePasskeyRequestOptionsAsync(konto), "application/json");
    }

    /// <summary>Prüft den Passkey des angemeldeten Kontos und erstellt dann den Export.</summary>
    /// <param name="credentialJson">Antwort des Browsers.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das ZIP oder die Seite mit Fehler.</returns>
    public async Task<IActionResult> OnPostPasskeyAsync(string? credentialJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentialJson))
        {
            return await FehlerAsync("Die Bestätigung mit Passkey wurde abgebrochen.", ct);
        }

        var pruefung = await anmeldung.PerformPasskeyAssertionAsync(credentialJson);
        if (!pruefung.Succeeded || pruefung.User.Id != benutzer.Id)
        {
            return await FehlerAsync("Dieser Passkey gehört nicht zu deinem Konto.", ct);
        }

        await konten.AddOrUpdatePasskeyAsync(pruefung.User, pruefung.Passkey);
        return await ExportierenAsync(ct);
    }

    private async Task<IActionResult> FehlerAsync(string text, CancellationToken ct)
    {
        ModelState.AddModelError(string.Empty, text);
        Bisherige = await export.BisherigeAsync(ct);
        return Page();
    }

    /// <summary>Erstellt den Export in einer temporären Datei und liefert ihn aus; die Datei verschwindet nach dem Herunterladen.</summary>
    private async Task<IActionResult> ExportierenAsync(CancellationToken ct)
    {
        var pfad = Path.GetTempFileName();
        var datei = new FileStream(pfad, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            var umfang = await export.SchreibenAsync(datei, ct);
            datei.Position = 0;
            await benachrichtigungen.AnRechtAsync(Berechtigungen.Gesamtexport,
                new(Benachrichtigungsart.Hinweis, $"Gesamtexport erstellt von {User.Identity?.Name}", "/Verwaltung/Export",
                    Text: $"{umfang.Tabellen} Tabellen, {umfang.Zeilen} Zeilen, {umfang.Dateien} Dateien"), benutzer.Id, ct);
            return File(datei, "application/zip", $"FEVAD-Gesamtexport-{Anzeige.Heute(zeit):yyyy-MM-dd}.zip");
        }
        catch
        {
            await datei.DisposeAsync();
            throw;
        }
    }
}
