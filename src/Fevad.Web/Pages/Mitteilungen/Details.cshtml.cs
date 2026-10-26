using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitteilungen;

/// <summary>
/// Eine Mitteilung (V42): Empfänger lesen und bestätigen; wer sie verwalten darf, sieht Empfängerzahl, Lesestand und veröffentlicht
/// oder zieht zurück. Beim Öffnen gilt die Benachrichtigung als gelesen.
/// </summary>
public class DetailsModel(AppDbContext db, Infrastructure.Kommunikation.Mitteilungen mitteilungen, Schreibrecht schreibrecht, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Die Mitteilung.</summary>
    public Mitteilung Mitteilung { get; private set; } = null!;
    /// <summary>Eintrag des angemeldeten Kontos als Empfänger, sonst null.</summary>
    public MitteilungEmpfaenger? Eigener { get; private set; }
    /// <summary>Der Benutzer darf die Mitteilung verwalten.</summary>
    public bool Verwalten { get; private set; }
    /// <summary>Bei einem Entwurf: wie viele Konten sie heute erreichen würde.</summary>
    public int? Vorschau { get; private set; }
    /// <summary>Lesestand für den Verwaltenden.</summary>
    public IReadOnlyList<Infrastructure.Kommunikation.Mitteilungen.Lesestand> Lesestand { get; private set; } = [];
    /// <summary>Beschreibung der Zielgruppe.</summary>
    public string Zielgruppe { get; private set; } = "";

    /// <summary>Zeigt die Mitteilung.</summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 404 oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LadenAsync(id, ct) ?? Page();

    /// <summary>Bestätigt das Lesen.</summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Mitteilung.</returns>
    public async Task<IActionResult> OnPostBestaetigenAsync(Guid id, CancellationToken ct)
    {
        if (benutzer.Id is not { } konto || !await mitteilungen.BestaetigenAsync(id, konto, ct))
        {
            return NotFound();
        }

        TempData["Meldung"] = "Danke, das Lesen ist bestätigt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Veröffentlicht den Entwurf.</summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Mitteilung.</returns>
    public async Task<IActionResult> OnPostVeroeffentlichenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Verwalten)
        {
            return Forbid();
        }

        var verfasser = await VerfasserAsync(ct);
        var anzahl = await mitteilungen.VeroeffentlichenAsync(id, verfasser, ct);
        TempData[anzahl is null ? "Fehler" : "Meldung"] = anzahl is null
            ? "Die Mitteilung ließ sich nicht veröffentlichen."
            : $"Veröffentlicht an {anzahl} Empfänger.";
        return RedirectToPage(new { id });
    }

    /// <summary>Zieht die Mitteilung zurück.</summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Mitteilung.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Verwalten)
        {
            return Forbid();
        }

        await mitteilungen.ZurueckziehenAsync(id, ct);
        TempData["Meldung"] = "Die Mitteilung ist zurückgezogen.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult?> LadenAsync(Guid id, CancellationToken ct)
    {
        var m = await db.Mitteilungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
        {
            return NotFound();
        }

        Mitteilung = m;
        Verwalten = await schreibrecht.DarfAsync(User, m, ct);
        if (benutzer.Id is { } konto && m.Status == MitteilungStatus.Veroeffentlicht && Request.Method == HttpMethods.Get)
        {
            Eigener = await mitteilungen.LesenAsync(id, konto, ct);
        }
        else if (benutzer.Id is { } k)
        {
            Eigener = await db.MitteilungEmpfaenger.AsNoTracking().FirstOrDefaultAsync(e => e.MitteilungId == id && e.KontoId == k, ct);
        }

        if (!Verwalten && (Eigener is null || m.Status != MitteilungStatus.Veroeffentlicht))
        {
            return Forbid();
        }

        if (Verwalten)
        {
            if (m.Status == MitteilungStatus.Entwurf)
            {
                Vorschau = (await mitteilungen.EmpfaengerAsync(m, Anzeige.LokalesDatum(DateTimeOffset.UtcNow), ct)).Count;
            }
            else
            {
                Lesestand = await mitteilungen.LesestandAsync(id, ct);
            }

            var namen = await db.Abteilungen.AsNoTracking().Where(a => m.Abteilungen.Contains(a.Id)).OrderBy(a => a.Sortierung).Select(a => a.Bezeichnung).ToListAsync(ct);
            Zielgruppe = m.Ziel switch
            {
                MitteilungZiel.Mitglieder => "Mitglieder: " + string.Join(", ", namen),
                MitteilungZiel.Eltern => "Eltern: " + string.Join(", ", namen),
                _ => "Eltern der Angemeldeten: " + await db.Jugendveranstaltungen.Where(v => v.Id == m.JugendveranstaltungId).Select(v => v.Dienst!.Titel).FirstOrDefaultAsync(ct),
            };
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
