using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Ausbildung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Lehrgangswünsche entscheiden und den Bedarf im Herbst an den Kreisbrandmeister melden (V54).</summary>
[Authorize(Policy = Berechtigungen.Lehrgangsmeldung)]
public class LehrgangswuenscheModel(AppDbContext db, Lehrgangswuensche wuensche, TimeProvider zeit) : PageModel
{
    /// <summary>Wunschjahr; ohne Angabe das kommende Jahr.</summary>
    [BindProperty(SupportsGet = true)] public int? Jahr { get; set; }

    /// <summary>Die Wünsche des Jahres.</summary>
    public IReadOnlyList<Lehrgangswuensche.Zeile> Zeilen { get; private set; } = [];

    /// <summary>Laufendes Jahr.</summary>
    public int AktuellesJahr { get; private set; }

    /// <summary>Mitglieder der Einsatzabteilung, für Wünsche ohne eigenen Zugang.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Aktive Lehrgänge.</summary>
    public IReadOnlyList<SelectListItem> Lehrgaenge { get; private set; } = [];

    /// <summary>Zeigt die Wünsche des Jahres.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 400 bei ungültigem Jahr.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!JahrPruefen())
        {
            return BadRequest();
        }

        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Herbstmeldung als CSV für den Kreisbrandmeister.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 400.</returns>
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (!JahrPruefen())
        {
            return BadRequest();
        }

        var o = await db.OrganisationAsync(ct);
        var zeilen = await wuensche.JahrAsync(Jahr!.Value, Anzeige.Heute(zeit), ct);
        return File(Lehrgangswuensche.Herbstmeldung(o.FeuerwehrName, Jahr.Value, zeilen), "text/csv; charset=utf-8", $"Lehrgangsbedarf-{Jahr}.csv");
    }

    /// <summary>Setzt Stand, Priorität und Bemerkung eines Wunsches.</summary>
    /// <param name="id">Kennung des Wunsches.</param>
    /// <param name="status">Neuer Stand.</param>
    /// <param name="prioritaet">Priorität.</param>
    /// <param name="bemerkung">Bemerkung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostStandAsync(Guid id, WunschStatus status, int? prioritaet, string? bemerkung, CancellationToken ct)
    {
        var wunsch = await db.Lehrgangswuensche.Include(w => w.Teilnahme).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (wunsch is null || !Enum.IsDefined(status))
        {
            return NotFound();
        }

        await wuensche.StandSetzenAsync(wunsch, status, prioritaet, bemerkung, Anzeige.Heute(zeit), ct);
        TempData["Meldung"] = status is WunschStatus.Angemeldet or WunschStatus.Bestaetigt
            ? "Gespeichert. Die Lehrgangsteilnahme steht in der Akte; Termine und Ergebnis dort eintragen."
            : "Gespeichert.";
        return RedirectToPage(new { jahr = wunsch.Jahr });
    }

    /// <summary>Trägt einen Wunsch für ein Mitglied ein, z. B. ohne eigenen Zugang.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="qualifikationId">Kennung des Lehrgangs.</param>
    /// <param name="begruendung">Hinweis.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostEintragenAsync(Guid mitgliedId, Guid qualifikationId, string? begruendung, CancellationToken ct)
    {
        if (!JahrPruefen())
        {
            return BadRequest();
        }

        TempData["Meldung"] = await wuensche.EintragenAsync(mitgliedId, qualifikationId, Jahr!.Value, begruendung, Anzeige.Heute(zeit), ct) ?? "Wunsch eingetragen.";
        return RedirectToPage(new { jahr = Jahr });
    }

    /// <summary>Vermerkt die offenen Wünsche des Jahres als an den Kreisbrandmeister gemeldet.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostGemeldetAsync(CancellationToken ct)
    {
        if (!JahrPruefen())
        {
            return BadRequest();
        }

        var anzahl = await wuensche.GemeldetAsync(Jahr!.Value, Anzeige.Heute(zeit), ct);
        TempData["Meldung"] = $"{anzahl} Wünsche als gemeldet vermerkt.";
        return RedirectToPage(new { jahr = Jahr });
    }

    private bool JahrPruefen()
    {
        AktuellesJahr = Anzeige.Heute(zeit).Year;
        Jahr ??= AktuellesJahr + 1;
        return Jahr >= AktuellesJahr - 5 && Jahr <= AktuellesJahr + Lehrgangswuensche.JahreVoraus;
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Zeilen = await wuensche.JahrAsync(Jahr!.Value, heute, ct);
        Mitglieder = await wuensche.Berechtigte(heute).AsNoTracking().OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString())).ToListAsync(ct);
        Lehrgaenge = await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv && q.Art == QualifikationArt.Lehrgang).OrderBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToListAsync(ct);
    }
}
