using Fevad.Domain.Technik;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Inventurprotokoll zur Stichtagsinventur (V60, GemHVO § 37): Kopf mit Stichtag, Zähler und Prüfer,
/// Summen je Befund, Fehl- und Schadensliste und alle Positionen; am Ende Unterschriftszeilen.
/// </summary>
public static class InventurprotokollPdf
{
    static InventurprotokollPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Eine gezählte oder noch offene Position.</summary>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Gerätebezeichnung.</param>
    /// <param name="SollStandort">Standort laut Bestand beim Anlegen der Inventur.</param>
    /// <param name="Befund">Befund; leer heißt nicht erfasst.</param>
    /// <param name="Bemerkung">Bemerkung des Zählers.</param>
    /// <param name="Anschaffungswert">Anschaffungswert brutto, für die Summe der Fehlbestände.</param>
    public sealed record Position(string Inventarnummer, string Bezeichnung, string? SollStandort, InventurBefund? Befund, string? Bemerkung, decimal? Anschaffungswert);

    /// <summary>Alles, was das Protokoll braucht.</summary>
    /// <param name="Kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="Bezeichnung">Bezeichnung der Inventur.</param>
    /// <param name="Stichtag">Stichtag der Inventur.</param>
    /// <param name="Bereich">Inventurgruppe oder „ganzer Bestand“.</param>
    /// <param name="Zaehler">Wer gezählt hat.</param>
    /// <param name="Pruefer">Wer geprüft hat.</param>
    /// <param name="AbgeschlossenAm">Abschluss in Ortszeit; leer bei laufender Inventur (Entwurf).</param>
    /// <param name="Stand">Erstellungszeitpunkt in Ortszeit.</param>
    /// <param name="Positionen">Alle Positionen.</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf,
        string Bezeichnung,
        DateOnly Stichtag,
        string Bereich,
        string? Zaehler,
        string? Pruefer,
        DateTime? AbgeschlossenAm,
        DateTime Stand,
        IReadOnlyList<Position> Positionen);

    /// <summary>Dateiname mit Stichtag.</summary>
    public static string Dateiname(Daten d) => $"Inventurprotokoll-{d.Stichtag:yyyy-MM-dd}.pdf";

    /// <summary>Text eines Befunds im Protokoll.</summary>
    public static string BefundText(InventurBefund? befund) => befund switch
    {
        InventurBefund.Vorhanden => "vorhanden",
        InventurBefund.Fehlt => "fehlt",
        InventurBefund.Beschaedigt => "beschädigt",
        null => "nicht erfasst",
        _ => befund.ToString()!,
    };

    /// <summary>Erzeugt das Protokoll.</summary>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, d.AbgeschlossenAm is null ? "Inventurprotokoll (Entwurf)" : "Inventurprotokoll"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(10);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(110);
                        c.RelativeColumn();
                    });
                    void Feld(string name, string wert)
                    {
                        t.Cell().PaddingVertical(1).Text(name).FontColor(Colors.Grey.Darken2);
                        t.Cell().PaddingVertical(1).Text(wert);
                    }

                    Feld("Inventur", d.Bezeichnung);
                    Feld("Stichtag", d.Stichtag.ToString("dd.MM.yyyy"));
                    Feld("Bereich", d.Bereich);
                    Feld("Gezählt von", d.Zaehler ?? "—");
                    Feld("Geprüft von", d.Pruefer ?? "—");
                    Feld("Abgeschlossen", d.AbgeschlossenAm is { } a ? $"{a:dd.MM.yyyy, HH:mm} Uhr" : "noch nicht abgeschlossen");
                });

                int Anzahl(InventurBefund? b) => d.Positionen.Count(p => p.Befund == b);
                spalte.Item().Text(text =>
                {
                    text.Span($"{d.Positionen.Count} Positionen: ").SemiBold();
                    text.Span(string.Join(", ", new InventurBefund?[] { InventurBefund.Vorhanden, InventurBefund.Beschaedigt, InventurBefund.Fehlt, null }
                        .Where(b => Anzahl(b) > 0)
                        .Select(b => $"{Anzahl(b)} {BefundText(b)}")));
                });

                var abweichungen = d.Positionen.Where(p => p.Befund is not InventurBefund.Vorhanden).ToList();
                if (abweichungen.Count > 0)
                {
                    var fehlwert = abweichungen.Where(p => p.Befund == InventurBefund.Fehlt).Sum(p => p.Anschaffungswert ?? 0);
                    spalte.Item().Text("Abweichungen").SemiBold().FontSize(11);
                    spalte.Item().Element(c => Tabelle(c, abweichungen));
                    if (fehlwert > 0)
                    {
                        spalte.Item().Text($"Anschaffungswert der fehlenden Geräte: {fehlwert.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} €");
                    }
                }

                spalte.Item().Text("Alle Positionen").SemiBold().FontSize(11);
                spalte.Item().Element(c => Tabelle(c, d.Positionen));

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    foreach (var rolle in new[] { "Zähler", "Prüfer" })
                    {
                        r.RelativeItem().PaddingRight(20).Column(c =>
                        {
                            c.Item().Height(24);
                            c.Item().BorderTop(0.75f).PaddingTop(2).Text($"Datum, Unterschrift {rolle}").FontSize(8);
                        });
                    }
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Stand {d.Stand:dd.MM.yyyy, HH:mm} Uhr · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();

    private static void Tabelle(IContainer container, IEnumerable<Position> positionen) =>
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(75);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.ConstantColumn(80);
                c.RelativeColumn(2);
            });
            t.Header(h =>
            {
                foreach (var titel in new[] { "Inventar-Nr.", "Gerät", "Soll-Standort", "Befund", "Bemerkung" })
                {
                    h.Cell().BorderBottom(1).PaddingBottom(2).Text(titel).SemiBold().FontSize(9);
                }
            });
            foreach (var p in positionen)
            {
                IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                t.Cell().Element(Zelle).Text(p.Inventarnummer).FontSize(9);
                t.Cell().Element(Zelle).Text(p.Bezeichnung).FontSize(9);
                t.Cell().Element(Zelle).Text(p.SollStandort ?? "").FontSize(9);
                t.Cell().Element(Zelle).Text(BefundText(p.Befund)).FontSize(9);
                t.Cell().Element(Zelle).Text(p.Bemerkung ?? "").FontSize(9);
            }
        });
}
