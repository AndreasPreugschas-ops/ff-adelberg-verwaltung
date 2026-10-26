using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace Fevad.Domain.Gemeinsam;

/// <summary>Art eines Datenaustauschs mit der Kämmerei (V23).</summary>
public enum ExportArt
{
    /// <summary>Sollstellungen aus festgesetzten Kostenersatzbescheiden (Debitor, Kassenzeichen).</summary>
    [Display(Name = "Sollstellungen Kostenersatz")] KostenersatzSollstellung,
    /// <summary>Auszahlung der Entschädigung je Person einer abgeschlossenen Abrechnung (Personalnummer als Schlüssel, F4).</summary>
    [Display(Name = "Entschädigung")] Entschaedigung,
    /// <summary>Buchungen der Kameradschaftskassen mit Steuerbereich und Steuersatz.</summary>
    [Display(Name = "Kameradschaftskasse (Steuergrundlagen)")] Kameradschaftskasse,
    /// <summary>Geräte und Fahrzeuge mit Anlagendaten für die Anlagenbuchhaltung.</summary>
    [Display(Name = "Inventar (Anlagenbuchhaltung)")] Inventar,
    /// <summary>Rückmeldung der Gemeindekasse zu Kostenersatz-Forderungen (Import: bezahlt, offen, gemahnt).</summary>
    [Display(Name = "Rückmeldung Gemeindekasse (Import)")] Rueckmeldung,
}

/// <summary>Zeichenkodierung einer Exportdatei.</summary>
public enum Exportkodierung
{
    /// <summary>UTF-8 mit Byte-Order-Mark (Excel erkennt Umlaute).</summary>
    [Display(Name = "UTF-8 mit BOM")] Utf8Bom,
    /// <summary>UTF-8 ohne Byte-Order-Mark.</summary>
    [Display(Name = "UTF-8")] Utf8,
    /// <summary>Windows-1252 (ANSI), für ältere Importe.</summary>
    [Display(Name = "Windows-1252 (ANSI)")] Windows1252,
}

/// <summary>
/// Spaltenzuordnung einer Exportart (V23). Das Importformat der Kämmerei (SAP) ist noch abzustimmen, deshalb lassen sich Spalten,
/// Reihenfolge, Überschriften, Trennzeichen, Kodierung und Buchungsstellen einstellen statt einer festen Schnittstelle.
/// Spalten: eine Zeile je Spalte, „Feld“ oder „Überschrift=Feld“; ein Wert in Anführungszeichen wird als fester Text ausgegeben.
/// </summary>
public class Exportformat : Entitaet
{
    /// <summary>Die Exportart; je Art ein Format.</summary>
    public ExportArt Art { get; set; }

    /// <summary>Trennzeichen, z. B. „;“, „,“ oder „\t“ für Tabulator.</summary>
    public string Trennzeichen { get; set; } = ";";

    /// <summary>Zeichenkodierung.</summary>
    public Exportkodierung Kodierung { get; set; }

    /// <summary>Erste Zeile mit Überschriften.</summary>
    public bool Kopfzeile { get; set; } = true;

    /// <summary>Komma als Dezimaltrennzeichen (sonst Punkt).</summary>
    public bool DezimalKomma { get; set; } = true;

    /// <summary>Datumsformat, z. B. „dd.MM.yyyy“ oder „yyyyMMdd“.</summary>
    public string Datumsformat { get; set; } = "dd.MM.yyyy";

    /// <summary>Spaltenzuordnung, eine Zeile je Spalte.</summary>
    public string Spalten { get; set; } = "";

    /// <summary>Buchungsstelle, z. B. Produkt „12.60.01“; als Feld „Buchungsstelle“ verwendbar.</summary>
    public string? Buchungsstelle { get; set; }

    /// <summary>Sachkonto; als Feld „Sachkonto“ verwendbar.</summary>
    public string? Sachkonto { get; set; }

    /// <summary>Tatsächliches Trennzeichen („\t“ wird zum Tabulator).</summary>
    public string Trenner => Trennzeichen == "\\t" ? "\t" : Trennzeichen;
}

