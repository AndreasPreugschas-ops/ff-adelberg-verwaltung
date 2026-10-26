using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Kleiderkammer;

/// <summary>
/// Ausgabe- und Rückgabebestätigung zum Unterschreiben. Rechtsverbindlich ist das unterschriebene Papier;
/// FEVAD vermerkt nur, dass es vorliegt.
/// </summary>
public static class QuittungPdf
{
    // Ohne eigene Zuweisung scheitert die erste Quittung nach dem Start, solange noch kein Antrags-PDF erzeugt wurde.
    static QuittungPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Position auf der Quittung.</summary>
    /// <param name="Inventarnummer">Inventarnummer bei Einzelstücken.</param>
    /// <param name="Artikel">Bezeichnung des Artikels.</param>
    /// <param name="Groesse">Größe.</param>
    /// <param name="Menge">Stückzahl.</param>
    /// <param name="Seriennummer">Seriennummer bei Einzelstücken.</param>
    /// <param name="Ordnungsgemaess">Bei Rückgabe: in ordnungsgemäßem Zustand.</param>
    /// <param name="Maengel">Bei Rückgabe: festgestellte Mängel.</param>
    public sealed record Position(string? Inventarnummer, string Artikel, string Groesse, int Menge, string? Seriennummer, bool? Ordnungsgemaess, string? Maengel);

