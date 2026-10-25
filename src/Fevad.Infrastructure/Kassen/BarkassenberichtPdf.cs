using System.Globalization;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Barkassenbericht mit Zählprotokoll zum Unterschreiben durch beide Zählenden (V03). Das PDF hängt FEVAD beim Übernehmen ins Kassenbuch
/// als Beleg an; das unterschriebene Papier kann zusätzlich hochgeladen werden.
/// </summary>
public static class BarkassenberichtPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static BarkassenberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Inhalt des Berichts.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Kasse">Name der Kasse.</param>
    /// <param name="Veranstaltung">Name der Veranstaltung.</param>
    /// <param name="Bericht">Der Bericht samt Zählung.</param>
    /// <param name="Steuersatz">Bezeichnung des Umsatzsteuersatzes.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Kasse, string Veranstaltung, Barkassenbericht Bericht, string Steuersatz);

    /// <summary>Dateiname aus Veranstaltung, Tag und Stelle.</summary>
    /// <param name="d">Inhalt des Berichts.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Daten d) =>
        $"Barkassenbericht {d.Bericht.Tag:yyyy-MM-dd} {Dateitext(d.Veranstaltung)} {Dateitext(d.Bericht.Stelle)}.pdf";

    /// <summary>Erzeugt den Bericht als PDF.</summary>
    /// <param name="d">Inhalt des Berichts.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var b = d.Bericht;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Barkassenbericht mit Zählprotokoll"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(120); c.RelativeColumn(); });
                    void Zeile(string etikett, string wert)
                    {
                        t.Cell().PaddingVertical(1).Text(etikett).FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(1).Text(wert).SemiBold();
                    }

                    Zeile("Kasse", d.Kasse);
                    Zeile("Veranstaltung", d.Veranstaltung);
                    Zeile("Festtag", b.Tag.ToString("dd.MM.yyyy", Deutsch));
                    Zeile("Kassenstelle", b.Stelle);
                    Zeile("Umsatzsteuersatz", d.Steuersatz);
                });

                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(70); c.ConstantColumn(90); });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(1).PaddingBottom(3).Text("Schein oder Münze").SemiBold().FontSize(9);
                        h.Cell().BorderBottom(1).PaddingBottom(3).AlignRight().Text("Anzahl").SemiBold().FontSize(9);
                        h.Cell().BorderBottom(1).PaddingBottom(3).AlignRight().Text("Betrag").SemiBold().FontSize(9);
                    });

                    foreach (var z in b.Zaehlung.Where(z => z.Anzahl > 0).OrderByDescending(z => z.Wert))
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                        t.Cell().Element(Zelle).Text(Euro(z.Wert));
                        t.Cell().Element(Zelle).AlignRight().Text(z.Anzahl.ToString(Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(Euro(z.Wert * z.Anzahl));
                    }

                    void Summe(string text, decimal betrag, bool fett = false)
                    {
                        t.Cell().ColumnSpan(2).PaddingTop(3).Text(text).SemiBold();
                        var zelle = t.Cell().PaddingTop(3).AlignRight().Text(Euro(betrag));
                        if (fett)
                        {
                            zelle.Bold();
                        }
                    }

                    Summe("Gezählter Bestand", b.Endbestand);
                    Summe("abzüglich Wechselgeld", b.Wechselgeld);
                    Summe("Einnahme", b.Einnahme, fett: true);
                });

                if (!string.IsNullOrWhiteSpace(b.Bemerkung))
                {
                    spalte.Item().Text($"Bemerkung: {b.Bemerkung}");
                }

                spalte.Item().PaddingTop(4).Text("Wir haben den Bestand gemeinsam gezählt und bestätigen die Angaben mit unserer Unterschrift.");
                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Unterschrift(string text) => r.RelativeItem().Column(c =>
                    {
                        c.Item().BorderBottom(0.75f).Height(24);
                        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });

                    Unterschrift("Ort, Datum");
                    r.ConstantItem(20);
                    Unterschrift($"Gezählt: {b.GezaehltVon}");
                    r.ConstantItem(20);
                    Unterschrift($"Gezählt: {b.ZweitePerson}");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"{d.Veranstaltung} · {b.Stelle} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);

    private static string Dateitext(string text) =>
        string.Concat(text.Select(z => char.IsLetterOrDigit(z) || z is ' ' or '-' ? z : '_')).Trim();
}
