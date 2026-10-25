using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>
/// Materialstamm: ein Ausrüstungsgegenstand, wie er vielfach vorkommen kann, z. B. „Druckschlauch C 42-15 von Parsch“.
/// Jedes <see cref="Geraet"/> gehört zu genau einem Materialstamm; Geräteart und damit Prüfplan kommen von hier.
/// </summary>
public class Material : Entitaet
{
    /// <summary>Bezeichnung des Materials.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Hersteller.</summary>
    public string? Hersteller { get; set; }

    /// <summary>Typ- oder Modellbezeichnung des Herstellers.</summary>
    public string? Typ { get; set; }

    /// <summary>Artikel- bzw. Teilenummer des Herstellers; hilft bei Suche und Nachbestellung.</summary>
    public string? Herstellerteilenummer { get; set; }

    /// <summary>Kennung der Geräteart.</summary>
    public Guid GeraeteartId { get; set; }
    /// <summary>Die Geräteart mit dem Prüfplan.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Anschaffungswert je Stück (netto). Bestimmt mit den Wertgrenzen, ob inventarisiert und wie abgeschrieben wird.</summary>
    public decimal? Stueckwert { get; set; }

    /// <summary>Jedes Stück bekommt eine Inventarnummer. Vorbelegt aus dem Stückwert, aber änderbar.</summary>
    public bool Inventarisierungspflichtig { get; set; } = true;

    /// <summary>Stücke werden bei Inventuren gezählt und brauchen eine Inventurgruppe. Nur zusammen mit der Inventarisierungspflicht.</summary>
    public bool Inventurpflichtig { get; set; } = true;

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }
    /// <summary>In Auswahllisten angeboten.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Die Stücke dieses Materials.</summary>
    public List<Geraet> Geraete { get; set; } = [];

    /// <summary>Bezeichnung, Hersteller und Typ in einer Zeile.</summary>
    public string Anzeigename =>
        string.Join(" · ", new[] { Bezeichnung, Hersteller, Typ }.Where(t => !string.IsNullOrWhiteSpace(t)));

    /// <summary>Vorschlag für die Inventarisierungspflicht aus dem Stückwert; ohne Wert wird inventarisiert.</summary>
    public static bool InventarisierungVorschlagen(decimal? stueckwert, int abWertEuro) =>
        stueckwert is null || stueckwert >= abWertEuro;

    /// <summary>Abschreibung nach dem Stückwert. Wird nie gespeichert.</summary>
    public static Abschreibungsart Abschreibung(decimal? stueckwert, int direktBisWertEuro) => stueckwert switch
    {
        null => Abschreibungsart.Unbekannt,
        _ when stueckwert <= direktBisWertEuro => Abschreibungsart.Direkt,
        _ => Abschreibungsart.Nutzungsdauer,
    };
}

/// <summary>Wie ein Material abgeschrieben wird.</summary>
public enum Abschreibungsart
{
    /// <summary>Kein Stückwert bekannt.</summary>
    [Display(Name = "ohne Stückwert")] Unbekannt,
    /// <summary>Im Jahr der Anschaffung voll abgeschrieben.</summary>
    [Display(Name = "direkt abgeschrieben")] Direkt,
    /// <summary>Über die Nutzungsdauer abgeschrieben.</summary>
    [Display(Name = "über die Nutzungsdauer")] Nutzungsdauer,
}

/// <summary>Art einer Inventurgruppe.</summary>
public enum InventurgruppenArt
{
    /// <summary>Ein Fahrzeug.</summary>
    [Display(Name = "Fahrzeug")] Fahrzeug,
    /// <summary>Ein Raum.</summary>
    [Display(Name = "Raum")] Raum,
    /// <summary>Ein Regal.</summary>
    [Display(Name = "Regal")] Regal,
    /// <summary>Ein Ort oder Bereich.</summary>
    [Display(Name = "Ort / Bereich")] Ort,
    /// <summary>Sonstiges.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>
/// Logischer Ort für die Inventur: ein Fahrzeug, ein Raum, ein Regal oder ein Bereich.
/// Jedes inventurpflichtige Gerät gehört zu einer Inventurgruppe, damit man es bei der Zählung dort findet.
/// </summary>
public class Inventurgruppe : Entitaet
{
    /// <summary>Bezeichnung der Gruppe.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Art der Gruppe.</summary>
    public InventurgruppenArt Art { get; set; }

    /// <summary>Bei Art Fahrzeug das Fahrzeug; Geräte auf dem Fahrzeug landen dann automatisch in dieser Gruppe.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug, wenn die Gruppe ein Fahrzeug ist.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Beschreibung.</summary>
    public string? Beschreibung { get; set; }
    /// <summary>Reihenfolge in Listen.</summary>
    public int Sortierung { get; set; }
    /// <summary>In Auswahllisten angeboten.</summary>
    public bool Aktiv { get; set; } = true;
}
