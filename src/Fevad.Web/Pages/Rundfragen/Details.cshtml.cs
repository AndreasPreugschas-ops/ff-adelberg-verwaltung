using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Mitteilungen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Rundfragen;

/// <summary>
/// Eine Rundfrage (V42): Empfänger antworten; wer sie verwalten darf, sieht Empfängerzahl und Auswertung (bei „Wer kann?“ auch nach
/// Qualifikationen), verschickt und beendet sie.
/// </summary>
public class DetailsModel(AppDbContext db, Infrastructure.Kommunikation.Rundfragen rundfragen, Schreibrecht schreibrecht, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Rundfrage.</summary>
    public Rundfrage Rundfrage { get; private set; } = null!;
    /// <summary>Eintrag des angemeldeten Kontos als Empfänger, sonst null.</summary>
    public RundfrageEmpfaenger? Eigener { get; private set; }
    /// <summary>Eigene Antwort (nicht bei anonymen Umfragen).</summary>
    public RundfrageAntwort? Antwort { get; private set; }
    /// <summary>Der Benutzer darf die Rundfrage verwalten.</summary>
    public bool Verwalten { get; private set; }
    /// <summary>Bei einem Entwurf: wie viele Konten sie heute erreichen würde.</summary>
    public int? Vorschau { get; private set; }
    /// <summary>Auswertung für den Verwaltenden.</summary>
    public Infrastructure.Kommunikation.Rundfragen.Ergebnis? Ergebnis { get; private set; }
    /// <summary>Antworten sind heute möglich.</summary>
    public bool Offen { get; private set; }

    /// <summary>Zeigt die Rundfrage.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 404 oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LadenAsync(id, ct) ?? Page();

    /// <summary>Speichert die eigene Antwort.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="auswahl">Gewählte Nummern.</param>
    /// <param name="bemerkung">Bemerkung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Rundfrage.</returns>
    public async Task<IActionResult> OnPostAntwortenAsync(Guid id, int[] auswahl, string? bemerkung, CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return Forbid();
        }

        var fehler = await rundfragen.AntwortenAsync(id, konto, auswahl, bemerkung, ct);
        TempData[fehler is null ? "Meldung" : "Fehler"] = fehler ?? "Danke, deine Antwort ist gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Verschickt den Entwurf.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Rundfrage.</returns>
    public async Task<IActionResult> OnPostVerschickenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Verwalten)
        {
            return Forbid();
        }

        var anzahl = await rundfragen.VerschickenAsync(id, await VerfasserAsync(ct), ct);
        TempData[anzahl is null ? "Fehler" : "Meldung"] = anzahl is null ? "Die Rundfrage ließ sich nicht verschicken." : $"Verschickt an {anzahl} Empfänger.";
        return RedirectToPage(new { id });
    }

    /// <summary>Beendet die Rundfrage.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Rundfrage.</returns>
    public async Task<IActionResult> OnPostBeendenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Verwalten)
        {
            return Forbid();
        }

        await rundfragen.BeendenAsync(id, ct);
        TempData["Meldung"] = "Die Rundfrage ist beendet.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult?> LadenAsync(Guid id, CancellationToken ct)
    {
        var r = await db.Rundfragen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return NotFound();
        }

        Rundfrage = r;
        Offen = r.AntwortenMoeglich(Anzeige.Heute(zeit));
        Verwalten = await schreibrecht.DarfAsync(User, r.Ziel, r.Abteilungen, r.JugendveranstaltungId, ct);
        if (benutzer.Id is { } konto && r.Status != RundfrageStatus.Entwurf && await rundfragen.EigeneAsync(id, konto, ct) is { } eigene)
        {
            (Eigener, Antwort) = eigene;
        }

        if (!Verwalten && Eigener is null)
        {
            return Forbid();
        }

        if (Verwalten)
        {
            if (r.Status == RundfrageStatus.Entwurf)
            {
                Vorschau = await rundfragen.VorschauAsync(r, Anzeige.Heute(zeit), ct);
            }
            else
            {
                Ergebnis = await rundfragen.ErgebnisAsync(r, ct);
            }
        }

        return null;
    }

    private async Task<string> VerfasserAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return "FEVAD";
        }

        var name = await db.Users.Where(u => u.Id == konto)
            .Select(u => db.Mitglieder.Where(x => x.Id == u.MitgliedId).Select(x => x.Vorname + " " + x.Nachname).FirstOrDefault() ?? u.UserName)
            .FirstOrDefaultAsync(ct);
        return name ?? "FEVAD";
    }
}
