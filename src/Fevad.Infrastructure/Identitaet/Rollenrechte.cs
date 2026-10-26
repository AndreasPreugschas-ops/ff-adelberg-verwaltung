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

/// <summary>Was eine zurückgehaltene Rechteänderung bewirkt.</summary>
public enum RechtefreigabeArt
{
    /// <summary>Eine Rolle bekommt ein Recht oder weitere Abteilungen dazu.</summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Recht für eine Rolle")] Rollenrecht,
    /// <summary>Ein Konto bekommt eine Rolle mit schutzbedürftigen Rechten.</summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Rolle für ein Konto")] Rollenzuweisung,
}

/// <summary>
/// Vier-Augen-Prinzip (F18): Eine Rechteänderung, die Gesundheits- oder Jugenddaten öffnet, wartet hier, bis eine zweite Person mit
/// <see cref="Berechtigungen.RechteFreigeben"/> sie freigibt oder ablehnt. Erst die Freigabe trägt das Recht bzw. die Rolle ein.
/// </summary>
public class Rechtefreigabe : Entitaet
{
    /// <summary>Recht für eine Rolle oder Rolle für ein Konto.</summary>
    public RechtefreigabeArt Art { get; set; }
    /// <summary>Technischer Name der Rolle.</summary>
    public required string Rolle { get; set; }
    /// <summary>Bei <see cref="RechtefreigabeArt.Rollenrecht"/>: das Recht.</summary>
    public string? Recht { get; set; }
    /// <summary>Bei <see cref="RechtefreigabeArt.Rollenrecht"/>: gewünschte Abteilungen, leer heißt alle.</summary>
    public List<Guid> Abteilungen { get; set; } = [];
    /// <summary>Bei <see cref="RechtefreigabeArt.Rollenzuweisung"/>: das Konto, das die Rolle bekommen soll.</summary>
    public Guid? KontoId { get; set; }
    /// <summary>Konto, das die Änderung beantragt hat; es darf sie nicht selbst freigeben.</summary>
    public Guid BeantragtVon { get; set; }
    /// <summary>Konto, das freigegeben oder abgelehnt hat.</summary>
    public Guid? EntschiedenVon { get; set; }
    /// <summary>Zeitpunkt der Entscheidung (UTC).</summary>
    public DateTimeOffset? EntschiedenAm { get; set; }
    /// <summary>true freigegeben, false abgelehnt, null offen.</summary>
    public bool? Freigegeben { get; set; }

    /// <summary>Wartet noch auf die zweite Person.</summary>
    public bool Offen => Freigegeben is null;
}
