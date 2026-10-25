using System.Globalization;
using System.Xml.Linq;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Entschaedigung;

/// <summary>
/// Auszahlungsliste einer abgeschlossenen Abrechnung als PDF und, für Kassier und Kommandant, als SEPA-Überweisungsdatei
/// (pain.001.001.09) zum Hochladen ins Onlinebanking. Keine Lastschrift. Die IBAN erscheint nur, wenn der Aufrufer das Recht hat.
/// </summary>
public static class Auszahlung
{
    static Auszahlung()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Empfänger einer Auszahlung.</summary>
    /// <param name="Iban">Normalisiert; null ohne Bankverbindung oder ohne Recht.</param>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Name">Name des Mitglieds.</param>
    /// <param name="Kontoinhaber">Abweichender Kontoinhaber.</param>
    /// <param name="Betrag">Auszuzahlender Betrag in Euro.</param>
    public sealed record Empfaenger(string Personalnummer, string Name, string? Kontoinhaber, string? Iban, decimal Betrag);

    /// <summary>Inhalt der Auszahlungsliste.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Abrechnung">Die abgeschlossene Abrechnung.</param>
    /// <param name="Empfaenger">Empfänger mit Beträgen.</param>
    /// <param name="MitIban">Die IBAN-Spalte wird gezeigt (nur mit Recht auf Bankdaten).</param>
    public sealed record Liste(AntragPdf.Kopfdaten Kopf, Abrechnung Abrechnung, IReadOnlyList<Empfaenger> Empfaenger, bool MitIban);

