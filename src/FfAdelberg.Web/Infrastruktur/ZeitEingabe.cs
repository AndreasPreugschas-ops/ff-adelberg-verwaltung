using System.Globalization;

namespace FfAdelberg.Web.Infrastruktur;

/// <summary>
/// Zeiteingabe als Text im 24-Stunden-Format, unabhängig von der Spracheinstellung des Browsers
/// (datetime-local zeigt je nach Gerät AM/PM). Erlaubt "22.10.2026 17:45" oder nur "17:45";
/// eine reine Uhrzeit gilt am Tag des Bezugs und rutscht auf den Folgetag, wenn sie davor läge.
/// </summary>
public static class ZeitEingabe
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private static readonly string[] DatumZeitFormate = ["d.M.yyyy H:mm", "d.M.yy H:mm", "d.M.yyyy HH:mm", "d.M.yyyy H.mm", "d.M.yyyy HHmm"];
    private static readonly string[] ZeitFormate = ["H:mm", "H.mm", "HHmm"];

    public static string Text(DateTimeOffset? zeit) =>
        zeit is null ? "" : Anzeige.Lokal(zeit.Value).ToString("dd.MM.yyyy HH:mm", Deutsch);

    /// <summary>Leerer Text ergibt null. Gibt false zurück, wenn der Text keine Zeit ist.</summary>
    public static bool TryLesen(string? text, DateTimeOffset? bezug, out DateTimeOffset? wert)
    {
        wert = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var eingabe = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (DateTime.TryParseExact(eingabe, DatumZeitFormate, Deutsch, DateTimeStyles.None, out var datumZeit))
        {
            wert = Anzeige.AlsUtc(DateOnly.FromDateTime(datumZeit), TimeOnly.FromDateTime(datumZeit));
            return true;
        }

        if (bezug is not null && DateTime.TryParseExact(eingabe, ZeitFormate, Deutsch, DateTimeStyles.None, out var uhrzeit))
        {
            var tag = Anzeige.LokalesDatum(bezug.Value);
            var zeit = Anzeige.AlsUtc(tag, TimeOnly.FromDateTime(uhrzeit));
            // Auf die Minute genau vergleichen: FE2 liefert Sekunden, die Eingabe nicht.
            var bezugMinute = bezug.Value.AddTicks(-(bezug.Value.Ticks % TimeSpan.TicksPerMinute));
            wert = zeit < bezugMinute ?Anzeige.AlsUtc(tag.AddDays(1), TimeOnly.FromDateTime(uhrzeit)) : zeit;
            return true;
        }

        return false;
    }
}
