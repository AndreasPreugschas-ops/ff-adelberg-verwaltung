using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Verleihung eines Dienstgrads an ein Mitglied ab einem Tag; frühere Zuordnungen bleiben als Laufbahn erhalten.</summary>
public class DienstgradZuordnung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel des Dienstgrads.</summary>
    public Guid DienstgradId { get; set; }
    /// <summary>Der verliehene Dienstgrad.</summary>
    public Dienstgrad? Dienstgrad { get; set; }

    /// <summary>Tag der Beförderung bzw. Ernennung.</summary>
    public DateOnly Ab { get; set; }
    /// <summary>Freitext, z. B. Anlass oder Beschluss.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Wie jemand in eine Funktion kommt.</summary>
public enum FunktionArt
{
    /// <summary>Durch Wahl, z. B. in der Hauptversammlung.</summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Wahl")] Wahl,
    /// <summary>Durch Bestellung, z. B. durch den Kommandanten.</summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Bestellung")] Bestellung,
}

/// <summary>
/// Funktion mit Beginn und optionalem Ende; beendete Zuordnungen bleiben als Historie. Ist fachlich, nicht technisch:
/// Rechte vergibt der Administrator über Rollen.
/// </summary>
public class FunktionZuordnung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel der Funktion.</summary>
    public Guid FunktionId { get; set; }
    /// <summary>Die Funktion.</summary>
    public Funktion? Funktion { get; set; }

    /// <summary>Abteilung bei Funktionen mit <see cref="Funktion.Abteilungsart"/>, z. B. welche Einsatzabteilung jemand führt.</summary>
    public Guid? AbteilungId { get; set; }
    /// <summary>Die Abteilung, für die die Funktion gilt.</summary>
    public Abteilung? Abteilung { get; set; }

    /// <summary>Wahl, wenn die Funktion gewählt wird (<see cref="Funktion.MitWahl"/>), sonst Bestellung.</summary>
    public FunktionArt Art { get; set; }
    /// <summary>Beginn der Funktion.</summary>
    public DateOnly GueltigAb { get; set; }
    /// <summary>Ende der Funktion; leer, solange sie besteht.</summary>
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Tag der Wahl, z. B. die Hauptversammlung.</summary>
    public DateOnly? Wahldatum { get; set; }

    /// <summary>Tag des Beschlusses bzw. der Bestellung, z. B. Zustimmung des Gemeinderats (<see cref="Funktion.BeschlussDurch"/>).</summary>
    public DateOnly? Beschlussdatum { get; set; }
    /// <summary>Freitext, z. B. kommissarisch oder Stellvertretung.</summary>
    public string? Bemerkung { get; set; }
}

