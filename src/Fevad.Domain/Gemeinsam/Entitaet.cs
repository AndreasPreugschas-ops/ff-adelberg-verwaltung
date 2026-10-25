namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Basis aller Entitäten. Schlüssel sind UUID v7, damit der spätere Notbetrieb
/// offline Datensätze anlegen kann, ohne mit der Cloud zu kollidieren.
/// </summary>
public abstract class Entitaet
{
    /// <summary>Eindeutiger Schlüssel (UUID v7, zeitlich sortierbar), wird beim Erzeugen vergeben.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Zeitpunkt der Anlage in UTC; setzt der Audit-Interceptor beim Speichern.</summary>
    public DateTimeOffset ErstelltAm { get; set; }
    /// <summary>Konto, das den Datensatz angelegt hat; leer bei Anlage durch das System.</summary>
    public Guid? ErstelltVon { get; set; }
    /// <summary>Zeitpunkt der letzten Änderung in UTC; leer, solange der Datensatz nie geändert wurde.</summary>
    public DateTimeOffset? GeaendertAm { get; set; }
    /// <summary>Konto, das den Datensatz zuletzt geändert hat.</summary>
    public Guid? GeaendertVon { get; set; }

    /// <summary>Soft-Delete. Ein globaler Query-Filter blendet gelöschte Datensätze aus.</summary>
    public DateTimeOffset? GeloeschtAm { get; set; }
}
