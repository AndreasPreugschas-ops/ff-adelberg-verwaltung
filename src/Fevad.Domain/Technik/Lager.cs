using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Gruppe von Verbrauchsmaterial im Lager (V16).</summary>
public enum Lagerkategorie
{
    /// <summary>Schaummittel, Netzmittel.</summary>
    [Display(Name = "Löschmittel")] Loeschmittel,
    /// <summary>Ölbinder, Bindevlies.</summary>
    [Display(Name = "Bindemittel")] Bindemittel,
    /// <summary>Verbandmaterial und Erste-Hilfe-Füllungen (DIN 13157/13169).</summary>
    [Display(Name = "Sanitätsmaterial")] Sanitaet,
    /// <summary>Batterien und Akkus.</summary>
    [Display(Name = "Batterien")] Batterien,
    /// <summary>Kraftstoff, Öl, Kettenöl.</summary>
    [Display(Name = "Betriebsstoffe")] Betriebsstoffe,
    /// <summary>Alles andere.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Art einer Lagerbewegung.</summary>
public enum LagerbewegungArt
{
    /// <summary>Lieferung oder Zugang.</summary>
    [Display(Name = "Eingang")] Eingang,
    /// <summary>Entnahme, z. B. im Einsatz.</summary>
    [Display(Name = "Entnahme")] Entnahme,
    /// <summary>Verfallen und entsorgt.</summary>
    [Display(Name = "Verfall")] Verfall,
    /// <summary>Bestand nach Zählung berichtigt.</summary>
    [Display(Name = "Korrektur")] Korrektur,
}

/// <summary>
/// Verbrauchsmaterial außerhalb der Kleiderkammer (V16), z. B. Schaummittel, Ölbinder, Batterien, Erste-Hilfe-Füllungen.
/// Bestand in Chargen mit Verfallsdatum; der Verfall erscheint wie eine Prüffrist.
/// </summary>
public class Lagerartikel : Entitaet
{
    /// <summary>Bezeichnung, z. B. „Ölbinder Typ III R“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Gruppe.</summary>
    public Lagerkategorie Kategorie { get; set; }

    /// <summary>Einheit, z. B. „Sack“, „l“, „Stück“.</summary>
    public required string Einheit { get; set; }

    /// <summary>Unter dieser Menge nachbestellen.</summary>
    public decimal Mindestmenge { get; set; }

    /// <summary>Wiederbeschaffungspreis je Einheit in Euro (brutto); Grundlage des Kostenersatzes.</summary>
    public decimal? Stueckpreis { get; set; }

    /// <summary>Lagerort, z. B. „Magazin Regal 3“.</summary>
    public string? Lagerort { get; set; }

    /// <summary>Bei neuen Entnahmen wählbar.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Chargen mit Restmenge und Verfall.</summary>
    public List<Lagercharge> Chargen { get; set; } = [];

    /// <summary>Bestand über alle Chargen.</summary>
    public decimal Bestand => Chargen.Sum(c => c.Menge);

    /// <summary>Bestand unter der Mindestmenge.</summary>
    public bool Nachbestellen => Mindestmenge > 0 && Bestand < Mindestmenge;

    /// <summary>Frühester Verfall einer Charge mit Restmenge.</summary>
    public DateOnly? NaechsterVerfall => Chargen.Where(c => c.Menge > 0 && c.VerfallAm != null).Min(c => c.VerfallAm);

    /// <summary>
    /// Verteilt eine Entnahme auf die Chargen: zuerst, was am frühesten verfällt (FEFO), Chargen ohne Verfall zuletzt;
    /// schon verfallene Chargen nicht. Ändert die Restmengen.
    /// </summary>
    /// <param name="menge">Entnommene Menge.</param>
    /// <param name="heute">Stichtag für „verfallen“.</param>
    /// <returns>Je Charge die entnommene Menge; null, wenn der verwendbare Bestand nicht reicht.</returns>
    public IReadOnlyList<(Lagercharge Charge, decimal Menge)>? Entnehmen(decimal menge, DateOnly heute)
    {
        if (menge <= 0)
        {
            return null;
        }

        var verwendbar = Chargen.Where(c => c.Menge > 0 && (c.VerfallAm is null || c.VerfallAm >= heute))
            .OrderBy(c => c.VerfallAm ?? DateOnly.MaxValue).ThenBy(c => c.EingangAm).ToList();
        if (verwendbar.Sum(c => c.Menge) < menge)
        {
            return null;
        }

        var ergebnis = new List<(Lagercharge, decimal)>();
        var rest = menge;
        foreach (var charge in verwendbar)
        {
            var teil = Math.Min(rest, charge.Menge);
            charge.Menge -= teil;
            ergebnis.Add((charge, teil));
            rest -= teil;
            if (rest == 0)
            {
                break;
            }
        }

        return ergebnis;
    }
}

/// <summary>Eine Lieferung eines Lagerartikels mit gemeinsamem Verfallsdatum.</summary>
public class Lagercharge : Entitaet
{
    /// <summary>Kennung des Artikels.</summary>
    public Guid LagerartikelId { get; set; }
    /// <summary>Der Artikel.</summary>
    public Lagerartikel? Lagerartikel { get; set; }

    /// <summary>Restmenge.</summary>
    public decimal Menge { get; set; }

    /// <summary>Verfallsdatum; leer, wenn es keins gibt.</summary>
    public DateOnly? VerfallAm { get; set; }

    /// <summary>Tag des Eingangs.</summary>
    public DateOnly EingangAm { get; set; }
}

/// <summary>Zu- oder Abgang im Lager; Entnahmen im Einsatz tragen den Einsatz und werden Kostenersatz-Posten.</summary>
public class Lagerbewegung : Entitaet
{
    /// <summary>Kennung des Artikels.</summary>
    public Guid LagerartikelId { get; set; }
    /// <summary>Der Artikel.</summary>
    public Lagerartikel? Lagerartikel { get; set; }

    /// <summary>Betroffene Charge.</summary>
    public Guid? LagerchargeId { get; set; }

    /// <summary>Art der Bewegung.</summary>
    public LagerbewegungArt Art { get; set; }

    /// <summary>Tag der Bewegung.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Menge, immer positiv; die Art bestimmt die Richtung (Korrektur: neue minus alte Menge, mit Vorzeichen).</summary>
    public decimal Menge { get; set; }

    /// <summary>Preis je Einheit zum Zeitpunkt der Bewegung.</summary>
    public decimal? Stueckpreis { get; set; }

    /// <summary>Einsatz der Entnahme.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Kostenersatz-Posten, in den die Entnahme übernommen wurde.</summary>
    public Guid? ZusatzkostenId { get; set; }

    /// <summary>Bemerkung (Sachstand).</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Wert der Bewegung in Euro; leer ohne Preis.</summary>
    public decimal? Wert => Stueckpreis is { } preis ? Math.Round(preis * Menge, 2) : null;
}
