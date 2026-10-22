namespace Fevad.Domain.Gemeinsam;

/// <summary>Protokoll eines Laufs der Fristerinnerung; bestimmt, wann die nächste Mail fällig ist.</summary>
public class Erinnerungslauf : Entitaet
{
    public DateTimeOffset Zeitpunkt { get; set; }
    public int Eintraege { get; set; }
    public int Empfaenger { get; set; }
}
