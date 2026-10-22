namespace FfAdelberg.Infrastructure.Persistenz;

/// <summary>Liefert den angemeldeten Benutzer für Audit-Felder. Implementierung in der Web-Schicht.</summary>
public interface IAktuellerBenutzer
{
    Guid? Id { get; }
}

public sealed class KeinBenutzer : IAktuellerBenutzer
{
    public Guid? Id => null;
}
