using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Kleiderkammer;

/// <summary>Kategorie eines Kleiderkammer-Artikels.</summary>
public enum ArtikelKategorie
{
    /// <summary>Einsatzjacke und -hose (Schutzkleidung).</summary>
    [Display(Name = "Einsatzkleidung")] Einsatzkleidung,
    /// <summary>Helm und Flammschutzhaube.</summary>
    [Display(Name = "Kopfschutz")] Kopfschutz,
    /// <summary>Feuerwehrschutzhandschuhe.</summary>
    [Display(Name = "Handschutz")] Handschutz,
    /// <summary>Feuerwehrschutzschuhe und -stiefel.</summary>
    [Display(Name = "Fußschutz")] Fussschutz,
    /// <summary>Haltegurt und Absturzsicherung.</summary>
    [Display(Name = "Absturzsicherung")] Absturzsicherung,
    /// <summary>Dienstkleidung, z. B. Dienstjacke und Mütze.</summary>
    [Display(Name = "Dienstkleidung")] Dienstkleidung,
    /// <summary>Sonstige Artikel.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Welche Größe aus dem Größenprofil des Mitglieds zu einem Artikel passt.</summary>
public enum Groessenart
{
    /// <summary>Jackengröße.</summary>
    [Display(Name = "Jacke")] Jacke,
    /// <summary>Hosengröße.</summary>
    [Display(Name = "Hose")] Hose,
    /// <summary>Größe von Hemd, Poloshirt oder Pullover.</summary>
    [Display(Name = "Oberteil")] Oberteil,
    /// <summary>Schuhgröße.</summary>
    [Display(Name = "Schuhe")] Schuhe,
    /// <summary>Handschuhgröße.</summary>
    [Display(Name = "Handschuhe")] Handschuhe,
    /// <summary>Kopfumfang, z. B. für Helme.</summary>
    [Display(Name = "Kopf")] Kopf,
    /// <summary>Ohne Größe.</summary>
    [Display(Name = "Einheitsgröße")] Einheitsgroesse,
}

/// <summary>
/// Artikel der Kleiderkammer. Mit Geräteart wird jedes Stück einzeln als <see cref="Geraet"/> geführt
/// (Inventarnummer, Prüfplan, Aussonderung). Ohne Geräteart ist es ein Mengenartikel mit Lagerbestand je Größe.
/// </summary>
public class Artikel : Katalogeintrag
{
    /// <summary>Kategorie des Artikels.</summary>
    public ArtikelKategorie Kategorie { get; set; }
    /// <summary>Welche Größe des Mitglieds zum Artikel passt.</summary>
    public Groessenart Groessenart { get; set; }

    /// <summary>Schlüssel der Geräteart bei Einzelstücken.</summary>
    public Guid? GeraeteartId { get; set; }
    /// <summary>Geräteart, mit der jedes Stück als Gerät mit Prüfplan geführt wird.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Gehört zur persönlichen Grundausstattung jedes aktiven Mitglieds einer Abteilung dieser Art, also aller Einsatzabteilungen.</summary>
    public AbteilungArt? GrundausstattungArt { get; set; }

    /// <summary>Norm des Artikels, z. B. „DIN EN 469“.</summary>
    public string? Norm { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Lagerbestände je Größe.</summary>
    public List<Lagerbestand> Bestaende { get; set; } = [];

    /// <summary>Preise bei den Lieferanten.</summary>
    public List<ArtikelPreis> Preise { get; set; } = [];

    /// <summary>Jedes Stück wird einzeln als Gerät geführt.</summary>
    public bool IstEinzelstueck => GeraeteartId is not null;
}

/// <summary>Lagerbestand und Mindestmenge eines Artikels in einer Größe.</summary>
public class Lagerbestand : Entitaet
{
    /// <summary>Schlüssel des Artikels.</summary>
    public Guid ArtikelId { get; set; }
    /// <summary>Der Artikel.</summary>
    public Artikel? Artikel { get; set; }

    /// <summary>Größe in einheitlicher Schreibweise (<see cref="Kleiderkammer.Groesse.Normalisieren"/>).</summary>
    public required string Groesse { get; set; }

    /// <summary>Stück im Lager. Nur bei Mengenartikeln; Einzelstücke zählt die Kleiderkammer über die Geräte.</summary>
    public int Menge { get; set; }

    /// <summary>Unterschreitet der Lagerbestand diese Menge, entsteht Nachbestellbedarf.</summary>
    public int Mindestmenge { get; set; }

    /// <summary>Lagerort, z. B. Regal in der Kleiderkammer.</summary>
    public string? Lagerort { get; set; }
}

/// <summary>Hilfsfunktionen für Größenangaben.</summary>
public static class Groesse
{
    /// <summary>Einheitliche Schreibweise, damit "xl" und "XL " dieselbe Größe sind.</summary>
    /// <param name="groesse">Eingegebene Größe.</param>
    /// <returns>Die Größe in Großbuchstaben mit einfachen Leerzeichen; leer bei fehlender Angabe.</returns>
    public static string Normalisieren(string? groesse) =>
        string.IsNullOrWhiteSpace(groesse) ? "" : string.Join(' ', groesse.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
