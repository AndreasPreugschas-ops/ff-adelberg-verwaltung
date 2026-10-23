using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Wer hat welchen Lehrgang: Matrix der aktiven Einsatzabteilung, z. B. für die Lehrgangsplanung.</summary>
public class LehrgaengeModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public QualifikationArt Art { get; set; } = QualifikationArt.Lehrgang;

    public IReadOnlyList<Qualifikation> Spalten { get; private set; } = [];
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zelle(DateOnly ErworbenAm, DateOnly? GueltigBis, Fristampel Ampel);

    public sealed record Zeile(Guid MitgliedId, string Name, IReadOnlyDictionary<Guid, Zelle> Zellen);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var warnung = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, heute, 90, ct);

        Spalten = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Aktiv && q.Art == Art)
            .OrderBy(q => q.Sortierung)
            .ToListAsync(ct);

        // Abzeichen gibt es auch in der Jugendfeuerwehr (Jugendflamme, Leistungsspange).
        var mitJugend = Art == QualifikationArt.Abzeichen;
        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s =>
                (s.Abteilung!.Art == AbteilungArt.Einsatz || (mitJugend && s.Abteilung!.Art == AbteilungArt.Jugend))
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .ToListAsync(ct);

        var ids = mitglieder.Select(m => m.Id).ToList();
        var spaltenIds = Spalten.Select(s => s.Id).ToList();
        var nachweise = await db.QualifikationNachweise.AsNoTracking()
            .Where(q => ids.Contains(q.MitgliedId) && spaltenIds.Contains(q.QualifikationId) && q.ErworbenAm <= heute)
            .Select(q => new { q.MitgliedId, q.QualifikationId, q.ErworbenAm, q.GueltigBis })
            .ToListAsync(ct);

        // Je Mitglied und Qualifikation zählt der jüngste Nachweis.
        var juengste = nachweise
            .GroupBy(n => (n.MitgliedId, n.QualifikationId))
            .ToDictionary(g => g.Key, g => g.MaxBy(n => n.ErworbenAm)!);

        Zeilen = mitglieder
            .Select(m => new Zeile(m.Id, $"{m.Nachname}, {m.Vorname}", Spalten
                .Where(s => juengste.ContainsKey((m.Id, s.Id)))
                .ToDictionary(s => s.Id, s =>
                {
                    var n = juengste[(m.Id, s.Id)];
                    return new Zelle(n.ErworbenAm, n.GueltigBis, Frist.Bewerten(n.GueltigBis, heute, warnung));
                })))
            .ToList();
    }
}
