using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>Auskunft nach Art. 15 DSGVO zum Ausdrucken oder Weitergeben, mit denselben Abschnitten wie die Seite „Meine Daten“.</summary>
public static class SelbstauskunftPdf
{
    static SelbstauskunftPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Ein Abschnitt mit bereits für die Anzeige aufbereiteten Werten.</summary>
    public sealed record Abschnitt(string Titel, IReadOnlyList<string> Spalten, IReadOnlyList<IReadOnlyList<string>> Zeilen);

    /// <summary>Inhalt des PDFs.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Name">Name der auskunftsberechtigten Person.</param>
    /// <param name="Erstellt">Zeitpunkt der Erstellung.</param>
    /// <param name="Abschnitte">Die Abschnitte der Auskunft.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Name, DateTimeOffset Erstellt, IReadOnlyList<Abschnitt> Abschnitte);

    /// <summary>Ohne Namen im Dateinamen, damit Downloads-Ordner und Mailanhänge nichts verraten.</summary>
    public static string Dateiname(DateTimeOffset erstellt) =>
        $"fevad-auskunft-{TimeZoneInfo.ConvertTime(erstellt, Ortszeit.Berlin):yyyy-MM-dd}.pdf";

    /// <summary>Erzeugt die Auskunft „Meine Daten“ als PDF.</summary>
    /// <param name="d">Inhalt des PDFs.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            // Quer, weil manche Abschnitte viele Spalten haben.
            seite.Size(PageSizes.A4.Landscape());
            seite.DefaultTextStyle(t => t.FontSize(8.5f));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Auskunft nach Art. 15 DSGVO"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text(t =>
                {
                    t.Span("Gespeicherte Daten zu ");
                    t.Span(d.Name).SemiBold();
                    t.Span($", Stand {TimeZoneInfo.ConvertTime(d.Erstellt, Ortszeit.Berlin):dd.MM.yyyy, HH:mm} Uhr. ");
                    t.Span("Wenn etwas nicht stimmt, wende dich an den Kommandanten. Fragen zum Datenschutz beantwortet der Datenschutzbeauftragte der Gemeinde.");
                });

                foreach (var a in d.Abschnitte)
                {
                    spalte.Item().PaddingTop(6).Text($"{a.Titel} ({a.Zeilen.Count})").FontSize(11).SemiBold();
                    if (a.Zeilen.Count == 0)
                    {
                        spalte.Item().Text("keine Einträge").FontColor(Colors.Grey.Darken1);
                        continue;
                    }

                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            foreach (var _ in a.Spalten)
                            {
                                c.RelativeColumn();
                            }
                        });
                        t.Header(h =>
                        {
                            foreach (var s in a.Spalten)
                            {
                                h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
                            }
                        });
                        foreach (var zeile in a.Zeilen)
                        {
                            foreach (var wert in zeile)
                            {
                                t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(1.5f).PaddingRight(4).Text(wert);
                            }
                        }
                    });
                }
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Vertraulich: personenbezogene Daten · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();
}
