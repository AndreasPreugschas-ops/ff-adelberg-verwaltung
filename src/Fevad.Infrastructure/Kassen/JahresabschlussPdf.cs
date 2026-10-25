using System.Globalization;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Jahresabschluss einer Kasse als PDF (V04, V05): Vermögensübersicht je Konto, Einnahmen und Ausgaben nach dem Wirtschaftsplan,
/// Bestandsverzeichnis am 31.12., Ausnahmebuchungen und Unterschriftsfelder für Kassenwart, Kommandant und die beiden Rechnungsprüfer.
/// Grundlage für die Vorlage an den Bürgermeister und die Entlastung durch die Hauptversammlung.
/// </summary>
public static class JahresabschlussPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static JahresabschlussPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Dateiname aus Kasse und Jahr.</summary>
    /// <param name="r">Die Jahresrechnung.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Kassenabschluss.Jahresrechnung r) =>
        $"Jahresabschluss {r.Jahr} {string.Concat(r.Kasse.Name.Select(z => char.IsLetterOrDigit(z) || z is ' ' or '-' ? z : '_')).Trim()}.pdf";

    /// <summary>Erzeugt den Jahresabschluss als PDF.</summary>
    /// <param name="kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="r">Die Jahresrechnung.</param>
    /// <param name="stand">Zeitpunkt der Erstellung in Ortszeit, z. B. „25.10.2026, 22:15 Uhr“.</param>
    /// <param name="abgeschlossen">Tag des Abschlusses in Ortszeit, z. B. „15.02.2027“; leer, solange das Jahr offen ist.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, Kassenabschluss.Jahresrechnung r, string stand, string? abgeschlossen) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, $"Jahresabschluss {r.Jahr}: {r.Kasse.Name}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(10);
                spalte.Item().Text(abgeschlossen is not null
                    ? $"Abgeschlossen am {abgeschlossen}. Sondervermögen nach § 18 FwG."
                    : "Entwurf: Das Jahr ist noch nicht abgeschlossen. Sondervermögen nach § 18 FwG.").FontColor(Colors.Grey.Darken1);

                Abschnitt(spalte, "Vermögensübersicht");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(90); c.ConstantColumn(90); c.ConstantColumn(90); });
                    Kopf(t, 1, "Konto", "01.01.", "31.12.", "Veränderung");
                    foreach (var k in r.Konten)
                    {
                        Zeile(t, 1, $"{k.Konto.Bezeichnung} ({Text(k.Konto.Art)})", Euro(k.Anfang), Euro(k.Ende), Euro(k.Ende - k.Anfang));
                    }

                    Summe(t, 1, "Summe", Euro(r.Anfang), Euro(r.Ende), Euro(r.Ende - r.Anfang));
                });
                if (r.Ruecklagen != 0)
                {
                    spalte.Item().Text($"Davon Rücklagen: {Euro(r.Ruecklagen)}");
                }

                Abschnitt(spalte, $"Einnahmen und Ausgaben nach dem Wirtschaftsplan{(r.PlanBeschlossen ? "" : " (Plan nicht beschlossen)")}");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(40); c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); c.ConstantColumn(80); });
                    Kopf(t, 2, "Nr.", "Position", "Plan", "Ist", "Abweichung");
                    foreach (var z in r.Plan)
                    {
                        Zeile(t, 2, z.Position.Nummer, z.Position.Bezeichnung, Euro(z.Ansatz), Euro(z.Ist), Euro(z.Abweichung));
                    }

                    if (r.OhnePosition != 0)
                    {
                        Zeile(t, 2, "", "ohne Position (Saldo)", "", Euro(r.OhnePosition), "");
                    }

                    Summe(t, 2, "", "Einnahmen", "", Euro(r.Einnahmen), "");
                    Summe(t, 2, "", "Ausgaben", "", Euro(r.Ausgaben), "");
                    Summe(t, 2, "", "Überschuss oder Fehlbetrag", "", Euro(r.Einnahmen - r.Ausgaben), "");
                });

                Abschnitt(spalte, "Bestandsverzeichnis am 31.12.");
                if (r.Bestand.Count == 0)
                {
                    spalte.Item().Text("Keine Gegenstände im Bestand.");
                }
                else
                {
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(80); });
                        Kopf(t, 4, "Nr.", "Gegenstand", "Anschaffung", "Standort", "Wert");
                        foreach (var v in r.Bestand)
                        {
                            Zeile(t, 4, v.Inventarnummer ?? "", v.Bezeichnung, v.Anschaffung.ToString("dd.MM.yyyy", Deutsch), v.Standort ?? "", Euro(v.Wert));
                        }

                        Summe(t, 4, "", $"{r.Bestand.Count} Gegenstände", "", "", Euro(r.Bestand.Sum(v => v.Wert)));
                    });
                }

                if (r.Ausnahmen.Count > 0)
                {
                    Abschnitt(spalte, "Ausnahmebuchungen nach dem Abschluss");
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(60); c.ConstantColumn(65); c.RelativeColumn(); c.ConstantColumn(80); });
                        Kopf(t, 3, "Beleg", "Datum", "Text und Begründung", "Betrag");
                        foreach (var b in r.Ausnahmen)
                        {
                            Zeile(t, 3, b.Kennung, b.Datum.ToString("dd.MM.yyyy", Deutsch), $"{b.Text}. Begründung: {b.AusnahmeBegruendung}", Euro(b.Betrag));
                        }
                    });
                }

                if (r.Abschluss is { } abschluss && (abschluss.Einnahmen != r.Einnahmen || abschluss.Ausgaben != r.Ausgaben))
                {
                    spalte.Item().Text($"Beim Abschluss: Einnahmen {Euro(abschluss.Einnahmen)}, Ausgaben {Euro(abschluss.Ausgaben)}, Endbestand "
                        + $"{Euro(abschluss.Endbestaende.Sum(e => e.Bestand))}. Die Abweichung kommt aus den Ausnahmebuchungen.").FontColor(Colors.Red.Darken2);
                }

                spalte.Item().PaddingTop(6).Text("Die Richtigkeit des Jahresabschlusses wird bestätigt.");
                spalte.Item().PaddingTop(24).Row(zeile =>
                {
                    Unterschrift(zeile, "Ort, Datum, Kassenwart");
                    zeile.ConstantItem(20);
                    Unterschrift(zeile, "Ort, Datum, Kommandant");
                });
                spalte.Item().PaddingTop(6).Text("Die Rechnungsprüfer haben die Kasse geprüft.");
                spalte.Item().PaddingTop(24).Row(zeile =>
                {
                    Unterschrift(zeile, "Ort, Datum, Rechnungsprüfer");
                    zeile.ConstantItem(20);
                    Unterschrift(zeile, "Ort, Datum, Rechnungsprüfer");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"{r.Kasse.Name} · Jahresabschluss {r.Jahr} · Stand {stand} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static void Abschnitt(ColumnDescriptor spalte, string titel) =>
        spalte.Item().PaddingTop(4).Text(titel).FontSize(12).SemiBold();

    // Spalten ab „rechts“ sind Beträge und stehen rechtsbündig.
    private static void Kopf(TableDescriptor t, int rechts, params string[] spalten) => t.Header(h =>
    {
        for (var i = 0; i < spalten.Length; i++)
        {
            var zelle = h.Cell().BorderBottom(1).PaddingBottom(3);
            (i >= rechts ? zelle.AlignRight() : zelle).Text(spalten[i]).SemiBold().FontSize(9);
        }
    });

    private static void Zeile(TableDescriptor t, int rechts, params string[] werte)
    {
        for (var i = 0; i < werte.Length; i++)
        {
            var zelle = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
            (i >= rechts ? zelle.AlignRight() : zelle).Text(werte[i]).FontSize(9);
        }
    }

    private static void Summe(TableDescriptor t, int rechts, params string[] werte)
    {
        for (var i = 0; i < werte.Length; i++)
        {
            var zelle = t.Cell().PaddingTop(3);
            (i >= rechts ? zelle.AlignRight() : zelle).Text(werte[i]).SemiBold().FontSize(9);
        }
    }

    private static void Unterschrift(RowDescriptor zeile, string text) => zeile.RelativeItem().Column(c =>
    {
        c.Item().BorderBottom(0.75f).Height(24);
        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
    });

    private static string Text(KontoArt art) => art switch
    {
        KontoArt.Barkasse => "Barkasse",
        KontoArt.Ruecklage => "Rücklage",
        _ => "Girokonto",
    };

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);
}
