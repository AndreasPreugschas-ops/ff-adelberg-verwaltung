using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Technik;

public enum Pruefart
{
    Sichtpruefung,
    Funktionspruefung,
    Belastungspruefung,
    NachGebrauch,
    Hersteller,
    Hauptuntersuchung,
    Uvv,
}

/// <summary>Geräteart mit Prüfplan nach DGUV Grundsatz 305-002.</summary>
public class Geraeteart : Katalogeintrag
{
    public int? AussonderungNachJahren { get; set; }
    public string? Pruefgrundlage { get; set; }

    public List<PruefplanPosition> Pruefplan { get; set; } = [];
}

public class PruefplanPosition : Entitaet
{
    public Guid GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    public Pruefart Pruefart { get; set; }
    public int IntervallMonate { get; set; }

    /// <summary>Prüfpunkte als JSON. Jedes Protokoll speichert den Stand, mit dem geprüft wurde.</summary>
    public string Checkliste { get; set; } = "[]";
}

public class Geraet : Entitaet
{
    public required string Inventarnummer { get; set; }

    public Guid GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    public required string Bezeichnung { get; set; }
    public string? Hersteller { get; set; }
    public string? Typ { get; set; }
    public string? Seriennummer { get; set; }
    public int? Baujahr { get; set; }
    public DateOnly? IndienstAm { get; set; }
    public DateOnly? AusgesondertAm { get; set; }

    /// <summary>Fahrzeug, auf dem das Gerät verlastet ist.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }
    public string? Lagerort { get; set; }

    public List<Pruefung> Pruefungen { get; set; } = [];
}
