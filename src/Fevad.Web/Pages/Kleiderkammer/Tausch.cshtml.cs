using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>
/// Tauschanforderungen der Mitglieder (V44): offene zuerst, mit Grund und Wunschgröße. Der Tausch selbst läuft als Rücknahme und Ausgabe;
/// hier wird die Anforderung erledigt oder mit Antwort abgelehnt, und das Mitglied bekommt eine Benachrichtigung.
/// </summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class TauschModel(AppDbContext db, Benachrichtigungsdienst benachrichtigungen, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Offene und kürzlich bearbeitete Anforderungen.</summary>
    public IReadOnlyList<Tauschanforderung> Anforderungen { get; private set; } = [];

    /// <summary>Darf Anforderungen bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Zeigt die Anforderungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;
        var seit = zeit.GetUtcNow().AddDays(-90);
        Anforderungen = await db.Tauschanforderungen.AsNoTracking()
            .Include(t => t.Mitglied).Include(t => t.Ausstattung).ThenInclude(a => a!.Artikel)
            .Where(t => t.Status == TauschStatus.Offen || t.BearbeitetAm >= seit)
            .OrderBy(t => t.Status).ThenBy(t => t.ErstelltAm).ToListAsync(ct);
    }

    /// <summary>Erledigt oder lehnt eine Anforderung ab.</summary>
    /// <param name="id">Die Anforderung.</param>
    /// <param name="status">Erledigt oder Abgelehnt.</param>
    /// <param name="antwort">Antwort an das Mitglied, z. B. ein Termin.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, TauschStatus status, string? antwort, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var t = await db.Tauschanforderungen.Include(x => x.Ausstattung).ThenInclude(a => a!.Artikel)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == TauschStatus.Offen, ct);
        if (t is null || status == TauschStatus.Offen)
        {
            return NotFound();
        }

        t.Status = status;
        t.BearbeitetAm = zeit.GetUtcNow();
        t.Antwort = string.IsNullOrWhiteSpace(antwort) ? null : antwort.Trim()[..Math.Min(antwort.Trim().Length, 500)];
        await db.SaveChangesAsync(ct);
        await benachrichtigungen.AnMitgliederAsync([t.MitgliedId],
            new(Benachrichtigungsart.Hinweis, $"Tausch {t.Ausstattung?.Artikel?.Bezeichnung}: {Anzeige.Text(status)}", "/Konto/Ausstattung", t.Id, t.Antwort), ct);
        TempData["Meldung"] = $"Anforderung {Anzeige.Text(status)}.";
        return RedirectToPage();
    }
}
