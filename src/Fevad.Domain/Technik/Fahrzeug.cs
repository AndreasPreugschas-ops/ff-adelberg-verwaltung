using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Fahrzeug der Feuerwehr mit Funkrufname, Prüfvorlage und Anlagendaten.</summary>
public class Fahrzeug : Entitaet, IAnlagegut
{
    /// <summary>Muss exakt der Einsatzmittelkennung in alamos FE2 entsprechen, z. B. "Florian Adelberg 43".</summary>
    public required string Funkrufname { get; set; }
    public required string Typ { get; set; }
    public string? Kennzeichen { get; set; }
    public string? Hersteller { get; set; }
    public string? Aufbau { get; set; }
    public int? Baujahr { get; set; }
    public DateOnly? IndienstAm { get; set; }
    public DateOnly? AusserDienstAm { get; set; }
    public string? Issi { get; set; }
    public string? FmsKennung { get; set; }
    public int? Sitzplaetze { get; set; }

    /// <summary>Geräteart mit dem Prüfplan des Fahrzeugs (HU, UVV).</summary>
    public Guid? PruefvorlageId { get; set; }
    public Geraeteart? Pruefvorlage { get; set; }

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

    public List<Geraet> Geraete { get; set; } = [];
}
