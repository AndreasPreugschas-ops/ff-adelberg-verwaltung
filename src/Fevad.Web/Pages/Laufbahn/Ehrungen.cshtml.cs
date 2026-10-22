using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Jahresliste für die Hauptversammlung: wer im Jahr eine Ehrung erreicht und was schon beantragt oder verliehen ist.</summary>
public class EhrungenModel(AppDbContext db, Laufbahnabfrage laufbahn, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    public IReadOnlyList<Erreicht> Offen { get; private set; } = [];
    public IReadOnlyList<Ehrung> Erfasst { get; private set; } = [];

    public sealed record Erreicht(Guid MitgliedId, string Mitglied, string Ehrungsart, int Jahre, DateOnly Am);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Jahr ??= heute.Year;
        var von = new DateOnly(Jahr.Value, 1, 1);
        var bis = new DateOnly(Jahr.Value, 12, 31);

        Erfasst = await db.Ehrungen.AsNoTracking().Include(e => e.Mitglied).Include(e => e.Ehrungsart)
            .Where(e => (e.VerliehenAm >= von && e.VerliehenAm <= bis)
                || (e.VerliehenAm == null && e.VoraussetzungAm >= von && e.VoraussetzungAm <= bis)
                || e.Status == EhrungStatus.Vorgeschlagen || e.Status == EhrungStatus.Beantragt)
            .OrderBy(e => e.Mitglied!.Nachname).ThenBy(e => e.Mitglied!.Vorname)
            .ToListAsync(ct);

        // Stand zum Jahresende: Wer im Jahr ausscheidet, kann noch geehrt werden.
        var staende = await laufbahn.LadenAsync(von, ct: ct);
        var arten = await db.Ehrungsarten.AsNoTracking()
            .Where(a => a.Aktiv && a.Grundlage != EhrungGrundlage.Ohne && a.Jahre > 0).ToListAsync(ct);
        var vorhanden = (await db.Ehrungen.AsNoTracking().Select(e => new { e.MitgliedId, e.EhrungsartId }).ToListAsync(ct))
            .ToLookup(e => e.MitgliedId, e => e.EhrungsartId);

        Offen = staende
            .SelectMany(s => arten.Select(a => (Stand: s, Art: a,
                Am: (a.Grundlage == EhrungGrundlage.Einsatzdienst ? s.Einsatzdienst : s.Zugehoerigkeit).ErreichtAm(a.Jahre!.Value))))
            .Where(x => x.Am >= von && x.Am <= bis && !vorhanden[x.Stand.MitgliedId].Contains(x.Art.Id))
            .Select(x => new Erreicht(x.Stand.MitgliedId, x.Stand.Name, x.Art.Bezeichnung, x.Art.Jahre!.Value, x.Am!.Value))
            .OrderBy(x => x.Am)
            .ToList();
    }
}