/// <summary>Felder je Exportart, Standardformate, Prüfung und Ausgabe der Datei (V23).</summary>
public static class Finanzexport
{
    /// <summary>Verfügbare Felder je Art mit Beschreibung.</summary>
    public static readonly IReadOnlyDictionary<ExportArt, (string Feld, string Beschreibung)[]> Felder = new Dictionary<ExportArt, (string, string)[]>
    {
        [ExportArt.KostenersatzSollstellung] =
        [
            ("Kassenzeichen", "Kassenzeichen des Bescheids"), ("Schuldner", "Name des Schuldners (Debitor)"), ("Anschrift", "Zustellanschrift"),
            ("Betrag", "festgesetzter Betrag"), ("FaelligAm", "Fälligkeit"), ("BekanntgegebenAm", "Tag der Bekanntgabe"),
            ("Einsatznummer", "Einsatznummer"), ("Einsatzdatum", "Tag des Einsatzes"), ("Tatbestand", "Tatbestand nach § 34 FwG"),
            ("Buchungsstelle", "Buchungsstelle aus dem Format"), ("Sachkonto", "Sachkonto aus dem Format"), ("Text", "Buchungstext"),
        ],
        [ExportArt.Entschaedigung] =
        [
            ("Personalnummer", "Personalnummer (Schlüssel für SAP, F4)"), ("Nachname", "Nachname"), ("Vorname", "Vorname"),
            ("Stunden", "abgerechnete Stunden"), ("Betrag", "Auszahlungsbetrag"), ("Von", "Beginn des Abrechnungszeitraums"),
            ("Bis", "Ende des Abrechnungszeitraums"), ("Abrechnung", "Bezeichnung der Abrechnung"),
            ("Buchungsstelle", "Buchungsstelle aus dem Format"), ("Sachkonto", "Sachkonto aus dem Format"), ("Text", "Buchungstext"),
        ],
        [ExportArt.Kameradschaftskasse] =
        [
            ("Kasse", "Name der Kasse"), ("Jahr", "Kassenjahr"), ("Belegnummer", "Belegnummer"), ("Datum", "Buchungstag"),
            ("Art", "Einnahme, Ausgabe oder Umbuchung"), ("Betrag", "Betrag brutto"), ("Steuerbereich", "steuerlicher Bereich"),
            ("Steuersatz", "Umsatzsteuersatz"), ("Partner", "Zahlungspartner"), ("Text", "Buchungstext"),
            ("Buchungsstelle", "Buchungsstelle aus dem Format"), ("Sachkonto", "Sachkonto aus dem Format"),
        ],
        [ExportArt.Inventar] =
        [
            ("Inventarnummer", "Inventarnummer bzw. Funkrufname"), ("Anlagennummer", "Anlagennummer (SAP)"), ("Bezeichnung", "Bezeichnung"),
            ("Typ", "Gerät oder Fahrzeug"), ("BeschafftAm", "Beschaffung bzw. Indienststellung"), ("Anschaffungswert", "Anschaffungswert brutto"),
            ("Foerdermittel", "erhaltene Fördermittel"), ("NutzungsdauerJahre", "Nutzungsdauer laut Anlagenbuchhaltung"),
            ("Eigentuemer", "Eigentümer"), ("AusgesondertAm", "Tag der Aussonderung"),
            ("Buchungsstelle", "Buchungsstelle aus dem Format"), ("Sachkonto", "Sachkonto aus dem Format"),
        ],
        [ExportArt.Rueckmeldung] =
        [
            ("Kassenzeichen", "Kassenzeichen des Bescheids"), ("Status", "bezahlt, offen oder gemahnt"),
            ("Bezahlt", "insgesamt bezahlter Betrag"), ("Datum", "Tag der Rückmeldung bzw. der letzten Zahlung"),
        ],
    };

    /// <summary>Spalten des Standardformats je Art.</summary>
    /// <param name="art">Die Art.</param>
    /// <returns>Spaltenzuordnung, eine Zeile je Spalte.</returns>
    public static string StandardSpalten(ExportArt art) => art switch
    {
        ExportArt.KostenersatzSollstellung => "Kassenzeichen\nSchuldner\nAnschrift\nBetrag\nFaelligAm\nBuchungsstelle\nSachkonto\nText",
        ExportArt.Entschaedigung => "Personalnummer\nNachname\nVorname\nBetrag\nBuchungsstelle\nSachkonto\nText",
        ExportArt.Kameradschaftskasse => "Kasse\nBelegnummer\nDatum\nArt\nBetrag\nSteuerbereich\nSteuersatz\nText",
        ExportArt.Inventar => "Inventarnummer\nAnlagennummer\nBezeichnung\nTyp\nBeschafftAm\nAnschaffungswert\nFoerdermittel\nNutzungsdauerJahre",
        _ => "Kassenzeichen\nStatus\nBezahlt\nDatum",
    };

