using Fevad.Domain.Gremien;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Hauptversammlung;

/// <summary>
/// Hauptversammlung für alle Angehörigen (V09): Niederschriften der Hauptversammlungen, für Kassen mit eingeschalteter Mitgliedssicht
/// die abgeschlossenen Jahresabschlüsse und Prüfberichte als PDF und der Stand der eigenen Auslagenerstattungen.
/// </summary>
[Authorize(Policy = Berechtigungen.VersammlungLesen)]
public class IndexModel(AppDbContext db, Kassenberichte berichte, Dienstplanung planung, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Berichte einer Kasse für die Mitglieder.</summary>
    /// <param name="Kasse">Die Kasse.</param>
    /// <param name="Abschluesse">Abgeschlossene Jahre, neueste zuerst.</param>
    /// <param name="Pruefungen">Abgeschlossene Prüfungen, neueste zuerst.</param>
    public sealed record Kassenbericht(Domain.Kassen.Kasse Kasse, IReadOnlyList<Jahresabschluss> Abschluesse, IReadOnlyList<Kassenpruefung> Pruefungen);

    /// <summary>Hauptversammlungen mit Niederschrift, neueste zuerst.</summary>
    public IReadOnlyList<Sitzung> Versammlungen { get; private set; } = [];

    /// <summary>Kassen mit eingeschalteter Mitgliedssicht.</summary>
    public IReadOnlyList<Kassenbericht> Kassen { get; private set; } = [];

    /// <summary>Eigene Auslagenerstattungen, die noch nicht gezahlt oder abgelehnt sind.</summary>
    public int OffeneErstattungen { get; private set; }

    /// <summary>Eigene Auslagenerstattungen insgesamt.</summary>
    public int Erstattungen { get; private set; }

    /// <summary>Lädt die Seite.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Versammlungen = await db.Sitzungen.AsNoTracking()
            .Where(s => s.Art == SitzungArt.Hauptversammlung && (s.Status == SitzungStatus.Durchgefuehrt || s.Status == SitzungStatus.ProtokollGenehmigt)
                && s.Protokoll != null && s.Protokoll != "")
            .OrderByDescending(s => s.Beginn).Take(20).ToListAsync(ct);
        var kassen = await db.Kassen.AsNoTracking().Where(k => k.Aktiv && k.BerichteFuerMitglieder).OrderBy(k => k.Sortierung).ThenBy(k => k.Name).ToListAsync(ct);
        var ids = kassen.Select(k => k.Id).ToList();
        var abschluesse = await db.Jahresabschluesse.AsNoTracking().Where(j => ids.Contains(j.KasseId)).OrderByDescending(j => j.Jahr).ToListAsync(ct);
        var pruefungen = (await db.Kassenpruefungen.AsNoTracking()
                .Where(p => ids.Contains(p.KasseId) && p.Pruefer1BestaetigtAm != null && p.Pruefer2BestaetigtAm != null).OrderByDescending(p => p.Bis).ToListAsync(ct))
            .ToList();
        Kassen = kassen.Select(k => new Kassenbericht(k, abschluesse.Where(j => j.KasseId == k.Id).Take(5).ToList(),
            pruefungen.Where(p => p.KasseId == k.Id).Take(5).ToList())).ToList();
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is { } mitgliedId)
        {
            var eigene = await db.Zahlungsanordnungen.AsNoTracking().Where(a => a.MitgliedId == mitgliedId && a.Art == AnordnungArt.Auslagenerstattung)
                .Select(a => a.Status).ToListAsync(ct);
            (Erstattungen, OffeneErstattungen) = (eigene.Count, eigene.Count(s => s == AnordnungStatus.Vorbereitet));
        }
    }

    /// <summary>Jahresabschluss einer Kasse mit Mitgliedssicht als PDF.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="jahr">Das abgeschlossene Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetAbschlussAsync(Guid kasse, int jahr, CancellationToken ct)
    {
        var k = await db.Kassen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kasse && x.Aktiv && x.BerichteFuerMitglieder, ct);
        if (k is null || !await db.Jahresabschluesse.AnyAsync(j => j.KasseId == kasse && j.Jahr == jahr, ct))
        {
            return NotFound();
        }

        var datei = await berichte.JahresabschlussAsync(k, jahr, ct);
        return File(datei.Inhalt, "application/pdf", datei.Name);
    }

    /// <summary>Abgeschlossener Prüfbericht einer Kasse mit Mitgliedssicht als PDF.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPruefberichtAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Kassenpruefungen.AsNoTracking().Include(x => x.Kasse).Include(x => x.Pruefer1).Include(x => x.Pruefer2)
            .Include(x => x.Beanstandungen).ThenInclude(b => b.Buchung).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is not { Abgeschlossen: true, Kasse: { Aktiv: true, BerichteFuerMitglieder: true } k })
        {
            return NotFound();
        }

        p.Beanstandungen = p.Beanstandungen.OrderBy(b => b.ErstelltAm).ToList();
        var datei = await berichte.PruefberichtAsync(p, k, ct);
        return File(datei.Inhalt, "application/pdf", datei.Name);
    }
}
