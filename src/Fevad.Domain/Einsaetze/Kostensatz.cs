using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

/// <summary>Wofür ein Kostensatz gilt.</summary>
public enum KostensatzArt
{
    /// <summary>Ein Fahrzeug.</summary>
    [Display(Name = "Fahrzeug")] Fahrzeug,
    /// <summary>Einsatzkräfte.</summary>
    [Display(Name = "Personal")] Personal,
    /// <summary>Ein Gerät.</summary>
    [Display(Name = "Gerät")] Geraet,
    /// <summary>Verbrauchsmaterial.</summary>
    [Display(Name = "Verbrauchsmaterial")] Verbrauch,
    /// <summary>Stundensatz einer Fahrzeugklasse der VOKeFw (z. B. „HLF 10“); gilt für jedes Fahrzeug dieser Klasse ohne eigenen Satz.</summary>
    [Display(Name = "Fahrzeugklasse (VOKeFw)")] Fahrzeugklasse,
    /// <summary>Verwaltungsgebühr für den Kostenersatzbescheid.</summary>
    [Display(Name = "Verwaltungsgebühr")] Gebuehr,
    /// <summary>Kosten nach tatsächlichem Aufwand; kein Satz, nur Art eines berechneten Postens.</summary>
    [Display(Name = "Zusatzkosten")] Zusatzkosten,
}

/// <summary>Einheit eines Kostensatzes.</summary>
public enum Kosteneinheit
{
    /// <summary>Je angefangene Stunde nach Rundung.</summary>
    [Display(Name = "Stunde")] Stunde,
    /// <summary>Je Tag.</summary>
    [Display(Name = "Tag")] Tag,
    /// <summary>Je Stück.</summary>
    [Display(Name = "Stück")] Stueck,
    /// <summary>Pauschal, unabhängig von Dauer und Menge.</summary>
    [Display(Name = "Pauschale")] Pauschale,
}

/// <summary>
/// Satz des Kostenersatzes nach § 34 FwG und der Feuerwehrkostenersatzsatzung der Gemeinde.
/// Stammdaten mit Gültigkeit: eine neue Fassung der Satzung ist eine neue Zeile ab ihrem Stichtag;
/// Berichte rechnen mit dem Satz, der am Einsatztag galt.
/// </summary>
public class Kostensatz : Entitaet
{
    /// <summary>Sätze, deren Quelle so beginnt, sind vorläufig (z. B. aus dem fiktiven Satzungsentwurf) und werden so gekennzeichnet.</summary>
    public const string PlatzhalterQuelle = "Platzhalter";

    /// <summary>Der Satz ist vorläufig und stammt nicht aus einer beschlossenen Satzung.</summary>
    public bool IstPlatzhalter => Quelle?.StartsWith(PlatzhalterQuelle, StringComparison.Ordinal) == true;

    /// <summary>Wofür der Satz gilt.</summary>
    public KostensatzArt Art { get; set; }

    /// <summary>Bei Art Fahrzeug: das Fahrzeug, für das der Satz gilt.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug bei Art Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Z. B. "Einsatzkraft", "Ölbindemittel (Sack)", "Tauchpumpe TP 4/1"; bei einer Fahrzeugklasse ihr Kurzname, z. B. "HLF 10".</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Einheit, in der abgerechnet wird.</summary>
    public Kosteneinheit Einheit { get; set; }

    /// <summary>Euro je Einheit.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Erster Tag der Gültigkeit.</summary>
    public DateOnly GueltigAb { get; set; }
    /// <summary>Letzter Tag der Gültigkeit; <c>null</c> ohne Ende.</summary>
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Herkunft, z. B. "Feuerwehrkostenersatzsatzung Adelberg, Anlage 1".</summary>
    public string? Quelle { get; set; }

    /// <summary>Der Satz gilt am Stichtag.</summary>
    /// <param name="stichtag">Der Tag.</param>
    /// <returns><c>true</c>, wenn gültig.</returns>
    public bool IstGueltigAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);
}