    /// <summary>Standardformat einer Art, solange keins eingerichtet ist.</summary>
    /// <param name="art">Die Art.</param>
    /// <returns>Ein nicht gespeichertes Format.</returns>
    public static Exportformat Standard(ExportArt art) => new() { Art = art, Spalten = StandardSpalten(art) };

    /// <summary>Eine Spalte der Zuordnung.</summary>
    /// <param name="Ueberschrift">Überschrift in der Kopfzeile.</param>
    /// <param name="Feld">Feldname oder null bei festem Text.</param>
    /// <param name="Text">Fester Text.</param>
    public sealed record Spalte(string Ueberschrift, string? Feld, string? Text);

    /// <summary>Liest die Spaltenzuordnung und prüft die Feldnamen.</summary>
    /// <param name="format">Das Format.</param>
    /// <param name="fehler">Fehlermeldung oder null.</param>
    /// <returns>Die Spalten.</returns>
    public static IReadOnlyList<Spalte> SpaltenLesen(Exportformat format, out string? fehler)
    {
        fehler = null;
        var bekannt = Felder[format.Art].Select(f => f.Feld).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var spalten = new List<Spalte>();
        foreach (var roh in format.Spalten.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var teile = roh.Split('=', 2, StringSplitOptions.TrimEntries);
            var (kopf, wert) = teile.Length == 2 ? (teile[0], teile[1]) : (roh, roh);
            if (wert.Length >= 2 && wert.StartsWith('"') && wert.EndsWith('"'))
            {
                spalten.Add(new Spalte(teile.Length == 2 ? kopf : "", null, wert[1..^1]));
            }
            else if (bekannt.TryGetValue(wert, out var feld))
            {
                spalten.Add(new Spalte(kopf == wert ? feld : kopf, feld, null));
            }
            else
            {
                fehler ??= $"Unbekanntes Feld „{wert}“. Erlaubt: {string.Join(", ", bekannt.Order())}.";
            }
        }

        if (spalten.Count == 0)
        {
            fehler ??= "Mindestens eine Spalte angeben.";
        }

        return spalten;
    }

