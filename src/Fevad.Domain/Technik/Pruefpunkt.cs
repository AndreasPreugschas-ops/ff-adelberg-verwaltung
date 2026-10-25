using System.Text.Json;

namespace Fevad.Domain.Technik;

/// <summary>Art eines Prüfpunkts.</summary>
public enum PruefpunktTyp
{
    /// <summary>Ja/Nein-Frage.</summary>
    JaNein,
    /// <summary>Messwert mit Einheit.</summary>
    Messwert,
    /// <summary>Freitext.</summary>
    Text,
}

/// <summary>Ein Punkt der Checkliste einer Prüfplanposition.</summary>
public sealed record Pruefpunkt(string Text, PruefpunktTyp Typ = PruefpunktTyp.JaNein, string? Einheit = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Liest gespeicherte Prüfpunkte.</summary>
    /// <param name="json">JSON aus der Datenbank; leer ergibt eine leere Liste.</param>
    /// <returns>Die Prüfpunkte.</returns>
    public static IReadOnlyList<Pruefpunkt> AusJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Pruefpunkt>>(json, Json) ?? [];

    /// <summary>Speichert Prüfpunkte als JSON.</summary>
    /// <param name="punkte">Die Prüfpunkte.</param>
    /// <returns>Das JSON.</returns>
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

    /// <summary>Schreibt die Checkliste für das Textfeld der Oberfläche, Gegenstück zu <see cref="AusZeilen"/>.</summary>
    /// <param name="punkte">Die Prüfpunkte.</param>
    /// <returns>Ein Prüfpunkt je Zeile.</returns>
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

    /// <summary>Liest gespeicherte Ergebnisse.</summary>
    /// <param name="json">JSON aus der Datenbank; leer ergibt eine leere Liste.</param>
    /// <returns>Die Ergebnisse.</returns>
    public static IReadOnlyList<PruefpunktErgebnis> AusJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<PruefpunktErgebnis>>(json, Json) ?? [];

    /// <summary>Speichert Ergebnisse als JSON.</summary>
    /// <param name="ergebnisse">Die Ergebnisse.</param>
    /// <returns>Das JSON.</returns>
    public static string AlsJson(IEnumerable<PruefpunktErgebnis> ergebnisse) => JsonSerializer.Serialize(ergebnisse, Json);
}
