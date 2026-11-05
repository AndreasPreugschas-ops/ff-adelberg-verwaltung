using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Ein Stück, das ein Mitglied gerade hat, mit den Fristen des Einzelstücks.</summary>
/// <param name="Id">Kennung der Ausstattung.</param>
/// <param name="Artikel">Bezeichnung des Artikels.</param>
/// <param name="Groesse">Größe, leer bei Einheitsgröße.</param>
/// <param name="Menge">Ausgegebene Menge.</param>
/// <param name="GeraetId">Kennung des Einzelstücks in der Technik, falls es eines ist.</param>
/// <param name="Inventarnummer">Inventarnummer bei Einzelstücken.</param>
/// <param name="AusgegebenAm">Tag der Ausgabe.</param>
/// <param name="NaechstePruefung">Nächste fällige Prüfung des Einzelstücks.</param>
/// <param name="NieGeprueft">Das Einzelstück wurde noch nie geprüft.</param>
/// <param name="Aussonderung">Ende der Lebensdauer.</param>
/// <param name="Ampel">Dringlichkeit aus Prüfung und Lebensdauer.</param>
/// <param name="TauschOffen">Für das Stück läuft eine Tauschanforderung.</param>
public sealed record AusruestungStueck(
    Guid Id, string Artikel, string Groesse, int Menge, Guid? GeraetId, string? Inventarnummer, DateOnly AusgegebenAm,
    DateOnly? NaechstePruefung, bool NieGeprueft, DateOnly? Aussonderung, Fristampel? Ampel, bool TauschOffen)
{
    /// <summary>Einzelstück mit Inventarnummer (persönliche Schutzausrüstung aus der Technik).</summary>
    public bool Einzelstueck => GeraetId is not null;
}

/// <summary>Lädt die aktuelle Ausrüstung eines Mitglieds für Kleiderkammer, Akte und „Meine Ausrüstung“.</summary>
public static class Ausruestung
{
    /// <summary>Alle ausgegebenen, nicht zurückgegebenen Stücke eines Mitglieds, Einzelstücke mit Prüf- und Lebensdauerfrist.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="stand">Fristen der Einzelstücke.</param>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Stücke in Artikelreihenfolge.</returns>
    public static async Task<IReadOnlyList<AusruestungStueck>> LadenAsync(AppDbContext db, Kleiderkammerstand stand, Guid mitgliedId, DateOnly heute, CancellationToken ct)
    {
        var zeilen = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.MitgliedId == mitgliedId && a.ZurueckAm == null)
            .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.Artikel!.Bezeichnung).ThenBy(a => a.AusgegebenAm)
            .Select(a => new
            {
                a.Id, Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.GeraetId,
                Inventarnummer = a.Geraet == null ? null : a.Geraet.Inventarnummer, a.AusgegebenAm,
            })
            .ToListAsync(ct);
        if (zeilen.Count == 0)
        {
            return [];
        }

        var tausch = (await db.Tauschanforderungen.AsNoTracking()
                .Where(t => t.MitgliedId == mitgliedId && t.Status == TauschStatus.Offen)
                .Select(t => t.AusstattungId).ToListAsync(ct))
            .ToHashSet();
        var psa = zeilen.Any(z => z.GeraetId is not null)
            ? (await stand.PsaAsync(heute, ct)).Where(p => p.MitgliedId == mitgliedId).ToDictionary(p => p.Stand.Id)
            : [];
        return zeilen
            .Select(a =>
            {
                var p = a.GeraetId is { } g ? psa.GetValueOrDefault(g) : null;
                return new AusruestungStueck(a.Id, a.Artikel, a.Groesse, a.Menge, a.GeraetId, a.Inventarnummer, a.AusgegebenAm,
                    p?.Stand.NaechsterTermin, p?.Stand.NieGeprueft == true, p?.Stand.AussonderungFaellig, p?.Stand.Ampel, tausch.Contains(a.Id));
            })
            .ToList();
    }

    /// <summary>Die dringlichste Ampel der Einzelstücke, null ohne Einzelstücke.</summary>
    /// <param name="stuecke">Die Stücke.</param>
    /// <returns>Rot vor Gelb vor Grün.</returns>
    public static Fristampel? Schlechteste(IEnumerable<AusruestungStueck> stuecke) =>
        stuecke.Where(s => s.Ampel is not null).Select(s => s.Ampel).Max();
}
