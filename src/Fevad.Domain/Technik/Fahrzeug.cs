using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

public class Fahrzeug : Entitaet
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

    public List<Geraet> Geraete { get; set; } = [];
}
