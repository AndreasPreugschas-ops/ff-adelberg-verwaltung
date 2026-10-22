using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

public class IndexModel(UserManager<Benutzer> benutzer, SignInManager<Benutzer> anmeldung, AppDbContext db) : PageModel
{
    public Benutzer Konto { get; private set; } = null!;
    public string? Mitglied { get; private set; }
    public IList<string> Rollen { get; private set; } = [];
    public int RestlicheCodes { get; private set; }
    public bool Pflicht => ZweiFaktorPflicht.Erforderlich(User);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Challenge();
        }

        Konto = konto;
        Rollen = await benutzer.GetRolesAsync(konto);
        RestlicheCodes = await benutzer.CountRecoveryCodesAsync(konto);
        if (konto.MitgliedId is { } mitgliedId)
        {
            Mitglied = await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
        }

        return Page();
    }

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
