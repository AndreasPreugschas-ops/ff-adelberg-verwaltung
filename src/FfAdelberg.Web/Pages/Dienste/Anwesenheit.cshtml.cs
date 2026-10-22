using FfAdelberg.Domain.Dienstbetrieb;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Dienste;

/// <summary>Anwesenheitsliste zum Antippen; jeder Tipp speichert sofort per HTMX.</summary>
[Authorize(Policy = Berechtigungen.AnwesenheitLesen)]
public class AnwesenheitModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    public Dienst? Dienst { get; private set; }
    public IReadOnlyList<Eintrag> Eintraege { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }
    public int Anwesend => Eintraege.Count(e => e.Status == AnwesenheitStatus.Anwesend);

    public sealed record Eintrag(Guid MitgliedId, string Name, string? Abteilung, AnwesenheitStatus? Status);

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

    /// <summary>Alle, die am Tag des Dienstes Mitglied waren, plus alle mit einem Eintrag.</summary>
    private async Task<IReadOnlyList<Eintrag>> EintraegeLadenAsync(Dienst dienst, CancellationToken ct)
    {
        var tag = Anzeige.LokalesDatum(dienst.Beginn);
        var status = await db.Anwesenheiten.AsNoTracking()
            .Where(a => a.DienstId == dienst.Id)
            .ToDictionaryAsync(a => a.MitgliedId, a => a.Status, ct);

        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)) || status.Keys.Contains(m.Id))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new
            {
                m.Id,
                m.Nachname,
                m.Vorname,
                Abteilung = m.Mitgliedschaften
                    .Where(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag))
                    .Select(s => s.Abteilung!.Kurzbezeichnung ?? s.Abteilung.Code)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return mitglieder
            .Select(m => new Eintrag(m.Id, $"{m.Nachname}, {m.Vorname}", m.Abteilung, status.TryGetValue(m.Id, out var s) ? s : null))
            .ToList();
    }
}
