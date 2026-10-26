using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Mitglieder;

/// <summary>Betreuer-Register der Jugend- und Kinderfeuerwehr mit dem Stand der Führungszeugnisse (V47); Fristen aus den Parametern.</summary>
public sealed class Betreuerregister(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Fristen zum Führungszeugnis.</summary>
    /// <param name="JahreJugend">Wiedervorlage in der Jugendfeuerwehr in Jahren.</param>
    /// <param name="JahreKinder">Wiedervorlage in der Kinderfeuerwehr in Jahren.</param>
    /// <param name="VorlaufTage">So viele Tage vorher „bald erneuern“.</param>
    /// <param name="HoechstalterMonate">Höchstalter des Zeugnisses bei der Einsicht.</param>
    public sealed record Fristen(int JahreJugend, int JahreKinder, int VorlaufTage, int HoechstalterMonate)
    {
        /// <summary>Wiedervorlage für die Abteilung des Betreuers.</summary>
        /// <param name="b">Der Betreuer mit geladener Abteilung.</param>
        /// <returns>Die Jahre.</returns>
        public int Jahre(Jugendbetreuer b) => b.Abteilung?.Art == AbteilungArt.Kinder ? JahreKinder : JahreJugend;
    }

    /// <summary>Ein Betreuer mit dem Stand seines Zeugnisses.</summary>
    /// <param name="Betreuer">Der Eintrag.</param>
    /// <param name="Status">Stand am Stichtag.</param>
    /// <param name="Wiedervorlage">Bis wann das Zeugnis erneut vorzulegen ist.</param>
    public sealed record Zeile(Jugendbetreuer Betreuer, Zeugnisstatus Status, DateOnly? Wiedervorlage);

    /// <summary>Liest die Fristen.</summary>
    /// <param name="heute">Stichtag der Parameter.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Fristen.</returns>
    public async Task<Fristen> FristenAsync(DateOnly heute, CancellationToken ct = default) => new(
        await parameter.ZahlAsync(ParameterSchluessel.FuehrungszeugnisWiedervorlageJahre, heute, 5, ct),
        await parameter.ZahlAsync(ParameterSchluessel.FuehrungszeugnisWiedervorlageJahreKinder, heute, 2, ct),
        await parameter.ZahlAsync(ParameterSchluessel.FuehrungszeugnisVorlaufTage, heute, 60, ct),
        await parameter.ZahlAsync(ParameterSchluessel.FuehrungszeugnisHoechstalterMonate, heute, 3, ct));

    /// <summary>Betreuer der Abteilungen mit Stand, sortiert nach Name.</summary>
    /// <param name="abteilungen">Freigegebene Abteilungen; null für alle.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="nurTaetige">Nur wer am Stichtag tätig ist oder noch anfängt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<Zeile>> ListeAsync(IReadOnlySet<Guid>? abteilungen, DateOnly heute, bool nurTaetige, CancellationToken ct = default)
    {
        var fristen = await FristenAsync(heute, ct);
        var abfrage = db.Jugendbetreuer.AsNoTracking().Include(b => b.Abteilung).AsQueryable();
        if (abteilungen is not null)
        {
            var ids = abteilungen.ToList();
            abfrage = abfrage.Where(b => ids.Contains(b.AbteilungId));
        }

        if (nurTaetige)
        {
            abfrage = abfrage.Where(b => b.Bis == null || b.Bis >= heute);
        }

        return (await abfrage.OrderBy(b => b.Nachname).ThenBy(b => b.Vorname).ToListAsync(ct))
            .Select(b => new Zeile(b, Fuehrungszeugnisregeln.Status(b, heute, fristen.Jahre(b), fristen.VorlaufTage),
                Fuehrungszeugnisregeln.NaechsteVorlage(b, fristen.Jahre(b))))
            .ToList();
    }
}
