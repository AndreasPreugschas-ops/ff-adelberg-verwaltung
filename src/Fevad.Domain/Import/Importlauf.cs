using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Import;

/// <summary>Was ein Altdatenimport anlegt (V68).</summary>
public enum Importziel
{
    /// <summary>Personen mit Anschrift, Kontakt, Abteilung, Eintritt, Austritt und Dienstgrad.</summary>
    [Display(Name = "Personen")] Personen,
    /// <summary>Lehrgänge, Qualifikationen und Fahrerlaubnisse je Person.</summary>
    [Display(Name = "Lehrgänge und Qualifikationen")] Qualifikationen,
    /// <summary>Geräte mit Inventarnummer, Materialstamm, Standort und letzter Prüfung.</summary>
    [Display(Name = "Geräte")] Geraete,
    /// <summary>Atemschutzfristen je Person: Vorsorge gültig bis, letzte Belastungsübung, Einsatzübung und Unterweisung; keine Befunde.</summary>
    [Display(Name = "Atemschutzfristen")] Atemschutz,
    /// <summary>Einsätze mit Nummer, Alarmzeit, Stichwort und Einsatzort; Berichte bleiben in Bearbeitung.</summary>
    [Display(Name = "Einsätze")] Einsaetze,
    /// <summary>Teilnahme von Personen an schon vorhandenen Einsätzen.</summary>
    [Display(Name = "Einsatzteilnahmen")] Einsatzteilnahmen,
}

/// <summary>Stand eines Importlaufs.</summary>
public enum ImportlaufStatus
{
    /// <summary>Datei hochgeladen, Zuordnung und Probelauf möglich.</summary>
    [Display(Name = "vorbereitet")] Vorbereitet,
    /// <summary>Übernommen; die Datei ist gelöscht, das Protokoll bleibt.</summary>
    [Display(Name = "übernommen")] Uebernommen,
    /// <summary>Verworfen; die Datei ist gelöscht.</summary>
    [Display(Name = "verworfen")] Verworfen,
}

/// <summary>
/// Ein Altdatenimport aus einer CSV- oder XLSX-Datei, etwa einem Export aus MP-Feuer (V68): Datei, Feldzuordnung, Ergebnis des
/// letzten Probelaufs und nach der Übernahme das Protokoll. Die Datei selbst wird mit der Übernahme oder dem Verwerfen gelöscht;
/// liegengebliebene Läufe löscht der Löschlauf.
/// </summary>
public class Importlauf : Entitaet
{
    /// <summary>Was importiert wird.</summary>
    public Importziel Ziel { get; set; }
    /// <summary>Stand des Laufs.</summary>
    public ImportlaufStatus Status { get; set; }
    /// <summary>Name der hochgeladenen Datei.</summary>
    public required string Dateiname { get; set; }
    /// <summary>Inhalt der Datei; leer nach Übernahme oder Verwerfen. Nicht im Protokoll.</summary>
    [NichtProtokollieren]
    public byte[] Inhalt { get; set; } = [];

    /// <summary>Feldzuordnung als JSON: Zielfeld → Spaltenüberschrift der Datei.</summary>
    public string Zuordnung { get; set; } = "{}";

    /// <summary>Datenzeilen der Datei ohne Überschrift.</summary>
    public int Zeilen { get; set; }
    /// <summary>Neu angelegte Datensätze (beim Probelauf: würden angelegt).</summary>
    public int Angelegt { get; set; }
    /// <summary>Ergänzte oder geänderte Datensätze.</summary>
    public int Aktualisiert { get; set; }
    /// <summary>Zeilen, die wegen eines Fehlers nicht übernommen wurden.</summary>
    public int Fehlerhaft { get; set; }
    /// <summary>Meldungen je Zeile als JSON-Liste; ohne Gesundheitsangaben. Nicht im Protokoll.</summary>
    [NichtProtokollieren]
    public string Meldungen { get; set; } = "[]";
    /// <summary>Zeitpunkt des letzten Probelaufs in UTC.</summary>
    public DateTimeOffset? ProbelaufAm { get; set; }

    /// <summary>Vermerk zur Freigabe der Übernahme durch Bürgermeister und Datenschutzbeauftragten (Datenschutz-Unterlage 00).</summary>
    public string? Freigabe { get; set; }
    /// <summary>Zeitpunkt der Übernahme in UTC.</summary>
    public DateTimeOffset? UebernommenAm { get; set; }
    /// <summary>Wer übernommen hat.</summary>
    public string? UebernommenVon { get; set; }
}

/// <summary>Gespeicherte Feldzuordnung, damit derselbe Export beim nächsten Mal ohne neue Zuordnung eingelesen werden kann.</summary>
public class Importvorlage : Entitaet
{
    /// <summary>Name der Vorlage, z. B. „MP-Feuer Personen“.</summary>
    public required string Name { get; set; }
    /// <summary>Wofür die Vorlage gilt.</summary>
    public Importziel Ziel { get; set; }
    /// <summary>Feldzuordnung als JSON: Zielfeld → Spaltenüberschrift.</summary>
    public string Zuordnung { get; set; } = "{}";
}

