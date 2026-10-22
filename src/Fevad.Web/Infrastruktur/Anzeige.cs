using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Fevad.Web.Infrastruktur;

/// <summary>Einheitliche Anzeige von Zeiten in deutscher Ortszeit, 24-Stunden-Format.</summary>
public static class Anzeige
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly CultureInfo Deutsch = new("de-DE");

    public static DateTimeOffset Lokal(DateTimeOffset zeit) => TimeZoneInfo.ConvertTime(zeit, Berlin);

    public static string DatumZeit(DateTimeOffset? zeit) =>
        zeit is null ? "–" : Lokal(zeit.Value).ToString("dd.MM.yyyy HH:mm", Deutsch);

    public static string Zeit(DateTimeOffset? zeit) =>
        zeit is null ? "–" : Lokal(zeit.Value).ToString("HH:mm", Deutsch);

    public static string Datum(DateOnly? datum) =>
        datum is null ? "–" : datum.Value.ToString("dd.MM.yyyy", Deutsch);

    public static DateOnly Heute(TimeProvider zeit) => LokalesDatum(zeit.GetUtcNow());

    public static DateOnly LokalesDatum(DateTimeOffset zeit) => DateOnly.FromDateTime(Lokal(zeit).DateTime);

    /// <summary>Ortszeit (Europe/Berlin) als UTC-Zeitpunkt.</summary>
    public static DateTimeOffset AlsUtc(DateOnly datum, TimeOnly uhrzeit)
    {
        var lokal = datum.ToDateTime(uhrzeit);
        return new DateTimeOffset(lokal, Berlin.GetUtcOffset(lokal)).ToUniversalTime();
    }

    /// <summary>Kalenderjahr nach deutscher Ortszeit als halboffenes UTC-Intervall [von, bis).</summary>
    public static (DateTimeOffset Von, DateTimeOffset Bis) Jahr(int jahr) =>
        (AlsUtc(new DateOnly(jahr, 1, 1), TimeOnly.MinValue), AlsUtc(new DateOnly(jahr + 1, 1, 1), TimeOnly.MinValue));

    /// <summary>Anzeigetext eines Enum-Werts aus [Display(Name = ...)], sonst der Name.</summary>
    public static string Text(Enum? wert) =>
        wert is null
            ? "–"
            : wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
