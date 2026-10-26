using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Teilnehmerliste einer Veranstaltung der Jugend (V48) als Nachweis für den Zuschuss: je Teilnehmer Name, Geburtsdatum, Anschrift, Tage und
/// eine Spalte für die Unterschrift, darunter die Betreuer mit ihren Tagen. Wird nur auf Abruf erzeugt und nicht gespeichert.
/// </summary>
public static class TeilnehmerlistePdf
{
    static TeilnehmerlistePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Geburtsdatum">Geburtsdatum, formatiert; leer bei Betreuern.</param>
    /// <param name="Anschrift">Anschrift in einer Zeile.</param>
    /// <param name="Tage">Teilnahmetage.</param>
    public sealed record Zeile(string Name, string? Geburtsdatum, string? Anschrift, int Tage);

    /// <summary>Inhalt der Liste.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Veranstaltung">Titel der Veranstaltung.</param>
    /// <param name="Zeitraum">Zeitraum, formatiert.</param>
    /// <param name="Ort">Ort.</param>
    /// <param name="Traeger">Träger, z. B. „Jugendfeuerwehr Adelberg“.</param>
    /// <param name="Zuschussgeber">Zuschussgeber, falls gewählt.</param>
    /// <param name="Teilnehmer">Teilnehmer.</param>
    /// <param name="Betreuer">Betreuer.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Veranstaltung, string Zeitraum, string? Ort, string Traeger, string? Zuschussgeber,
        IReadOnlyList<Zeile> Teilnehmer, IReadOnlyList<Zeile> Betreuer);

    /// <summary>Erzeugt die Liste quer auf A4.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Size(PageSizes.A4.Landscape());
            seite.DefaultTextStyle(t => t.FontSize(9));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Teilnehmerliste"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span(d.Veranstaltung).SemiBold();
                    t.Span($" · {d.Zeitraum}" + (string.IsNullOrWhiteSpace(d.Ort) ? "" : $" · {d.Ort}"));
                });
                spalte.Item().Text($"Träger: {d.Traeger}" + (d.Zuschussgeber is null ? "" : $" · Nachweis für: {d.Zuschussgeber}"));
                spalte.Item().Element(c => Tabelle(c, d.Teilnehmer, mitGeburtsdatum: true));
                spalte.Item().PaddingTop(6).Text("Betreuer").SemiBold();
                spalte.Item().Element(c => Tabelle(c, d.Betreuer, mitGeburtsdatum: false));
                spalte.Item().Text($"Teilnehmer: {d.Teilnehmer.Count}, Teilnehmertage: {d.Teilnehmer.Sum(z => z.Tage)}; Betreuer: {d.Betreuer.Count}, "
                    + $"Betreuertage: {d.Betreuer.Sum(z => z.Tage)}");
                spalte.Item().PaddingTop(24).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Ort, Datum").FontSize(8);
                    });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Unterschrift Jugendwart / Leitung der Veranstaltung").FontSize(8);
                    });
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Vertraulich: personenbezogene Daten von Minderjährigen · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();

    private static void Tabelle(IContainer container, IReadOnlyList<Zeile> zeilen, bool mitGeburtsdatum) =>
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(22);
                c.RelativeColumn(1.6f);
                if (mitGeburtsdatum)
                {
                    c.RelativeColumn(0.8f);
                }

                c.RelativeColumn(2.4f);
                c.RelativeColumn(0.4f);
                c.RelativeColumn(1.6f);
            });
            t.Header(h =>
            {
                var titel = mitGeburtsdatum
                    ? new[] { "Nr.", "Name", "Geboren", "Anschrift", "Tage", "Unterschrift" }
                    : ["Nr.", "Name", "Anschrift", "Tage", "Unterschrift"];
                foreach (var s in titel)
                {
                    h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
                }
            });
            var nummer = 0;
            foreach (var z in zeilen)
            {
                IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingRight(4);
                t.Cell().Element(Zelle).Text((++nummer).ToString());
                t.Cell().Element(Zelle).Text(z.Name);
                if (mitGeburtsdatum)
                {
                    t.Cell().Element(Zelle).Text(z.Geburtsdatum ?? "");
                }

                t.Cell().Element(Zelle).Text(z.Anschrift ?? "");
                t.Cell().Element(Zelle).Text(z.Tage.ToString());
                t.Cell().Element(Zelle).Text("");
            }
        });
}
