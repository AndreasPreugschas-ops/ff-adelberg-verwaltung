using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Fevad.Infrastructure.Auswertungen.JahresberichtPdf;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Kurzer Quartalsbericht für den Bürgermeister (V56): Einsätze, Dienstbetrieb, Personalstand und Abschlüsse als Summen, ohne Namen. Die Zahlen
/// kommen aus derselben Berechnung wie der Jahresbericht.
/// </summary>
public static class QuartalsberichtPdf
{
    private const string Rot = "#A50D26";

    static QuartalsberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt den Bericht.</summary>
    /// <param name="bericht">Kennzahlen des Quartals.</param>
    /// <param name="quartal">Quartal 1 bis 4.</param>
    /// <param name="organisation">Name der Feuerwehr.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung für die Fußzeile.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Jahresstatistik.Bericht bericht, int quartal, string organisation, DateTimeOffset erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var (erster, letzter) = Jahresstatistik.Quartal(bericht.Jahr, quartal);
            seite.Size(PageSizes.A4);
            seite.Margin(2, Unit.Centimetre);
            seite.DefaultTextStyle(t => t.FontSize(10));
            seite.Header().Column(kopf =>
            {
                kopf.Item().Text(organisation).FontSize(9).FontColor(Colors.Grey.Darken1);
                kopf.Item().Text($"Quartalsbericht {quartal}/{bericht.Jahr}").FontSize(20).Bold().FontColor(Rot);
                kopf.Item().PaddingBottom(8).Text($"Für den Bürgermeister: {Datum(erster)} bis {Datum(letzter)}, Personalstand zum {Datum(letzter)}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            seite.Content().Column(inhalt =>
            {
                inhalt.Spacing(12);
                foreach (var hinweis in bericht.Hinweise)
                {
                    inhalt.Item().Background(Colors.Yellow.Lighten4).Padding(6).Text(hinweis).FontSize(9);
                }

                var e = bericht.Einsaetze;
                inhalt.Item().Element(Abschnitt("Einsätze"));
                inhalt.Item().Element(Tabelle(
                    ["Kategorie", "Einsätze", "Personenstunden"],
                    [
                        .. e.NachKategorie.Where(k => k.Anzahl > 0).Select(k => new[] { k.Bezeichnung, Ganz(k.Anzahl), Zahl(k.Personenstunden) }),
                        ["Gesamt", Ganz(e.Freigegeben), Zahl(e.Personenstunden)],
                    ],
                    summenzeile: true));
                inhalt.Item().Element(Kennzahlen(
                [
                    ("Gerettete Personen", Ganz(e.Gerettete)),
                    ("Verletzte Feuerwehrangehörige", Ganz(e.VerletzteFeuerwehr)),
                    ("Überörtliche Einsätze", Ganz(e.Ueberoertlich)),
                    ("Alarme werktags 06:00–18:00 Uhr", Ganz(e.WerktagsTagsueber)),
                ]));

                var d = bericht.Dienste;
                inhalt.Item().Element(Abschnitt("Übungs- und Dienstbetrieb"));
                inhalt.Item().Element(Tabelle(
                    ["Art", "Anzahl", "Stunden", "Personenstunden"],
                    [
                        .. d.NachArt.Where(a => a.Anzahl > 0).Select(a => new[] { DienstArtText(a.Art), Ganz(a.Anzahl), Zahl(a.Stunden), Zahl(a.Personenstunden) }),
                        ["Gesamt", Ganz(d.NachArt.Sum(a => a.Anzahl)), Zahl(d.NachArt.Sum(a => a.Stunden)), Zahl(d.NachArt.Sum(a => a.Personenstunden))],
                    ],
                    summenzeile: true));

                var p = bericht.Personal;
                inhalt.Item().Element(Abschnitt($"Personalstand zum {Datum(letzter)}"));
                inhalt.Item().Element(Tabelle(
                    ["Abteilung", "Gesamt", "aktiv", "Zugänge", "Abgänge"],
                    [.. p.Abteilungen.Select(a => new[] { a.Bezeichnung, Ganz(a.Gesamt), Ganz(a.Aktiv), Ganz(a.Zugaenge), Ganz(a.Abgaenge) })]));

                if (bericht.Qualifikationen.Count > 0 || bericht.Befoerderungen.Count > 0)
                {
                    inhalt.Item().Element(Abschnitt("Aus- und Fortbildung"));
                    inhalt.Item().Element(Tabelle(
                        ["Lehrgang / Qualifikation", "Abschlüsse"],
                        [.. bericht.Qualifikationen.Select(q => new[] { q.Text, Ganz(q.Anzahl) }), ["Beförderungen", Ganz(bericht.Befoerderungen.Count)]]));
                }

                if (bericht.Chronik.Count > 0)
                {
                    inhalt.Item().Element(Abschnitt("Aus der Chronik"));
                    foreach (var c in bericht.Chronik)
                    {
                        inhalt.Item().Text($"{Datum(c.Datum)}: {c.Titel}");
                    }
                }
            });

            seite.Footer().AlignRight().Text($"Erstellt am {Datum(Fevad.Domain.Gemeinsam.Ortszeit.Datum(erstellt))}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        })).AlsPdf();
}
