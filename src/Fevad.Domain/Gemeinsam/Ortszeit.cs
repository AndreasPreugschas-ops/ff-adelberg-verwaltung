namespace Fevad.Domain.Gemeinsam;

/// <summary>Umrechnung zwischen gespeicherter UTC-Zeit und deutscher Ortszeit für fachliche Stichtage.</summary>
public static class Ortszeit
{
    /// <summary>Zeitzone Europe/Berlin mit Sommer- und Winterzeit.</summary>
    public static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Kalendertag in deutscher Ortszeit, auf den ein UTC-Zeitpunkt fällt.</summary>
    /// <param name="zeit">Gespeicherter Zeitpunkt.</param>
    /// <returns>Das Datum in Ortszeit.</returns>
    public static DateOnly Datum(DateTimeOffset zeit) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit, Berlin).DateTime);

    /// <summary>Beginn des Tages (00:00 Ortszeit) als UTC-Zeitpunkt.</summary>
    /// <param name="datum">Kalendertag in Ortszeit.</param>
    /// <returns>Der UTC-Zeitpunkt von 00:00 Uhr Ortszeit an diesem Tag.</returns>
    public static DateTimeOffset TagesbeginnUtc(DateOnly datum)
    {
        var lokal = datum.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(lokal, Berlin.GetUtcOffset(lokal)).ToUniversalTime();
    }

    /// <summary>Anzahl der Kalendertage (Ortszeit), die der Zeitraum berührt; mindestens 1.</summary>
    /// <param name="von">Beginn des Zeitraums.</param>
    /// <param name="bis">Ende des Zeitraums; ein Ende um genau 00:00 Uhr zählt den neuen Tag nicht mit.</param>
    /// <returns>Die Zahl der berührten Kalendertage, mindestens 1.</returns>
    public static int Kalendertage(DateTimeOffset von, DateTimeOffset bis)
    {
        if (bis <= von)
        {
            return 1;
        }

        // Ein Ende um genau 00:00 zählt nicht als weiterer Tag.
        var letzter = Datum(bis.AddTicks(-1));
        return letzter.DayNumber - Datum(von).DayNumber + 1;
    }
}
