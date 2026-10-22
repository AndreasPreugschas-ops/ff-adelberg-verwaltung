namespace Fevad.Domain.Gemeinsam;

/// <summary>Umrechnung zwischen gespeicherter UTC-Zeit und deutscher Ortszeit für fachliche Stichtage.</summary>
public static class Ortszeit
{
    public static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static DateOnly Datum(DateTimeOffset zeit) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit, Berlin).DateTime);

    /// <summary>Beginn des Tages (00:00 Ortszeit) als UTC-Zeitpunkt.</summary>
    public static DateTimeOffset TagesbeginnUtc(DateOnly datum)
    {
        var lokal = datum.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(lokal, Berlin.GetUtcOffset(lokal)).ToUniversalTime();
    }

    /// <summary>Anzahl der Kalendertage (Ortszeit), die der Zeitraum berührt; mindestens 1.</summary>
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
