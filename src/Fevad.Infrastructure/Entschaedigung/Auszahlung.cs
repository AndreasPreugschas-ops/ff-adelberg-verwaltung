using System.Globalization;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Kassen;
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
        })).AlsPdf();

    /// <summary>Summe einer Postenart auf dem Summenblatt.</summary>
    /// <param name="Art">Postenart als Text, z. B. „Einsatz“ oder „Funktionspauschale“.</param>
    /// <param name="Posten">Anzahl der Posten.</param>
    /// <param name="Betrag">Summe in Euro.</param>
    public sealed record Artsumme(string Art, int Posten, decimal Betrag);

    /// <summary>Angaben für das Summenblatt zur Kassenanordnung.</summary>
    /// <param name="Liste">Empfänger mit Beträgen, ohne IBAN.</param>
    /// <param name="Gemeinde">Name der Gemeinde, die anordnet und zahlt.</param>
    /// <param name="Buchungsstelle">Haushaltsstelle aus den Parametern, z. B. Produkt und Sachkonto.</param>
    /// <param name="Rechtsgrundlage">Satzung, nach der abgerechnet wurde.</param>
    /// <param name="JeArt">Summen je Postenart.</param>
    public sealed record Summenblatt(Liste Liste, string Gemeinde, string? Buchungsstelle, string Rechtsgrundlage, IReadOnlyList<Artsumme> JeArt);

    /// <summary>Dateiname des Summenblatts.</summary>
    /// <param name="a">Die Abrechnung.</param>
    /// <returns>Der Dateiname.</returns>
    public static string SummenblattDateiname(Abrechnung a) => $"Kassenanordnung_Entschaedigung_{a.Von:yyyy-MM-dd}_{a.Bis:yyyy-MM-dd}.pdf";

    /// <summary>
    /// Summenblatt als Anlage zur Kassenanordnung der Gemeinde (GemKVO): Summen je Postenart und je Person mit Personalnummer, darunter
    /// „sachlich und rechnerisch richtig“ (Feuerwehrkommandant) und „angeordnet“ (Bürgermeister oder Kämmerei). Ohne Bankverbindungen.
    /// </summary>
    /// <param name="b">Inhalt des Summenblatts.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] SummenblattPdf(Summenblatt b) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var l = b.Liste;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, l.Kopf, "Anlage zur Kassenanordnung: Entschädigung der Feuerwehrangehörigen"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span("An: ").SemiBold();
                    t.Span(b.Gemeinde + ", Gemeindekasse");
                });
                spalte.Item().Text($"{l.Abrechnung.Bezeichnung}, Zeitraum {l.Abrechnung.Von:dd.MM.yyyy} bis {l.Abrechnung.Bis:dd.MM.yyyy}"
                    + (l.Abrechnung.AbgeschlossenAm is { } am ? $", abgeschlossen am {TimeZoneInfo.ConvertTime(am, Ortszeit.Berlin):dd.MM.yyyy}" : ", ENTWURF"));
                spalte.Item().Text($"Rechtsgrundlage: § 16 FwG, {b.Rechtsgrundlage}").FontSize(9);
                if (!string.IsNullOrWhiteSpace(b.Buchungsstelle))
                {
                    spalte.Item().Text($"Buchungsstelle: {b.Buchungsstelle}").FontSize(9);
                }

                spalte.Item().PaddingTop(4).Text("Summen je Art").SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(60);
                        c.ConstantColumn(90);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Art").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Posten").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                    });
                    foreach (var a in b.JeArt)
                    {
                        t.Cell().PaddingVertical(1.5f).Text(a.Art);
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(a.Posten.ToString(Deutsch));
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(a.Betrag));
                    }
                });

                spalte.Item().PaddingTop(4).Text("Auszahlung je Person").SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.ConstantColumn(90);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Pers.-Nr.").SemiBold();
                        h.Cell().BorderBottom(0.5f).Text("Name").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                    });
                    foreach (var e in l.Empfaenger)
                    {
                        t.Cell().PaddingVertical(1.5f).Text(e.Personalnummer);
                        t.Cell().PaddingVertical(1.5f).Text(e.Name);
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(e.Betrag));
                    }

                    t.Cell().ColumnSpan(2).BorderTop(0.5f).PaddingTop(2).Text($"Gesamtsumme ({l.Empfaenger.Count} Empfänger)").SemiBold();
                    t.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Euro(l.Empfaenger.Sum(e => e.Betrag))).SemiBold();
                });

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Feld(string titel, string unter) => r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(titel).SemiBold();
                        c.Item().PaddingTop(24).BorderBottom(0.5f).Height(2);
                        c.Item().PaddingTop(2).Text(unter).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });
                    Feld("Sachlich und rechnerisch richtig", "Datum, Feuerwehrkommandant");
                    r.ConstantItem(30);
                    Feld("Angeordnet", "Datum, Bürgermeister oder Kämmerei");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();

    /// <summary>
    /// SEPA-Überweisung (pain.001.001.09) für alle Empfänger mit IBAN und Betrag über 0. Verwendungszweck und Ende-zu-Ende-Kennung
    /// tragen die Personalnummer (Schlüssel der Kämmerei in SAP). Wirft <see cref="InvalidOperationException"/>, wenn niemand
    /// überwiesen werden kann oder die Auftraggeber-IBAN ungültig ist.
    /// </summary>
    /// <param name="l">Inhalt der Liste.</param>
    /// <param name="auftraggeber">Name des Auftraggebers.</param>
    /// <param name="auftraggeberIban">IBAN des belasteten Kontos.</param>
    /// <param name="ausfuehrung">Gewünschter Ausführungstag.</param>
    /// <param name="jetzt">Zeitpunkt der Erstellung.</param>
    /// <returns>Die XML-Datei als Bytes.</returns>
    public static byte[] Sepa(Liste l, string auftraggeber, string auftraggeberIban, DateOnly ausfuehrung, DateTimeOffset jetzt)
    {
        var posten = l.Empfaenger.Where(e => e.Iban is not null && e.Betrag > 0)
            .Select(e => new SepaUeberweisung.Posten(e.Kontoinhaber ?? e.Name, e.Iban!, e.Betrag,
                $"Entschaedigung {l.Abrechnung.Bezeichnung} PNr {e.Personalnummer}", SepaUeberweisung.Referenz(e.Personalnummer, l.Abrechnung.Id)))
            .ToList();
        if (posten.Count == 0)
        {
            throw new InvalidOperationException("Niemand in der Liste hat eine Bankverbindung.");
        }

        return SepaUeberweisung.Erzeugen(posten, auftraggeber, auftraggeberIban, ausfuehrung, jetzt);
    }
}
