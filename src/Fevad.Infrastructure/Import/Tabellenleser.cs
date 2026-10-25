using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Fevad.Infrastructure.Import;

/// <summary>Eine eingelesene Tabelle: Spaltenüberschriften aus der ersten Zeile, darunter die Datenzeilen.</summary>
/// <param name="Spalten">Überschriften, leere als „Spalte N“.</param>
/// <param name="Zeilen">Datenzeilen; jede so lang wie <paramref name="Spalten"/>.</param>
public sealed record Tabelle(IReadOnlyList<string> Spalten, IReadOnlyList<string[]> Zeilen);

/// <summary>
/// Liest CSV (Trennzeichen Semikolon, Komma oder Tabulator, UTF-8 oder Windows-1252, wie Windows-Programme es schreiben) und
/// XLSX (erstes Tabellenblatt) ohne zusätzliche Bibliothek.
/// </summary>
public static class Tabellenleser
{
    /// <summary>Höchstzahl an Datenzeilen, damit ein falscher Export den Server nicht lahmlegt.</summary>
    public const int HoechstZeilen = 20000;

    private static readonly XNamespace Tabellenblatt = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Beziehungen = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PaketBeziehungen = "http://schemas.openxmlformats.org/package/2006/relationships";

    static Tabellenleser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Liest die Datei nach ihrer Endung.</summary>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="dateiname">Dateiname mit Endung .csv, .txt oder .xlsx.</param>
    /// <returns>Die Tabelle.</returns>
    /// <exception cref="FormatException">Die Datei ist leer, zu groß oder kein lesbares Format.</exception>
    public static Tabelle Lesen(byte[] inhalt, string dateiname)
    {
        var zeilen = Path.GetExtension(dateiname).ToLowerInvariant() switch
        {
            ".xlsx" => Xlsx(inhalt),
            ".csv" or ".txt" => Csv(Text(inhalt)),
            _ => throw new FormatException("Nur CSV- oder XLSX-Dateien."),
        };
        zeilen = zeilen.Where(z => z.Any(w => !string.IsNullOrWhiteSpace(w))).ToList();
        if (zeilen.Count == 0)
        {
            throw new FormatException("Die Datei enthält keine Zeilen.");
        }

        if (zeilen.Count - 1 > HoechstZeilen)
        {
            throw new FormatException($"Höchstens {HoechstZeilen} Zeilen je Datei.");
        }

        var breite = zeilen.Max(z => z.Count);
        var spalten = Enumerable.Range(0, breite)
            .Select(i => i < zeilen[0].Count && !string.IsNullOrWhiteSpace(zeilen[0][i]) ? zeilen[0][i].Trim() : $"Spalte {i + 1}")
            .ToList();
        // Doppelte Überschriften eindeutig machen, damit die Zuordnung sie unterscheiden kann.
        for (var i = 0; i < spalten.Count; i++)
        {
            var n = spalten.Take(i).Count(s => string.Equals(s, spalten[i], StringComparison.OrdinalIgnoreCase));
            if (n > 0)
            {
                spalten[i] = $"{spalten[i]} ({n + 1})";
            }
        }

        return new Tabelle(spalten, zeilen.Skip(1).Select(z => Enumerable.Range(0, breite).Select(i => i < z.Count ? z[i].Trim() : "").ToArray()).ToList());
    }

