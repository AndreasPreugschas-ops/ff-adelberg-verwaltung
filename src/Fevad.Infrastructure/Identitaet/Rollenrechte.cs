using Fevad.Domain.Gemeinsam;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Anzeigename und Zweck einer Rolle. Die Rolle selbst ist die Identity-Rolle mit demselben <see cref="Name"/>.</summary>
public class Rollenprofil : Entitaet
{
    public required string Name { get; set; }
    public required string Bezeichnung { get; set; }
    public string? Beschreibung { get; set; }

    /// <summary>Mitgelieferte Rollen lassen sich umbenennen und anders berechtigen, aber nicht löschen.</summary>
    public bool Systemrolle => Rollen.Alle.Contains(Name);
}

/// <summary>Ein Recht einer Rolle, bei Rechten mit Abteilungsbezug wahlweise nur für bestimmte Abteilungen.</summary>
public class RolleRecht : Entitaet
{
    public required string Rolle { get; set; }
    public required string Recht { get; set; }

    /// <summary>Leer: alle Abteilungen. Sonst nur diese und ihre Unterabteilungen (z. B. Jugendfeuerwehr mit Kinderfeuerwehr).</summary>
    public List<Guid> Abteilungen { get; set; } = [];
}

/// <summary>Merkt sich, dass die Vorbelegung eines Rechts übernommen ist; danach gilt nur noch, was in der Datenbank steht.</summary>
public class RechtEingefuehrt : Entitaet
{
    public required string Recht { get; set; }
}
