using System.Globalization;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Inventarbrücke zur Anlagenbuchhaltung der Gemeinde (V60, GemHVO §§ 37, 38): Fahrzeuge und inventarisierungspflichtige
/// Geräte mit Anlagennummer, Wert, Nutzungsdauer, Eigentümer und Förderung, dazu die Abgänge des Jahres.
/// </summary>
public sealed class Anlagennachweis(AppDbContext db, ParameterLeser parameter)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Eine Zeile des Nachweises.</summary>
    /// <param name="Id">Id des Geräts oder Fahrzeugs.</param>
    /// <param name="IstFahrzeug">Fahrzeug statt Gerät.</param>
    /// <param name="Nummer">Inventarnummer bzw. Funkrufname.</param>
    /// <param name="Bezeichnung">Bezeichnung bzw. Fahrzeugtyp.</param>
    /// <param name="Anlagennummer">Nummer der Anlagenbuchhaltung.</param>
    /// <param name="Zugang">Beschaffungs- bzw. Indienststellungsdatum.</param>
    /// <param name="Anschaffungswert">Anschaffungswert brutto.</param>
    /// <param name="NutzungsdauerJahre">Nutzungsdauer laut Anlagenbuchhaltung.</param>
    /// <param name="Eigentuemer">Eigentümer.</param>
    /// <param name="Foerdermittel">Erhaltene Fördermittel.</param>
    /// <param name="Foerdergeber">Fördergeber.</param>
    /// <param name="ZweckbindungBis">Ende der Zweckbindung.</param>
    /// <param name="Standort">Fahrzeug oder Lagerort.</param>
    /// <param name="Abgang">Tag der Aussonderung bzw. Außerdienststellung.</param>
    /// <param name="Verwertung">Art der Verwertung.</param>
    /// <param name="Erloes">Verkaufserlös.</param>
    /// <param name="Beschluss">Verweis auf den Aussonderungsbeschluss.</param>
    public sealed record Zeile(
        Guid Id,
        bool IstFahrzeug,
        string Nummer,
        string Bezeichnung,
        string? Anlagennummer,
        DateOnly? Zugang,
        decimal? Anschaffungswert,
        int? NutzungsdauerJahre,
        Eigentuemer Eigentuemer,
        decimal? Foerdermittel,
        string? Foerdergeber,
        DateOnly? ZweckbindungBis,
        string? Standort,
        DateOnly? Abgang,
        Verwertung? Verwertung,
        decimal? Erloes,
        string? Beschluss);

    /// <summary>
    /// Bestand am Jahresende bzw. heute und Abgänge im Jahr. Geräte zählen ab der Inventarisierungsgrenze
    /// oder mit Anlagennummer; Fahrzeuge immer.
    /// </summary>
    /// <param name="jahr">Jahr der Abgänge.</param>
    /// <param name="heute">Heutiges Datum (Ortszeit) für die Inventarisierungsgrenze.</param>
    /// <param name="ct">Abbruch.</param>
    public async Task<IReadOnlyList<Zeile>> ErstellenAsync(int jahr, DateOnly heute, CancellationToken ct)
    {
        var grenze = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, heute, 150, ct);
        var erster = new DateOnly(jahr, 1, 1);
        var letzter = new DateOnly(jahr, 12, 31);

        var fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => f.AusserDienstAm == null || (f.AusserDienstAm >= erster && f.AusserDienstAm <= letzter))
            .Select(f => new Zeile(f.Id, true, f.Funkrufname, f.Typ, f.Anlagennummer, f.IndienstAm, f.Anschaffungswert, f.NutzungsdauerJahre,
                f.Eigentuemer, f.Foerdermittel, f.Foerdergeber, f.ZweckbindungBis, null, f.AusserDienstAm, f.Verwertung, f.Erloes, f.Aussonderungsbeschluss))
            .ToListAsync(ct);

        var geraete = await db.Geraete.AsNoTracking()
            .Where(g => g.Anlagennummer != null || g.Anschaffungspreis >= grenze || (g.Anschaffungspreis == null && g.Material!.Inventarisierungspflichtig && g.Material.Stueckwert >= grenze))
            .Where(g => g.AusgesondertAm == null || (g.AusgesondertAm >= erster && g.AusgesondertAm <= letzter))
            .Select(g => new Zeile(g.Id, false, g.Inventarnummer, g.Bezeichnung, g.Anlagennummer, g.BeschafftAm ?? g.IndienstAm, g.Anschaffungspreis ?? g.Material!.Stueckwert,
                g.NutzungsdauerJahre, g.Eigentuemer, g.Foerdermittel, g.Foerdergeber, g.ZweckbindungBis,
                g.Fahrzeug != null ? g.Fahrzeug.Funkrufname + (g.Lagerort == null ? "" : " / " + g.Lagerort) : g.Lagerort,
                g.AusgesondertAm, g.Verwertung, g.Erloes, g.Aussonderungsbeschluss))
            .ToListAsync(ct);

        return fahrzeuge.OrderBy(f => f.Nummer, StringComparer.CurrentCulture)
            .Concat(geraete.OrderBy(g => g.Nummer, StringComparer.Ordinal))
            .ToList();
    }

    /// <summary>CSV für die Kämmerei (Semikolon, UTF-8 mit BOM, deutsche Zahlen).</summary>
    public static byte[] Csv(IEnumerable<Zeile> zeilen)
    {
        var csv = new StringBuilder();
        csv.Append("Art;Anlagennummer;Inventarnummer/Funkrufname;Bezeichnung;Zugang;Anschaffungswert brutto;Nutzungsdauer Jahre;Eigentümer;Fördermittel;Fördergeber;Zweckbindung bis;Standort;Abgang;Verwertung;Erlös;Beschluss\r\n");
        foreach (var z in zeilen)
        {
            csv.Append(string.Join(';',
                z.IstFahrzeug ? "Fahrzeug" : "Gerät",
                Feld(z.Anlagennummer),
                Feld(z.Nummer),
                Feld(z.Bezeichnung),
                z.Zugang?.ToString("dd.MM.yyyy") ?? "",
                z.Anschaffungswert?.ToString("0.00", Deutsch) ?? "",
                z.NutzungsdauerJahre?.ToString(Deutsch) ?? "",
                Text(z.Eigentuemer),
                z.Foerdermittel?.ToString("0.00", Deutsch) ?? "",
                Feld(z.Foerdergeber),
                z.ZweckbindungBis?.ToString("dd.MM.yyyy") ?? "",
                Feld(z.Standort),
                z.Abgang?.ToString("dd.MM.yyyy") ?? "",
                z.Verwertung is { } v ? Text(v) : "",
                z.Erloes?.ToString("0.00", Deutsch) ?? "",
                Feld(z.Beschluss)));
            csv.Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>Anzeigetext eines Enum-Werts aus dem Display-Attribut.</summary>
    public static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.GetName() ?? wert.ToString();

    /// <summary>Maskiert Semikolon, Anführungszeichen und Zeilenumbrüche; führende Formelzeichen werden entschärft.</summary>
    private static string Feld(string? wert)
    {
        if (string.IsNullOrEmpty(wert))
        {
            return "";
        }

        if (wert[0] is '=' or '+' or '-' or '@')
        {
            wert = "'" + wert;
        }

        return wert.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{wert.Replace("\"", "\"\"")}\"" : wert;
    }
}
