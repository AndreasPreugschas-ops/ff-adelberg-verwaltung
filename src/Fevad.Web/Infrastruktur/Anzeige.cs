using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Fevad.Web.Infrastruktur;

/// <summary>Einheitliche Anzeige von Zeiten in deutscher Ortszeit, 24-Stunden-Format.</summary>
public static class Anzeige
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Rechnet einen Zeitpunkt in Ortszeit Europe/Berlin um.</summary>
    /// <param name="zeit">Zeitpunkt.</param>
    /// <returns>Derselbe Zeitpunkt mit deutschem Versatz.</returns>
    public static DateTimeOffset Lokal(DateTimeOffset zeit) => TimeZoneInfo.ConvertTime(zeit, Berlin);

    /// <summary>„TT.MM.JJJJ HH:MM“ in Ortszeit oder „–“.</summary>
    /// <param name="zeit">Zeitpunkt.</param>
    /// <returns>Der Text.</returns>
    public static string DatumZeit(DateTimeOffset? zeit) =>
        zeit is null ? "–" : Lokal(zeit.Value).ToString("dd.MM.yyyy HH:mm", Deutsch);

    /// <summary>„HH:MM“ in Ortszeit oder „–“.</summary>
    /// <param name="zeit">Zeitpunkt.</param>
    /// <returns>Der Text.</returns>
    public static string Zeit(DateTimeOffset? zeit) =>
        zeit is null ? "–" : Lokal(zeit.Value).ToString("HH:mm", Deutsch);

    /// <summary>„TT.MM.JJJJ“ oder „–“.</summary>
    /// <param name="datum">Datum.</param>
    /// <returns>Der Text.</returns>
    public static string Datum(DateOnly? datum) =>
        datum is null ? "–" : datum.Value.ToString("dd.MM.yyyy", Deutsch);

    /// <summary>"03.11.2026–07.11.2026", nur ein Tag oder "–".</summary>
    public static string Zeitraum(DateOnly? von, DateOnly? bis) =>
        (von, bis) switch
        {
            (null, null) => "–",
            ({ } v, { } b) when v != b => $"{Datum(v)}–{Datum(b)}",
            _ => Datum(von ?? bis),
        };

    /// <summary>Betrag mit zwei Nachkommastellen und Eurozeichen, z. B. „1.234,50 €“.</summary>
    /// <param name="betrag">Betrag in Euro.</param>
    /// <returns>Der Text.</returns>
    public static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    /// <summary>Zahl im deutschen Format.</summary>
    /// <param name="wert">Der Wert.</param>
    /// <param name="stellen">Nachkommastellen.</param>
    /// <returns>Der Text.</returns>
    public static string Zahl(decimal wert, int stellen = 2) => wert.ToString("N" + stellen, Deutsch);

    /// <summary>Heutiges Datum in Ortszeit.</summary>
    /// <param name="zeit">Zeitgeber.</param>
    /// <returns>Das Datum.</returns>
    public static DateOnly Heute(TimeProvider zeit) => LokalesDatum(zeit.GetUtcNow());

    /// <summary>Kalendertag eines Zeitpunkts in Ortszeit.</summary>
    /// <param name="zeit">Zeitpunkt.</param>
    /// <returns>Das Datum.</returns>
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
