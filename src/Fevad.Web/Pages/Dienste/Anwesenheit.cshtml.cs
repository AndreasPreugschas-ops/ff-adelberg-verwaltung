using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Anwesenheitsliste zum Antippen; jeder Tipp speichert sofort per HTMX.</summary>
[Authorize(Policy = Berechtigungen.AnwesenheitLesen)]
public class AnwesenheitModel(AppDbContext db, Dienstplanung planung, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    public Dienst? Dienst { get; private set; }
    public IReadOnlyList<Eintrag> Eintraege { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }
    public int Anwesend => Eintraege.Count(e => e.Status == AnwesenheitStatus.Anwesend);

    public int Zusagen => Eintraege.Count(e => e.Rueckmeldung == RueckmeldungAntwort.Zusage);
    public int NichtUebernommen => Eintraege.Count(e => e.Rueckmeldung is not null && e.Status is null);

    /// <param name="Rueckmeldung">Zu- oder Absage des Mitglieds vorab, als Hinweis beim Erfassen.</param>
    public sealed record Eintrag(Guid MitgliedId, string Name, string? Abteilung, AnwesenheitStatus? Status, RueckmeldungAntwort? Rueckmeldung = null);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Dienst = await db.Dienste.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (Dienst is null)
        {
            return NotFound();
        }

        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteBearbeiten)).Succeeded;
        Eintraege = await EintraegeLadenAsync(Dienst, ct);
        return Page();
    }

    /// <summary>HTMX: schaltet Anwesend → Entschuldigt → keine Angabe → Anwesend und liefert die Zeile zurück.</summary>
    public async Task<IActionResult> OnPostUmschaltenAsync(Guid id, Guid mitgliedId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var dienst = await db.Dienste.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dienst is null)
        {
            return NotFound();
        }

        var anwesenheit = await db.Anwesenheiten.FirstOrDefaultAsync(a => a.DienstId == id && a.MitgliedId == mitgliedId, ct);
        switch (anwesenheit?.Status)
        {
            case null:
                db.Anwesenheiten.Add(new Anwesenheit { DienstId = id, MitgliedId = mitgliedId, Status = AnwesenheitStatus.Anwesend, ErfasstUeber = ErfassungsWeg.Web });
                break;
            case AnwesenheitStatus.Anwesend:
                anwesenheit.Status = AnwesenheitStatus.Entschuldigt;
                // Von Hand korrigiert gilt nicht mehr als bloß übernommene Rückmeldung (zählt beim Löschen des Dienstes).
                anwesenheit.ErfasstUeber = ErfassungsWeg.Web;
                break;
            default:
                db.Anwesenheiten.Remove(anwesenheit);
                break;
        }

        if (dienst.Status == DienstStatus.Geplant && dienst.Beginn <= zeit.GetUtcNow())
        {
            dienst.Status = DienstStatus.Durchgefuehrt;
        }

        await db.SaveChangesAsync(ct);

        Dienst = dienst;
        DarfBearbeiten = true;
        Eintraege = await EintraegeLadenAsync(dienst, ct);
        ViewData["ZaehlerAktualisieren"] = true;

        // Stand das Mitglied nur wegen seines Eintrags auf der Liste, verschwindet die Zeile (leere Antwort ersetzt sie).
        var eintrag = Eintraege.FirstOrDefault(e => e.MitgliedId == mitgliedId);
        return eintrag is null
            ? MitViewData("_AnwesenheitZaehler", this)
            : MitViewData("_AnwesenheitZeile", (this, eintrag));
    }

    /// <summary>Übernimmt Zusagen als anwesend und Absagen als entschuldigt, wo noch nichts erfasst ist.</summary>
    public async Task<IActionResult> OnPostUebernehmenAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        if (!await db.Dienste.AnyAsync(d => d.Id == id, ct))
        {
            return NotFound();
        }

        var anzahl = await planung.RueckmeldungenUebernehmenAsync(id, ct);
        TempData["Meldung"] = anzahl == 1 ? "1 Rückmeldung übernommen." : $"{anzahl} Rückmeldungen übernommen.";
        return RedirectToPage(new { id });
    }

    // PageModel.Partial übernimmt nur den ModelState, ZaehlerAktualisieren ginge dabei verloren.
    private PartialViewResult MitViewData(string name, object model)
    {
        var daten = new ViewDataDictionary(MetadataProvider, ViewData.ModelState) { Model = model };
        foreach (var (schluessel, wert) in ViewData)
        {
            daten[schluessel] = wert;
        }

        return new PartialViewResult { ViewName = name, ViewData = daten };
    }

    /// <summary>Die Zielgruppe des Dienstes plus alle mit einem Eintrag oder einer Rückmeldung.</summary>
    private async Task<IReadOnlyList<Eintrag>> EintraegeLadenAsync(Dienst dienst, CancellationToken ct)
    {
        var status = await db.Anwesenheiten.AsNoTracking()
            .Where(a => a.DienstId == dienst.Id)
            .ToDictionaryAsync(a => a.MitgliedId, a => a.Status, ct);
        var rueckmeldungen = await db.Rueckmeldungen.AsNoTracking()
            .Where(r => r.DienstId == dienst.Id)
            .ToDictionaryAsync(r => r.MitgliedId, r => r.Antwort, ct);

        var kreis = await planung.TeilnehmerkreisAsync(dienst, ct);
        var weitere = status.Keys.Concat(rueckmeldungen.Keys).Except(kreis.Select(k => k.MitgliedId)).ToList();
        if (weitere.Count > 0)
        {
            kreis.AddRange(await db.Mitglieder.AsNoTracking()
                .Where(m => weitere.Contains(m.Id))
                .Select(m => new Dienstplanung.Teilnehmer(m.Id, m.Nachname, m.Vorname, null))
                .ToListAsync(ct));
        }

        return kreis
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new Eintrag(m.MitgliedId, $"{m.Nachname}, {m.Vorname}", m.Abteilung,
                status.TryGetValue(m.MitgliedId, out var s) ? s : null,
                rueckmeldungen.TryGetValue(m.MitgliedId, out var r) ? r : null))
            .ToList();
    }
}
