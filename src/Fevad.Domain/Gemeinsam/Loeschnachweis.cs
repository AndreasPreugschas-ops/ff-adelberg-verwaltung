namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Nachweis eines Löschlaufs oder einer endgültigen Löschung: welche Regel, wie viele Datensätze, wann, ausgelöst von wem.
/// Enthält keine Inhalte der gelöschten Daten.
/// </summary>
public class Loeschnachweis : Entitaet
{
    public DateTimeOffset Zeitpunkt { get; set; }

    /// <summary>Regel aus dem Löschkonzept, z. B. „L1 Alarm-Rohdaten“.</summary>
    public required string Regel { get; set; }

    public int Anzahl { get; set; }

    /// <summary>Leer beim nächtlichen Lauf, sonst das Konto, das die Löschung ausgelöst hat.</summary>
    public Guid? AusgeloestVon { get; set; }
}
