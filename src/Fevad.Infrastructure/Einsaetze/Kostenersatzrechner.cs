using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Berechnet die Kostenaufstellung eines Kostenersatz-Vorgangs: Fahrzeuge (eigener Satz oder VOKeFw-Klasse), Personal, Ausrüstung,
/// Kosten nach tatsächlichem Aufwand mit Vorhaltezuschlag und Verwaltungsgebühr, mit den am Einsatztag gültigen Sätzen.
/// </summary>
public sealed class Kostenersatzrechner(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Ergebnis der Berechnung.</summary>
    /// <param name="Aufstellung">Posten und Hinweise; null, wenn noch keine Kostensätze hinterlegt sind.</param>
    /// <param name="RundungMinuten">Rundungseinheit in Minuten.</param>
    /// <param name="VorhaltezuschlagProzent">Zuschlag auf Verbrauchsmaterial in Prozent.</param>
    /// <param name="Fahrzeugnamen">Anzeigenamen der Fahrzeuge des Einsatzes.</param>
    public sealed record Ergebnis(Kostenaufstellung? Aufstellung, int RundungMinuten, decimal VorhaltezuschlagProzent, IReadOnlyDictionary<Guid, string> Fahrzeugnamen);

    /// <summary>Berechnet die Aufstellung.</summary>
    /// <param name="einsatz">Einsatz mit Fahrzeugen, Teilnahmen und Ausrüstung samt Kostensätzen.</param>
    /// <param name="vorgang">Vorgang mit Zusatzkosten oder null für die reine Berechnung aus dem Bericht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<Ergebnis> BerechnenAsync(Einsatz einsatz, Kostenersatzvorgang? vorgang, CancellationToken ct)
    {
        var tag = Ortszeit.Datum(einsatz.Alarmiert);
        var rundung = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzRundungMinuten, tag, 30, ct);
        var zuschlag = await parameter.DezimalAsync(ParameterSchluessel.KostenersatzVorhaltezuschlagProzent, tag, ct) ?? 10m;
        var ids = einsatz.Fahrzeuge.Select(f => f.FahrzeugId).ToList();
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().IgnoreQueryFilters().Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, Name = f.Funkrufname + " (" + f.Typ + ")", f.Kostenklasse }).ToListAsync(ct);
        var namen = fahrzeuge.ToDictionary(f => f.Id, f => f.Name);
        var klassen = fahrzeuge.ToDictionary(f => f.Id, f => f.Kostenklasse);
        var saetze = await db.Kostensaetze.AsNoTracking().ToListAsync(ct);
        if (saetze.Count == 0)
        {
            return new(null, rundung, zuschlag, namen);
        }

        var aufstellung = Kostenersatz.Berechnen(einsatz, saetze, rundung, id => namen.GetValueOrDefault(id, "Fahrzeug"), klassen.GetValueOrDefault);
        if (vorgang is not null)
        {
            var gebuehr = vorgang.Verwaltungsgebuehr
                ? saetze.Where(s => s.Art == KostensatzArt.Gebuehr && s.IstGueltigAm(tag)).MaxBy(s => s.GueltigAb)
                : null;
            aufstellung = Kostenersatz.Ergaenzen(aufstellung, vorgang.Zusatzkosten, zuschlag, gebuehr);
            if (vorgang.Verwaltungsgebuehr && gebuehr is null)
            {
                aufstellung = aufstellung with { Hinweise = [.. aufstellung.Hinweise, $"Verwaltungsgebühr: kein Satz am {tag:dd.MM.yyyy} hinterlegt."] };
            }
        }

        return new(aufstellung, rundung, zuschlag, namen);
    }
}
