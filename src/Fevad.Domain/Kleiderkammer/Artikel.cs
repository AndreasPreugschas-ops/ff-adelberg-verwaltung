using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Kleiderkammer;

public enum ArtikelKategorie
{
    [Display(Name = "Einsatzkleidung")] Einsatzkleidung,
    [Display(Name = "Kopfschutz")] Kopfschutz,
    [Display(Name = "Handschutz")] Handschutz,
    [Display(Name = "Fußschutz")] Fussschutz,
    [Display(Name = "Absturzsicherung")] Absturzsicherung,
    [Display(Name = "Dienstkleidung")] Dienstkleidung,
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Welche Größe aus dem Größenprofil des Mitglieds zu einem Artikel passt.</summary>
public enum Groessenart
{
    [Display(Name = "Jacke")] Jacke,
    [Display(Name = "Hose")] Hose,
    [Display(Name = "Oberteil")] Oberteil,
    [Display(Name = "Schuhe")] Schuhe,
    [Display(Name = "Handschuhe")] Handschuhe,
    [Display(Name = "Kopf")] Kopf,
    [Display(Name = "Einheitsgröße")] Einheitsgroesse,
}

/// <summary>
/// Artikel der Kleiderkammer. Mit Geräteart wird jedes Stück einzeln als <see cref="Geraet"/> geführt
/// (Inventarnummer, Prüfplan, Aussonderung). Ohne Geräteart ist es ein Mengenartikel mit Lagerbestand je Größe.
/// </summary>
public class Artikel : Katalogeintrag
{
    public ArtikelKategorie Kategorie { get; set; }
    public Groessenart Groessenart { get; set; }

    public Guid? GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Gehört zur persönlichen Grundausstattung jedes aktiven Mitglieds dieser Abteilung.</summary>
    public Guid? GrundausstattungAbteilungId { get; set; }
    public Abteilung? GrundausstattungAbteilung { get; set; }

    public string? Norm { get; set; }
    public string? Bemerkung { get; set; }

    public List<Lagerbestand> Bestaende { get; set; } = [];

    public bool IstEinzelstueck => GeraeteartId is not null;
}

/// <summary>Lagerbestand und Mindestmenge eines Artikels in einer Größe.</summary>
public class Lagerbestand : Entitaet
{
    public Guid ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }

    public required string Groesse { get; set; }

    /// <summary>Stück im Lager. Nur bei Mengenartikeln; Einzelstücke zählt die Kleiderkammer über die Geräte.</summary>
    public int Menge { get; set; }

    /// <summary>Unterschreitet der Lagerbestand diese Menge, entsteht Nachbestellbedarf.</summary>
    public int Mindestmenge { get; set; }

    public string? Lagerort { get; set; }
}

public static class Groesse
{
    /// <summary>Einheitliche Schreibweise, damit "xl" und "XL " dieselbe Größe sind.</summary>
    public static string Normalisieren(string? groesse) =>
        string.IsNullOrWhiteSpace(groesse) ? "" : string.Join(' ', groesse.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
