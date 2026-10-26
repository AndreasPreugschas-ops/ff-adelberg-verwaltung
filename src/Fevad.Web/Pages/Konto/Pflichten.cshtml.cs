using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>Meine Pflichten im Jahreskreis (V51): was über die eigenen Funktionen ansteht, mit den Vermerken der Vorgänger.</summary>
public class PflichtenModel(AppDbContext db, Jahreskreis jahreskreis, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Pflichten, nach Termin.</summary>
    public IReadOnlyList<Jahreskreis.Zeile> Zeilen { get; private set; } = [];

    /// <summary>Zeigt die eigenen Pflichten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        Zeilen = await jahreskreis.EigeneAsync(mitgliedId, Anzeige.Heute(zeit), ct);
        return Page();
    }

    /// <summary>Vermerkt eine eigene Pflicht als erledigt.</summary>
    /// <param name="pflichtId">Kennung der Pflicht.</param>
    /// <param name="bemerkung">Bemerkung für den Nachfolger.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid pflichtId, string? bemerkung, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var pflicht = await db.Fuehrungspflichten.Include(p => p.Erledigungen).FirstOrDefaultAsync(p => p.Id == pflichtId, ct);
        if (pflicht is null || !await jahreskreis.VerantwortlichAsync(pflicht, await planung.MitgliedIdAsync(benutzer.Id, ct), heute, ct))
        {
            return NotFound();
        }

        TempData["Meldung"] = await jahreskreis.ErledigenAsync(pflicht, heute, User.Identity?.Name, bemerkung, ct)
            ? $"„{pflicht.Bezeichnung}“ als erledigt vermerkt. Danke!"
            : "Für diese Pflicht steht gerade nichts an.";
        return RedirectToPage();
    }
}
