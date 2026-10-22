namespace FfAdelberg.Domain.Gemeinsam;

/// <summary>
/// Basis aller Entitäten. Schlüssel sind UUID v7, damit der spätere Notbetrieb
/// offline Datensätze anlegen kann, ohne mit der Cloud zu kollidieren.
/// </summary>
public abstract class Entitaet
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset ErstelltAm { get; set; }
    public Guid? ErstelltVon { get; set; }
    public DateTimeOffset? GeaendertAm { get; set; }
    public Guid? GeaendertVon { get; set; }

    /// <summary>Soft-Delete. Ein globaler Query-Filter blendet gelöschte Datensätze aus.</summary>
    public DateTimeOffset? GeloeschtAm { get; set; }
}
