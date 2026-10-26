using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Gremien;

/// <summary>
/// Jahreskreis der Führungspflichten (V51): welche Pflicht wann ansteht, wer sie über seine Funktion trägt und was zuletzt vermerkt
/// wurde. Die Übergabe an den Nachfolger geschieht über die Funktion: wer sie übernimmt, sieht die Pflichten samt Vermerken.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
public sealed class Jahreskreis(AppDbContext db)
{
    /// <summary>Eine Pflicht mit ihrem Stand.</summary>
    /// <param name="Pflicht">Die Pflicht mit Erledigungen.</param>
    /// <param name="Termin">Aktueller Termin (Vorlauf begonnen) oder sonst der nächste Stichtag.</param>
    /// <param name="Ampel">Rot überfällig, gelb im Vorlauf, <c>null</c> erledigt oder nicht dran.</param>
    /// <param name="Inhaber">Heutige Inhaber der verantwortlichen Funktionen.</param>
    /// <param name="Letzte">Letzte Erledigung.</param>
    public sealed record Zeile(Fuehrungspflicht Pflicht, DateOnly? Termin, Fristampel? Ampel, IReadOnlyList<string> Inhaber, PflichtErledigung? Letzte)
    {
        /// <summary>Der Termin ist dran und noch nicht erledigt.</summary>
        public bool Offen => Ampel is not null;
    }

    /// <summary>Alle aktiven Pflichten mit Stand, nach Termin.</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<Zeile>> AlleAsync(DateOnly heute, CancellationToken ct = default)
    {
        var pflichten = await db.Fuehrungspflichten.AsNoTracking().Include(p => p.Erledigungen).Where(p => p.Aktiv).ToListAsync(ct);
        var inhaber = await InhaberAsync(heute, ct);
        return pflichten.Select(p => ZeileAus(p, heute, inhaber)).OrderBy(z => z.Termin ?? DateOnly.MaxValue).ThenBy(z => z.Pflicht.Sortierung).ToList();
    }

    /// <summary>Pflichten, die ein Mitglied über eine heute gültige Funktion trägt.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<Zeile>> EigeneAsync(Guid mitgliedId, DateOnly heute, CancellationToken ct = default)
    {
        var codes = await FunktionscodesAsync(mitgliedId, heute, ct);
        if (codes.Count == 0)
        {
            return [];
        }

        return (await AlleAsync(heute, ct)).Where(z => z.Pflicht.Funktionscodes.Any(codes.Contains)).ToList();
    }

    /// <summary>Darf das Mitglied die Pflicht als erledigt vermerken (Inhaber einer verantwortlichen Funktion)?</summary>
    /// <param name="pflicht">Die Pflicht.</param>
    /// <param name="mitgliedId">Kennung des Mitglieds oder <c>null</c>.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn das Mitglied verantwortlich ist.</returns>
    public async Task<bool> VerantwortlichAsync(Fuehrungspflicht pflicht, Guid? mitgliedId, DateOnly heute, CancellationToken ct = default) =>
        mitgliedId is { } id && pflicht.Funktionscodes.Any((await FunktionscodesAsync(id, heute, ct)).Contains);

    /// <summary>Vermerkt den aktuellen Termin einer Pflicht als erledigt.</summary>
    /// <param name="pflicht">Die Pflicht (verfolgt, mit Erledigungen).</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="name">Name dessen, der erledigt.</param>
    /// <param name="bemerkung">Bemerkung für den Nachfolger.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn vermerkt; <c>false</c>, wenn nichts ansteht oder schon erledigt ist.</returns>
    public async Task<bool> ErledigenAsync(Fuehrungspflicht pflicht, DateOnly heute, string? name, string? bemerkung, CancellationToken ct = default)
    {
        if (pflicht.AktuellerTermin(heute) is not { } termin || pflicht.Erledigungen.Any(e => e.Stichtag == termin))
        {
            return false;
        }

        var text = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
        // Über das DbSet anlegen: die Id steht schon fest, über die Navigation hielte EF den Vermerk für vorhanden und versuchte ein UPDATE.
        db.PflichtErledigungen.Add(new PflichtErledigung
        {
            PflichtId = pflicht.Id, Stichtag = termin, ErledigtAm = heute, ErledigtVon = name, Bemerkung = text is { Length: > 1000 } ? text[..1000] : text,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Zeile ZeileAus(Fuehrungspflicht p, DateOnly heute, IReadOnlyDictionary<string, List<string>> inhaber)
    {
        var namen = p.Funktionscodes.SelectMany(c => inhaber.GetValueOrDefault(c) ?? []).Distinct().ToList();
        var termin = p.AktuellerTermin(heute) is { } aktuell && p.Erledigungen.All(e => e.Stichtag != aktuell) ? aktuell : p.NaechsterTermin(heute);
        var letzte = p.Erledigungen.OrderByDescending(e => e.Stichtag).FirstOrDefault();
        return new Zeile(p, termin, p.Ampel(heute), namen, letzte);
    }

    private async Task<Dictionary<string, List<string>>> InhaberAsync(DateOnly heute, CancellationToken ct) =>
        (await db.FunktionZuordnungen.AsNoTracking()
            .Where(f => f.GueltigAb <= heute && (f.GueltigBis == null || f.GueltigBis >= heute))
            .Select(f => new { f.Funktion!.Code, f.Mitglied!.Vorname, f.Mitglied.Nachname })
            .ToListAsync(ct))
        .GroupBy(f => f.Code)
        .ToDictionary(g => g.Key, g => g.Select(f => $"{f.Vorname} {f.Nachname}").Distinct().ToList());

    private async Task<List<string>> FunktionscodesAsync(Guid mitgliedId, DateOnly heute, CancellationToken ct) =>
        await db.FunktionZuordnungen.AsNoTracking()
            .Where(f => f.MitgliedId == mitgliedId && f.GueltigAb <= heute && (f.GueltigBis == null || f.GueltigBis >= heute))
            .Select(f => f.Funktion!.Code).Distinct().ToListAsync(ct);
}