    /// <summary>Inhalt der Quittung.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Gemeinde">Name der Gemeinde als Eigentümerin.</param>
    /// <param name="Nummer">Quittungsnummer.</param>
    /// <param name="Art">Ausgabe oder Rücknahme.</param>
    /// <param name="Zeitpunkt">Zeitpunkt des Vorgangs.</param>
    /// <param name="Name">Name des Mitglieds.</param>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Positionen">Die Positionen.</param>
    /// <param name="Bemerkung">Bemerkung zum Vorgang.</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, string Gemeinde, string Nummer, VorgangArt Art, DateTimeOffset Zeitpunkt,
        string Name, string Personalnummer, IReadOnlyList<Position> Positionen, string? Bemerkung);

    /// <summary>Überschrift der Quittung.</summary>
    /// <param name="art">Ausgabe oder Rücknahme.</param>
    /// <returns>Die Überschrift.</returns>
    public static string Titel(VorgangArt art) => art == VorgangArt.Ausgabe ? "Ausgabebestätigung Ausrüstung" : "Rückgabebestätigung Ausrüstung";

    /// <summary>Dateiname aus Art und Quittungsnummer.</summary>
    /// <param name="d">Inhalt der Quittung.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Daten d) => $"{(d.Art == VorgangArt.Ausgabe ? "Ausgabe" : "Rueckgabe")}-{d.Nummer}.pdf";

    /// <summary>Hinweise auf der Ausgabebestätigung; das Mitglied bestätigt sie mit der Unterschrift.</summary>
    public static IReadOnlyList<string> Hinweise(string gemeinde) =>
    [
        $"Die Ausrüstung bleibt Eigentum der {gemeinde}. Sie wird ausschließlich für den Feuerwehrdienst überlassen.",
        "Die Ausrüstung ist pfleglich zu behandeln und nach den Vorgaben des Herstellers zu reinigen und aufzubewahren. "
            + "Schäden und Verlust sind unverzüglich der Kleiderkammer bzw. dem Gerätewart zu melden.",
        "Beim Austritt aus der Feuerwehr, beim Wechsel in eine Abteilung ohne diesen Bedarf oder auf Anforderung ist die Ausrüstung "
            + "vollständig und gereinigt an die Feuerwehr zurückzugeben.",
    ];

    /// <summary>Erzeugt die Quittung als PDF.</summary>
    /// <param name="d">Inhalt der Quittung.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, Titel(d.Art)));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(110); c.RelativeColumn(); });
                    void Zeile(string etikett, string wert)
                    {
                        t.Cell().PaddingVertical(1).Text(etikett).FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(1).Text(wert).SemiBold();
                    }

                    Zeile("Nummer", d.Nummer);
                    Zeile("Mitglied", $"{d.Name} (Nr. {d.Personalnummer})");
                    Zeile(d.Art == VorgangArt.Ausgabe ? "Ausgegeben am" : "Zurückgegeben am", TimeZoneInfo.ConvertTime(d.Zeitpunkt, Ortszeit.Berlin).ToString("dd.MM.yyyy, HH:mm 'Uhr'"));
                });

                spalte.Item().Text(d.Art == VorgangArt.Ausgabe
                    ? "Das Mitglied hat die folgende Ausrüstung erhalten:"
                    : "Die Feuerwehr bestätigt die Rückgabe der folgenden Ausrüstung:");

                spalte.Item().Table(t =>
                {
                    var rueckgabe = d.Art == VorgangArt.Ruecknahme;
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(80);
                        c.RelativeColumn(3);
                        c.ConstantColumn(45);
                        c.ConstantColumn(35);
                        c.RelativeColumn(2);
                        if (rueckgabe)
                        {
                            c.RelativeColumn(3);
                        }
                    });

                    t.Header(h =>
                    {
                        foreach (var titel in rueckgabe
                            ? new[] { "Inventar-Nr.", "Artikel", "Größe", "Menge", "Serien-Nr.", "Zustand bei Rückgabe" }
                            : new[] { "Inventar-Nr.", "Artikel", "Größe", "Menge", "Serien-Nr." })
                        {
                            h.Cell().BorderBottom(1).PaddingBottom(3).Text(titel).SemiBold().FontSize(9);
                        }
                    });

                    foreach (var p in d.Positionen)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
                        t.Cell().Element(Zelle).Text(p.Inventarnummer ?? "–").Bold().FontSize(11);
                        t.Cell().Element(Zelle).Text(p.Artikel);
                        t.Cell().Element(Zelle).Text(p.Groesse.Length == 0 ? "–" : p.Groesse);
                        t.Cell().Element(Zelle).AlignRight().Text(p.Menge.ToString());
                        t.Cell().Element(Zelle).Text(p.Seriennummer ?? "–").FontSize(9);
                        if (rueckgabe)
                        {
                            t.Cell().Element(Zelle).Text(p.Ordnungsgemaess switch
                            {
                                true => "ordnungsgemäß",
                                false => "nicht ordnungsgemäß" + (string.IsNullOrWhiteSpace(p.Maengel) ? "" : $": {p.Maengel}"),
                                null => "–",
                            });
                        }
                    }
                });

                if (!string.IsNullOrWhiteSpace(d.Bemerkung))
                {
                    spalte.Item().Text($"Bemerkung: {d.Bemerkung}");
                }

                if (d.Art == VorgangArt.Ausgabe)
                {
                    spalte.Item().PaddingTop(6).Text("Hinweise").SemiBold();
                    foreach (var (hinweis, i) in Hinweise(d.Gemeinde).Select((h, i) => (h, i + 1)))
                    {
                        spalte.Item().Row(r =>
                        {
                            r.ConstantItem(16).Text($"{i}.");
                            r.RelativeItem().Text(hinweis);
                        });
                    }

                    spalte.Item().PaddingTop(4).Text("Mit meiner Unterschrift bestätige ich den Empfang der Ausrüstung und habe die Hinweise zur Kenntnis genommen.");
                }
                else
                {
                    spalte.Item().PaddingTop(4).Text("Ausrüstung mit Mängeln wird vom Gerätewart geprüft. Die Rückgabe ist damit vollständig vermerkt.");
                }

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Unterschrift(string text) => r.RelativeItem().Column(c =>
                    {
                        c.Item().BorderBottom(0.75f).Height(24);
                        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });

                    Unterschrift("Ort, Datum");
                    r.ConstantItem(20);
                    Unterschrift(d.Art == VorgangArt.Ausgabe ? "Unterschrift Mitglied (Empfang)" : "Unterschrift Mitglied");
                    r.ConstantItem(20);
                    Unterschrift(d.Art == VorgangArt.Ausgabe ? "Unterschrift ausgebende Person" : "Unterschrift für die Feuerwehr (Rücknahme)");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"{d.Nummer} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
