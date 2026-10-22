using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Mitglieder;

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
    Wahl,
    Bestellung,
}

/// <summary>Satzungsfunktion mit Amtszeit. Ist fachlich, nicht technisch: Rechte vergibt der Administrator über Rollen.</summary>
public class FunktionZuordnung : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid FunktionId { get; set; }
    public Funktion? Funktion { get; set; }

    public FunktionArt Art { get; set; }
    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public DateOnly? Beschlussdatum { get; set; }
    public string? Bemerkung { get; set; }
}
