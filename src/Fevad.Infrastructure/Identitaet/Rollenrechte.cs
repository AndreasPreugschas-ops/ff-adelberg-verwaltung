using Fevad.Domain.Gemeinsam;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Anzeigename und Zweck einer Rolle. Die Rolle selbst ist die Identity-Rolle mit demselben <see cref="Name"/>.</summary>
public class Rollenprofil : Entitaet
{
    /// <summary>Technischer Name der Identity-Rolle, z. B. „Kommandant“.</summary>
    public required string Name { get; set; }
    /// <summary>Anzeigename der Rolle.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Zweck der Rolle für die Rechteverwaltung.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Mitgelieferte Rollen lassen sich umbenennen und anders berechtigen, aber nicht löschen.</summary>
    public bool Systemrolle => Rollen.Alle.Contains(Name);
}

/// <summary>Ein Recht einer Rolle, bei Rechten mit Abteilungsbezug wahlweise nur für bestimmte Abteilungen.</summary>
public class RolleRecht : Entitaet
{
    /// <summary>Name der Rolle.</summary>
    public required string Rolle { get; set; }
    /// <summary>Name des Rechts aus <see cref="Berechtigungen"/>.</summary>
    public required string Recht { get; set; }

    /// <summary>Leer: alle Abteilungen. Sonst nur diese und ihre Unterabteilungen (z. B. Jugendfeuerwehr mit Kinderfeuerwehr).</summary>
    public List<Guid> Abteilungen { get; set; } = [];
}

/// <summary>Merkt sich, dass die Vorbelegung eines Rechts übernommen ist; danach gilt nur noch, was in der Datenbank steht.</summary>
public class RechtEingefuehrt : Entitaet
{
    /// <summary>Name des Rechts aus <see cref="Berechtigungen"/>.</summary>
    public required string Recht { get; set; }
}
