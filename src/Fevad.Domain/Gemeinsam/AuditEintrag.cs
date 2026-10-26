namespace Fevad.Domain.Gemeinsam;

/// <summary>Art der protokollierten Änderung an einem Datensatz.</summary>
public enum AuditAktion
{
    /// <summary>Der Datensatz wurde neu angelegt.</summary>
    Angelegt,
    /// <summary>Mindestens ein Feld des Datensatzes wurde geändert.</summary>
    Geaendert,
    /// <summary>Der Datensatz wurde gelöscht (auch Soft-Delete).</summary>
    Geloescht,
    /// <summary>Gesundheitsdaten zum Datensatz wurden angezeigt (Leseprotokoll, F17).</summary>
    Gelesen,
}

/// <summary>
/// Änderungsprotokoll für personenbezogene Daten (Rechenschaftspflicht DSGVO).
/// Wird vom AuditInterceptor geschrieben, nie von Hand.
/// </summary>
public class AuditEintrag
{
    /// <summary>Fortlaufender technischer Schlüssel des Protokolleintrags.</summary>
    public long Id { get; set; }
    /// <summary>Zeitpunkt der Änderung in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Konto, das die Änderung ausgelöst hat; leer bei Hintergrundläufen ohne angemeldeten Benutzer.</summary>
    public Guid? BenutzerId { get; set; }
    /// <summary>Name der Datenbanktabelle des geänderten Datensatzes.</summary>
    public required string Tabelle { get; set; }
    /// <summary>Schlüssel des geänderten Datensatzes.</summary>
    public Guid DatensatzId { get; set; }
    /// <summary>Ob der Datensatz angelegt, geändert oder gelöscht wurde.</summary>
    public AuditAktion Aktion { get; set; }

    /// <summary>Geänderte Felder als JSON: { "Feld": { "alt": ..., "neu": ... } }.</summary>
    public string? Aenderungen { get; set; }
}