/// <summary>Liest Werte aus Altdaten in deutschen und technischen Schreibweisen.</summary>
public static class Altwert
{
    private static readonly string[] Datumsformate = ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "yyyyMMdd"];

    /// <summary>Datum aus „TT.MM.JJJJ“, ISO oder einer Excel-Seriennummer.</summary>
    /// <param name="text">Der Wert der Zelle.</param>
    /// <param name="datum">Das Datum.</param>
    /// <returns><c>true</c>, wenn der Text ein Datum ist.</returns>
    public static bool Datum(string? text, out DateOnly datum)
    {
        datum = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim();
        if (DateTime.TryParseExact(t, Datumsformate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            datum = DateOnly.FromDateTime(d);
            return datum.Year >= 1900;
        }

        // Excel speichert Datumswerte als Tage seit dem 30.12.1899.
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var seriell) && seriell is > 1 and < 80000)
        {
            datum = DateOnly.FromDateTime(DateTime.FromOADate(seriell));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Zeitpunkt aus Datum und Uhrzeit in Ortszeit (Europe/Berlin), als UTC. Das Datum darf die Uhrzeit schon enthalten
    /// („24.10.2026 18:30“, ISO oder Excel-Seriennummer mit Bruchteil); eine eigene Uhrzeit „HH:MM“ oder als Excel-Bruchteil geht vor.
    /// </summary>
    /// <param name="datum">Wert der Datumszelle.</param>
    /// <param name="uhrzeit">Wert der Uhrzeitzelle oder leer.</param>
    /// <param name="zeitpunkt">Der Zeitpunkt in UTC.</param>
    /// <returns><c>true</c>, wenn Datum und Uhrzeit lesbar sind.</returns>
    public static bool Zeitpunkt(string? datum, string? uhrzeit, out DateTimeOffset zeitpunkt)
    {
        zeitpunkt = default;
        if (string.IsNullOrWhiteSpace(datum))
        {
            return false;
        }

        var t = datum.Trim();
        DateTime lokal;
        if (DateTime.TryParseExact(t, ["dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy H:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var mitZeit))
        {
            lokal = mitZeit;
        }
        else if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var seriell) && seriell is > 1 and < 80000)
        {
            lokal = DateTime.FromOADate(seriell);
        }
        else if (Datum(t, out var tag))
        {
            lokal = tag.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(uhrzeit))
        {
            var u = uhrzeit.Trim();
            if (TimeOnly.TryParseExact(u, ["HH:mm", "H:mm", "HH:mm:ss", "HHmm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var zeit))
            {
                lokal = lokal.Date + zeit.ToTimeSpan();
            }
            else if (double.TryParse(u, NumberStyles.Float, CultureInfo.InvariantCulture, out var bruch) && bruch is >= 0 and < 1)
            {
                lokal = lokal.Date + TimeSpan.FromMinutes(Math.Round(bruch * 24 * 60));
            }
            else
            {
                return false;
            }
        }

        lokal = DateTime.SpecifyKind(lokal, DateTimeKind.Unspecified);
        if (Ortszeit.Berlin.IsInvalidTime(lokal))
        {
            // Uhrzeit in der Lücke der Sommerzeitumstellung: gemeint ist eine Stunde später.
            lokal = lokal.AddHours(1);
        }

        zeitpunkt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(lokal, Ortszeit.Berlin), TimeSpan.Zero);
        return lokal.Year >= 1900;
    }

    /// <summary>Betrag aus „1.234,56“, „1234.56“ oder „1234,56 €“.</summary>
    /// <param name="text">Der Wert der Zelle.</param>
    /// <param name="betrag">Der Betrag.</param>
    /// <returns><c>true</c>, wenn der Text ein Betrag ist.</returns>
    public static bool Betrag(string? text, out decimal betrag)
    {
        betrag = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Replace("€", "").Replace("EUR", "").Trim();
        // Komma als Dezimaltrenner, wenn es nach dem letzten Punkt steht (deutsch), sonst gilt der Punkt.
        var deutsch = t.LastIndexOf(',') > t.LastIndexOf('.');
        return decimal.TryParse(t, NumberStyles.Number, deutsch ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture, out betrag);
    }

    /// <summary>Ja/Nein aus „ja“, „x“, „1“, „wahr“, „true“ bzw. „nein“, „0“, „falsch“, „false“ oder leer.</summary>
    /// <param name="text">Der Wert der Zelle.</param>
    /// <param name="wert">Der Wahrheitswert; leer ist „nein“.</param>
    /// <returns><c>true</c>, wenn der Text als Ja oder Nein lesbar ist.</returns>
    public static bool JaNein(string? text, out bool wert)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "ja" or "j" or "x" or "1" or "wahr" or "true" or "yes" or "y":
                wert = true;
                return true;
            case null or "" or "nein" or "n" or "0" or "falsch" or "false" or "no" or "-":
                wert = false;
                return true;
            default:
                wert = false;
                return false;
        }
    }

    /// <summary>Vergleichsform für Namen aus Katalogen: Kleinbuchstaben, Umlaute ausgeschrieben, nur Buchstaben und Ziffern.</summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Die Vergleichsform.</returns>
    public static string Schluessel(string? text)
    {
        var t = (text ?? "").Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        return new string(t.Where(char.IsLetterOrDigit).ToArray());
    }
}