    /// <summary>UTF-8 mit oder ohne BOM; ist der Text kein gültiges UTF-8, gilt Windows-1252.</summary>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <returns>Der Text.</returns>
    public static string Text(byte[] inhalt)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(inhalt).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(inhalt);
        }
    }

    /// <summary>CSV nach RFC 4180 mit Anführungszeichen; das Trennzeichen ist das häufigste der ersten Zeile.</summary>
    /// <param name="text">Der Text der Datei.</param>
    /// <returns>Die Zeilen mit ihren Werten.</returns>
    public static List<List<string>> Csv(string text)
    {
        var ersteZeile = text.Split('\n', 2)[0];
        var trenner = new[] { ';', ',', '\t' }.OrderByDescending(t => ersteZeile.Count(c => c == t)).First();
        var zeilen = new List<List<string>>();
        var zeile = new List<string>();
        var wert = new StringBuilder();
        var inAnfuehrung = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inAnfuehrung)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    wert.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inAnfuehrung = false;
                }
                else
                {
                    wert.Append(c);
                }
            }
            else if (c == '"' && wert.Length == 0)
            {
                inAnfuehrung = true;
            }
            else if (c == trenner)
            {
                zeile.Add(wert.ToString());
                wert.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                zeile.Add(wert.ToString());
                wert.Clear();
                zeilen.Add(zeile);
                zeile = [];
            }
            else
            {
                wert.Append(c);
            }
        }

        if (wert.Length > 0 || zeile.Count > 0)
        {
            zeile.Add(wert.ToString());
            zeilen.Add(zeile);
        }

        return zeilen;
    }

    /// <summary>Erstes Tabellenblatt einer XLSX-Datei; Zahlen wie gespeichert (Datumswerte als Seriennummer).</summary>
    private static List<List<string>> Xlsx(byte[] inhalt)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(inhalt), ZipArchiveMode.Read);
            XDocument Xml(string pfad) => XDocument.Load(zip.GetEntry(pfad)?.Open() ?? throw new FormatException($"{pfad} fehlt."));

            var texte = zip.GetEntry("xl/sharedStrings.xml") is null
                ? []
                : Xml("xl/sharedStrings.xml").Root!.Elements(Tabellenblatt + "si")
                    .Select(si => string.Concat(si.Descendants(Tabellenblatt + "t").Select(t => t.Value))).ToList();

            var blatt = Xml("xl/workbook.xml").Root!.Element(Tabellenblatt + "sheets")!.Elements(Tabellenblatt + "sheet").First();
            var beziehung = (string)blatt.Attribute(Beziehungen + "id")!;
            var ziel = (string)Xml("xl/_rels/workbook.xml.rels").Root!.Elements(PaketBeziehungen + "Relationship")
                .First(r => (string?)r.Attribute("Id") == beziehung).Attribute("Target")!;
            var pfad = ziel.StartsWith('/') ? ziel.TrimStart('/') : "xl/" + ziel;

            var zeilen = new List<List<string>>();
            foreach (var row in Xml(pfad).Root!.Element(Tabellenblatt + "sheetData")!.Elements(Tabellenblatt + "row"))
            {
                var zeile = new List<string>();
                foreach (var zelle in row.Elements(Tabellenblatt + "c"))
                {
                    var spalte = Spaltennummer((string?)zelle.Attribute("r")) ?? zeile.Count;
                    while (zeile.Count < spalte)
                    {
                        zeile.Add("");
                    }

                    var v = zelle.Element(Tabellenblatt + "v")?.Value;
                    zeile.Add((string?)zelle.Attribute("t") switch
                    {
                        "s" => int.TryParse(v, out var i) && i < texte.Count ? texte[i] : "",
                        "inlineStr" => string.Concat(zelle.Descendants(Tabellenblatt + "t").Select(t => t.Value)),
                        "b" => v == "1" ? "ja" : "nein",
                        _ => v ?? "",
                    });
                }

                zeilen.Add(zeile);
                if (zeilen.Count > HoechstZeilen + 1)
                {
                    break;
                }
            }

            return zeilen;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or InvalidOperationException or NullReferenceException)
        {
            throw new FormatException("Die XLSX-Datei ist nicht lesbar.", ex);
        }
    }

    /// <summary>Nullbasierte Spalte aus einem Zellbezug wie „C12“.</summary>
    private static int? Spaltennummer(string? bezug)
    {
        if (string.IsNullOrEmpty(bezug))
        {
            return null;
        }

        var n = 0;
        foreach (var c in bezug.TakeWhile(char.IsLetter))
        {
            n = n * 26 + (char.ToUpper(c, CultureInfo.InvariantCulture) - 'A' + 1);
        }

        return n - 1;
    }
}
