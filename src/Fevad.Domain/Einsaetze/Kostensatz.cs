using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

public enum KostensatzArt
{
    [Display(Name = "Fahrzeug")] Fahrzeug,
    [Display(Name = "Personal")] Personal,
    [Display(Name = "Gerät")] Geraet,
    [Display(Name = "Verbrauchsmaterial")] Verbrauch,
}

public enum Kosteneinheit
{
    [Display(Name = "Stunde")] Stunde,
    [Display(Name = "Tag")] Tag,
    [Display(Name = "Stück")] Stueck,
    [Display(Name = "Pauschale")] Pauschale,
}

/// <summary>
/// Satz des Kostenersatzes nach § 34 FwG und der Feuerwehrkostenersatzsatzung der Gemeinde.
/// Stammdaten mit Gültigkeit: eine neue Fassung der Satzung ist eine neue Zeile ab ihrem Stichtag;
/// Berichte rechnen mit dem Satz, der am Einsatztag galt.
/// </summary>
public class Kostensatz : Entitaet
{
    public KostensatzArt Art { get; set; }

    /// <summary>Bei Art Fahrzeug: das Fahrzeug, für das der Satz gilt.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Z. B. "Einsatzkraft", "Ölbindemittel (Sack)", "Tauchpumpe TP 4/1".</summary>
    public required string Bezeichnung { get; set; }

    public Kosteneinheit Einheit { get; set; }

    /// <summary>Euro je Einheit.</summary>
    public decimal Betrag { get; set; }

    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Herkunft, z. B. "Feuerwehrkostenersatzsatzung Adelberg, Anlage 1".</summary>
    public string? Quelle { get; set; }

    public bool IstGueltigAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);
}
