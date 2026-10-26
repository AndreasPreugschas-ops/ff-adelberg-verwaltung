namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Antrag eines Mitglieds auf Löschung seiner Daten nach Art. 17 DSGVO (V39), gestellt im eigenen Konto. Die Verwaltung prüft
/// gegen die Aufbewahrungspflichten des Löschkonzepts und antwortet; gelöscht wird über die bestehenden Seiten. Löschung nach L52.
/// </summary>
public class Loeschanfrage : Entitaet
{
    /// <summary>Konto, das die Anfrage gestellt hat.</summary>
    public Guid KontoId { get; set; }
    /// <summary>Mitglied des Kontos zum Zeitpunkt der Anfrage, falls verknüpft.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Name der anfragenden Person, damit die Anfrage auch nach Löschung des Kontos zuzuordnen ist.</summary>
    public required string Name { get; set; }
    /// <summary>Was gelöscht werden soll, in eigenen Worten.</summary>
    public required string Text { get; set; }
    /// <summary>Zeitpunkt der Anfrage in UTC.</summary>
    public DateTimeOffset GestelltAm { get; set; }

    /// <summary>Zeitpunkt der Erledigung in UTC; leer, solange offen.</summary>
    public DateTimeOffset? ErledigtAm { get; set; }
    /// <summary>Antwort der Verwaltung, z. B. was gelöscht wurde und was wegen Aufbewahrungspflichten bleibt.</summary>
    public string? Antwort { get; set; }
    /// <summary>Konto, das die Anfrage erledigt hat.</summary>
    public Guid? ErledigtVon { get; set; }
}
