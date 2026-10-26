using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Lieferschein für die Abgabe an eine fremde Werkstatt (V61): Geräte mit Inventar- und Seriennummer, Spalten für die Werkstatt
/// zum Abhaken (gereinigt, geprüft, gefüllt) und Unterschriften für Abgabe und Rückgabe.
/// </summary>
public static class LieferscheinPdf
{
    static LieferscheinPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Ein Gerät auf dem Lieferschein.</summary>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Gerätebezeichnung.</param>
    /// <param name="Seriennummer">Seriennummer des Herstellers.</param>
    /// <param name="Hinweis">Hinweis für die Werkstatt, z. B. „nach Einsatz“ oder „Prüfung fällig“.</param>
    public sealed record Zeile(string Inventarnummer, string Bezeichnung, string? Seriennummer, string? Hinweis);

    /// <summary>Alles, was der Lieferschein braucht.</summary>
    /// <param name="Kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="Nummer">Auftragsnummer.</param>
    /// <param name="Werkstatt">Name der Werkstatt.</param>
    /// <param name="AbgegebenAm">Tag der Abgabe.</param>
    /// <param name="AbgegebenVon">Name der abgebenden Person.</param>
    /// <param name="Bemerkung">Bemerkung zum Auftrag.</param>
    /// <param name="Zeilen">Die Geräte.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Nummer, string Werkstatt, DateOnly AbgegebenAm, string? AbgegebenVon, string? Bemerkung, IReadOnlyList<Zeile> Zeilen);

    /// <summary>Dateiname des Lieferscheins.</summary>
    public static string Dateiname(Daten d) => $"Lieferschein-{d.Nummer}.pdf";

    /// <summary>Erzeugt das PDF.</summary>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Lieferschein {d.Nummer}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span("An: ").SemiBold();
                    t.Span(d.Werkstatt);
                });
                spalte.Item().Text(t =>
                {
                    t.Span("Abgegeben am: ").SemiBold();
                    t.Span($"{d.AbgegebenAm:dd.MM.yyyy}");
                    if (d.AbgegebenVon is not null)
                    {
                        t.Span($" durch {d.AbgegebenVon}");
                    }
                });
                if (d.Bemerkung is not null)
                {
                    spalte.Item().Text(d.Bemerkung);
                }

                spalte.Item().Text($"{d.Zeilen.Count} Geräte. Bitte je Gerät abhaken und das Werkstattprotokoll beilegen.").FontSize(9);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(22);
                        c.ConstantColumn(75);
                        c.RelativeColumn();
                        c.ConstantColumn(70);
                        c.ConstantColumn(38);
                        c.ConstantColumn(38);
                        c.ConstantColumn(38);
                    });
                    t.Header(h =>
                    {
                        foreach (var titel in new[] { "Nr.", "Inventar-Nr.", "Gerät", "Serien-Nr.", "gerein.", "geprüft", "gefüllt" })
                        {
                            h.Cell().BorderBottom(1).PaddingBottom(2).Text(titel).SemiBold().FontSize(8);
                        }
                    });
                    var nummer = 0;
                    foreach (var z in d.Zeilen)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                        t.Cell().Element(Zelle).Text($"{++nummer}").FontSize(9);
                        t.Cell().Element(Zelle).Text(z.Inventarnummer).SemiBold();
                        t.Cell().Element(Zelle).Text(text =>
                        {
                            text.Span(z.Bezeichnung);
                            if (z.Hinweis is not null)
                            {
                                text.Span($" ({z.Hinweis})").FontSize(8).FontColor(Colors.Grey.Darken2);
                            }
                        });
                        t.Cell().Element(Zelle).Text(z.Seriennummer ?? "").FontSize(9);
                        for (var i = 0; i < 3; i++)
                        {
                            t.Cell().Element(Zelle).AlignCenter().Width(10).Height(10).Border(0.75f);
                        }
                    }
                });

                spalte.Item().PaddingTop(20).Row(zeile =>
                {
                    foreach (var titel in new[] { "Übergeben (Feuerwehr)", "Übernommen (Werkstatt)", "Zurück erhalten (Feuerwehr)" })
                    {
                        zeile.RelativeItem().PaddingRight(10).Column(c =>
                        {
                            c.Item().Height(28).BorderBottom(0.75f);
                            c.Item().Text($"Datum, Unterschrift: {titel}").FontSize(8);
                        });
                    }
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Lieferschein {d.Nummer} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
