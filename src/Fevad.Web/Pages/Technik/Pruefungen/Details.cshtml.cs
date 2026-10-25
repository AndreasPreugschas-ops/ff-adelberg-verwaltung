using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Pruefungen;

/// <summary>Prüfprotokoll zum Drucken. Fehlerhafte Protokolle werden mit Grund storniert, nie geändert (V58).</summary>
public class DetailsModel(AppDbContext db, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer, Pruefungserfassung erfassung) : PageModel
{
    /// <summary>Storno ist möglich und der Benutzer darf prüfen.</summary>
    public bool DarfStornieren { get; private set; }
    /// <summary>Name des Kontos, das die Prüfung erfasst hat.</summary>
    public string? ErfasstVon { get; private set; }
    /// <summary>Name des Kontos, das storniert hat.</summary>
    public string? StorniertVon { get; private set; }

    /// <summary>Die Prüfung.</summary>
    public Pruefung Pruefung { get; private set; } = null!;
    /// <summary>Geprüftes Gerät oder Fahrzeug als Text.</summary>
    public string Gegenstand { get; private set; } = "";
    /// <summary>Ausgefüllte Checkliste.</summary>
    public IReadOnlyList<PruefpunktErgebnis> Ergebnisse { get; private set; } = [];
    /// <summary>Mängel aus dieser Prüfung.</summary>
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];

    /// <summary>Zeigt das Prüfprotokoll.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var pruefung = await db.Pruefungen.AsNoTracking()
            .Include(p => p.Geraet)
            .Include(p => p.Fahrzeug)
            .Include(p => p.Pruefer)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pruefung is null)
        {
            return NotFound();
        }

        Pruefung = pruefung;
        Gegenstand = pruefung.Geraet is { } g ? $"{g.Inventarnummer} {g.Bezeichnung}" : pruefung.Fahrzeug?.Funkrufname ?? "";
        Ergebnisse = PruefpunktErgebnis.AusJson(pruefung.Ergebnisse);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.PruefungId == id)).ToListAsync(ct);
        ErfasstVon = await KontoNameAsync(pruefung.ErstelltVon, ct);
        StorniertVon = await KontoNameAsync(pruefung.StorniertVon, ct);
        DarfStornieren = pruefung.StornoHindernis() is null && (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        return Page();
    }

    /// <summary>Storniert die Prüfung mit Grund; Einsatzsperren, die sie aufgehoben hat, gelten wieder.</summary>
    public async Task<IActionResult> OnPostStornierenAsync(Guid id, string? grund, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        var fehler = await erfassung.StornierenAsync(id, aktuellerBenutzer.Id ?? Guid.Empty, grund ?? "", ct);
        if (fehler is null)
        {
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = "Prüfung storniert. Bitte die richtige Prüfung neu erfassen.";
        }
        else
        {
            TempData["Fehler"] = fehler;
        }

        return RedirectToPage(new { id });
    }

    private async Task<string?> KontoNameAsync(Guid? benutzerId, CancellationToken ct) =>
        benutzerId is null
            ? null
            : await (from b in db.Users
                     where b.Id == benutzerId
                     join m in db.Mitglieder on b.MitgliedId equals m.Id into ms
                     from m in ms.DefaultIfEmpty()
                     select m != null ? m.Vorname + " " + m.Nachname : b.UserName)
                .FirstOrDefaultAsync(ct);
}
