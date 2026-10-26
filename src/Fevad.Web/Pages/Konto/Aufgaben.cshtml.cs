using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>Meine Aufgaben (V17): freigegebene Maßnahmen aus Nachbesprechungen, für die das Mitglied verantwortlich ist.</summary>
public class AufgabenModel(AppDbContext db, Massnahmenliste massnahmen, Dienstplanung planung, IAktuellerBenutzer benutzer, ParameterLeser parameter,
    TimeProvider zeit) : PageModel
{
    /// <summary>Eigene offene Maßnahmen, nach Termin.</summary>
    public IReadOnlyList<Massnahme> Offene { get; private set; } = [];
    /// <summary>Heutiges Datum für die Ampel.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Tage vor dem Termin, ab denen gelb gilt.</summary>
    public int WarnTage { get; private set; }

    /// <summary>Zeigt die eigenen Aufgaben.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        Heute = Anzeige.Heute(zeit);
        WarnTage = await parameter.ZahlAsync(ParameterSchluessel.MassnahmeWarnTage, Heute, 14, ct);
        Offene = await massnahmen.Offene(mitgliedId).ToListAsync(ct);
        return Page();
    }

    /// <summary>Meldet eine eigene Maßnahme als erledigt.</summary>
    /// <param name="massnahmeId">Kennung der Maßnahme.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid massnahmeId, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var massnahme = await db.Massnahmen.FirstOrDefaultAsync(m => m.Id == massnahmeId && m.VerantwortlichId == mitgliedId
            && m.Status == MassnahmeStatus.Freigegeben, ct);
        if (mitgliedId is null || massnahme is null)
        {
            return NotFound();
        }

        massnahme.Erledigen(Anzeige.Heute(zeit));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Als erledigt gemeldet. Danke!";
        return RedirectToPage();
    }
}
