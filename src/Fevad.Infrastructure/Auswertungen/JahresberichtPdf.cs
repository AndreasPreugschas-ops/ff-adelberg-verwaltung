using System.Globalization;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>Jahresbericht als PDF für die Hauptversammlung und als Anlage zur Jahresmeldung.</summary>
public static class JahresberichtPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private const string Rot = "#A50D26";

    static JahresberichtPdf()
    {
        // Community-Lizenz: kostenfrei für gemeinnützige Organisationen und Umsätze unter 1 Mio. USD.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Erzeugen(Jahresstatistik.Bericht bericht, string organisation, DateTimeOffset erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(2, Unit.Centimetre);
            seite.DefaultTextStyle(t => t.FontSize(10));

            seite.Header().Column(kopf =>
            {
                kopf.Item().Text(organisation).FontSize(9).FontColor(Colors.Grey.Darken1);
                kopf.Item().Text($"Jahresbericht {bericht.Jahr}").FontSize(20).Bold().FontColor(Rot);
                kopf.Item().PaddingBottom(8).Text($"Einsätze und Dienste im Kalenderjahr {bericht.Jahr}, Personalstand zum {Datum(bericht.Stichtag)}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            seite.Content().Column(inhalt =>
            {
                inhalt.Spacing(12);

                if (bericht.Hinweise.Count > 0)
                {
                    inhalt.Item().Background(Colors.Yellow.Lighten4).Padding(6).Column(h =>
                    {
                        foreach (var hinweis in bericht.Hinweise)
                        {
                            h.Item().Text(hinweis).FontSize(9);
                        }
                    });
                }

                var e = bericht.Einsaetze;
                inhalt.Item().Element(Abschnitt("Einsätze"));
                inhalt.Item().Element(Tabelle(
                    ["Kategorie", "Einsätze", "Personenstunden"],
                    [
                        .. e.NachKategorie.Select(k => new[] { k.Bezeichnung, Ganz(k.Anzahl), Zahl(k.Personenstunden) }),
                        ["Gesamt", Ganz(e.Freigegeben), Zahl(e.Personenstunden)],
                    ],
                    summenzeile: true));
                inhalt.Item().Element(Kennzahlen(
                [
                    ("Gerettete Personen", Ganz(e.Gerettete)),
                    ("Verletzte Personen", Ganz(e.Verletzte)),
                    ("Tote", Ganz(e.Tote)),
                    ("Verletzte Feuerwehrangehörige", Ganz(e.VerletzteFeuerwehr)),
                    ("Überörtliche Einsätze", Ganz(e.Ueberoertlich)),
                    ("Teilnehmer je Einsatz (Durchschnitt)", Zahl(e.DurchschnittTeilnehmer)),
                    ("Ausrückezeit (Median)", e.AusrueckzeitMedianMinuten is { } m ? $"{m.ToString("0.0", Deutsch)} min" : "–"),
                    ("Alarme werktags 06:00–18:00", Ganz(e.WerktagsTagsueber)),
                ]));
                inhalt.Item().Element(Tabelle(
                    [.. Enumerable.Range(1, 12).Select(m => Deutsch.DateTimeFormat.GetAbbreviatedMonthName(m))],
                    [[.. e.NachMonat.Select(Ganz)]]));

                inhalt.Item().Element(Abschnitt("Übungs- und Dienstbetrieb"));
                var d = bericht.Dienste;
                inhalt.Item().Element(Tabelle(
                    ["Art", "Anzahl", "Stunden", "Teilnahmen", "Personenstunden"],
                    [
                        .. d.NachArt.Select(a => new[] { DienstArtText(a.Art), Ganz(a.Anzahl), Zahl(a.Stunden), Ganz(a.Teilnahmen), Zahl(a.Personenstunden) }),
                        ["Gesamt", Ganz(d.NachArt.Sum(a => a.Anzahl)), Zahl(d.NachArt.Sum(a => a.Stunden)), Ganz(d.NachArt.Sum(a => a.Teilnahmen)), Zahl(d.NachArt.Sum(a => a.Personenstunden))],
                    ],
                    summenzeile: true));
                inhalt.Item().Text($"Standortfortbildung: {d.FortbildungZielErreicht} von {d.FortbildungBasis} Angehörigen der Einsatzabteilung haben mindestens {d.FortbildungZielStunden.ToString("0.#", Deutsch)} Stunden erreicht.");

                inhalt.Item().Element(Abschnitt($"Personalstand zum {Datum(bericht.Stichtag)}"));
                var p = bericht.Personal;
                inhalt.Item().Element(Tabelle(
                    ["Abteilung", "Gesamt", "aktiv", "Probezeit", "befreit", "Zugänge", "Abgänge"],
                    [.. p.Abteilungen.Select(a => new[] { a.Bezeichnung, Ganz(a.Gesamt), Ganz(a.Aktiv), Ganz(a.Probezeit), Ganz(a.Befreit), Ganz(a.Zugaenge), Ganz(a.Abgaenge) })]));
                if (p.Uebertritte.Count > 0)
                {
                    inhalt.Item().Text("Übertritte: " + string.Join(", ", p.Uebertritte.Select(u => $"{u.Text} ({u.Anzahl})")));
                }

                if (p.Abgaenge.Count > 0)
                {
                    inhalt.Item().Text("Abgänge nach Grund: " + string.Join(", ", p.Abgaenge.Select(a => $"{a.Text} ({a.Anzahl})")));
                }

                inhalt.Item().Element(Tabelle(
                    ["Alter (Einsatzabt.)", .. p.AltersstrukturEinsatzabteilung.Select(a => a.Bezeichnung), "Durchschnitt"],
                    [["Anzahl", .. p.AltersstrukturEinsatzabteilung.Select(a => Ganz(a.Anzahl)), p.DurchschnittsalterEinsatzabteilung is { } schnitt ? schnitt.ToString("0.0", Deutsch) : "–"]]));
                inhalt.Item().Text($"In den nächsten fünf Jahren erreichen {p.ErreichenAltersgrenzeInFuenfJahren} Angehörige der Einsatzabteilung die Altersgrenze von {p.Altersgrenze} Jahren.");

                if (bericht.Befoerderungen.Count > 0)
                {
                    inhalt.Item().Element(Abschnitt("Beförderungen"));
                    inhalt.Item().Element(Tabelle(
                        ["Datum", "Name", "Dienstgrad"],
                        [.. bericht.Befoerderungen.Select(b => new[] { Datum(b.Datum), b.Name, b.Dienstgrad })]));
                }

                if (bericht.Qualifikationen.Count > 0)
                {
                    inhalt.Item().Element(Abschnitt("Aus- und Fortbildung"));
                    inhalt.Item().Element(Tabelle(
                        ["Lehrgang / Qualifikation", "Abschlüsse"],
                        [.. bericht.Qualifikationen.Select(q => new[] { q.Text, Ganz(q.Anzahl) })]));
                }

                if (bericht.Fahrzeuge.Count > 0)
                {
                    inhalt.Item().Element(Abschnitt("Fahrzeuge im Einsatz"));
                    inhalt.Item().Element(Tabelle(
                        ["Fahrzeug", "Typ", "Einsätze", "Kilometer"],
                        [.. bericht.Fahrzeuge.Select(f => new[] { f.Funkrufname, f.Typ, Ganz(f.Einsaetze), Ganz(f.Kilometer) })]));
                }
            });

            seite.Footer().Row(fuss =>
            {
                var lokal = TimeZoneInfo.ConvertTime(erstellt, Ortszeit.Berlin);
                fuss.RelativeItem().Text($"Erstellt am {lokal.ToString("dd.MM.yyyy HH:mm", Deutsch)} mit FEVAD").FontSize(8).FontColor(Colors.Grey.Darken1);
                fuss.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("Seite ");
                    t.CurrentPageNumber();
                    t.Span(" von ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();

    public static string DienstArtText(DienstArt art) => art switch
    {
        DienstArt.Uebung => "Übung",
        DienstArt.Lehrgang => "Lehrgang (extern)",
        _ => art.ToString(),
    };

    private static Action<IContainer> Abschnitt(string titel) => c =>
        c.BorderBottom(1).BorderColor(Rot).PaddingBottom(2).Text(titel).FontSize(13).Bold().FontColor(Rot);

    private static Action<IContainer> Kennzahlen(IReadOnlyList<(string Text, string Wert)> werte) => c => c.Table(t =>
    {
        t.ColumnsDefinition(s =>
        {
            s.RelativeColumn(3);
            s.RelativeColumn(1);
            s.RelativeColumn(3);
            s.RelativeColumn(1);
        });
        foreach (var (text, wert) in werte)
        {
            t.Cell().PaddingVertical(1).Text(text);
            t.Cell().PaddingVertical(1).AlignRight().PaddingRight(12).Text(wert).Bold();
        }
    });

    private static Action<IContainer> Tabelle(IReadOnlyList<string> kopf, IReadOnlyList<string[]> zeilen, bool summenzeile = false) => c => c.Table(t =>
    {
        t.ColumnsDefinition(s =>
        {
            s.RelativeColumn(kopf.Count > 8 ? 1 : 2.5f);
            for (var i = 1; i < kopf.Count; i++)
            {
                s.RelativeColumn();
            }
        });

        t.Header(h =>
        {
            for (var i = 0; i < kopf.Count; i++)
            {
                var zelle = h.Cell().Background(Colors.Grey.Lighten3).Padding(3);
                (i == 0 ? zelle : zelle.AlignRight()).Text(kopf[i]).Bold().FontSize(9);
            }
        });

        for (var z = 0; z < zeilen.Count; z++)
        {
            var fett = summenzeile && z == zeilen.Count - 1;
            for (var i = 0; i < zeilen[z].Length; i++)
            {
                var zelle = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                var text = (i == 0 ? zelle : zelle.AlignRight()).Text(zeilen[z][i]).FontSize(9);
                if (fett)
                {
                    text.Bold();
                }
            }
        }
    });

    private static string Ganz(int wert) => wert.ToString("N0", Deutsch);

    private static string Zahl(decimal wert) => wert.ToString("N1", Deutsch);

    private static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", Deutsch);
}
