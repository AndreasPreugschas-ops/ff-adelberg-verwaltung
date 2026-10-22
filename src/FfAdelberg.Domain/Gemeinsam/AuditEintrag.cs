namespace FfAdelberg.Domain.Gemeinsam;

public enum AuditAktion
{
    Angelegt,
    Geaendert,
    Geloescht,
}

/// <summary>
/// Änderungsprotokoll für personenbezogene Daten (Rechenschaftspflicht DSGVO).
/// Wird vom AuditInterceptor geschrieben, nie von Hand.
/// </summary>
public class AuditEintrag
{
    public long Id { get; set; }
    public DateTimeOffset Zeitpunkt { get; set; }
    public Guid? BenutzerId { get; set; }
    public required string Tabelle { get; set; }
    public Guid DatensatzId { get; set; }
    public AuditAktion Aktion { get; set; }

    /// <summary>Geänderte Felder als JSON: { "Feld": { "alt": ..., "neu": ... } }.</summary>
    public string? Aenderungen { get; set; }
}
