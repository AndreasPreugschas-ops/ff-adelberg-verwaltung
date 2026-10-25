using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Ausbildungsmatrix der Einsatzabteilung: wer welche Lehrgänge und Fahrerlaubnisse hat, und je Funktion Soll (Voraussetzungen
/// laut Katalog) und Ist (Inhaber mit und ohne Voraussetzung, dazu wer sie übernehmen könnte). Ohne Atemschutz-Vorsorge.
/// </summary>
public class AusbildungsmatrixModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Nur diese Abteilung.</summary>
    [BindProperty(SupportsGet = true)] public Guid? AbteilungId { get; set; }

    /// <summary>Eine Spalte der Matrix.</summary>
    /// <param name="Code">Code der Qualifikation.</param>
    /// <param name="Kurz">Kurzbezeichnung im Spaltenkopf.</param>
    /// <param name="Bezeichnung">Volle Bezeichnung.</param>
    public sealed record Spalte(string Code, string Kurz, string Bezeichnung);

    /// <summary>Zelle: gültig, abgelaufen oder leer (null).</summary>
    /// <summary>Stand einer Qualifikation.</summary>
    public enum Stand
    {
        /// <summary>Gültig.</summary>
        Gueltig,
        /// <summary>Abgelaufen.</summary>
        Abgelaufen,
    }

    /// <summary>Eine Person in der Matrix.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Qualifikationen">Stand je Qualifikationscode.</param>
    /// <param name="Funktionen">Funktionen der Person.</param>
    public sealed record Zeile(Guid MitgliedId, string Name, IReadOnlyDictionary<string, Stand> Qualifikationen, IReadOnlyList<string> Funktionen);

    /// <summary>Soll und Ist einer Funktion mit Voraussetzungen.</summary>
    /// <param name="Bezeichnung">Bezeichnung der Funktion.</param>
    /// <param name="Voraussetzungen">Voraussetzungen als Text.</param>
    /// <param name="MitVoraussetzung">Inhaber, die die Voraussetzungen erfüllen.</param>
    /// <param name="OhneVoraussetzung">Inhaber ohne erfüllte Voraussetzungen.</param>
    /// <param name="Moegliche">Zahl aller Personen, die die Voraussetzungen erfüllen.</param>
    public sealed record Funktionsstand(string Bezeichnung, string Voraussetzungen, IReadOnlyList<string> MitVoraussetzung,
        IReadOnlyList<string> OhneVoraussetzung, int Moegliche);

    /// <summary>Spalten der Matrix.</summary>
    public IReadOnlyList<Spalte> Spalten { get; private set; } = [];
    /// <summary>Personen der Matrix.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Funktionen mit Voraussetzungen.</summary>
    public IReadOnlyList<Funktionsstand> Funktionen { get; private set; } = [];
    /// <summary>Auswahl der Abteilungen.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Anzahl gültiger Nachweise je Spalte.</summary>
    public int Ist(string code) => Zeilen.Count(z => z.Qualifikationen.TryGetValue(code, out var s) && s == Stand.Gueltig);

    /// <summary>Lädt die Matrix.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Art == AbteilungArt.Einsatz).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);

        var personen = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz
                && (AbteilungId == null || s.AbteilungId == AbteilungId)
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();

        var nachweise = (await db.QualifikationNachweise.AsNoTracking()
                .Where(n => ids.Contains(n.MitgliedId) && n.ErworbenAm <= heute
                    && (n.Qualifikation!.Art == QualifikationArt.Lehrgang || n.Qualifikation.Art == QualifikationArt.Fahrerlaubnis))
                .Select(n => new { n.MitgliedId, n.Qualifikation!.Code, n.GueltigBis })
                .ToListAsync(ct))
            .GroupBy(n => n.MitgliedId)
            .ToDictionary(g => g.Key, g => g.GroupBy(n => n.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(c => c.Key, c => c.Any(n => n.GueltigBis is null || n.GueltigBis >= heute) ? Stand.Gueltig : Stand.Abgelaufen,
                    StringComparer.OrdinalIgnoreCase));

        var zuordnungen = await db.FunktionZuordnungen.AsNoTracking()
            .Where(f => ids.Contains(f.MitgliedId) && f.GueltigAb <= heute && (f.GueltigBis == null || f.GueltigBis >= heute))
            .Select(f => new { f.MitgliedId, f.FunktionId, f.Funktion!.Bezeichnung })
            .ToListAsync(ct);
        var funktionen = await db.Funktionen.AsNoTracking().Where(f => f.Aktiv).OrderBy(f => f.Sortierung).ToListAsync(ct);
        var qualifikationen = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Aktiv && (q.Art == QualifikationArt.Lehrgang || q.Art == QualifikationArt.Fahrerlaubnis))
            .OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
            .ToListAsync(ct);
        var namen = qualifikationen.ToDictionary(q => q.Code, q => q.Kurzbezeichnung ?? q.Code, StringComparer.OrdinalIgnoreCase);

        // Spalten: was jemand hat oder eine Funktion verlangt, damit die Matrix nicht aus leeren Spalten besteht.
        var verlangt = funktionen.SelectMany(f => Abzeichenpruefung.Gruppen(f.Voraussetzungen).SelectMany(g => g)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vorhanden = nachweise.Values.SelectMany(d => d.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Spalten = qualifikationen.Where(q => verlangt.Contains(q.Code) || vorhanden.Contains(q.Code))
            .Select(q => new Spalte(q.Code, q.Kurzbezeichnung ?? q.Code, q.Bezeichnung)).ToList();

        var leer = new Dictionary<string, Stand>(StringComparer.OrdinalIgnoreCase);
        Zeilen = personen.Select(p => new Zeile(p.Id, $"{p.Nachname}, {p.Vorname}", nachweise.GetValueOrDefault(p.Id) ?? leer,
            zuordnungen.Where(z => z.MitgliedId == p.Id).Select(z => z.Bezeichnung).Distinct().ToList())).ToList();

        bool Erfuellt(Zeile z, string? voraussetzungen) => Abzeichenpruefung.Gruppen(voraussetzungen)
            .All(g => g.Any(c => z.Qualifikationen.TryGetValue(c, out var s) && s == Stand.Gueltig));

        Funktionen = funktionen
            .Where(f => !string.IsNullOrWhiteSpace(f.Voraussetzungen))
            .Select(f =>
            {
                var inhaber = Zeilen.Where(z => zuordnungen.Any(x => x.MitgliedId == z.MitgliedId && x.FunktionId == f.Id)).ToList();
                return new Funktionsstand(
                    f.Bezeichnung,
                    string.Join(" und ", Abzeichenpruefung.Gruppen(f.Voraussetzungen).Select(g => string.Join(" oder ", g.Select(c => namen.GetValueOrDefault(c, c))))),
                    inhaber.Where(z => Erfuellt(z, f.Voraussetzungen)).Select(z => z.Name).ToList(),
                    inhaber.Where(z => !Erfuellt(z, f.Voraussetzungen)).Select(z => z.Name).ToList(),
                    Zeilen.Count(z => Erfuellt(z, f.Voraussetzungen)));
            })
            .ToList();
    }
}
