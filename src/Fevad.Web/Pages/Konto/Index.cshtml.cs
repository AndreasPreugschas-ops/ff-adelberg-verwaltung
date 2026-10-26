using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>Eigenes Konto: Rollen, Zwei-Faktor-Anmeldung, Wiederherstellungscodes und Erinnerungen.</summary>
public class IndexModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung, AppDbContext db) : PageModel
{
    /// <summary>Das angemeldete Konto.</summary>
    public Benutzer Konto { get; private set; } = null!;
    /// <summary>Name des zugeordneten Mitglieds.</summary>
    public string? Mitglied { get; private set; }
    /// <summary>Rollen des Kontos.</summary>
    public IList<string> Rollen { get; private set; } = [];
    /// <summary>Zahl der noch unbenutzten Wiederherstellungscodes.</summary>
    public int RestlicheCodes { get; private set; }

    /// <summary>Als aktiver Atemschutzgeräteträger gekennzeichnet: darf sich selbst als nicht einsetzbar melden.</summary>
    public bool Atemschutztraeger { get; private set; }
    /// <summary>Dem Konto sind Kinder als Erziehungsberechtigter zugeordnet (Elternkonto).</summary>
    public bool HatKinder { get; private set; }
    /// <summary>Zwei-Faktor-Anmeldung ist für dieses Konto Pflicht.</summary>
    public bool Pflicht => ZweiFaktorPflicht.Erforderlich(User);

    /// <summary>Zeigt das Konto.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder die Anmeldung.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        Konto = konto;
        Rollen = await benutzer.GetRolesAsync(konto);
        RestlicheCodes = await benutzer.CountRecoveryCodesAsync(konto);
        HatKinder = await db.Erziehungsberechtigte.AnyAsync(e => e.KontoId == konto.Id, ct);
        if (konto.MitgliedId is { } mitgliedId)
        {
            Mitglied = await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
            Atemschutztraeger = await db.Mitglieder.AnyAsync(m => m.Id == mitgliedId && m.AtemschutzAktiv, ct);
        }

        return Page();
    }

    /// <summary>Speichert, an was FEVAD das Mitglied selbst per Mail erinnert (V38).</summary>
    /// <param name="fristen">Eigene Fristen (Lehrgänge, Nachweise, Atemschutz).</param>
    /// <param name="ausstattung">Fällige Prüfungen der eigenen Schutzausrüstung.</param>
    /// <param name="rueckmeldungen">Offene Rückmeldungen zu Terminen.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostErinnerungenAsync(bool fristen, bool ausstattung, bool rueckmeldungen)
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        konto.OhneErinnerungFristen = !fristen;
        konto.OhneErinnerungAusstattung = !ausstattung;
        konto.OhneErinnerungRueckmeldungen = !rueckmeldungen;
        await benutzer.UpdateAsync(konto);
        TempData["Meldung"] = "Erinnerungen gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Erzeugt neue Wiederherstellungscodes; die alten werden ungültig.</summary>
    /// <returns>Weiterleitung zu den neuen Codes.</returns>
    public async Task<IActionResult> OnPostNeueCodesAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { TwoFactorEnabled: true } konto)
        {
            return RedirectToPage();
        }

        var codes = await benutzer.GenerateNewTwoFactorRecoveryCodesAsync(konto, 10);
        TempData[WiederherstellungscodesModel.TempDataSchluessel] = string.Join(';', codes ?? []);
        return RedirectToPage("Wiederherstellungscodes");
    }

    /// <summary>Schaltet die Zwei-Faktor-Anmeldung aus und setzt den App-Schlüssel zurück.</summary>
    /// <returns>Weiterleitung auf diese Seite.</returns>
    public async Task<IActionResult> OnPostAusschaltenAsync()
    {
        if (Pflicht || await benutzer.GetUserAsync(User) is not { } konto)
        {
            return RedirectToPage();
        }

        await benutzer.SetTwoFactorEnabledAsync(konto, false);
        await benutzer.ResetAuthenticatorKeyAsync(konto);
        await anmeldung.RefreshSignInAsync(konto);
        TempData["Meldung"] = "Zwei-Faktor-Anmeldung ausgeschaltet.";
        return RedirectToPage();
    }

    /// <summary>Meldet das Konto auf allen Geräten ab.</summary>
    /// <returns>Weiterleitung zur Anmeldung.</returns>
    public async Task<IActionResult> OnPostUeberallAbmeldenAsync()
    {
        if (await benutzer.GetUserAsync(User) is { } konto)
        {
            // Neuer Sicherheitsstempel: andere Sitzungen fallen bei der nächsten Prüfung raus, „Gerät merken“ verfällt.
            await benutzer.UpdateSecurityStampAsync(konto);
            await anmeldung.ForgetTwoFactorClientAsync();
        }

        await anmeldung.SignOutAsync();
        return RedirectToPage("Anmelden");
    }
}
