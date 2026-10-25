using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Jahresliste für die Hauptversammlung: wer im Jahr eine Ehrung erreicht und was schon beantragt oder verliehen ist.</summary>
public class EhrungenModel(AppDbContext db, Laufbahnabfrage laufbahn, TimeProvider zeit, Datenbereich datenbereich) : PageModel
{
    /// <summary>Betrachtetes Jahr; ohne Angabe das laufende.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    /// <summary>Im Jahr erreichte Ehrungen, die noch nicht erfasst sind.</summary>
    public IReadOnlyList<Erreicht> Offen { get; private set; } = [];
    /// <summary>Erfasste Ehrungen des Jahres sowie alle vorgeschlagenen und beantragten.</summary>
    public IReadOnlyList<Ehrung> Erfasst { get; private set; } = [];

    /// <summary>Antragsschluss der Verbandsehrungen für Verleihungen in diesem Jahr (im Vorjahr).</summary>
    public DateOnly? Antragsschluss { get; private set; }

    /// <summary>Eine erreichte, noch nicht erfasste Ehrung.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Mitglied">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Ehrungsart">Bezeichnung der Ehrungsart.</param>
    /// <param name="Jahre">Angerechnete Jahre.</param>
    /// <param name="Am">Tag, an dem die Voraussetzung erfüllt ist.</param>
    public sealed record Erreicht(Guid MitgliedId, string Mitglied, string Ehrungsart, int Jahre, DateOnly Am);

    /// <summary>Lädt offene und erfasste Ehrungen des Jahres.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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

        // Alle, die im Jahr dabei waren, mit Stand zum Jahresende: Wer im Jahr ausscheidet, kann noch geehrt werden.
        // Die Vorschläge nutzen dieselben Regeln wie die Laufbahn (Vorstufe, Funktion, Leistungsspange, 65plus).
        var ids = await (await datenbereich.OhneJugendAsync(db.Mitglieder.AsNoTracking(), User, bis))
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= bis && (s.GueltigBis == null || s.GueltigBis >= von)))
            .Select(m => m.Id).ToListAsync(ct);
        var staende = await laufbahn.LadenAsync(bis, ids, ct);

        Offen = staende
            .SelectMany(s => s.Ehrungsvorschlaege.Where(v => v.ErreichtAm >= von && v.ErreichtAm <= bis)
                .Select(v => new Erreicht(s.MitgliedId, s.Name, v.Ehrungsart, v.Jahre, v.ErreichtAm)))
            .OrderBy(x => x.Am)
            .ToList();

        Antragsschluss = await laufbahn.AntragsschlussAsync(new DateOnly(Jahr.Value - 1, 1, 1), ct);
    }
}
