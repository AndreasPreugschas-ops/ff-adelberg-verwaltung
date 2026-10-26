using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Kleiderkammer;

/// <summary>Art eines Kleiderkammer-Vorgangs.</summary>
public enum VorgangArt
{
    /// <summary>Ausgabe an ein Mitglied.</summary>
    [Display(Name = "Ausgabe")] Ausgabe,
    /// <summary>Rücknahme vom Mitglied.</summary>
    [Display(Name = "Rücknahme")] Ruecknahme,
}

/// <summary>Was mit einem zurückgenommenen Stück geschieht.</summary>
public enum RuecknahmeZustand
{
    /// <summary>Wieder ins Lager.</summary>
    [Display(Name = "wieder einlagern")] Einlagern,
    /// <summary>Aussondern.</summary>
    [Display(Name = "aussondern")] Aussondern,
}

/// <summary>Ausgabe oder Rücknahme an einem Termin; Grundlage der Quittung.</summary>
public class Kleiderkammervorgang : Entitaet
{
    /// <summary>Quittungsnummer, z. B. "KK-2026-0007".</summary>
    public required string Nummer { get; set; }
    /// <summary>Ausgabe oder Rücknahme.</summary>
    public VorgangArt Art { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Zeitpunkt des Vorgangs in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Freitext, erscheint auf der Quittung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Wann die unterschriebene Quittung vorlag oder das Mitglied den Vorgang in FEVAD bestätigt hat.</summary>
    public DateTimeOffset? QuittiertAm { get; set; }

    /// <summary>Das Mitglied hat den Vorgang selbst in FEVAD bestätigt (V44) statt auf Papier zu unterschreiben.</summary>
    public bool QuittiertDigital { get; set; }
}

/// <summary>
/// Was ein Mitglied in Besitz hat oder hatte. Eine Ausgabe legt Zeilen an, eine Rücknahme schließt sie;
/// wird nur ein Teil zurückgegeben, wird die Zeile geteilt.
/// </summary>
public class Ausstattung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel des Artikels.</summary>
    public Guid ArtikelId { get; set; }
    /// <summary>Der Artikel.</summary>
    public Artikel? Artikel { get; set; }

    /// <summary>Ausgegebene Größe.</summary>
    public required string Groesse { get; set; }
    /// <summary>Ausgegebene Stückzahl; bei Einzelstücken 1.</summary>
    public int Menge { get; set; } = 1;

    /// <summary>Bei Einzelstücken das ausgegebene Gerät.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Das ausgegebene Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Tag der Ausgabe.</summary>
    public DateOnly AusgegebenAm { get; set; }
    /// <summary>Schlüssel des Ausgabevorgangs.</summary>
    public Guid AusgabeVorgangId { get; set; }
    /// <summary>Der Ausgabevorgang mit Quittung.</summary>
    public Kleiderkammervorgang? AusgabeVorgang { get; set; }

    /// <summary>Tag der Rückgabe; leer, solange das Mitglied das Stück hat.</summary>
    public DateOnly? ZurueckAm { get; set; }
    /// <summary>Schlüssel des Rücknahmevorgangs.</summary>
    public Guid? RuecknahmeVorgangId { get; set; }
    /// <summary>Der Rücknahmevorgang.</summary>
    public Kleiderkammervorgang? RuecknahmeVorgang { get; set; }
    /// <summary>Wieder einlagern oder aussondern.</summary>
    public RuecknahmeZustand? Zustand { get; set; }

    /// <summary>Bei der Rückgabe festgestellt: in ordnungsgemäßem Zustand oder nicht.</summary>
    public bool? RueckgabeOrdnungsgemaess { get; set; }

    /// <summary>Mängel bei nicht ordnungsgemäßer Rückgabe; erscheinen auf der Rückgabebestätigung.</summary>
    public string? RueckgabeMaengel { get; set; }

    /// <summary>Das Mitglied hat das Stück noch.</summary>
    public bool IstOffen => ZurueckAm is null;
}

/// <summary>Größe eines Mitglieds je Größenart, damit die Ausgabe passend vorschlägt.</summary>
public class Groessenangabe : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Größenart, z. B. Jacke.</summary>
    public Groessenart Groessenart { get; set; }
    /// <summary>Größe in einheitlicher Schreibweise.</summary>
    public required string Wert { get; set; }
    /// <summary>Tag, an dem die Größe erfasst wurde.</summary>
    public DateOnly ErfasstAm { get; set; }
}
