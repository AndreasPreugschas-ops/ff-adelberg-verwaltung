using System.Globalization;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Gremien;

/// <summary>Serienbrief als PDF (V56): je Empfänger eine Seite, als Brief mit Briefkopf und Anschrift oder als Urkunde.</summary>
public static class SerienbriefPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static SerienbriefPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt das PDF.</summary>
    /// <param name="kopf">Briefkopf mit Logo.</param>
    /// <param name="form">Brief oder Urkunde.</param>
    /// <param name="schreiben">Die gefüllten Schreiben.</param>
    /// <param name="datum">Datum des Schreibens.</param>
    /// <param name="unterzeichner">Name unter der Unterschriftslinie; leer lässt nur den Namen der Feuerwehr.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, VorlageForm form, IReadOnlyList<Serienbriefe.Schreiben> schreiben, DateOnly datum, string? unterzeichner) =>
        Document.Create(dokument =>
        {
            foreach (var s in schreiben)
            {
                dokument.Page(seite =>
                {
                    AntragPdf.Seitenrahmen(seite);
                    if (form == VorlageForm.Urkunde)
                    {
                        Urkunde(seite, kopf, s, datum, unterzeichner);
                    }
                    else
                    {
                        Brief(seite, kopf, s, datum, unterzeichner);
                    }
                });
            }
        }).AlsPdf();

    private static void Brief(PageDescriptor seite, AntragPdf.Kopfdaten kopf, Serienbriefe.Schreiben s, DateOnly datum, string? unterzeichner)
    {
        seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, s.Betreff));
        seite.Content().PaddingTop(10).Column(spalte =>
        {
            spalte.Spacing(8);
            spalte.Item().Column(anschrift =>
            {
                foreach (var zeile in s.An.Anschrift)
                {
                    anschrift.Item().Text(zeile);
                }
            });
            spalte.Item().PaddingTop(16).AlignRight().Text(datum.ToString("dd.MM.yyyy", Deutsch));
            foreach (var absatz in Absaetze(s.Text))
            {
                spalte.Item().Text(absatz);
            }

            spalte.Item().PaddingTop(30).Width(220).Column(c =>
            {
                c.Item().BorderBottom(0.75f).Height(4);
                c.Item().PaddingTop(2).Text(unterzeichner ?? kopf.Feuerwehr).FontSize(9);
                if (unterzeichner is not null)
                {
                    c.Item().Text(kopf.Feuerwehr).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                }
            });
        });
    }

    private static void Urkunde(PageDescriptor seite, AntragPdf.Kopfdaten kopf, Serienbriefe.Schreiben s, DateOnly datum, string? unterzeichner)
    {
        seite.Content().PaddingTop(30).Column(spalte =>
        {
            spalte.Spacing(14);
            if (kopf.Logo is { Length: > 0 } logo)
            {
                var platz = spalte.Item().AlignCenter().Width(110).Height(90);
                if (kopf.LogoInhaltstyp == AntragPdf.SvgInhaltstyp)
                {
                    platz.Svg(System.Text.Encoding.UTF8.GetString(logo)).FitArea();
                }
                else
                {
                    platz.Image(logo).FitArea();
                }
            }

            spalte.Item().AlignCenter().Text(kopf.Feuerwehr).FontSize(14).SemiBold();
            spalte.Item().PaddingTop(30).AlignCenter().Text(s.Betreff).FontSize(30).Bold().FontColor(Colors.Red.Darken3);
            spalte.Item().PaddingTop(20).Column(text =>
            {
                text.Spacing(12);
                foreach (var absatz in Absaetze(s.Text))
                {
                    text.Item().AlignCenter().Text(absatz).FontSize(14).AlignCenter();
                }
            });
            spalte.Item().PaddingTop(60).Row(zeile =>
            {
                zeile.RelativeItem().AlignBottom().Text(datum.ToString("dd.MM.yyyy", Deutsch)).FontSize(10);
                zeile.ConstantItem(220).Column(c =>
                {
                    c.Item().BorderBottom(0.75f).Height(4);
                    c.Item().PaddingTop(2).Text(unterzeichner ?? "Kommandant").FontSize(9);
                });
            });
        });
    }

    /// <summary>Teilt den Text an Leerzeilen in Absätze; einfache Zeilenumbrüche bleiben im Absatz.</summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Die Absätze.</returns>
    internal static IEnumerable<string> Absaetze(string text) =>
        text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
