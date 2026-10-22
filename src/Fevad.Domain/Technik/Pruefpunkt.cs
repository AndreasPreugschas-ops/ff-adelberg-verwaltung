using System.Text.Json;

namespace Fevad.Domain.Technik;

public enum PruefpunktTyp
{
    JaNein,
    Messwert,
    Text,
}

/// <summary>Ein Punkt der Checkliste einer Prüfplanposition.</summary>
public sealed record Pruefpunkt(string Text, PruefpunktTyp Typ = PruefpunktTyp.JaNein, string? Einheit = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<Pruefpunkt> AusJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Pruefpunkt>>(json, Json) ?? [];

    public static string AlsJson(IEnumerable<Pruefpunkt> punkte) => JsonSerializer.Serialize(punkte, Json);

    /// <summary>
    /// Liest die Checkliste aus dem Textfeld der Oberfläche: ein Prüfpunkt je Zeile.
    /// „Prüfdruck [bar]“ wird ein Messwert mit Einheit, „Bemerkung [Text]“ ein Freitext, alles andere Ja/Nein.
    /// </summary>
    public static IReadOnlyList<Pruefpunkt> AusZeilen(string? zeilen) =>
        (zeilen ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ZeileLesen)
            .ToList();

    public static string AlsZeilen(IEnumerable<Pruefpunkt> punkte) =>
        string.Join('\n', punkte.Select(p => p.Typ switch
        {
            PruefpunktTyp.Messwert => $"{p.Text} [{p.Einheit}]",
            PruefpunktTyp.Text => $"{p.Text} [Text]",
            _ => p.Text,
        }));

    private static Pruefpunkt ZeileLesen(string zeile)
    {
        var start = zeile.LastIndexOf('[');
        if (!zeile.EndsWith(']') || start <= 0)
        {
            return new Pruefpunkt(zeile);
        }

        var text = zeile[..start].TrimEnd();
        var zusatz = zeile[(start + 1)..^1].Trim();
        return zusatz.Equals("Text", StringComparison.OrdinalIgnoreCase)
            ? new Pruefpunkt(text, PruefpunktTyp.Text)
            : new Pruefpunkt(text, PruefpunktTyp.Messwert, zusatz);
    }
}

/// <summary>Ausgefüllter Prüfpunkt im Protokoll. <see cref="InOrdnung"/> ist bei Messwert und Text optional.</summary>
public sealed record PruefpunktErgebnis(string Text, PruefpunktTyp Typ, string? Einheit, bool? InOrdnung, string? Wert)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<PruefpunktErgebnis> AusJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<PruefpunktErgebnis>>(json, Json) ?? [];

    public static string AlsJson(IEnumerable<PruefpunktErgebnis> ergebnisse) => JsonSerializer.Serialize(ergebnisse, Json);
}
