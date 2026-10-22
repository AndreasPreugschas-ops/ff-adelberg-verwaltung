using System.Security.Cryptography;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Termine;

/// <summary>„Meine Termine“: kommende Dienste der eigenen Zielgruppe mit Zu-/Absage und Kalender-Abo.</summary>
public class IndexModel(Terminlader lader, ParameterLeser parameter, UserManager<Benutzer> benutzer, IOptions<AnmeldungOptionen> anmeldung, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<TerminKarte> Karten { get; private set; } = [];
    public bool HatMitglied { get; private set; }
    public int Offen => Karten.Count(k => k.KannMelden && k.Antwort is null);
    public string? AboLink { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        HatMitglied = await lader.MitgliedIdAsync(ct) is not null;
        var heute = Anzeige.Heute(zeit);
        var vorschau = await parameter.ZahlAsync(ParameterSchluessel.KalenderVorschauMonate, heute, 12, ct);
        Karten = await lader.KartenAsync(Anzeige.AlsUtc(heute, TimeOnly.MinValue), Anzeige.AlsUtc(heute.AddMonths(vorschau), TimeOnly.MinValue), alle: false, ct);

        if (await benutzer.GetUserAsync(User) is { KalenderToken: { } token })
        {
            // Die öffentliche Adresse gilt auch für Kalender-Apps außerhalb des Gerätehaus-Netzes.
            var basis = anmeldung.Value.OeffentlicheAdresse?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            AboLink = $"{basis}/kalender/{token}.ics";
        }

        return Page();
    }

    /// <summary>Erzeugt einen neuen Abo-Link; der bisherige wird damit ungültig.</summary>
    public async Task<IActionResult> OnPostAboAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { MitgliedId: not null } konto)
        {
            return Forbid();
        }

        var neu = konto.KalenderToken is not null;
        konto.KalenderToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        await benutzer.UpdateAsync(konto);
        TempData["Meldung"] = neu ? "Neuer Abo-Link erzeugt, der alte gilt nicht mehr." : "Abo-Link erzeugt.";
        return RedirectToPage(null, null, "abo");
    }

    /// <summary>Schaltet das Abo ab.</summary>
    public async Task<IActionResult> OnPostAboAusAsync()
    {
        if (await benutzer.GetUserAsync(User) is not { } konto)
        {
            return Forbid();
        }

        konto.KalenderToken = null;
        await benutzer.UpdateAsync(konto);
        TempData["Meldung"] = "Kalender-Abo abgeschaltet.";
        return RedirectToPage(null, null, "abo");
    }
}
