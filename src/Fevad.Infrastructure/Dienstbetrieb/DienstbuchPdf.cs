using System.Globalization;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Auswertungen;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>Dienstbuch des Jahres als PDF (V53): alle durchgeführten Ausbildungsdienste mit Thema, Übungsleiter, Anwesenden und Stunden, ohne Namen der Teilnehmer.</summary>
public static class DienstbuchPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static DienstbuchPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt das Dienstbuch quer auf A4.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="dienste">Durchgeführte Dienste.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, int jahr, IReadOnlyList<Ausbildungsplaene.Planzeile> dienste, string erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Size(PageSizes.A4.Landscape());
            seite.DefaultTextStyle(t => t.FontSize(9));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, $"Dienstbuch {jahr}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text($"{dienste.Count} durchgeführte Ausbildungsdienste, {dienste.Sum(d => d.Anwesend)} Teilnahmen, "
                    + $"{dienste.Sum(d => d.Personenstunden).ToString("0.#", Deutsch)} Personenstunden.");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);
                        c.ConstantColumn(62);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(1.2f);
                        c.ConstantColumn(50);
                        c.ConstantColumn(60);
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "Datum", "Zeit", "Art", "Titel", "Thema", "Übungsleiter", "Anwesend", "Pers.-Std." })
                        {
                            h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
                        }
                    });
                    foreach (var d in dienste)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingRight(4);
                        var beginn = TimeZoneInfo.ConvertTime(d.Beginn, Ortszeit.Berlin);
                        var ende = TimeZoneInfo.ConvertTime(d.Ende, Ortszeit.Berlin);
                        t.Cell().Element(Zelle).Text(beginn.ToString("dd.MM.yyyy", Deutsch));
                        t.Cell().Element(Zelle).Text($"{beginn:HH\\:mm}–{ende:HH\\:mm}");
                        t.Cell().Element(Zelle).Text(JahresberichtPdf.DienstArtText(d.Art));
                        t.Cell().Element(Zelle).Text(d.Titel);
                        t.Cell().Element(Zelle).Text(d.Thema ?? "");
                        t.Cell().Element(Zelle).Text(d.Leiter ?? "");
                        t.Cell().Element(Zelle).AlignRight().Text(d.Anwesend.ToString(Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(d.Personenstunden.ToString("0.#", Deutsch));
                    }
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Erstellt am {erstellt} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
