namespace Fevad.Domain.Gemeinsam;

/// <summary>Protokoll eines Laufs der Fristerinnerung; bestimmt, wann die nächste Mail fällig ist.</summary>
public class Erinnerungslauf : Entitaet
{
    /// <summary>Zeitpunkt des Laufs in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Anzahl der fälligen oder abgelaufenen Fristen, die der Lauf gefunden hat.</summary>
    public int Eintraege { get; set; }
    /// <summary>Anzahl der Konten, die eine Erinnerungsmail erhalten haben.</summary>
    public int Empfaenger { get; set; }
}
