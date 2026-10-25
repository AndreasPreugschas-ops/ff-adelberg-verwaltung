using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Fahrzeug der Feuerwehr mit Funkrufname, Prüfvorlage und Anlagendaten.</summary>
public class Fahrzeug : Entitaet, IAnlagegut
{
    /// <summary>Muss exakt der Einsatzmittelkennung in alamos FE2 entsprechen, z. B. "Florian Adelberg 43".</summary>
    public required string Funkrufname { get; set; }
    /// <summary>Fahrzeugtyp, z. B. „HLF 10“.</summary>
    public required string Typ { get; set; }
    /// <summary>Amtliches Kennzeichen.</summary>
    public string? Kennzeichen { get; set; }
    /// <summary>Hersteller des Fahrgestells.</summary>
    public string? Hersteller { get; set; }
    /// <summary>Aufbauhersteller.</summary>
    public string? Aufbau { get; set; }
    /// <summary>Baujahr.</summary>
    public int? Baujahr { get; set; }
    /// <summary>Tag der Indienststellung.</summary>
    public DateOnly? IndienstAm { get; set; }
    /// <summary>Tag der Außerdienststellung.</summary>
    public DateOnly? AusserDienstAm { get; set; }
    /// <summary>ISSI des Fahrzeugfunkgeräts (Digitalfunk).</summary>
    public string? Issi { get; set; }
    /// <summary>FMS-Kennung.</summary>
    public string? FmsKennung { get; set; }
    /// <summary>Zahl der Sitzplätze.</summary>
    public int? Sitzplaetze { get; set; }

    /// <summary>Geräteart mit dem Prüfplan des Fahrzeugs (HU, UVV).</summary>
    public Guid? PruefvorlageId { get; set; }
    /// <summary>Die Prüfvorlage.</summary>
    public Geraeteart? Pruefvorlage { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Anschaffungswert brutto in Euro (V60).</summary>
    public decimal? Anschaffungswert { get; set; }

    /// <inheritdoc />
    public string? Anlagennummer { get; set; }

    /// <inheritdoc />
    public int? NutzungsdauerJahre { get; set; }

    /// <inheritdoc />
    public Eigentuemer Eigentuemer { get; set; }

    /// <inheritdoc />
    public decimal? Foerdermittel { get; set; }

    /// <inheritdoc />
    public string? Foerdergeber { get; set; }

    /// <inheritdoc />
    public DateOnly? ZweckbindungBis { get; set; }

    /// <summary>Grund der Außerdienststellung bzw. Aussonderung.</summary>
    public string? Aussonderungsgrund { get; set; }

    /// <inheritdoc />
    public Verwertung? Verwertung { get; set; }

    /// <inheritdoc />
    public decimal? Erloes { get; set; }

    /// <inheritdoc />
    public string? Aussonderungsbeschluss { get; set; }

    /// <inheritdoc />
    public DateOnly? FoerdergeberMitgeteiltAm { get; set; }

    /// <summary>Verlastete Geräte.</summary>
    public List<Geraet> Geraete { get; set; } = [];
}
