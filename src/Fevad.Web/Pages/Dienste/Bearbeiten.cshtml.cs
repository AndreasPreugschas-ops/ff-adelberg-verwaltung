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
public class BearbeitenModel(AppDbContext db, Dienstplanung planung, Datenbereich bereich, TimeProvider zeit) : PageModel
{
    public Dienst Dienst { get; private set; } = null!;
    public Zielgruppenlisten Listen { get; private set; } = null!;
    public int FolgendeInSerie { get; private set; }
    public IReadOnlyList<Meldung> Meldungen { get; private set; } = [];

    [BindProperty]
    public bool Abgeschlossen { get; set; }

    [BindProperty]
    public Planungsformular Formular { get; set; } = new();

    [BindProperty]
    public DienstStatus Status { get; set; }

    [BindProperty]
    public bool FolgendeAnpassen { get; set; }

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
        await planung.AbsagenAbgleichenAsync(dienst, ct);

        var angepasst = FolgendeAnpassen ? await planung.FolgendeAnpassenAsync(dienst, bisherigerBeginn, ct) : 0;
        TempData["Meldung"] = angepasst > 0 ? $"Gespeichert, {angepasst} folgende Termine angepasst." : "Gespeichert.";
        return RedirectToPage(new { id });
    }

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
        Listen = await Zielgruppenlisten.LadenAsync(db, Anzeige.Heute(zeit), ct, dienst);
        Meldungen = await Meldungsliste.LadenAsync(db, planung, bereich, User, dienst, ct);
        FolgendeInSerie = dienst.SerieId is null
            ? 0
            : await db.Dienste.CountAsync(d => d.SerieId == dienst.SerieId && d.Beginn > dienst.Beginn, ct);
        return true;
    }
}
