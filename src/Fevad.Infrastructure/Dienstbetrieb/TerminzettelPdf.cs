using System.Globalization;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Auswertungen;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Terminzettel auf Papier (V41): die kommenden Termine eines Mitglieds für alle, die kein Smartphone nutzen. Der Papierweg ist
/// nur ein zusätzlicher Druck; alle Funktionen bleiben in FEVAD. Zeiten in Ortszeit, 24-Stunden-Format.
/// </summary>
public static class TerminzettelPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static TerminzettelPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt den Terminzettel hochkant auf A4.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <param name="fuer">Name des Mitglieds.</param>
    /// <param name="zeitraum">Zeitraum in Worten, z. B. „01.11.2026 bis 31.01.2027“.</param>
    /// <param name="termine">Die Termine, nach Beginn sortiert.</param>
    /// <param name="hinweis">Hinweis unter der Liste, z. B. wie man zu- oder absagt.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, string fuer, string zeitraum, IReadOnlyList<Dienst> termine, string? hinweis, string erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(11));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, "Termine"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span("Für ").FontSize(12);
                    t.Span(fuer).FontSize(12).SemiBold();
                    t.Span($" · {zeitraum}").FontSize(12);
                });
                if (termine.Count == 0)
                {
                    spalte.Item().Text("In diesem Zeitraum stehen keine Termine an.");
                    return;
                }

                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(78);
                        c.ConstantColumn(78);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.3f);
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "Tag", "Zeit", "Termin", "Ort" })
                        {
                            h.Cell().BorderBottom(0.5f).PaddingVertical(3).PaddingRight(4).Text(s).SemiBold();
                        }
                    });
                    foreach (var d in termine)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingRight(4);
                        var beginn = TimeZoneInfo.ConvertTime(d.Beginn, Ortszeit.Berlin);
                        var ende = TimeZoneInfo.ConvertTime(d.Ende, Ortszeit.Berlin);
                        t.Cell().Element(Zelle).Text(beginn.ToString("ddd dd.MM.", Deutsch));
                        t.Cell().Element(Zelle).Text($"{beginn:HH\\:mm}–{ende:HH\\:mm} Uhr");
                        t.Cell().Element(Zelle).Text(x =>
                        {
                            x.Span(d.Titel).SemiBold();
                            x.Span($" ({JahresberichtPdf.DienstArtText(d.Art)})");
                            if (d.Status == DienstStatus.Abgesagt)
                            {
                                x.Span(" – abgesagt").FontColor(Colors.Red.Darken2);
                            }

                            if (!string.IsNullOrWhiteSpace(d.Thema))
                            {
                                x.Line("");
                                x.Span(d.Thema).FontSize(10);
                            }
                        });
                        t.Cell().Element(Zelle).Text(d.Ort ?? "");
                    }
                });
                if (!string.IsNullOrWhiteSpace(hinweis))
                {
                    spalte.Item().PaddingTop(6).Text(hinweis).FontSize(10);
                }
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Stand {erstellt} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();
}
