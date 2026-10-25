using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Entschaedigung;

/// <summary>
/// Export einer Abrechnung für die Gemeindekasse: Auszahlungsliste je Person und Einzelposten,
/// als CSV (Semikolon, UTF-8 mit BOM, Dezimalkomma; öffnet sich direkt in Excel) oder als Excel-Datei.
/// Das Spaltenlayout ist mit der Kämmerei abzustimmen; Schlüssel ist die Personalnummer.
/// </summary>
public sealed class AbrechnungExport(AppDbContext db)
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Auszahlungssumme einer Person.</summary>
    /// <param name="Personalnummer">Personalnummer; Schlüssel für die Kämmerei.</param>
    /// <param name="Nachname">Nachname.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Stunden">Summe der abgerechneten Stunden.</param>
    /// <param name="Betrag">Summe in Euro.</param>
    public sealed record Summe(string Personalnummer, string Nachname, string Vorname, decimal Stunden, decimal Betrag);

    /// <summary>Einzelposten einer Person.</summary>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Nachname">Nachname.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Datum">Tag des Vorgangs bzw. Beginn des Pauschalenanteils.</param>
    /// <param name="DatumBis">Ende des Pauschalenanteils.</param>
    /// <param name="Art">Herkunft des Postens.</param>
    /// <param name="Vorgang">Vorgang, z. B. „Einsatz“ oder Dienstart.</param>
    /// <param name="Bezeichnung">Lesbarer Text des Postens.</param>
    /// <param name="Minuten">Tatsächliche Dauer vor Rundung.</param>
    /// <param name="Menge">Abgerechnete Menge.</param>
    /// <param name="Einheit">Einheit der Menge.</param>
    /// <param name="Satz">Satz je Einheit.</param>
    /// <param name="Betrag">Betrag in Euro.</param>
    public sealed record Zeile(
        string Personalnummer, string Nachname, string Vorname, DateOnly Datum, DateOnly? DatumBis, PostenArt Art,
        string Vorgang, string Bezeichnung, int? Minuten, decimal Menge, Entschaedigungseinheit Einheit, decimal Satz, decimal Betrag);

    /// <summary>Alles, was der Export braucht.</summary>
    /// <param name="Abrechnung">Die Abrechnung.</param>
    /// <param name="Summen">Summen je Person.</param>
    /// <param name="Zeilen">Einzelposten.</param>
    public sealed record Daten(Abrechnung Abrechnung, IReadOnlyList<Summe> Summen, IReadOnlyList<Zeile> Zeilen);

    /// <summary>Lädt Abrechnung, Summen je Person und Einzelposten.</summary>
    /// <param name="id">Schlüssel der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Exportdaten.</returns>
    public async Task<Daten> LadenAsync(Guid id, CancellationToken ct = default)
    {
        var abrechnung = await db.Abrechnungen.AsNoTracking().Include(a => a.Posten).SingleAsync(a => a.Id == id, ct);
        var ids = abrechnung.Posten.Select(p => p.MitgliedId).Distinct().ToList();

        // Auch ausgeschiedene oder gelöschte Mitglieder bekommen ihr Geld.
        var mitglieder = await db.Mitglieder.IgnoreQueryFilters().AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var zeilen = abrechnung.Posten
            .Select(p =>
            {
                var m = mitglieder[p.MitgliedId];
                return new Zeile(m.Personalnummer, m.Nachname, m.Vorname, p.Datum, p.DatumBis, p.Art, p.Vorgang, p.Bezeichnung,
                    p.Minuten, p.Menge, p.Einheit, p.Satz, p.Betrag);
            })
            .OrderBy(z => z.Nachname).ThenBy(z => z.Vorname).ThenBy(z => z.Datum).ThenBy(z => z.Bezeichnung)
            .ToList();

        var summen = zeilen
            .GroupBy(z => (z.Personalnummer, z.Nachname, z.Vorname))
            .Select(g => new Summe(g.Key.Personalnummer, g.Key.Nachname, g.Key.Vorname,
                g.Where(z => z.Einheit == Entschaedigungseinheit.Stunde).Sum(z => z.Menge),
                g.Sum(z => z.Betrag)))
            .ToList();

        return new Daten(abrechnung, summen, zeilen);
    }

    /// <summary>Dateiname des Exports aus Bezeichnung und Zeitraum.</summary>
    /// <param name="abrechnung">Die Abrechnung.</param>
    /// <param name="endung">Dateiendung ohne Punkt, z. B. „csv“.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Abrechnung abrechnung, string endung)
    {
        var name = new string(abrechnung.Bezeichnung.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return $"Entschaedigung_{name}_{abrechnung.Von:yyyyMMdd}-{abrechnung.Bis:yyyyMMdd}.{endung}";
    }

    /// <summary>Anzeigetext einer Einheit.</summary>
    /// <param name="einheit">Einheit.</param>
    /// <returns>Der Text, z. B. „Std.“.</returns>
    public static string EinheitText(Entschaedigungseinheit einheit) => einheit switch
    {
        Entschaedigungseinheit.Stunde => "Std.",
        Entschaedigungseinheit.Tag => "Tag",
        Entschaedigungseinheit.HalberTag => "halber Tag",
        Entschaedigungseinheit.Jahresanteil => "Tage anteilig",
        _ => einheit.ToString(),
    };

    /// <summary>Anzeigetext einer Postenart.</summary>
    /// <param name="art">Postenart.</param>
    /// <returns>Der Text.</returns>
    public static string ArtText(PostenArt art) => art switch
    {
        PostenArt.Funktionspauschale => "Funktionspauschale",
        _ => art.ToString(),
    };

    /// <summary>Dauer als „Stunden:Minuten“.</summary>
    /// <param name="minuten">Dauer in Minuten.</param>
    /// <returns>Der Text, leer ohne Dauer.</returns>
    public static string Dauer(int? minuten) => minuten is { } m ? $"{m / 60}:{m % 60:00}" : "";

    /// <summary>Auszahlungsliste: eine Zeile je Person.</summary>
    public static byte[] SummenCsv(Daten daten)
    {
        var csv = new StringBuilder();
        CsvZeile(csv, "Personalnummer", "Nachname", "Vorname", "Stunden", "Betrag");
        foreach (var s in daten.Summen)
        {
            CsvZeile(csv, s.Personalnummer, s.Nachname, s.Vorname, Zahl(s.Stunden), Zahl(s.Betrag));
        }

        return Utf8MitBom(csv);
    }

    /// <summary>Einzelposten: eine Zeile je Teilnahme oder Pauschalenanteil.</summary>
    public static byte[] PostenCsv(Daten daten)
    {
        var csv = new StringBuilder();
        CsvZeile(csv, "Personalnummer", "Nachname", "Vorname", "Datum", "Bis", "Art", "Vorgang", "Bezeichnung", "Dauer", "Menge", "Einheit", "Satz", "Betrag");
        foreach (var z in daten.Zeilen)
        {
            CsvZeile(csv, z.Personalnummer, z.Nachname, z.Vorname, Datum(z.Datum), z.DatumBis is { } b ? Datum(b) : "", ArtText(z.Art), z.Vorgang,
                z.Bezeichnung, Dauer(z.Minuten), Zahl(z.Menge), EinheitText(z.Einheit), Zahl(z.Satz), Zahl(z.Betrag));
        }

        return Utf8MitBom(csv);
    }

    /// <summary>Erzeugt den Export als Excel-Datei mit Blättern für Summen und Einzelposten.</summary>
    /// <param name="daten">Die Exportdaten.</param>
    /// <returns>Die XLSX-Datei als Bytes.</returns>
    public static byte[] Excel(Daten daten)
    {
        using var mappe = new XLWorkbook();

        var summen = mappe.Worksheets.Add("Auszahlung");
        Kopf(summen, "Personalnummer", "Nachname", "Vorname", "Stunden", "Betrag (€)");
        var zeile = 2;
        foreach (var s in daten.Summen)
        {
            summen.Cell(zeile, 1).SetValue(s.Personalnummer);
            summen.Cell(zeile, 2).SetValue(s.Nachname);
            summen.Cell(zeile, 3).SetValue(s.Vorname);
            summen.Cell(zeile, 4).SetValue(s.Stunden);
            summen.Cell(zeile, 5).SetValue(s.Betrag);
            zeile++;
        }

        summen.Cell(zeile, 4).SetValue("Summe");
        summen.Cell(zeile, 5).SetValue(daten.Summen.Sum(s => s.Betrag));
        summen.Row(zeile).Style.Font.Bold = true;
        summen.Column(4).Style.NumberFormat.Format = "#,##0.00";
        summen.Column(5).Style.NumberFormat.Format = "#,##0.00";
        summen.Columns().AdjustToContents();

        var posten = mappe.Worksheets.Add("Posten");
        Kopf(posten, "Personalnummer", "Nachname", "Vorname", "Datum", "Bis", "Art", "Vorgang", "Bezeichnung", "Dauer", "Menge", "Einheit", "Satz (€)", "Betrag (€)");
        zeile = 2;
        foreach (var z in daten.Zeilen)
        {
            posten.Cell(zeile, 1).SetValue(z.Personalnummer);
            posten.Cell(zeile, 2).SetValue(z.Nachname);
            posten.Cell(zeile, 3).SetValue(z.Vorname);
            posten.Cell(zeile, 4).SetValue(z.Datum.ToDateTime(TimeOnly.MinValue));
            if (z.DatumBis is { } bis)
            {
                posten.Cell(zeile, 5).SetValue(bis.ToDateTime(TimeOnly.MinValue));
            }

            posten.Cell(zeile, 6).SetValue(ArtText(z.Art));
            posten.Cell(zeile, 7).SetValue(z.Vorgang);
            posten.Cell(zeile, 8).SetValue(z.Bezeichnung);
            posten.Cell(zeile, 9).SetValue(Dauer(z.Minuten));
            posten.Cell(zeile, 10).SetValue(z.Menge);
            posten.Cell(zeile, 11).SetValue(EinheitText(z.Einheit));
            posten.Cell(zeile, 12).SetValue(z.Satz);
            posten.Cell(zeile, 13).SetValue(z.Betrag);
            zeile++;
        }

        posten.Column(4).Style.NumberFormat.Format = "dd.mm.yyyy";
        posten.Column(5).Style.NumberFormat.Format = "dd.mm.yyyy";
        posten.Column(12).Style.NumberFormat.Format = "#,##0.00";
        posten.Column(13).Style.NumberFormat.Format = "#,##0.00";
        posten.Columns().AdjustToContents();

        var info = mappe.Worksheets.Add("Info");
        info.Cell(1, 1).SetValue("Abrechnung");
        info.Cell(1, 2).SetValue(daten.Abrechnung.Bezeichnung);
        info.Cell(2, 1).SetValue("Zeitraum");
        info.Cell(2, 2).SetValue($"{Datum(daten.Abrechnung.Von)} bis {Datum(daten.Abrechnung.Bis)}");
        info.Cell(3, 1).SetValue("Status");
        info.Cell(3, 2).SetValue(daten.Abrechnung.IstAbgeschlossen ? "abgeschlossen" : "Entwurf – nicht zur Auszahlung");
        info.Columns().AdjustToContents();

        using var strom = new MemoryStream();
        mappe.SaveAs(strom);
        return strom.ToArray();
    }

    private static void Kopf(IXLWorksheet blatt, params string[] spalten)
    {
        for (var i = 0; i < spalten.Length; i++)
        {
            blatt.Cell(1, i + 1).SetValue(spalten[i]);
        }

        blatt.Row(1).Style.Font.Bold = true;
        blatt.SheetView.FreezeRows(1);
    }

    private static string Zahl(decimal wert) => wert.ToString("0.00", Deutsch);

    private static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", Deutsch);

    internal static void CsvZeile(StringBuilder csv, params string[] felder) =>
        csv.Append(string.Join(';', felder.Select(Feld))).Append("\r\n");

    private static string Feld(string wert)
    {
        // Schutz gegen Formel-Injektion beim Öffnen in Excel.
        if (wert.Length > 0 && wert[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(wert, NumberStyles.Number, Deutsch, out _))
        {
            wert = "'" + wert;
        }

        return wert.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{wert.Replace("\"", "\"\"")}\"" : wert;
    }

    internal static byte[] Utf8MitBom(StringBuilder csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
}
