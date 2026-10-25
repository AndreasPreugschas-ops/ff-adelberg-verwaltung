namespace Fevad.Infrastructure.Persistenz;

/// <summary>Liefert den angemeldeten Benutzer für Audit-Felder. Implementierung in der Web-Schicht.</summary>
public interface IAktuellerBenutzer
{
    /// <summary>Schlüssel des angemeldeten Kontos; leer ohne Anmeldung oder bei Hintergrundläufen.</summary>
    Guid? Id { get; }
}

/// <summary>Ersatz ohne angemeldeten Benutzer, z. B. für Hintergrunddienste und Werkzeuge.</summary>
public sealed class KeinBenutzer : IAktuellerBenutzer
{
    /// <inheritdoc />
    public Guid? Id => null;
}
