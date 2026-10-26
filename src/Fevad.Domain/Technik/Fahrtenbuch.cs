using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Technik;

/// <summary>Anlass einer Fahrt im Fahrtenbuch.</summary>
public enum Fahrtzweck
{
    /// <summary>Einsatzfahrt; vorbelegt aus dem Einsatzbericht.</summary>
    [Display(Name = "Einsatz")] Einsatz,
    /// <summary>Übung oder Ausbildung; vorbelegt aus dem Dienstplan.</summary>
    [Display(Name = "Übung")] Uebung,
    /// <summary>Dienstfahrt, z. B. Lehrgang, Beschaffung.</summary>
    [Display(Name = "Dienstfahrt")] Dienstfahrt,
    /// <summary>Fahrt zur Werkstatt, HU oder Prüfung.</summary>
    [Display(Name = "Werkstatt / Prüfung")] Werkstatt,
    /// <summary>Bewegungsfahrt, damit das Fahrzeug nicht steht.</summary>
    [Display(Name = "Bewegungsfahrt")] Bewegungsfahrt,
    /// <summary>Sonstiges.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Kraftstoff einer Betankung.</summary>
public enum Kraftstoff
{
    /// <summary>Diesel.</summary>
    [Display(Name = "Diesel")] Diesel,
    /// <summary>Benzin, z. B. für Aggregate aus dem Kanister.</summary>
    [Display(Name = "Benzin")] Benzin,
    /// <summary>AdBlue; zählt nicht zum Verbrauch je 100 km.</summary>
    [Display(Name = "AdBlue")] AdBlue,
}

/// <summary>
/// Eintrag im digitalen Fahrtenbuch (V63, E11). Am Handy erfasst, aus Einsatz und Übung vorbelegt. Der Fahrer wird nach Ablauf der Frist
/// entfernt (Löschregel L63); Strecke und Kilometer bleiben für Verbrauch und Jahresbericht.
/// </summary>
public class Fahrt : Entitaet
{
    /// <summary>Kennung des Fahrzeugs.</summary>
    public Guid FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Abfahrt (UTC).</summary>
    public DateTimeOffset Beginn { get; set; }

    /// <summary>Rückkehr (UTC).</summary>
    public DateTimeOffset? Ende { get; set; }

    /// <summary>Anlass.</summary>
    public Fahrtzweck Zweck { get; set; }

    /// <summary>Ziel oder Strecke, z. B. „Göppingen, Atemschutzwerkstatt“.</summary>
    public string? Ziel { get; set; }

    /// <summary>Einsatz, aus dem die Fahrt vorbelegt wurde.</summary>
    public Guid? EinsatzId { get; set; }

    /// <summary>Dienst (Übung), aus dem die Fahrt vorbelegt wurde.</summary>
    public Guid? DienstId { get; set; }

    /// <summary>Fahrer; nach der Frist geleert (L63).</summary>
    public Guid? FahrerId { get; set; }
    /// <summary>Der Fahrer.</summary>
    public Mitglied? Fahrer { get; set; }

    /// <summary>Kilometerstand bei Abfahrt.</summary>
    public int? KmBeginn { get; set; }

    /// <summary>Kilometerstand bei Rückkehr.</summary>
    public int? KmEnde { get; set; }

    /// <summary>Gefahrene Kilometer, wenn die Stände nicht abgelesen wurden.</summary>
    public int? Kilometer { get; set; }

    /// <summary>Bemerkung, z. B. Auffälligkeiten am Fahrzeug.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Gefahrene Strecke: Differenz der Stände, sonst die eingetragenen Kilometer.</summary>
    public int? Strecke => KmBeginn is { } a && KmEnde is { } b && b >= a ? b - a : Kilometer;
}

/// <summary>Betankung eines Fahrzeugs (V63, E11), z. B. mit der Tankkarte der Gemeinde.</summary>
public class Betankung : Entitaet
{
    /// <summary>Kennung des Fahrzeugs.</summary>
    public Guid FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Tag der Betankung.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Kraftstoff.</summary>
    public Kraftstoff Kraftstoff { get; set; }

    /// <summary>Menge in Litern.</summary>
    public decimal Liter { get; set; }

    /// <summary>Betrag brutto in Euro.</summary>
    public decimal? Betrag { get; set; }

    /// <summary>Kilometerstand beim Tanken.</summary>
    public int? Kilometerstand { get; set; }

    /// <summary>Belegnummer oder Tankstelle.</summary>
    public string? Beleg { get; set; }

    /// <summary>Wer getankt hat; nach der Frist geleert (L63).</summary>
    public Guid? GetanktVonId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? GetanktVon { get; set; }
}

/// <summary>Jahresverbrauch eines Fahrzeugs aus Fahrtenbuch und Betankungen (V63).</summary>
public static class Fahrzeugverbrauch
{
    /// <summary>Werte eines Jahres.</summary>
    /// <param name="Jahr">Kalenderjahr.</param>
    /// <param name="Fahrten">Zahl der Fahrten.</param>
    /// <param name="Kilometer">Gefahrene Kilometer laut Fahrtenbuch.</param>
    /// <param name="Liter">Getankter Kraftstoff (Diesel und Benzin) in Litern.</param>
    /// <param name="AdBlueLiter">Getanktes AdBlue in Litern.</param>
    /// <param name="Betrag">Summe der Tankbelege.</param>
    /// <param name="LiterJe100Km">Verbrauch je 100 km; leer ohne Kilometer.</param>
    public sealed record Jahreswert(int Jahr, int Fahrten, int Kilometer, decimal Liter, decimal AdBlueLiter, decimal Betrag, decimal? LiterJe100Km);

    /// <summary>Fasst Fahrten und Betankungen je Kalenderjahr zusammen, jüngstes Jahr zuerst.</summary>
    /// <param name="fahrten">Jahr (Ortszeit) und Strecke je Fahrt.</param>
    /// <param name="betankungen">Die Betankungen.</param>
    /// <returns>Eine Zeile je Jahr mit Fahrten oder Betankungen.</returns>
    public static IReadOnlyList<Jahreswert> JeJahr(IEnumerable<(int Jahr, int? Strecke)> fahrten, IEnumerable<Betankung> betankungen)
    {
        var f = fahrten.ToLookup(x => x.Jahr);
        var b = betankungen.ToLookup(x => x.Datum.Year);
        return f.Select(g => g.Key).Union(b.Select(g => g.Key)).OrderDescending()
            .Select(jahr =>
            {
                var km = f[jahr].Sum(x => x.Strecke ?? 0);
                var liter = b[jahr].Where(x => x.Kraftstoff != Kraftstoff.AdBlue).Sum(x => x.Liter);
                return new Jahreswert(jahr, f[jahr].Count(), km, liter, b[jahr].Where(x => x.Kraftstoff == Kraftstoff.AdBlue).Sum(x => x.Liter),
                    b[jahr].Sum(x => x.Betrag ?? 0), km > 0 && liter > 0 ? Math.Round(liter * 100 / km, 1) : null);
            })
            .ToList();
    }
}
