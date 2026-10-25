using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Import;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Liest Kontoauszüge aus dem Onlinebanking (V07): camt.053 als XML oder ZIP mit mehreren XML-Dateien (jede Version, erkannt an den
/// Elementnamen) und CSV nach einem <see cref="Bankformat"/>. Vorgemerkte Umsätze zählen nicht, nur gebuchte.
/// </summary>
public static class Kontoauszugleser
{
    /// <summary>Höchstzahl an Umsätzen je Datei.</summary>
    public const int HoechstUmsaetze = 20000;

    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Liest eine camt.053-Datei (.xml) oder ein ZIP mit solchen Dateien.</summary>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="dateiname">Dateiname mit Endung .xml oder .zip.</param>
    /// <returns>Umsätze, Schlusssaldo des letzten Auszugs und IBAN des Kontos.</returns>
    /// <exception cref="FormatException">Die Datei ist kein lesbarer camt.053-Auszug.</exception>
    public static Auszug Camt(byte[] inhalt, string dateiname)
    {
        var dokumente = new List<XDocument>();
        if (Path.GetExtension(dateiname).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(inhalt), ZipArchiveMode.Read);
                foreach (var eintrag in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName))
                {
                    if (eintrag.Length > 50 * 1024 * 1024)
                    {
                        throw new FormatException($"„{eintrag.Name}“ ist zu groß.");
                    }

                    using var strom = eintrag.Open();
                    dokumente.Add(Laden(strom));
                }
            }
            catch (InvalidDataException)
            {
                throw new FormatException("Das ZIP-Archiv ist beschädigt.");
            }
        }
        else
        {
            dokumente.Add(Laden(new MemoryStream(inhalt)));
        }

        var auszuege = dokumente.SelectMany(d => d.Descendants().Where(e => e.Name.LocalName == "Stmt")).ToList();
        if (auszuege.Count == 0)
        {
            throw new FormatException("Kein camt.053-Kontoauszug gefunden (Element „Stmt“ fehlt).");
        }

        var zeilen = new List<Auszugszeile>();
        string? iban = null;
        (DateOnly Tag, decimal Betrag)? schluss = null;
        foreach (var stmt in auszuege)
        {
            iban ??= Kind(Kind(Kind(stmt, "Acct"), "Id"), "IBAN")?.Value.Trim();
            foreach (var bal in Kinder(stmt, "Bal"))
            {
                var art = Kind(Kind(Kind(bal, "Tp"), "CdOrPrtry"), "Cd")?.Value;
                if (art is "CLBD" && Betrag(bal) is { } saldo && Tag(Kind(bal, "Dt")) is { } tag && (schluss is null || tag >= schluss.Value.Tag))
                {
                    schluss = (tag, saldo);
                }
            }

            foreach (var ntry in Kinder(stmt, "Ntry"))
            {
                var status = Kind(ntry, "Sts") is { } sts ? (Kind(sts, "Cd")?.Value ?? sts.Value).Trim() : "BOOK";
                if (status != "BOOK")
                {
                    continue;
                }

                var buchungstag = Tag(Kind(ntry, "BookgDt")) ?? throw new FormatException("Ein Umsatz hat keinen Buchungstag.");
                var valuta = Tag(Kind(ntry, "ValDt"));
                var gutschrift = Kind(ntry, "CdtDbtInd")?.Value == "CRDT";
                var details = Kinder(ntry, "NtryDtls").SelectMany(d => Kinder(d, "TxDtls")).ToList();
                var zusatz = Kind(ntry, "AddtlNtryInf")?.Value;
                // Sammelbuchungen mit eigenen Beträgen je Posten werden aufgeteilt, sonst gilt der Betrag des Umsatzes.
                if (details.Count > 1 && details.All(d => PostenBetrag(d) is not null))
                {
                    zeilen.AddRange(details.Select(d => Zeile(buchungstag, valuta, gutschrift, PostenBetrag(d)!.Value, d, zusatz)));
                }
                else
                {
                    zeilen.Add(Zeile(buchungstag, valuta, gutschrift, Betrag(ntry) ?? 0m, details.FirstOrDefault(), zusatz));
                }

                if (zeilen.Count > HoechstUmsaetze)
                {
                    throw new FormatException($"Höchstens {HoechstUmsaetze} Umsätze je Datei.");
                }
            }
        }

        return new Auszug(zeilen, schluss?.Betrag, iban is null ? null : Iban.Normalisieren(iban));
    }

    /// <summary>Liest eine CSV-Datei nach dem Bankformat.</summary>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="format">Spaltenzuordnung der Bank.</param>
    /// <returns>Die Umsätze ohne Saldo.</returns>
    /// <exception cref="FormatException">Spalten fehlen oder Werte lassen sich nicht lesen.</exception>
    public static Auszug Csv(byte[] inhalt, Bankformat format)
    {
        var alle = Tabellenleser.Csv(Tabellenleser.Text(inhalt)).Skip(Math.Max(0, format.KopfzeilenUeberspringen))
            .Where(z => z.Any(w => !string.IsNullOrWhiteSpace(w))).ToList();
        if (alle.Count == 0)
        {
            throw new FormatException("Die Datei enthält keine Zeilen.");
        }

        if (alle.Count - 1 > HoechstUmsaetze)
        {
            throw new FormatException($"Höchstens {HoechstUmsaetze} Umsätze je Datei.");
        }

        var kopf = alle[0].Select(s => s.Trim()).ToList();
        int? Spalte(string? name, bool pflicht = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return pflicht ? throw new FormatException("Im Bankformat fehlt eine Pflichtspalte.") : null;
            }

            var i = kopf.FindIndex(k => string.Equals(k, name.Trim(), StringComparison.OrdinalIgnoreCase));
            return i >= 0 ? i : throw new FormatException($"Spalte „{name}“ nicht gefunden. Vorhanden: {string.Join(", ", kopf.Where(k => k.Length > 0))}.");
        }

        var tag = Spalte(format.SpalteBuchungstag, pflicht: true)!.Value;
        var valuta = Spalte(format.SpalteValuta);
        var betrag = Spalte(format.SpalteBetrag);
        var soll = Spalte(format.SpalteSoll);
        var haben = Spalte(format.SpalteHaben);
        var sollHaben = Spalte(format.SpalteSollHaben);
        if (betrag is null && soll is null && haben is null)
        {
            throw new FormatException("Im Bankformat fehlt die Spalte für den Betrag (oder Soll und Haben).");
        }

        var partei = Spalte(format.SpalteGegenpartei);
        var zweck = Spalte(format.SpalteVerwendungszweck);
        var referenz = Spalte(format.SpalteReferenz);
        var zeilen = new List<Auszugszeile>();
        for (var n = 1; n < alle.Count; n++)
        {
            var z = alle[n];
            string? Wert(int? i) => i is { } x && x < z.Count && !string.IsNullOrWhiteSpace(z[x]) ? z[x].Trim() : null;
            if (Wert(tag) is not { } tagText)
            {
                continue; // Summen- oder Fußzeilen ohne Datum
            }

            var buchungstag = Datum(tagText, format.Datumsformat) ?? throw new FormatException($"Zeile {n + 1}: „{tagText}“ ist kein Datum ({format.Datumsformat}).");
            decimal wert;
            if (betrag is not null)
            {
                wert = Zahl(Wert(betrag), format.Dezimalkomma, n);
                if (sollHaben is not null && Wert(sollHaben) is { } sh && sh.StartsWith('S'))
                {
                    wert = -Math.Abs(wert);
                }
            }
            else
            {
                wert = Math.Abs(Zahl(Wert(haben), format.Dezimalkomma, n)) - Math.Abs(Zahl(Wert(soll), format.Dezimalkomma, n));
            }

            zeilen.Add(new Auszugszeile(buchungstag, Wert(valuta) is { } v ? Datum(v, format.Datumsformat) : null, wert, Wert(partei), Wert(zweck),
                Bankabgleich.Referenz(Wert(referenz))));
        }

        return new Auszug(zeilen, null, null);
    }

    private static XDocument Laden(Stream strom)
    {
        try
        {
            using var leser = XmlReader.Create(strom, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(leser);
        }
        catch (XmlException fehler)
        {
            throw new FormatException($"Keine gültige XML-Datei: {fehler.Message}");
        }
    }

    private static XElement? Kind(XElement? e, string name) => e?.Elements().FirstOrDefault(x => x.Name.LocalName == name);

    private static IEnumerable<XElement> Kinder(XElement e, string name) => e.Elements().Where(x => x.Name.LocalName == name);

    private static decimal? Betrag(XElement e) =>
        Kind(e, "Amt") is { } amt && decimal.TryParse(amt.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)
            ? (Kind(e, "CdtDbtInd")?.Value == "DBIT" && e.Name.LocalName == "Bal" ? -b : b)
            : null;

    private static decimal? PostenBetrag(XElement tx) =>
        Kind(Kind(Kind(tx, "AmtDtls"), "TxAmt"), "Amt") is { } amt && decimal.TryParse(amt.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)
            ? b
            : Kind(tx, "Amt") is { } direkt && decimal.TryParse(direkt.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateOnly? Tag(XElement? e)
    {
        var text = (Kind(e, "Dt") ?? Kind(e, "DtTm"))?.Value;
        return text is not null && DateOnly.TryParse(text.Length >= 10 ? text[..10] : text, CultureInfo.InvariantCulture, out var tag) ? tag : null;
    }

    private static Auszugszeile Zeile(DateOnly tag, DateOnly? valuta, bool gutschrift, decimal betrag, XElement? tx, string? zusatz)
    {
        var parteien = Kind(tx, "RltdPties");
        // Bei einer Gutschrift ist der Zahler die Gegenpartei, bei einer Lastschrift der Empfänger; ab camt.053.001.08 steckt der Name in „Pty“.
        var rolle = Kind(parteien, gutschrift ? "Dbtr" : "Cdtr");
        var name = (Kind(Kind(rolle, "Pty"), "Nm") ?? Kind(rolle, "Nm"))?.Value.Trim();
        var zweck = Kind(tx, "RmtInf") is { } rmt ? string.Join(" ", Kinder(rmt, "Ustrd").Select(u => u.Value.Trim())) : null;
        return new Auszugszeile(tag, valuta, gutschrift ? Math.Abs(betrag) : -Math.Abs(betrag), string.IsNullOrWhiteSpace(name) ? null : name,
            string.IsNullOrWhiteSpace(zweck) ? zusatz?.Trim() : zweck, Bankabgleich.Referenz(Kind(Kind(tx, "Refs"), "EndToEndId")?.Value));
    }

    private static DateOnly? Datum(string text, string format)
    {
        string[] formate = [format, "dd.MM.yyyy", "dd.MM.yy", "yyyy-MM-dd", "d.M.yyyy"];
        return DateOnly.TryParseExact(text.Trim(), formate, Deutsch, DateTimeStyles.None, out var tag) ? tag : null;
    }

    private static decimal Zahl(string? text, bool dezimalkomma, int zeile)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        var bereinigt = text.Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Replace("€", "").Replace(" ", "").Replace(" ", "").Trim();
        return decimal.TryParse(bereinigt, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign,
            dezimalkomma ? Deutsch : CultureInfo.InvariantCulture, out var wert)
            ? wert
            : throw new FormatException($"Zeile {zeile + 1}: „{text}“ ist kein Betrag.");
    }
}