    /// <summary>Erzeugt die Datei.</summary>
    /// <param name="format">Das Format.</param>
    /// <param name="zeilen">Zeilen als Feld-Wert-Paare; Werte vom Typ decimal, int, DateOnly oder string.</param>
    /// <returns>Die Datei als Bytes in der eingestellten Kodierung.</returns>
    public static byte[] Erzeugen(Exportformat format, IEnumerable<IReadOnlyDictionary<string, object?>> zeilen)
    {
        var spalten = SpaltenLesen(format, out var fehler);
        if (fehler is not null)
        {
            throw new InvalidOperationException(fehler);
        }

        var text = new StringBuilder();
        if (format.Kopfzeile)
        {
            text.Append(string.Join(format.Trenner, spalten.Select(s => Feld(s.Ueberschrift, format.Trenner)))).Append("\r\n");
        }

        foreach (var zeile in zeilen)
        {
            text.Append(string.Join(format.Trenner, spalten.Select(s => Feld(Wert(format, s, zeile), format.Trenner)))).Append("\r\n");
        }

        return format.Kodierung switch
        {
            Exportkodierung.Utf8 => new UTF8Encoding(false).GetBytes(text.ToString()),
            Exportkodierung.Windows1252 => CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(text.ToString()),
            _ => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())],
        };
    }

    private static string Wert(Exportformat format, Spalte spalte, IReadOnlyDictionary<string, object?> zeile)
    {
        if (spalte.Text is { } fest)
        {
            return fest;
        }

        object? wert = spalte.Feld switch
        {
            "Buchungsstelle" => format.Buchungsstelle,
            "Sachkonto" => format.Sachkonto,
            _ => zeile.GetValueOrDefault(spalte.Feld!),
        };
        var kultur = format.DezimalKomma ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
        return wert switch
        {
            null => "",
            decimal d => d.ToString("0.00", kultur),
            DateOnly t => t.ToString(format.Datumsformat, CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, kultur),
            _ => wert.ToString() ?? "",
        };
    }

    // Felder mit Trennzeichen, Anführungszeichen oder Zeilenumbruch in Anführungszeichen; führende Formelzeichen entschärfen.
    private static string Feld(string wert, string trenner)
    {
        if (wert.Length > 0 && "=+-@".Contains(wert[0]) && !decimal.TryParse(wert, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out _))
        {
            wert = "'" + wert;
        }

        return wert.Contains(trenner) || wert.Contains('"') || wert.Contains('\n') || wert.Contains('\r')
            ? "\"" + wert.Replace("\"", "\"\"") + "\""
            : wert;
    }

    /// <summary>Eine Zeile der Rückmeldung der Gemeindekasse.</summary>
    /// <param name="Kassenzeichen">Kassenzeichen.</param>
    /// <param name="Status">Status in Kleinbuchstaben.</param>
    /// <param name="Bezahlt">Insgesamt bezahlter Betrag oder null.</param>
    /// <param name="Datum">Tag oder null.</param>
    public sealed record Rueckmeldung(string Kassenzeichen, string Status, decimal? Bezahlt, DateOnly? Datum);

    /// <summary>Liest eine Rückmeldungsdatei nach dem eingestellten Format; die Kopfzeile wird übersprungen, wenn eingestellt.</summary>
    /// <param name="format">Format der Art <see cref="ExportArt.Rueckmeldung"/>.</param>
    /// <param name="inhalt">Dateiinhalt als Text.</param>
    /// <param name="fehler">Fehler je Zeile.</param>
    /// <returns>Die gelesenen Zeilen.</returns>
    public static IReadOnlyList<Rueckmeldung> RueckmeldungLesen(Exportformat format, string inhalt, out IReadOnlyList<string> fehler)
    {
        var meldungen = new List<string>();
        var spalten = SpaltenLesen(format, out var formatFehler);
        if (formatFehler is not null)
        {
            fehler = [formatFehler];
            return [];
        }

        var kultur = format.DezimalKomma ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
        var ergebnis = new List<Rueckmeldung>();
        var zeilen = inhalt.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = format.Kopfzeile ? 1 : 0; i < zeilen.Length; i++)
        {
            var werte = Zerlegen(zeilen[i], format.Trenner);
            string? Hole(string feld)
            {
                var index = spalten.ToList().FindIndex(s => s.Feld == feld);
                return index >= 0 && index < werte.Count ? werte[index].Trim() : null;
            }

            var kz = Hole("Kassenzeichen");
            if (string.IsNullOrWhiteSpace(kz))
            {
                meldungen.Add($"Zeile {i + 1}: Kassenzeichen fehlt.");
                continue;
            }

            decimal? bezahlt = null;
            if (Hole("Bezahlt") is { Length: > 0 } b)
            {
                if (!decimal.TryParse(b, NumberStyles.Number, kultur, out var wert))
                {
                    meldungen.Add($"Zeile {i + 1}: Betrag „{b}“ nicht lesbar.");
                    continue;
                }

                bezahlt = wert;
            }

            DateOnly? datum = null;
            if (Hole("Datum") is { Length: > 0 } d)
            {
                if (!DateOnly.TryParseExact(d, format.Datumsformat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tag))
                {
                    meldungen.Add($"Zeile {i + 1}: Datum „{d}“ passt nicht zum Format {format.Datumsformat}.");
                    continue;
                }

                datum = tag;
            }

            ergebnis.Add(new Rueckmeldung(kz, (Hole("Status") ?? "").ToLowerInvariant(), bezahlt, datum));
        }

        fehler = meldungen;
        return ergebnis;
    }

    private static List<string> Zerlegen(string zeile, string trenner)
    {
        var werte = new List<string>();
        var aktuell = new StringBuilder();
        var inAnfuehrung = false;
        for (var i = 0; i < zeile.Length; i++)
        {
            var c = zeile[i];
            if (c == '"')
            {
                if (inAnfuehrung && i + 1 < zeile.Length && zeile[i + 1] == '"')
                {
                    aktuell.Append('"');
                    i++;
                }
                else
                {
                    inAnfuehrung = !inAnfuehrung;
                }
            }
            else if (!inAnfuehrung && string.CompareOrdinal(zeile, i, trenner, 0, trenner.Length) == 0)
            {
                werte.Add(aktuell.ToString());
                aktuell.Clear();
                i += trenner.Length - 1;
            }
            else
            {
                aktuell.Append(c);
            }
        }

        werte.Add(aktuell.ToString());
        return werte;
    }
}
