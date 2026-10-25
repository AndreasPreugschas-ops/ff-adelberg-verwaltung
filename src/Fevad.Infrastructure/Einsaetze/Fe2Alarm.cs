using System.Globalization;
using System.Text.Json;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Gelesene Felder eines alamos-FE2-Alarms (Datenformat der externen Schnittstelle v2).
/// Das Ausgangsformat steht erst an der echten FE2-Instanz fest, deshalb liest der Parser
/// tolerant: Felder unter <c>data</c> oder auf oberster Ebene, fehlende Felder bleiben leer.
/// </summary>
public sealed record Fe2Alarm
{
    /// <summary>Art der Meldung, z. B. ALARM, STATUS oder CLOSE.</summary>
    public string? Typ { get; init; }
    /// <summary>Kennung des Alarms in FE2.</summary>
    public string? ExterneId { get; init; }
    /// <summary>Zeitpunkt der Alarmierung.</summary>
    public DateTimeOffset? Zeitpunkt { get; init; }
    /// <summary>Einsatzstichwort.</summary>
    public string? Stichwort { get; init; }
    /// <summary>Beschreibung des Stichworts.</summary>
    public string? StichwortBeschreibung { get; init; }
    /// <summary>Kategorie des Stichworts.</summary>
    public string? StichwortKategorie { get; init; }
    /// <summary>Zusatz zum Stichwort.</summary>
    public string? StichwortZusatz { get; init; }
    /// <summary>Zeilen der Alarmmeldung.</summary>
    public IReadOnlyList<string> Meldung { get; init; } = [];
    /// <summary>Straße des Einsatzorts.</summary>
    public string? Strasse { get; init; }
    /// <summary>Hausnummer des Einsatzorts.</summary>
    public string? Hausnummer { get; init; }
    /// <summary>Postleitzahl des Einsatzorts.</summary>
    public string? Plz { get; init; }
    /// <summary>Ort des Einsatzorts.</summary>
    public string? Ort { get; init; }
    /// <summary>Ortsteil des Einsatzorts.</summary>
    public string? Ortsteil { get; init; }
    /// <summary>Objekt am Einsatzort.</summary>
    public string? Objekt { get; init; }
    /// <summary>Geografische Breite.</summary>
    public double? Latitude { get; init; }
    /// <summary>Geografische Länge.</summary>
    public double? Longitude { get; init; }
    /// <summary>Alarmierte Fahrzeuge bzw. Einheiten.</summary>
    public IReadOnlyList<string> Fahrzeuge { get; init; } = [];

    /// <summary>Nur echte Alarme werden zu Einsatzberichten; STATUS, CLOSE usw. nicht. Ohne Typ gilt der Alarm als ALARM.</summary>
    public bool IstAlarm => Typ is null || Typ.Equals("ALARM", StringComparison.OrdinalIgnoreCase);

    /// <exception cref="JsonException">Kein gültiges JSON.</exception>
    public static Fe2Alarm Lesen(string json)
    {
        using var dokument = JsonDocument.Parse(json);
        var wurzel = dokument.RootElement;
        if (wurzel.ValueKind != JsonValueKind.Object)
        {
            return new Fe2Alarm();
        }

        var data = wurzel.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : wurzel;
        var ort = data.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.Object ? l : data;
        var (lat, lng) = Koordinaten(ort);

        return new Fe2Alarm
        {
            Typ = Text(wurzel, "type"),
            ExterneId = Text(data, "externalId") ?? Text(wurzel, "externalId"),
            Zeitpunkt = Zeit(wurzel, "timestamp") ?? Zeit(data, "timestamp"),
            Stichwort = Text(data, "keyword"),
            StichwortBeschreibung = Text(data, "keyword_description"),
            StichwortKategorie = Text(data, "keyword_category"),
            StichwortZusatz = Text(data, "keyword_misc"),
            Meldung = Texte(data, "message"),
            Strasse = Text(ort, "street"),
            Hausnummer = Text(ort, "house"),
            Plz = Text(ort, "postalCode"),
            Ort = Text(ort, "city"),
            Ortsteil = Text(ort, "district") ?? Text(ort, "city_abbr"),
            Objekt = Text(ort, "building"),
            Latitude = lat,
            Longitude = lng,
            Fahrzeuge = FahrzeugNamen(data),
        };
    }

    private static string? Text(JsonElement objekt, string name)
    {
        if (!objekt.TryGetProperty(name, out var wert))
        {
            return null;
        }

        var text = wert.ValueKind switch
        {
            JsonValueKind.String => wert.GetString(),
            JsonValueKind.Number => wert.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>message kann eine Liste von Zeilen oder ein einzelner Text sein.</summary>
    private static IReadOnlyList<string> Texte(JsonElement objekt, string name)
    {
        if (!objekt.TryGetProperty(name, out var wert))
        {
            return [];
        }

        if (wert.ValueKind == JsonValueKind.Array)
        {
            return wert.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        return Text(objekt, name) is { } einzeln ? [einzeln] : [];
    }

    /// <summary>Millisekunden seit 1970 (FE2-Standard) oder ISO-8601-Text.</summary>
    private static DateTimeOffset? Zeit(JsonElement objekt, string name)
    {
        if (!objekt.TryGetProperty(name, out var wert))
        {
            return null;
        }

        if (wert.ValueKind == JsonValueKind.Number && wert.TryGetInt64(out var ms))
        {
            return ms is > 0 and < 253402300799999 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
        }

        return wert.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(wert.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var zeit)
                ? zeit.ToUniversalTime()
                : null;
    }

    /// <summary>coordinate ist [lng, lat] (GeoJSON-Reihenfolge); ersatzweise lat/lng als eigene Felder.</summary>
    private static (double? Lat, double? Lng) Koordinaten(JsonElement ort)
    {
        if (ort.TryGetProperty("coordinate", out var c)
            && c.ValueKind == JsonValueKind.Array
            && c.GetArrayLength() >= 2
            && Zahl(c[0]) is { } lng
            && Zahl(c[1]) is { } lat)
        {
            return Gueltig(lat, lng);
        }

        return ort.TryGetProperty("lat", out var la) && Zahl(la) is { } lat2
            && ort.TryGetProperty("lng", out var ln) && Zahl(ln) is { } lng2
                ? Gueltig(lat2, lng2)
                : (null, null);

        // TryGetDouble wirft bei null oder Text; Koordinaten sind optional und dürfen den Alarm nicht scheitern lassen.
        static double? Zahl(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d) ? d : null;

        static (double?, double?) Gueltig(double lat, double lng) =>
            lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && (lat != 0 || lng != 0) ? (lat, lng) : (null, null);
    }

    private static IReadOnlyList<string> FahrzeugNamen(JsonElement data)
    {
        if (!data.TryGetProperty("vehicles", out var fahrzeuge) || fahrzeuge.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return fahrzeuge.EnumerateArray()
            .Select(f => f.ValueKind == JsonValueKind.Object ? Text(f, "radioName") ?? Text(f, "name") : f.ValueKind == JsonValueKind.String ? f.GetString() : null)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
