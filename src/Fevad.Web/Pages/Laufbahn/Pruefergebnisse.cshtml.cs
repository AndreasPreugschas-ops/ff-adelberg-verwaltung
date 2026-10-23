using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>
/// Ergebnisse der Personalprüfung, die nach jeder Änderung (z. B. neuer Lehrgang), beim Start und täglich läuft.
/// Zurückgestellte Anlässe meldet die Prüfung nicht erneut; entfallene sind von selbst erledigt.
/// </summary>
public class PruefergebnisseModel(
    AppDbContext db, Personalpruefung pruefung, Datenbereich datenbereich, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public sealed record Zeile(Pruefhinweis Hinweis, string Name);

    public IReadOnlyList<Zeile> Offen { get; private set; } = [];
    public IReadOnlyList<Zeile> Abgeschlossen { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }
    public DateTimeOffset? LetzteErkennung { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBearbeiten)).Succeeded;
        var mitglieder = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, Anzeige.Heute(zeit));
        var sichtbar = db.Pruefhinweise.AsNoTracking().Where(p => mitglieder.Any(m => m.Id == p.MitgliedId));
        var seit = zeit.GetUtcNow().AddDays(-90);

        Offen = await Laden(sichtbar.Where(p => p.Status == PruefhinweisStatus.Offen)
            .OrderBy(p => p.FaelligAm == null).ThenBy(p => p.FaelligAm).ThenBy(p => p.Mitglied!.Nachname), ct);
        Abgeschlossen = await Laden(sichtbar.Where(p => p.Status != PruefhinweisStatus.Offen && (p.AbgeschlossenAm == null || p.AbgeschlossenAm >= seit))
            .OrderByDescending(p => p.AbgeschlossenAm), ct);
        LetzteErkennung = await sichtbar.MaxAsync(p => (DateTimeOffset?)p.ErkanntAm, ct);
    }

    private static async Task<IReadOnlyList<Zeile>> Laden(IQueryable<Pruefhinweis> abfrage, CancellationToken ct) =>
        (await abfrage.Select(p => new { P = p, p.Mitglied!.Nachname, p.Mitglied.Vorname }).Take(500).ToListAsync(ct))
            .Select(x => new Zeile(x.P, $"{x.Nachname}, {x.Vorname}")).ToList();

    /// <summary>Alle Mitglieder sofort prüfen, z. B. nach einer Regeländerung.</summary>
    public async Task<IActionResult> OnPostAllePruefenAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var ergebnis = await pruefung.PruefenAsync(null, zeit.GetUtcNow(), ct);
        TempData["Meldung"] = $"{ergebnis.Geprueft} Mitglieder geprüft: {ergebnis.Neu} neue, {ergebnis.Entfallen} entfallene Hinweise.";
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostZurueckstellenAsync(Guid hinweisId, CancellationToken ct) =>
        StatusSetzenAsync(hinweisId, PruefhinweisStatus.Zurueckgestellt, ct);

    public Task<IActionResult> OnPostWiederOeffnenAsync(Guid hinweisId, CancellationToken ct) =>
        StatusSetzenAsync(hinweisId, PruefhinweisStatus.Offen, ct);

    private async Task<IActionResult> StatusSetzenAsync(Guid hinweisId, PruefhinweisStatus status, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var mitglieder = await datenbereich.Filtern(db.Mitglieder, User, Anzeige.Heute(zeit));
        var hinweis = await db.Pruefhinweise.Where(p => mitglieder.Any(m => m.Id == p.MitgliedId)).SingleOrDefaultAsync(p => p.Id == hinweisId, ct);
        if (hinweis is null)
        {
            return NotFound();
        }

        hinweis.Status = status;
        hinweis.AbgeschlossenAm = status == PruefhinweisStatus.Offen ? null : zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = status == PruefhinweisStatus.Offen ? "Wieder offen." : "Zurückgestellt; die Prüfung meldet diesen Anlass nicht erneut.";
        return RedirectToPage();
    }
}