    /// <summary>Dateiname mit Abrechnungszeitraum.</summary>
    /// <param name="a">Die Abrechnung.</param>
    /// <param name="endung">Dateiendung ohne Punkt.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Abrechnung a, string endung) => $"Auszahlung_{a.Von:yyyy-MM-dd}_{a.Bis:yyyy-MM-dd}.{endung}";

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    /// <summary>Erzeugt die Auszahlungsliste als PDF.</summary>
    /// <param name="l">Inhalt der Liste.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Pdf(Liste l) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, l.Kopf, "Auszahlungsliste Entschädigung"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"{l.Abrechnung.Bezeichnung}, {l.Abrechnung.Von:dd.MM.yyyy} bis {l.Abrechnung.Bis:dd.MM.yyyy}"
                    + (l.Abrechnung.AbgeschlossenAm is { } am ? $", abgeschlossen am {TimeZoneInfo.ConvertTime(am, Ortszeit.Berlin):dd.MM.yyyy}" : ", ENTWURF"));
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(60);
                        c.RelativeColumn(3);
                        if (l.MitIban)
                        {
                            c.RelativeColumn(4);
                        }

                        c.ConstantColumn(80);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Pers.-Nr.").SemiBold();
                        h.Cell().BorderBottom(0.5f).Text("Name").SemiBold();
                        if (l.MitIban)
                        {
                            h.Cell().BorderBottom(0.5f).Text("Kontoinhaber, IBAN").SemiBold();
                        }

                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                    });
                    foreach (var e in l.Empfaenger)
                    {
                        t.Cell().PaddingVertical(1.5f).Text(e.Personalnummer);
                        t.Cell().PaddingVertical(1.5f).Text(e.Name);
                        if (l.MitIban)
                        {
                            t.Cell().PaddingVertical(1.5f).Text(e.Iban is null ? "keine Bankverbindung" : $"{e.Kontoinhaber ?? e.Name}, {Iban.Formatieren(e.Iban)}");
                        }

                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(e.Betrag));
                    }

                    t.Cell().ColumnSpan(l.MitIban ? 3u : 2u).BorderTop(0.5f).PaddingTop(2).Text($"Summe ({l.Empfaenger.Count} Empfänger)").SemiBold();
                    t.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Euro(l.Empfaenger.Sum(e => e.Betrag))).SemiBold();
                });

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Unterschrift(string text) => r.RelativeItem().Column(c =>
                    {
                        c.Item().BorderBottom(0.5f).Height(20);
                        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });
                    Unterschrift("Ort, Datum");
                    r.ConstantItem(20);
                    Unterschrift("Feuerwehrkommandant");
                    r.ConstantItem(20);
                    Unterschrift("sachlich richtig / angewiesen");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span(l.MitIban ? "Vertraulich: enthält Bankverbindungen · Seite " : "Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    /// <summary>
    /// SEPA-Überweisung (pain.001.001.09) für alle Empfänger mit IBAN und Betrag über 0. Wirft <see cref="InvalidOperationException"/>,
    /// wenn niemand überwiesen werden kann oder die Auftraggeber-IBAN ungültig ist.
    /// </summary>
    public static byte[] Sepa(Liste l, string auftraggeber, string auftraggeberIban, DateOnly ausfuehrung, DateTimeOffset jetzt)
    {
        var iban = Iban.Normalisieren(auftraggeberIban) ?? throw new InvalidOperationException("Die IBAN des Auftraggebers ist ungültig.");
        if (string.IsNullOrWhiteSpace(auftraggeber))
        {
            throw new InvalidOperationException("Den Namen des Auftraggebers angeben.");
        }

        var posten = l.Empfaenger.Where(e => e.Iban is not null && e.Betrag > 0).ToList();
        if (posten.Count == 0)
        {
            throw new InvalidOperationException("Niemand in der Liste hat eine Bankverbindung.");
        }

        XNamespace ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.09";
        var kennung = $"FEVAD{jetzt:yyyyMMddHHmmss}";
        string Betrag(decimal b) => b.ToString("0.00", CultureInfo.InvariantCulture);
        var summe = posten.Sum(p => p.Betrag);
        var zweck = Text($"Entschaedigung {l.Abrechnung.Bezeichnung}", 140);
        var dokument = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "CstmrCdtTrfInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", kennung),
                        new XElement(ns + "CreDtTm", jetzt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                        new XElement(ns + "NbOfTxs", posten.Count),
                        new XElement(ns + "CtrlSum", Betrag(summe)),
                        new XElement(ns + "InitgPty", new XElement(ns + "Nm", Text(auftraggeber, 70)))),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", kennung + "-1"),
                        new XElement(ns + "PmtMtd", "TRF"),
                        new XElement(ns + "NbOfTxs", posten.Count),
                        new XElement(ns + "CtrlSum", Betrag(summe)),
                        new XElement(ns + "PmtTpInf", new XElement(ns + "SvcLvl", new XElement(ns + "Cd", "SEPA"))),
                        new XElement(ns + "ReqdExctnDt", new XElement(ns + "Dt", ausfuehrung.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                        new XElement(ns + "Dbtr", new XElement(ns + "Nm", Text(auftraggeber, 70))),
                        new XElement(ns + "DbtrAcct", new XElement(ns + "Id", new XElement(ns + "IBAN", iban))),
                        new XElement(ns + "DbtrAgt", new XElement(ns + "FinInstnId", new XElement(ns + "Othr", new XElement(ns + "Id", "NOTPROVIDED")))),
                        new XElement(ns + "ChrgBr", "SLEV"),
                        posten.Select((p, i) => new XElement(ns + "CdtTrfTxInf",
                            new XElement(ns + "PmtId", new XElement(ns + "EndToEndId", $"{kennung}-{i + 1}")),
                            new XElement(ns + "Amt", new XElement(ns + "InstdAmt", new XAttribute("Ccy", "EUR"), Betrag(p.Betrag))),
                            new XElement(ns + "Cdtr", new XElement(ns + "Nm", Text(p.Kontoinhaber ?? p.Name, 70))),
                            new XElement(ns + "CdtrAcct", new XElement(ns + "Id", new XElement(ns + "IBAN", p.Iban))),
                            new XElement(ns + "RmtInf", new XElement(ns + "Ustrd", zweck))))))));
        using var strom = new MemoryStream();
        dokument.Save(strom);
        return strom.ToArray();
    }

    /// <summary>SEPA erlaubt nur einen eingeschränkten Zeichensatz; Umlaute werden umschrieben, der Rest durch Leerzeichen ersetzt.</summary>
    public static string Text(string text, int laenge)
    {
        var umschrieben = text.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");
        var erlaubt = new string(umschrieben.Select(c => char.IsAsciiLetterOrDigit(c) || "/-?:().,'+ ".Contains(c) ? c : ' ').ToArray()).Trim();
        return erlaubt.Length > laenge ? erlaubt[..laenge] : erlaubt;
    }
}
