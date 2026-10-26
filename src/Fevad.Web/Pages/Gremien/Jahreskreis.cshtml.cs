using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>
/// Jahreskreis der Führungspflichten (V51): alle wiederkehrenden Pflichten mit Termin, verantwortlicher Funktion, heutigen Inhabern und
/// letztem Vermerk. Erledigt vermerken dürfen die Inhaber der Funktion und wer Gremien bearbeiten darf.
/// </summary>
public class JahreskreisModel(AppDbContext db, Jahreskreis jahreskreis, Dienstplanung planung, IAktuellerBenutzer benutzer,
    IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Pflichten mit Stand, nach Termin.</summary>
    public IReadOnlyList<Jahreskreis.Zeile> Zeilen { get; private set; } = [];
    /// <summary>Bezeichnungen der Funktionen nach Code.</summary>
    public IReadOnlyDictionary<string, string> Funktionen { get; private set; } = new Dictionary<string, string>();
    /// <summary>Darf den Katalog pflegen und jede Pflicht als erledigt vermerken.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Kennungen der Pflichten, die das angemeldete Mitglied über seine Funktion trägt.</summary>
    public IReadOnlySet<Guid> Eigene { get; private set; } = new HashSet<Guid>();

    /// <summary>Zeigt den Jahreskreis.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GremienBearbeiten)).Succeeded;
        Zeilen = await jahreskreis.AlleAsync(heute, ct);
        Funktionen = await db.Funktionen.AsNoTracking().ToDictionaryAsync(f => f.Code, f => f.Bezeichnung, ct);
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is { } mitgliedId)
        {
            Eigene = (await jahreskreis.EigeneAsync(mitgliedId, heute, ct)).Select(z => z.Pflicht.Id).ToHashSet();
        }
    }

    /// <summary>Vermerkt den aktuellen Termin einer Pflicht als erledigt.</summary>
    /// <param name="pflichtId">Kennung der Pflicht.</param>
    /// <param name="bemerkung">Bemerkung für den Nachfolger.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid pflichtId, string? bemerkung, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var pflicht = await db.Fuehrungspflichten.Include(p => p.Erledigungen).FirstOrDefaultAsync(p => p.Id == pflichtId, ct);
        if (pflicht is null)
        {
            return NotFound();
        }

        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.GremienBearbeiten)).Succeeded
            && !await jahreskreis.VerantwortlichAsync(pflicht, await planung.MitgliedIdAsync(benutzer.Id, ct), heute, ct))
        {
            return Forbid();
        }

        TempData["Meldung"] = await jahreskreis.ErledigenAsync(pflicht, heute, User.Identity?.Name, bemerkung, ct)
            ? $"„{pflicht.Bezeichnung}“ als erledigt vermerkt."
            : "Für diese Pflicht steht gerade nichts an.";
        return RedirectToPage();
    }
}
