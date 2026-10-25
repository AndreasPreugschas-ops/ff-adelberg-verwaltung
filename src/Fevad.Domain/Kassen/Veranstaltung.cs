using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kassen;

/// <summary>
/// Veranstaltung einer Kasse als Kostenträger (V03), z. B. Hocketse oder Sonnwendfeuer. Einnahmen und Ausgaben werden mit Verweis auf
/// die Veranstaltung gebucht; die Abrechnung zeigt das Ergebnis und bei gemeinsamen Festen den Anteil der Feuerwehr.
/// </summary>
public class Veranstaltung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Name, z. B. „Hocketse 2026“.</summary>
    public required string Name { get; set; }

    /// <summary>Erster Tag.</summary>
    public DateOnly Beginn { get; set; }

    /// <summary>Letzter Tag bei mehrtägigen Veranstaltungen.</summary>
    public DateOnly? Ende { get; set; }

    /// <summary>Mitveranstalter bei gemeinsamen Festen, z. B. „Musikverein“.</summary>
    public string? Mitveranstalter { get; set; }

    /// <summary>Anteil der Feuerwehr am Ergebnis in Prozent (Umsatzschlüssel); 100 ohne Mitveranstalter.</summary>
    public decimal AnteilProzent { get; set; } = 100;

    /// <summary>Freie Bemerkung, z. B. Absprachen mit dem Mitveranstalter.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Die Abrechnung ist fertig; neue Barkassenberichte gehen dann nicht mehr.</summary>
    public bool Abgerechnet { get; set; }

    /// <summary>Barkassenberichte der Festtage.</summary>
    public List<Barkassenbericht> Barkassenberichte { get; set; } = [];

    /// <summary>Jahr der Veranstaltung.</summary>
    public int Jahr => Beginn.Year;
}

/// <summary>
/// Barkassenbericht einer Kassenstelle an einem Festtag (V03): Wechselgeld, Zählprotokoll nach Stückelung und zwei Zählende. Die Einnahme
/// ist der gezählte Bestand abzüglich Wechselgeld und wird als eine Buchung ins Kassenbuch übernommen.
/// </summary>
public class Barkassenbericht : Entitaet
{
    /// <summary>Schlüssel der Veranstaltung.</summary>
    public Guid VeranstaltungId { get; set; }

    /// <summary>Die Veranstaltung.</summary>
    public Veranstaltung? Veranstaltung { get; set; }

    /// <summary>Festtag.</summary>
    public DateOnly Tag { get; set; }

    /// <summary>Kassenstelle, z. B. „Getränke“ oder „Essen“.</summary>
    public required string Stelle { get; set; }

    /// <summary>Wechselgeld zu Beginn in Euro.</summary>
    public decimal Wechselgeld { get; set; }

    /// <summary>Gezählte Scheine und Münzen.</summary>
    public List<Zaehlposition> Zaehlung { get; set; } = [];

    /// <summary>Erste zählende Person (Name, auch ohne Konto in FEVAD).</summary>
    public required string GezaehltVon { get; set; }

    /// <summary>Zweite zählende Person; Vier-Augen-Prinzip beim Zählen.</summary>
    public required string ZweitePerson { get; set; }

    /// <summary>Umsatzsteuersatz der Einnahme; bei Speisen und Getränken je Satz einen eigenen Bericht anlegen.</summary>
    public Umsatzsteuersatz Steuersatz { get; set; } = Umsatzsteuersatz.Regel;

    /// <summary>Freie Bemerkung, z. B. Fehlbetrag erklärt.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Buchung im Kassenbuch, sobald übernommen.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Die Buchung.</summary>
    public Buchung? Buchung { get; set; }

    /// <summary>Gezählter Bestand in Euro.</summary>
    public decimal Endbestand => Zaehlung.Sum(z => z.Wert * z.Anzahl);

    /// <summary>Einnahme: gezählter Bestand abzüglich Wechselgeld.</summary>
    public decimal Einnahme => Endbestand - Wechselgeld;
}

/// <summary>Eine Zeile des Zählprotokolls: Wert eines Scheins oder einer Münze und die Anzahl.</summary>
public class Zaehlposition : Entitaet
{
    /// <summary>Schlüssel des Barkassenberichts.</summary>
    public Guid BarkassenberichtId { get; set; }

    /// <summary>Wert eines Stücks in Euro, z. B. 50 oder 0,20.</summary>
    public decimal Wert { get; set; }

    /// <summary>Anzahl der Stücke.</summary>
    public int Anzahl { get; set; }

    /// <summary>Scheine und Münzen des Euro, größte zuerst.</summary>
    public static IReadOnlyList<decimal> Stueckelung { get; } = [500m, 200m, 100m, 50m, 20m, 10m, 5m, 2m, 1m, 0.50m, 0.20m, 0.10m, 0.05m, 0.02m, 0.01m];
}

/// <summary>Abrechnung einer Veranstaltung aus ihren Buchungen.</summary>
/// <param name="Einnahmen">Summe der Einnahmen in Euro, Stornos abgezogen.</param>
/// <param name="Ausgaben">Summe der Ausgaben in Euro, Stornos abgezogen.</param>
/// <param name="AnteilProzent">Anteil der Feuerwehr in Prozent.</param>
public sealed record Veranstaltungsabrechnung(decimal Einnahmen, decimal Ausgaben, decimal AnteilProzent)
{
    /// <summary>Ergebnis der Veranstaltung insgesamt.</summary>
    public decimal Ergebnis => Einnahmen - Ausgaben;

    /// <summary>Anteil der Feuerwehr am Ergebnis, auf Cent gerundet.</summary>
    public decimal Anteil => Math.Round(Ergebnis * AnteilProzent / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Berechnet die Abrechnung aus den Buchungen der Veranstaltung.</summary>
    /// <param name="buchungen">Buchungen mit Verweis auf die Veranstaltung, samt Stornos.</param>
    /// <param name="anteilProzent">Anteil der Feuerwehr in Prozent.</param>
    /// <returns>Die Abrechnung.</returns>
    public static Veranstaltungsabrechnung Berechnen(IEnumerable<Buchung> buchungen, decimal anteilProzent)
    {
        var liste = buchungen.ToList();
        return new Veranstaltungsabrechnung(
            liste.Where(b => b.Art == BuchungArt.Einnahme).Sum(Kassenregeln.Ist),
            liste.Where(b => b.Art == BuchungArt.Ausgabe).Sum(Kassenregeln.Ist),
            anteilProzent);
    }
}
