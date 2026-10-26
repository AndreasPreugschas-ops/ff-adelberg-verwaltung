using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>Tageszeit eines Einsatzes für die Zeitkennzahlen; tagsüber an Werktagen ist die Tagesverfügbarkeit am knappsten.</summary>
public enum Tageszeit
{
    /// <summary>Montag bis Freitag, 06:00 bis 18:00 Uhr.</summary>
    [Display(Name = "werktags 06:00–18:00 Uhr")] WerktagsTag,
    /// <summary>Werktags abends und nachts sowie am Wochenende (Feiertage zählen wie Werktage).</summary>
    [Display(Name = "abends, nachts, Wochenende")] Uebrige,
}

/// <summary>Rechenregeln der Führungskennzahlen (V52): Median, Perzentil, Schutzziel, Tageszeit.</summary>
public static class Kennzahlen
{
    /// <summary>Perzentil nach der Nächster-Rang-Methode, z. B. 0,9 für das 90. Perzentil.</summary>
    /// <param name="werte">Messwerte.</param>
    /// <param name="anteil">Anteil zwischen 0 und 1.</param>
    /// <returns>Der Wert oder <c>null</c> ohne Messwerte.</returns>
    public static double? Perzentil(IEnumerable<double> werte, double anteil)
    {
        var sortiert = werte.OrderBy(w => w).ToList();
        if (sortiert.Count == 0)
        {
            return null;
        }

        var rang = (int)Math.Ceiling(Math.Clamp(anteil, 0, 1) * sortiert.Count);
        return sortiert[Math.Clamp(rang - 1, 0, sortiert.Count - 1)];
    }

    /// <summary>Median (bei gerader Anzahl Mittel der beiden mittleren Werte).</summary>
    /// <param name="werte">Messwerte.</param>
    /// <returns>Der Median oder <c>null</c> ohne Messwerte.</returns>
    public static double? Median(IEnumerable<double> werte)
    {
        var sortiert = werte.OrderBy(w => w).ToList();
        if (sortiert.Count == 0)
        {
            return null;
        }

        var mitte = sortiert.Count / 2;
        return sortiert.Count % 2 == 1 ? sortiert[mitte] : (sortiert[mitte - 1] + sortiert[mitte]) / 2;
    }

    /// <summary>Tageszeit der Alarmierung in Ortszeit.</summary>
    /// <param name="alarmiert">Alarmierung (UTC).</param>
    /// <returns>Die Tageszeit.</returns>
    public static Tageszeit TageszeitVon(DateTimeOffset alarmiert)
    {
        var ort = TimeZoneInfo.ConvertTime(alarmiert, Ortszeit.Berlin);
        return ort.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && ort.Hour is >= 6 and < 18 ? Tageszeit.WerktagsTag : Tageszeit.Uebrige;
    }

    /// <summary>
    /// Wie viele Funktionen bis zu einer Frist an der Einsatzstelle waren: Besatzung aller Fahrzeuge, die bis dahin eingetroffen sind.
    /// </summary>
    /// <param name="alarmiert">Alarmierung.</param>
    /// <param name="fahrzeuge">Eintreffzeit und Besatzung je Fahrzeug.</param>
    /// <param name="minuten">Frist in Minuten nach Alarmierung.</param>
    /// <returns>Anzahl der Funktionen.</returns>
    public static int FunktionenBis(DateTimeOffset alarmiert, IEnumerable<(DateTimeOffset? Eingetroffen, int Besatzung)> fahrzeuge, int minuten) =>
        fahrzeuge.Where(f => f.Eingetroffen is { } t && t <= alarmiert.AddMinutes(minuten)).Sum(f => f.Besatzung);

    /// <summary>Minuten zwischen zwei Zeitpunkten, nur wenn beide da sind und die Reihenfolge stimmt.</summary>
    /// <param name="von">Beginn.</param>
    /// <param name="bis">Ende.</param>
    /// <returns>Minuten oder <c>null</c>.</returns>
    public static double? Minuten(DateTimeOffset von, DateTimeOffset? bis) =>
        bis is { } b && b >= von ? (b - von).TotalMinutes : null;
}
