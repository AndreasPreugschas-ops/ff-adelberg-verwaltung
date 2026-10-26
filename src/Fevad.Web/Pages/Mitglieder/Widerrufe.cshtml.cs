using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Folgeaufgaben nach einem Widerruf (V45): Mobilnummer aus der WhatsApp-Gruppe nehmen, veröffentlichte Fotos entfernen. FEVAD kann das nicht
/// selbst; der Widerruf wirkt sofort, die Aufgabe bleibt offen, bis sie hier als erledigt vermerkt ist. Sichtbar für alle, die das Mitglied bearbeiten dürfen.
/// </summary>
public class WiderrufeModel(AppDbContext db, Datenbereich datenbereich, Einwilligungsregister register, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Folgeaufgaben, älteste zuerst.</summary>
    public IReadOnlyList<Einwilligung> Offene { get; private set; } = [];

    /// <summary>Zeigt die offenen Aufgaben.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Offene = await (await AbfrageAsync(ct)).AsNoTracking().Include(e => e.Mitglied).Include(e => e.Einwilligungsart)
            .OrderBy(e => e.WiderrufenAm).ToListAsync(ct);
    }

    /// <summary>Vermerkt die Folgeaufgabe als erledigt.</summary>
    /// <param name="id">Kennung der widerrufenen Erklärung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid id, CancellationToken ct)
    {
        if (await (await AbfrageAsync(ct)).FirstOrDefaultAsync(e => e.Id == id, ct) is not { } eintrag)
        {
            return NotFound();
        }

        eintrag.FolgeaufgabeErledigtAm = Anzeige.Heute(zeit);
        eintrag.FolgeaufgabeErledigtVon = User.Identity?.Name;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Als erledigt vermerkt.";
        return RedirectToPage();
    }

    private async Task<IQueryable<Einwilligung>> AbfrageAsync(CancellationToken ct) =>
        register.OffeneFolgeaufgaben(await datenbereich.FilternBearbeitenAsync(db.Mitglieder, User, Anzeige.Heute(zeit), ct));
}
