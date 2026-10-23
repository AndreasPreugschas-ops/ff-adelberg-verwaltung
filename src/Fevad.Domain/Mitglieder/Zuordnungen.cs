using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public class DienstgradZuordnung : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid DienstgradId { get; set; }
    public Dienstgrad? Dienstgrad { get; set; }

    public DateOnly Ab { get; set; }
    public string? Bemerkung { get; set; }
}

public enum FunktionArt
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Wahl")] Wahl,
    [System.ComponentModel.DataAnnotations.Display(Name = "Bestellung")] Bestellung,
}

/// <summary>
/// Funktion mit Beginn und optionalem Ende; beendete Zuordnungen bleiben als Historie. Ist fachlich, nicht technisch:
/// Rechte vergibt der Administrator über Rollen.
/// </summary>
public class FunktionZuordnung : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid FunktionId { get; set; }
    public Funktion? Funktion { get; set; }

    /// <summary>Abteilung bei Funktionen mit <see cref="Funktion.Abteilungsart"/>, z. B. welche Einsatzabteilung jemand führt.</summary>
    public Guid? AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    public FunktionArt Art { get; set; }
    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public DateOnly? Beschlussdatum { get; set; }
    public string? Bemerkung { get; set; }
}

