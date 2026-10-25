using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Einen geplanten Dienst ändern, absagen oder löschen; bei Serien wahlweise auch alle folgenden Termine. Darunter die An- und
/// Abmeldungen; geändert werden sie in einem eigenen Fenster (<see cref="MeldungenModel"/>).
/// Der Jugendwart darf nur Dienste der Jugendfeuerwehr ändern und sie nicht aus der Jugendfeuerwehr herausnehmen.
/// </summary>
public class BearbeitenModel(AppDbContext db, Dienstplanung planung, Datenbereich bereich) : PageModel
{
    /// <summary>Der bearbeitete Dienst.</summary>
    public Dienst Dienst { get; private set; } = null!;
    /// <summary>Auswahllisten für Zielgruppe und Übungsleiter.</summary>
    public Zielgruppenlisten Listen { get; private set; } = null!;
    /// <summary>Anzahl der späteren Termine derselben Serie.</summary>
    public int FolgendeInSerie { get; private set; }
    /// <summary>An- und Abmeldungen zum Dienst.</summary>
    public IReadOnlyList<Meldung> Meldungen { get; private set; } = [];

    /// <summary>Der Dienst ist fertig bearbeitet.</summary>
    [BindProperty]
    public bool Abgeschlossen { get; set; }

    /// <summary>Formulardaten der Planung.</summary>
    [BindProperty]
    public Planungsformular Formular { get; set; } = new();

    /// <summary>Status des Dienstes (geplant, durchgeführt, abgesagt).</summary>
    [BindProperty]
    public DienstStatus Status { get; set; }

    /// <summary>Geänderte Zeiten und Angaben auch auf die folgenden Termine der Serie übertragen.</summary>
    [BindProperty]
    public bool FolgendeAnpassen { get; set; }

    /// <summary>Zeigt den Dienst zum Bearbeiten.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!await bereich.DarfDienstBearbeitenAsync(User, Dienst.AbteilungIds, ct))
        {
            return Forbid();
        }

        Formular = Planungsformular.Aus(Dienst);
        Status = Dienst.Status;
        Abgeschlossen = Dienst.Abgeschlossen;
        return Page();
    }

    /// <summary>Speichert die Änderungen, auf Wunsch auch für die folgenden Serientermine.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!await bereich.DarfDienstBearbeitenAsync(User, Dienst.AbteilungIds, ct)
            || !await bereich.DarfDienstBearbeitenAsync(User, Formular.AbteilungIds, ct))
        {
            return Forbid();
        }

        Formular.ZeitenLesen(ModelState, nameof(Formular), out var beginn, out var ende);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var dienst = await db.Dienste.Include(d => d.Abteilungen).FirstAsync(d => d.Id == id, ct);
        var bisherigerBeginn = dienst.Beginn;
        await Formular.UebertragenAsync(db, dienst, beginn, ende, ct);
        dienst.Status = Status;
        dienst.Abgeschlossen = Abgeschlossen;
        await db.SaveChangesAsync(ct);
        await planung.AbwesenheitenAbgleichenAsync(dienst, ct);
        await planung.AbsagenAbgleichenAsync(dienst, ct);

        var angepasst = FolgendeAnpassen ? await planung.FolgendeAnpassenAsync(dienst, bisherigerBeginn, ct) : 0;
        TempData["Meldung"] = angepasst > 0 ? $"Gespeichert, {angepasst} folgende Termine angepasst." : "Gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht den Dienst, auf Wunsch samt den folgenden Serienterminen.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="folgende">Auch die folgenden Termine der Serie löschen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Dienstliste, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, bool folgende, CancellationToken ct)
    {
        var dienst = await db.Dienste.Include(d => d.Abteilungen).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dienst is null)
        {
            return NotFound();
        }

        if (!await bereich.DarfDienstBearbeitenAsync(User, dienst.AbteilungIds, ct))
        {
            return Forbid();
        }

        if (await db.Anwesenheiten.AnyAsync(a => a.DienstId == id && a.ErfasstUeber != ErfassungsWeg.Rueckmeldung, ct))
        {
            TempData["Fehler"] = "Für diesen Dienst ist schon Anwesenheit erfasst. Bitte absagen statt löschen.";
            return RedirectToPage(new { id });
        }

        var weitere = folgende ? await planung.FolgendeLoeschenAsync(dienst, ct) : 0;
        await planung.LoeschenAsync([dienst], ct);
        TempData["Meldung"] = weitere > 0 ? $"Dienst und {weitere} folgende Termine gelöscht." : "Dienst gelöscht.";
        return RedirectToPage("Index");
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var dienst = await db.Dienste.AsNoTracking().MitZielgruppe().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dienst is null)
        {
            return false;
        }

        Dienst = dienst;
        // Übungsleiter nach dem Personalstand am Diensttag, damit sich Dienste rückwirkend erfassen lassen (Andi, 25.10.2026).
        Listen = await Zielgruppenlisten.LadenAsync(db, Anzeige.LokalesDatum(dienst.Beginn), ct, dienst);
        Meldungen = await Meldungsliste.LadenAsync(db, planung, bereich, User, dienst, ct);
        FolgendeInSerie = dienst.SerieId is null
            ? 0
            : await db.Dienste.CountAsync(d => d.SerieId == dienst.SerieId && d.Beginn > dienst.Beginn, ct);
        return true;
    }
}
