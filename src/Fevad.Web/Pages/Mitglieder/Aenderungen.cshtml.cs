using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Offene Änderungsanträge der Mitglieder (V36) übernehmen oder ablehnen. Zu sehen sind nur Mitglieder, die der Benutzer bearbeiten
/// darf; Anträge mit Beruf oder Arbeitgeber übernimmt nur, wer zusätzlich Mitglieder.Arbeitgeber hat.
/// </summary>
[Authorize(Policy = Berechtigungen.MitgliederBearbeiten)]
public class AenderungenModel(AppDbContext db, Datenbereich datenbereich, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Ein offener Antrag mit dem Mitglied.</summary>
    /// <param name="Antrag">Der Antrag mit Feldern.</param>
    /// <param name="Mitglied">Das Mitglied mit den heute gespeicherten Werten.</param>
    public sealed record Eintrag(Aenderungsantrag Antrag, Mitglied Mitglied);

    /// <summary>Offene Anträge, älteste zuerst.</summary>
    public IReadOnlyList<Eintrag> Offene { get; private set; } = [];
    /// <summary>Der Benutzer darf Beruf und Arbeitgeber sehen und übernehmen.</summary>
    public bool DarfArbeitgeber { get; private set; }

    /// <summary>Zeigt die offenen Anträge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Übernimmt einen Antrag in die Stammdaten; abgelöste Anschriften kommen in den Verlauf.</summary>
    /// <param name="antragId">Kennung des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostUebernehmenAsync(Guid antragId, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (Offene.FirstOrDefault(e => e.Antrag.Id == antragId) is not { } eintrag)
        {
            return NotFound();
        }

        if (eintrag.Antrag.BetrifftArbeitgeber && !DarfArbeitgeber)
        {
            return Forbid();
        }

        var antrag = await db.Aenderungsantraege.Include(a => a.Felder).FirstAsync(a => a.Id == antragId, ct);
        var mitglied = await db.Mitglieder.FirstAsync(m => m.Id == antrag.MitgliedId, ct);
        db.FruehereAnschriften.AddRange(antrag.Uebernehmen(mitglied, Anzeige.Heute(zeit), zeit.GetUtcNow(), User.Identity?.Name));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Änderungen von {mitglied.Anzeigename} übernommen.";
        return RedirectToPage();
    }

    /// <summary>Lehnt einen Antrag mit Begründung ab.</summary>
    /// <param name="antragId">Kennung des Antrags.</param>
    /// <param name="grund">Begründung, die das Mitglied sieht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehler oder 404.</returns>
    public async Task<IActionResult> OnPostAblehnenAsync(Guid antragId, string? grund, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (Offene.FirstOrDefault(e => e.Antrag.Id == antragId) is not { } eintrag)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(grund) || grund.Length > 500)
        {
            ModelState.AddModelError($"grund-{antragId}", "Bitte kurz begründen (höchstens 500 Zeichen); das Mitglied sieht die Begründung.");
            return Page();
        }

        var antrag = await db.Aenderungsantraege.FirstAsync(a => a.Id == antragId, ct);
        antrag.Ablehnen(grund, zeit.GetUtcNow(), User.Identity?.Name);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Antrag von {eintrag.Mitglied.Anzeigename} abgelehnt.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfArbeitgeber = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederArbeitgeber)).Succeeded;
        var mitglieder = await datenbereich.FilternBearbeitenAsync(db.Mitglieder.AsNoTracking(), User, Anzeige.Heute(zeit), ct);
        var offene = await db.Aenderungsantraege.AsNoTracking().Include(a => a.Felder)
            .Where(a => a.Status == AenderungsantragStatus.Offen)
            .Join(mitglieder, a => a.MitgliedId, m => m.Id, (a, m) => new { a, m })
            .ToListAsync(ct);
        Offene = offene.OrderBy(x => x.a.ErstelltAm).Select(x => new Eintrag(x.a, x.m)).ToList();
    }
}
