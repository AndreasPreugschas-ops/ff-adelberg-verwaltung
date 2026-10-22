using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Kleiderkammer;

public enum VorgangArt
{
    [Display(Name = "Ausgabe")] Ausgabe,
    [Display(Name = "Rücknahme")] Ruecknahme,
}

public enum RuecknahmeZustand
{
    [Display(Name = "wieder einlagern")] Einlagern,
    [Display(Name = "aussondern")] Aussondern,
}

/// <summary>Ausgabe oder Rücknahme an einem Termin; Grundlage der Quittung.</summary>
public class Kleiderkammervorgang : Entitaet
{
    /// <summary>Quittungsnummer, z. B. "KK-2026-0007".</summary>
    public required string Nummer { get; set; }
    public VorgangArt Art { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateTimeOffset Zeitpunkt { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Wann die unterschriebene Quittung vorlag.</summary>
    public DateTimeOffset? QuittiertAm { get; set; }
}

/// <summary>
/// Was ein Mitglied in Besitz hat oder hatte. Eine Ausgabe legt Zeilen an, eine Rücknahme schließt sie;
/// wird nur ein Teil zurückgegeben, wird die Zeile geteilt.
/// </summary>
public class Ausstattung : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    public required string Groesse { get; set; }
    public int Menge { get; set; } = 1;

    /// <summary>Bei Einzelstücken das ausgegebene Gerät.</summary>
    public Guid? GeraetId { get; set; }
    public Geraet? Geraet { get; set; }

    public DateOnly AusgegebenAm { get; set; }
    public Guid AusgabeVorgangId { get; set; }
    public Kleiderkammervorgang? AusgabeVorgang { get; set; }

    public DateOnly? ZurueckAm { get; set; }
    public Guid? RuecknahmeVorgangId { get; set; }
    public Kleiderkammervorgang? RuecknahmeVorgang { get; set; }
    public RuecknahmeZustand? Zustand { get; set; }

    public bool IstOffen => ZurueckAm is null;
}

/// <summary>Größe eines Mitglieds je Größenart, damit die Ausgabe passend vorschlägt.</summary>
public class Groessenangabe : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Groessenart Groessenart { get; set; }
    public required string Wert { get; set; }
    public DateOnly ErfasstAm { get; set; }
}
