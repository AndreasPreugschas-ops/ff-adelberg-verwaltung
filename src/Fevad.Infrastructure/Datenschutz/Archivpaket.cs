using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Archivpaket für das Archiv (V65, § 3 Abs. 1 LArchG BW: Datenbanken samt Metadaten sind Unterlagen): je Unterlage ein PDF/A zum Lesen und
/// CSV-Dateien je Tabelle, dazu ein Verzeichnis, Metadaten als XML mit Prüfsummen und eine Beschreibung des Datenmodells, als ZIP.
/// </summary>
public sealed class Archivpaket(AppDbContext db, Selbstauskunft auskunft, ParameterLeser parameter)
{
    static Archivpaket()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erstellt das ZIP für die übernommenen Unterlagen.</summary>
    /// <param name="angebote">Die Unterlagen, die das Archiv übernimmt.</param>
    /// <param name="jetzt">Zeitpunkt der Erstellung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das ZIP als Bytes.</returns>
    public async Task<byte[]> ErstellenAsync(IReadOnlyList<Archivangebot> angebote, DateTimeOffset jetzt, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin).DateTime);
        var stelle = await parameter.WertAsync(ParameterSchluessel.ArchivStelle, heute, ct) ?? "Archiv";
        var organisation = await db.OrganisationAsync(ct);
        var unterlagen = new XElement("unterlagen");
        var verzeichnis = new StringBuilder("Nr;Art;Bezeichnung;Jahr;Frist abgelaufen;Angeboten;Ordner\r\n");
        using var speicher = new MemoryStream();
        using (var zip = new ZipArchive(speicher, ZipArchiveMode.Create, leaveOpen: true))
        {
            var nr = 0;
            foreach (var a in angebote.OrderBy(a => a.Art).ThenBy(a => a.Jahr).ThenBy(a => a.Bezeichnung))
            {
                nr++;
                var ordner = $"{nr:000}-{a.Art}";
                var abschnitte = await AbschnitteAsync(a, ct);
                var bewertung = Archivierung.Katalog.Single(k => k.Art == a.Art);
                var dateien = new XElement("dateien");
                dateien.Add(Datei(zip, $"{ordner}/unterlage.pdf", Pdf(organisation.FeuerwehrName, a, abschnitte, jetzt)));
                for (var i = 0; i < abschnitte.Count; i++)
                {
                    dateien.Add(Datei(zip, $"{ordner}/{i + 1:00}-{Dateiname(abschnitte[i].Titel)}.csv", Csv(abschnitte[i])));
                }

                unterlagen.Add(new XElement("unterlage",
                    new XAttribute("nr", nr), new XAttribute("art", Archivierung.Anzeigename(a.Art)), new XAttribute("jahr", a.Jahr),
                    new XElement("bezeichnung", a.Bezeichnung),
                    new XElement("entstehung", $"FEVAD, {organisation.FeuerwehrName}"),
                    new XElement("loeschregel", bewertung.Regel),
                    new XElement("frist", $"{await parameter.WertAsync(bewertung.FristSchluessel, heute, ct)} {bewertung.Frist}, abgelaufen am {a.FaelligAm:yyyy-MM-dd}"),
                    new XElement("rechtsgrundlage", bewertung.Rechtsgrundlage),
                    new XElement("personenbezug", a.Art == Archivgut.Kassenjahr ? "ja, Zahlungsempfänger und Einzahler" : "ja"),
                    new XElement("angeboten", a.AngebotenAm?.ToString("yyyy-MM-dd")),
                    new XElement("entscheidung", a.EntschiedenAm?.ToString("yyyy-MM-dd")),
                    dateien));
                verzeichnis.Append(string.Join(';', new object?[] { nr, Archivierung.Anzeigename(a.Art), a.Bezeichnung, a.Jahr, a.FaelligAm.ToString("dd.MM.yyyy"), a.AngebotenAm?.ToString("dd.MM.yyyy"), ordner }.Select(Feld))).Append("\r\n");
            }

            Datei(zip, "verzeichnis.csv", Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(verzeichnis.ToString())).ToArray());
            Datei(zip, "datenmodell.txt", Encoding.UTF8.GetBytes(Datenmodell));
            var metadaten = new XDocument(new XElement("archivpaket",
                new XAttribute("erstellt", jetzt.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement("abgebende_stelle", organisation.FeuerwehrName),
                new XElement("archiv", stelle),
                new XElement("system", "FEVAD"),
                new XElement("pruefsummen", "SHA-256 je Datei"),
                unterlagen));
            Datei(zip, "metadaten.xml", Encoding.UTF8.GetBytes(metadaten.Declaration + Environment.NewLine + metadaten));
        }

        return speicher.ToArray();
    }

    private async Task<IReadOnlyList<Selbstauskunft.Abschnitt>> AbschnitteAsync(Archivangebot a, CancellationToken ct)
    {
        switch (a.Art)
        {
            case Archivgut.Mitgliederakte:
                return await auskunft.DatensatzAsync(typeof(Mitglied), a.DatensatzId, ct);
            case Archivgut.Mitgliederbescheid:
                return await auskunft.DatensatzAsync(typeof(Mitgliederbescheid), a.DatensatzId, ct);
            case Archivgut.Einsatzbericht:
                return await auskunft.DatensatzAsync(typeof(Einsatz), a.DatensatzId, ct);
            default:
                var abschluss = await db.Jahresabschluesse.AsNoTracking().FirstOrDefaultAsync(j => j.Id == a.DatensatzId, ct);
                var liste = new List<Selbstauskunft.Abschnitt>
                {
                    await auskunft.TabelleAsync(typeof(Jahresabschluss), "Jahresabschluss", new Dictionary<string, object> { ["Id"] = a.DatensatzId }, ct),
                };
                if (abschluss is not null)
                {
                    liste.Add(await auskunft.TabelleAsync(typeof(Buchung), "Buchungen",
                        new Dictionary<string, object> { ["KasseId"] = abschluss.KasseId, ["Jahr"] = abschluss.Jahr }, ct));
                }

                return liste;
        }
    }

    private static XElement Datei(ZipArchive zip, string name, byte[] inhalt)
    {
        var eintrag = zip.CreateEntry(name, CompressionLevel.Optimal);
        using (var strom = eintrag.Open())
        {
            strom.Write(inhalt);
        }

        return new XElement("datei", new XAttribute("name", name), new XAttribute("sha256", Convert.ToHexStringLower(SHA256.HashData(inhalt))));
    }

    private static byte[] Csv(Selbstauskunft.Abschnitt abschnitt)
    {
        var text = new StringBuilder();
        text.Append(string.Join(';', abschnitt.Spalten.Select(Feld))).Append("\r\n");
        foreach (var zeile in abschnitt.Zeilen)
        {
            text.Append(string.Join(';', zeile.Select(Wert).Select(Feld))).Append("\r\n");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    private static byte[] Pdf(string feuerwehr, Archivangebot a, IReadOnlyList<Selbstauskunft.Abschnitt> abschnitte, DateTimeOffset jetzt) =>
        Document.Create(dok => dok.Page(seite =>
        {
            seite.Size(PageSizes.A4.Landscape());
            seite.Margin(1.5f, Unit.Centimetre);
            seite.DefaultTextStyle(t => t.FontSize(8));
            seite.Header().Column(k =>
            {
                k.Item().Text($"{Archivierung.Anzeigename(a.Art)}: {a.Bezeichnung}").FontSize(13).Bold();
                k.Item().Text($"{feuerwehr} · Archivpaket aus FEVAD vom {TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin):dd.MM.yyyy HH:mm} Uhr");
            });
            seite.Content().PaddingTop(8).Column(k =>
            {
                foreach (var abschnitt in abschnitte)
                {
                    k.Item().PaddingTop(8).Text(abschnitt.Titel).FontSize(10).Bold();
                    if (abschnitt.Zeilen.Count == 0)
                    {
                        k.Item().Text("keine Einträge");
                        continue;
                    }

                    // Jede Zeile als Liste „Spalte: Wert“, damit auch breite Tabellen lesbar bleiben.
                    foreach (var zeile in abschnitt.Zeilen)
                    {
                        k.Item().PaddingTop(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(t =>
                        {
                            for (var i = 0; i < abschnitt.Spalten.Count; i++)
                            {
                                t.Span(abschnitt.Spalten[i] + ": ").SemiBold();
                                t.Span(Wert(zeile[i]) + (i < abschnitt.Spalten.Count - 1 ? " · " : ""));
                            }
                        });
                    }
                }
            });
            seite.Footer().AlignRight().Text(t =>
            {
                t.Span("Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf(archiv: true);

    private static string Wert(object? wert) => wert switch
    {
        null => "",
        DateTimeOffset z => TimeZoneInfo.ConvertTime(z, Ortszeit.Berlin).ToString("dd.MM.yyyy HH:mm"),
        DateOnly d => d.ToString("dd.MM.yyyy"),
        bool b => b ? "ja" : "nein",
        decimal d => d.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
        _ => wert.ToString() ?? "",
    };

    private static string Feld(object? wert)
    {
        var text = wert?.ToString() ?? "";
        return text.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private static string Dateiname(string titel) =>
        new string(titel.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    private const string Datenmodell = """
        Archivpaket aus FEVAD (Feuerwehrverwaltung Adelberg)

        Aufbau
        - verzeichnis.csv: alle Unterlagen des Pakets mit Nummer, Art, Bezeichnung, Jahr und Ordner.
        - metadaten.xml: je Unterlage Entstehung, Löschregel, Aufbewahrungsfrist, Rechtsgrundlage, Personenbezug,
          Datum des Angebots und der Entscheidung sowie die SHA-256-Prüfsumme jeder Datei.
        - Ordner je Unterlage (Nummer-Art): unterlage.pdf (PDF/A, alle Angaben lesbar) und je Tabelle eine CSV-Datei.

        CSV-Dateien
        - Zeichensatz UTF-8 mit BOM, Trennzeichen Semikolon, Zeilenende CRLF, Felder mit Semikolon oder Anführungszeichen in "…".
        - Die erste Zeile enthält die Spaltennamen. Verweise auf andere Tabellen stehen als lesbare Bezeichnung, nicht als Schlüssel.
        - Datumswerte als TT.MM.JJJJ, Zeitpunkte in deutscher Ortszeit (Europe/Berlin) als TT.MM.JJJJ HH:MM.
        - „Stammdaten“ bzw. „Jahresabschluss“ ist der Datensatz selbst; die übrigen Dateien sind Tabellen, die auf ihn verweisen
          (z. B. Mitgliedschaften, Dienstgrade, Lehrgänge eines Mitglieds; Fahrzeuge und Beteiligte eines Einsatzes; Buchungen eines Kassenjahrs).
        - Gesundheitsdaten (Atemschutz-Vorsorge) enthalten nur Status und Frist, keine Befunde.
        """;
}
