using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Prüfplaketten „geprüft bis MM/JJJJ“ mit QR-Code (V58, DGUV Grundsatz 305-002: nächste Prüfung am Gerät erkennbar).
/// Zwei Formate nach Andi (25.10.2026): Etikettendrucker mit einem Etikett je Seite oder A4-Etikettenbogen.
/// </summary>
public static class PlakettenPdf
{
    static PlakettenPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Druckziel der Plaketten.</summary>
    public enum Format
    {
        /// <summary>Etikettendrucker: jede Seite ist genau ein Etikett.</summary>
        Drucker,

        /// <summary>A4-Bogen mit festen Spalten und Zeilen ohne Seitenrand.</summary>
        Bogen,
    }

    /// <summary>Inhalt einer Plakette.</summary>
    /// <param name="Inventarnummer">Nummer am Gerät, groß gedruckt.</param>
    /// <param name="Bezeichnung">Gerätebezeichnung, wird bei Platzmangel abgeschnitten.</param>
    /// <param name="NaechstePruefung">Nächste Fälligkeit über alle Prüfplanpositionen; leer, wenn nichts geplant ist.</param>
    /// <param name="QrSvg">QR-Code als SVG, führt auf die Geräteseite.</param>
    public sealed record Plakette(string Inventarnummer, string Bezeichnung, DateOnly? NaechstePruefung, string QrSvg);

    /// <summary>Maße aus den Parametern <c>Technik.Etikett.*</c>.</summary>
    /// <param name="BreiteMm">Breite eines Druckeretiketts.</param>
    /// <param name="HoeheMm">Höhe eines Druckeretiketts.</param>
    /// <param name="Spalten">Spalten auf dem A4-Bogen.</param>
    /// <param name="Zeilen">Zeilen auf dem A4-Bogen.</param>
    public sealed record Masse(float BreiteMm, float HoeheMm, int Spalten, int Zeilen);

    /// <summary>Text für das Fälligkeitsfeld: „MM/JJJJ“ oder ein Hinweis ohne Datum.</summary>
    public static string GeprueftBis(DateOnly? naechste) => naechste is { } d ? d.ToString("MM/yyyy") : "nach Gebrauch";

    /// <summary>Erzeugt das PDF; leere Listen ergeben ein leeres Etikett, damit der Drucker nicht hängt.</summary>
    public static byte[] Erzeugen(IReadOnlyList<Plakette> plaketten, Format format, Masse masse, string feuerwehr) =>
        format == Format.Drucker ? Drucker(plaketten, masse, feuerwehr) : Bogen(plaketten, masse, feuerwehr);

    private static byte[] Drucker(IReadOnlyList<Plakette> plaketten, Masse masse, string feuerwehr) =>
        Document.Create(dokument =>
        {
            foreach (var plakette in plaketten.DefaultIfEmpty(new Plakette("", "", null, "")))
            {
                dokument.Page(seite =>
                {
                    seite.Size(masse.BreiteMm, masse.HoeheMm, Unit.Millimetre);
                    seite.Margin(1.5f, Unit.Millimetre);
                    seite.Content().Element(c => Inhalt(c, plakette, feuerwehr, masse.HoeheMm - 3));
                });
            }
        }).GeneratePdf();

    private static byte[] Bogen(IReadOnlyList<Plakette> plaketten, Masse masse, string feuerwehr)
    {
        var spalten = Math.Max(1, masse.Spalten);
        var zeilen = Math.Max(1, masse.Zeilen);
        var hoeheMm = 297f / zeilen;
        return Document.Create(dokument => dokument.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(0);
            seite.Content().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    for (var i = 0; i < spalten; i++)
                    {
                        c.RelativeColumn();
                    }
                });

                foreach (var plakette in plaketten)
                {
                    t.Cell().Height(hoeheMm, Unit.Millimetre).Padding(3, Unit.Millimetre)
                        .Element(c => Inhalt(c, plakette, feuerwehr, hoeheMm - 6));
                }
            });
        })).GeneratePdf();
    }

    private static void Inhalt(IContainer container, Plakette p, string feuerwehr, float hoeheMm)
    {
        if (p.Inventarnummer.Length == 0)
        {
            return;
        }

        container.Row(r =>
        {
            r.ConstantItem(hoeheMm, Unit.Millimetre).AlignMiddle().Svg(p.QrSvg);
            r.ConstantItem(2, Unit.Millimetre);
            r.RelativeItem().AlignMiddle().Column(c =>
            {
                c.Item().Text("geprüft bis").FontSize(6.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text(GeprueftBis(p.NaechstePruefung)).FontSize(14).Bold();
                c.Item().Text(p.Inventarnummer).FontSize(8.5f).SemiBold();
                c.Item().Height(9).Text(p.Bezeichnung).FontSize(6.5f).ClampLines(1);
                c.Item().Text(feuerwehr).FontSize(5.5f).FontColor(Colors.Grey.Darken1).ClampLines(1);
            });
        });
    }
}
