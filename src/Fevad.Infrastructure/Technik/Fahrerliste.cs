using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Fahrerliste je Fahrzeug (V59): für aktive Mitglieder der Einsatzabteilungen nur „Fahrberechtigung ausreichend ja/nein bis Datum“.
/// Klassen, Führerscheindaten und Kontrolltermine verlassen diese Klasse nicht (Andi, 25.10.2026: Gerätewarte sehen keine Personaldaten).
/// </summary>
public sealed class Fahrerliste(AppDbContext db)
{
    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Ausreichend">Darf das Fahrzeug fahren.</param>
    /// <param name="Bis">Bis wann; leer = ohne Ende oder bei „nein“.</param>
    public sealed record Zeile(Guid MitgliedId, string Name, bool Ausreichend, DateOnly? Bis);

    /// <summary>
    /// Berechnet die Liste für ein Fahrzeug. Aufgeführt werden Mitglieder mit ausreichender Fahrerlaubnis und Maschinisten ohne sie.
    /// </summary>
    /// <param name="gesamtmasseKg">Zulässige Gesamtmasse des Fahrzeugs.</param>
    /// <param name="stichtag">Prüftag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zuerst die Berechtigten nach Name, dann die Maschinisten ohne ausreichende Fahrerlaubnis.</returns>
    public async Task<IReadOnlyList<Zeile>> LadenAsync(int gesamtmasseKg, DateOnly stichtag, CancellationToken ct)
    {
        var personen = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();

        var qualifikationen = await db.QualifikationNachweise.AsNoTracking()
            .Where(q => ids.Contains(q.MitgliedId)
                && (q.Qualifikation!.Art == QualifikationArt.Fahrerlaubnis || q.Qualifikation.Code == Qualifikation.CodeMaschinist))
            .Select(q => new { q.MitgliedId, q.Qualifikation!.Art, q.Qualifikation.FahrzeugmasseBisKg, q.ErworbenAm, q.GueltigBis })
            .ToListAsync(ct);

        // Die Führerscheinkontrolle zählt nur, wenn eine Nachweisart dafür angelegt ist.
        var kontrollarten = await db.Nachweisarten.AsNoTracking()
            .Where(a => a.Pflicht == NachweisPflicht.MitFahrerlaubnis).Select(a => a.Id).ToListAsync(ct);
        var kontrollen = kontrollarten.Count == 0
            ? []
            : await db.Nachweise.AsNoTracking()
                .Where(n => ids.Contains(n.MitgliedId) && kontrollarten.Contains(n.NachweisartId) && n.Datum <= stichtag)
                .GroupBy(n => n.MitgliedId)
                .Select(g => new { MitgliedId = g.Key, Bis = g.Max(n => n.GueltigBis) })
                .ToDictionaryAsync(x => x.MitgliedId, x => x.Bis, ct);

        var jePerson = qualifikationen.ToLookup(q => q.MitgliedId);
        var zeilen = new List<Zeile>();
        foreach (var p in personen)
        {
            var eigene = jePerson[p.Id].ToList();
            if (eigene.Count == 0)
            {
                continue;
            }

            DateOnly? kontrolle = kontrollarten.Count == 0 ? null
                : kontrollen.TryGetValue(p.Id, out var bis) ? bis ?? DateOnly.MaxValue : DateOnly.MinValue;
            var ergebnis = Fahrberechtigung.Pruefen(
                gesamtmasseKg,
                eigene.Where(q => q.Art == QualifikationArt.Fahrerlaubnis).Select(q => new Fahrberechtigung.Erlaubnis(q.FahrzeugmasseBisKg, q.ErworbenAm, q.GueltigBis)),
                kontrolle,
                stichtag);
            var maschinist = eigene.Any(q => q.Art != QualifikationArt.Fahrerlaubnis);
            if (ergebnis.Ausreichend || maschinist)
            {
                var bisTag = ergebnis.Bis == DateOnly.MaxValue ? null : ergebnis.Bis;
                zeilen.Add(new Zeile(p.Id, $"{p.Nachname}, {p.Vorname}", ergebnis.Ausreichend, bisTag));
            }
        }

        return zeilen
            .OrderByDescending(z => z.Ausreichend)
            .ThenBy(z => z.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
