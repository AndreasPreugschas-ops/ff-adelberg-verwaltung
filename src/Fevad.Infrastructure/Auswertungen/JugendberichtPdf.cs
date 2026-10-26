using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Fevad.Infrastructure.Auswertungen.JahresberichtPdf;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>Jugendbericht als PDF für die Hauptversammlung (V49): Zahlen der Jugendstatistik ohne Namen.</summary>
public static class JugendberichtPdf
{
    private const string Rot = "#A50D26";

    static JugendberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Dateiname des Berichts.</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="endung">Dateiendung ohne Punkt.</param>
    /// <returns>Der Dateiname, z. B. „Jugendbericht_2026.pdf“.</returns>
    public static string Dateiname(int jahr, string endung) => $"Jugendbericht_{jahr}.{endung}";

    /// <summary>Erzeugt den Jugendbericht.</summary>
    /// <param name="b">Die berechnete Jugendstatistik.</param>
    /// <param name="organisation">Name der Feuerwehr für den Kopf.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung für die Fußzeile.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Jugendstatistik.Bericht b, string organisation, DateTimeOffset erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(2, Unit.Centimetre);
            seite.DefaultTextStyle(t => t.FontSize(10));

            seite.Header().Column(kopf =>
            {
                kopf.Item().Text(organisation).FontSize(9).FontColor(Colors.Grey.Darken1);
                kopf.Item().Text($"Jugendbericht {b.Jahr}").FontSize(20).Bold().FontColor(Rot);
                kopf.Item().PaddingBottom(8).Text($"Jugend- und Kinderfeuerwehr im Kalenderjahr {b.Jahr}, Stand {Datum(b.Stichtag)}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            seite.Content().Column(inhalt =>
            {
                inhalt.Spacing(12);
                if (b.Hinweise.Count > 0)
                {
                    inhalt.Item().Background(Colors.Yellow.Lighten4).Padding(6).Column(h =>
                    {
                        foreach (var hinweis in b.Hinweise)
                        {
                            h.Item().Text(hinweis).FontSize(9);
                        }
                    });
                }

                foreach (var g in b.Gruppen)
                {
                    inhalt.Item().Element(Abschnitt(g.Bezeichnung));
                    inhalt.Item().Element(Tabelle(
                        ["Jahrgang", "männlich", "weiblich", "divers", "ohne Angabe", "gesamt"],
                        [
                            .. g.Jahrgaenge.Select(j => new[] { j.Jahrgang.ToString(), Ganz(j.Maennlich), Ganz(j.Weiblich), Ganz(j.Divers), Ganz(j.OhneAngabe), Ganz(j.Gesamt) }),
                            ["Gesamt", Ganz(g.Jahrgaenge.Sum(j => j.Maennlich)), Ganz(g.Jahrgaenge.Sum(j => j.Weiblich)), Ganz(g.Jahrgaenge.Sum(j => j.Divers)),
                                Ganz(g.Jahrgaenge.Sum(j => j.OhneAngabe)), Ganz(g.Gesamt)],
                        ],
                        summenzeile: true));
                    inhalt.Item().Element(Kennzahlen(
                    [
                        ("Zugänge", Ganz(g.Zugaenge)),
                        ("Betreuer am 31.12.", Ganz(g.Betreuer)),
                        .. g.Abgaenge.Select(a => ($"Abgang: {a.Text}", Ganz(a.Anzahl))),
                    ]));
                }

                inhalt.Item().Element(Abschnitt("Dienste und Veranstaltungen"));
                inhalt.Item().Element(b.Dienste.Count == 0
                    ? c => c.Text("Keine durchgeführten Dienste.").FontSize(9)
                    : Tabelle(
                        ["Art", "Anzahl", "Stunden", "Teilnahmen", "Personenstunden"],
                        [
                            .. b.Dienste.Select(d => new[] { DienstArtText(d.Art), Ganz(d.Anzahl), Zahl(d.Stunden), Ganz(d.Teilnahmen), Zahl(d.Personenstunden) }),
                            ["Gesamt", Ganz(b.Dienste.Sum(d => d.Anzahl)), Zahl(b.Dienste.Sum(d => d.Stunden)), Ganz(b.Dienste.Sum(d => d.Teilnahmen)), Zahl(b.Dienste.Sum(d => d.Personenstunden))],
                        ],
                        summenzeile: true));

                inhalt.Item().Element(Abschnitt("Abzeichen"));
                inhalt.Item().Element(b.Abzeichen.Count == 0
                    ? c => c.Text("Keine Abzeichen im Jahr.").FontSize(9)
                    : Tabelle(["Abzeichen", "Anzahl"], [.. b.Abzeichen.Select(a => new[] { a.Text, Ganz(a.Anzahl) })]));
            });

            seite.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Erstellt am {TimeZoneInfo.ConvertTime(erstellt, Fevad.Domain.Gemeinsam.Ortszeit.Berlin):dd.MM.yyyy, HH:mm} Uhr").FontSize(8).FontColor(Colors.Grey.Darken1);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Seite ");
                    t.CurrentPageNumber();
                    t.Span(" von ");
                    t.TotalPages();
                });
            });
        })).AlsPdf();
}
