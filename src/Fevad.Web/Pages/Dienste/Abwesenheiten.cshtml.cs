using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Gemeldete Abwesenheiten für den Kommandanten (V37): anzeigepflichtige zur Kenntnis nehmen (Satzung § 5 Abs. 6)
/// und alle laufenden und künftigen mit Grund sehen.
/// </summary>
[Authorize(Policy = Berechtigungen.AbwesenheitenLesen)]
public class AbwesenheitenModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Anzeigepflichtige Abwesenheiten, die noch niemand zur Kenntnis genommen hat.</summary>
    public IReadOnlyList<Abwesenheit> Offen { get; private set; } = [];
    /// <summary>Alle laufenden und künftigen Abwesenheiten.</summary>
    public IReadOnlyList<Abwesenheit> Alle { get; private set; } = [];
    /// <summary>Ab so vielen Tagen ist eine Abwesenheit anzuzeigen; 0 = nie.</summary>
    public int AnzeigeAbTagen { get; private set; }

    /// <summary>Lädt die Listen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        AnzeigeAbTagen = await planung.AnzeigeAbTagenAsync(ct);
        Offen = await planung.OffeneAnzeigenAsync(ct);
        var heute = Anzeige.Heute(zeit);
        Alle = await db.Abwesenheiten.AsNoTracking().Include(a => a.Mitglied)
            .Where(a => a.Bis >= heute)
            .OrderBy(a => a.Von).ThenBy(a => a.Mitglied!.Nachname)
            .ToListAsync(ct);
    }

    /// <summary>Nimmt eine angezeigte Abwesenheit zur Kenntnis.</summary>
    /// <param name="abwesenheitId">Kennung der Abwesenheit.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 404.</returns>
    public async Task<IActionResult> OnPostKenntnisAsync(Guid abwesenheitId, CancellationToken ct)
    {
        var abwesenheit = await db.Abwesenheiten.FirstOrDefaultAsync(a => a.Id == abwesenheitId, ct);
        if (abwesenheit is null)
        {
            return NotFound();
        }

        abwesenheit.KenntnisGenommenAm ??= zeit.GetUtcNow();
        abwesenheit.KenntnisGenommenVon ??= benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Zur Kenntnis genommen.";
        return RedirectToPage();
    }
}
