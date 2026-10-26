using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>Stichtagsnachweis der Einsatzabteilung als PDF für Gemeinde und Förderantrag (V28).</summary>
public static class StichtagslistePdf
{
    static StichtagslistePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Dateiname mit Jahr.</summary>
    public static string Dateiname(Stichtagsliste.Nachweis n, string endung, bool nurAnzahlen = false) =>
        $"Stichtagsliste-Einsatzabteilung-{n.Stichtag.Year}{(nurAnzahlen ? "-Anzahlen" : "")}.{endung}";

    /// <summary>Erzeugt das PDF: Beträge oben, darunter die Angehörigen, am Ende die Bestätigung durch den Kommandanten.</summary>
    /// <param name="kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="n">Der berechnete Nachweis.</param>
    /// <param name="stand">Erstellungszeitpunkt in Ortszeit.</param>
    /// <param name="nurAnzahlen">Ohne Namen, nur Anzahlen je Abteilung und Status (Datenminimierung, wenn die Stelle keine Namen verlangt).</param>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, Stichtagsliste.Nachweis n, DateTime stand, bool nurAnzahlen = false) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, $"Angehörige der Einsatzabteilung am {n.Stichtag:dd.MM.yyyy}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(10);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.ConstantColumn(80);
                    });
                    foreach (var b in n.Betraege)
                    {
                        t.Cell().PaddingVertical(2).Column(c =>
                        {
                            c.Item().Text(b.Bezeichnung).SemiBold();
                            c.Item().Text(b.Grundlage).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        t.Cell().PaddingVertical(2).Text(b.Rechnung);
                        t.Cell().PaddingVertical(2).AlignRight().Text(Stichtagsliste.Euro(b.Euro)).SemiBold();
                    }
                });

                spalte.Item().Text($"{n.Angehoerige.Count} Angehörige").SemiBold().FontSize(11);
                if (nurAnzahlen)
                {
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.ConstantColumn(60);
                        });
                        foreach (var titel in new[] { "Abteilung", "Status", "Anzahl" })
                        {
                            t.Cell().BorderBottom(1).PaddingBottom(2).Text(titel).SemiBold().FontSize(9);
                        }

                        foreach (var g in n.Angehoerige.GroupBy(a => (a.Abteilung, a.Status)))
                        {
                            t.Cell().PaddingVertical(2).Text(g.Key.Abteilung);
                            t.Cell().PaddingVertical(2).Text(Stichtagsliste.StatusText(g.Key.Status));
                            t.Cell().PaddingVertical(2).AlignRight().Text(g.Count().ToString());
                        }
                    });
                }
                else
                {
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(25);
                            c.ConstantColumn(70);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.ConstantColumn(60);
                            c.ConstantColumn(70);
                        });
                        t.Header(h =>
                        {
                            foreach (var titel in new[] { "Nr.", "Pers.-Nr.", "Name", "Abteilung", "Status", "Eintritt" })
                            {
                                h.Cell().BorderBottom(1).PaddingBottom(2).Text(titel).SemiBold().FontSize(9);
                            }
                        });
                        var nr = 0;
                        foreach (var a in n.Angehoerige)
                        {
                            IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                            t.Cell().Element(Zelle).Text((++nr).ToString()).FontSize(9);
                            t.Cell().Element(Zelle).Text(a.Personalnummer).FontSize(9);
                            t.Cell().Element(Zelle).Text($"{a.Nachname}, {a.Vorname}").FontSize(9);
                            t.Cell().Element(Zelle).Text(a.Abteilung).FontSize(9);
                            t.Cell().Element(Zelle).Text(Stichtagsliste.StatusText(a.Status)).FontSize(9);
                            t.Cell().Element(Zelle).Text(a.EintrittEinsatzabteilung.ToString("dd.MM.yyyy")).FontSize(9);
                        }
                    });
                }

                spalte.Item().PaddingTop(30).Width(250).Column(c =>
                {
                    c.Item().Height(24);
                    c.Item().BorderTop(0.75f).PaddingTop(2).Text("Datum, Unterschrift Feuerwehrkommandant").FontSize(8);
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Stand {stand:dd.MM.yyyy, HH:mm} Uhr · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
