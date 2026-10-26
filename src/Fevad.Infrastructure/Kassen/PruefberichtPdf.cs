using System.Globalization;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Prüfbericht der Rechnungsprüfer (V05): geprüfter Zeitraum, Zahlen der Kasse, Beanstandungen mit Antworten, Ergebnis und Empfehlung
/// zur Entlastung, Unterschriftsfelder beider Prüfer. Grundlage für den Bericht in der Hauptversammlung.
/// </summary>
public static class PruefberichtPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static PruefberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Inhalt des Berichts.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Kasse">Name der Kasse.</param>
    /// <param name="Pruefung">Die Prüfung mit Prüfern und Beanstandungen (samt Buchung).</param>
    /// <param name="Einnahmen">Einnahmen im Zeitraum.</param>
    /// <param name="Ausgaben">Ausgaben im Zeitraum.</param>
    /// <param name="Buchungen">Zahl der Buchungen im Zeitraum.</param>
    /// <param name="Konten">Kontostände am Ende des Zeitraums.</param>
    /// <param name="Ausnahmen">Zahl der Ausnahmebuchungen im Zeitraum.</param>
    /// <param name="Bestaetigt1">Bestätigung des ersten Prüfers in Ortszeit, z. B. „12.02.2027, 19:30 Uhr“.</param>
    /// <param name="Bestaetigt2">Bestätigung des zweiten Prüfers in Ortszeit.</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, string Kasse, Kassenpruefung Pruefung, decimal Einnahmen, decimal Ausgaben, int Buchungen,
        IReadOnlyList<(string Konto, decimal Bestand)> Konten, int Ausnahmen, string? Bestaetigt1, string? Bestaetigt2);

    /// <summary>Dateiname aus Kasse und Zeitraum.</summary>
    /// <param name="d">Inhalt des Berichts.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Daten d) =>
        $"Prüfbericht {string.Concat(d.Kasse.Select(z => char.IsLetterOrDigit(z) || z is ' ' or '-' ? z : '_')).Trim()} {d.Pruefung.Von:yyyy-MM-dd} bis {d.Pruefung.Bis:yyyy-MM-dd}.pdf";

    /// <summary>Erzeugt den Bericht als PDF.</summary>
    /// <param name="d">Inhalt des Berichts.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var p = d.Pruefung;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Bericht der Rechnungsprüfer: {d.Kasse}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                    void Zeile(string etikett, string wert)
                    {
                        t.Cell().PaddingVertical(1).Text(etikett).FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(1).Text(wert).SemiBold();
                    }

                    Zeile("Geprüfter Zeitraum", $"{p.Von.ToString("dd.MM.yyyy", Deutsch)} bis {p.Bis.ToString("dd.MM.yyyy", Deutsch)}");
                    Zeile("Geprüft am", p.GeprueftAm?.ToString("dd.MM.yyyy", Deutsch) ?? "–");
                    Zeile("Rechnungsprüfer", $"{p.Pruefer1?.Anzeigename} und {p.Pruefer2?.Anzeigename}");
                    Zeile("Buchungen im Zeitraum", d.Buchungen.ToString(Deutsch) + (d.Ausnahmen > 0 ? $", davon {d.Ausnahmen} Ausnahmebuchungen nach dem Abschluss" : ""));
                    Zeile("Einnahmen", Euro(d.Einnahmen));
                    Zeile("Ausgaben", Euro(d.Ausgaben));
                    foreach (var (konto, bestand) in d.Konten)
                    {
                        Zeile($"Bestand {konto} am {p.Bis.ToString("dd.MM.", Deutsch)}", Euro(bestand));
                    }
                });

                spalte.Item().PaddingTop(4).Text("Beanstandungen").FontSize(12).SemiBold();
                if (p.Beanstandungen.Count == 0)
                {
                    spalte.Item().Text("Keine.");
                }

                var nummer = 0;
                foreach (var b in p.Beanstandungen.OrderBy(b => b.ErstelltAm))
                {
                    nummer++;
                    spalte.Item().Column(c =>
                    {
                        c.Item().Text($"{nummer}. {(b.Buchung is { } buchung ? $"Beleg {buchung.Kennung}: " : "")}{b.Text}");
                        c.Item().PaddingLeft(12).Text($"Antwort des Kassenwarts: {b.Antwort ?? "keine"}").FontColor(Colors.Grey.Darken2);
                        c.Item().PaddingLeft(12).Text(b.Erledigt ? "ausgeräumt" : "offen").FontColor(b.Erledigt ? Colors.Green.Darken2 : Colors.Red.Darken2);
                    });
                }

                spalte.Item().PaddingTop(4).Text("Ergebnis").FontSize(12).SemiBold();
                spalte.Item().Text(string.IsNullOrWhiteSpace(p.Ergebnis) ? "–" : p.Ergebnis);
                spalte.Item().Text(p.EntlastungEmpfohlen
                    ? "Die Rechnungsprüfer empfehlen der Hauptversammlung, dem Kassenwart Entlastung zu erteilen."
                    : "Eine Empfehlung zur Entlastung wird nicht ausgesprochen.").SemiBold();

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Unterschrift(string text) => r.RelativeItem().Column(c =>
                    {
                        c.Item().BorderBottom(0.75f).Height(24);
                        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });

                    Unterschrift($"Rechnungsprüfer: {p.Pruefer1?.Anzeigename}{(d.Bestaetigt1 is null ? "" : $" (in FEVAD bestätigt {d.Bestaetigt1})")}");
                    r.ConstantItem(20);
                    Unterschrift($"Rechnungsprüfer: {p.Pruefer2?.Anzeigename}{(d.Bestaetigt2 is null ? "" : $" (in FEVAD bestätigt {d.Bestaetigt2})")}");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"{d.Kasse} · Prüfbericht{(p.Abgeschlossen ? "" : " (Entwurf)")} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);
}
