using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>
/// Materialstamm: ein Ausrüstungsgegenstand, wie er vielfach vorkommen kann, z. B. „Druckschlauch C 42-15 von Parsch“.
/// Jedes <see cref="Geraet"/> gehört zu genau einem Materialstamm; Geräteart und damit Prüfplan kommen von hier.
/// </summary>
public class Material : Entitaet
{
    public required string Bezeichnung { get; set; }
    public string? Hersteller { get; set; }

    /// <summary>Typ- oder Modellbezeichnung des Herstellers.</summary>
    public string? Typ { get; set; }

    /// <summary>Artikel- bzw. Teilenummer des Herstellers; hilft bei Suche und Nachbestellung.</summary>
    public string? Herstellerteilenummer { get; set; }

    public Guid GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Anschaffungswert je Stück (netto). Bestimmt mit den Wertgrenzen, ob inventarisiert und wie abgeschrieben wird.</summary>
    public decimal? Stueckwert { get; set; }

    /// <summary>Jedes Stück bekommt eine Inventarnummer. Vorbelegt aus dem Stückwert, aber änderbar.</summary>
    public bool Inventarisierungspflichtig { get; set; } = true;

    /// <summary>Stücke werden bei Inventuren gezählt und brauchen eine Inventurgruppe. Nur zusammen mit der Inventarisierungspflicht.</summary>
    public bool Inventurpflichtig { get; set; } = true;

    public string? Bemerkung { get; set; }
    public bool Aktiv { get; set; } = true;

    public List<Geraet> Geraete { get; set; } = [];

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

public enum Abschreibungsart
{
    [Display(Name = "ohne Stückwert")] Unbekannt,
    [Display(Name = "direkt abgeschrieben")] Direkt,
    [Display(Name = "über die Nutzungsdauer")] Nutzungsdauer,
}

public enum InventurgruppenArt
{
    [Display(Name = "Fahrzeug")] Fahrzeug,
    [Display(Name = "Raum")] Raum,
    [Display(Name = "Regal")] Regal,
    [Display(Name = "Ort / Bereich")] Ort,
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>
/// Logischer Ort für die Inventur: ein Fahrzeug, ein Raum, ein Regal oder ein Bereich.
/// Jedes inventurpflichtige Gerät gehört zu einer Inventurgruppe, damit man es bei der Zählung dort findet.
/// </summary>
public class Inventurgruppe : Entitaet
{
    public required string Bezeichnung { get; set; }
    public InventurgruppenArt Art { get; set; }

    /// <summary>Bei Art Fahrzeug das Fahrzeug; Geräte auf dem Fahrzeug landen dann automatisch in dieser Gruppe.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    public string? Beschreibung { get; set; }
    public int Sortierung { get; set; }
    public bool Aktiv { get; set; } = true;
}
