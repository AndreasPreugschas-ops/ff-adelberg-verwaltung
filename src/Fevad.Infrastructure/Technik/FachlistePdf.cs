using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Fachliste eines Fahrzeugs oder des Gerätehauses (V58): je Gerätefach die Geräte mit nächster Prüfung,
/// dazu eine Spalte zum Abhaken bei der Beladekontrolle.
/// </summary>
public static class FachlistePdf
{
    static FachlistePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Ein Gerät in der Liste.</summary>
    /// <param name="Fach">Lagerort bzw. Geräteraum; leer wird „ohne Fach“.</param>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Gerätebezeichnung.</param>
    /// <param name="NaechstePruefung">Nächste Fälligkeit über alle Prüfplanpositionen.</param>
    /// <param name="Zustand">Zustand als Text, z. B. „außer Betrieb“.</param>
    public sealed record Zeile(string? Fach, string Inventarnummer, string Bezeichnung, DateOnly? NaechstePruefung, string Zustand);

    /// <summary>Alles, was die Liste braucht.</summary>
    /// <param name="Kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="Ort">Fahrzeug (Funkrufname und Typ) oder „Gerätehaus“.</param>
    /// <param name="Stand">Erstellungszeitpunkt in Ortszeit, für die Fußzeile.</param>
    /// <param name="Zeilen">Geräte; die Gruppierung nach Fach übernimmt das PDF.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Ort, DateTime Stand, IReadOnlyList<Zeile> Zeilen);

    /// <summary>Dateiname ohne Sonderzeichen.</summary>
    public static string Dateiname(Daten d) =>
        $"Fachliste-{new string(d.Ort.Select(z => char.IsLetterOrDigit(z) ? z : '-').ToArray()).Trim('-')}.pdf";

    /// <summary>Erzeugt das PDF, je Fach eine Tabelle.</summary>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Fachliste {d.Ort}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(10);
                if (d.Zeilen.Count == 0)
                {
                    spalte.Item().Text("Keine Geräte erfasst.");
                }

                foreach (var fach in d.Zeilen.GroupBy(z => string.IsNullOrWhiteSpace(z.Fach) ? "ohne Fach" : z.Fach!).OrderBy(g => g.Key, StringComparer.CurrentCulture))
                {
                    spalte.Item().Text(fach.Key).SemiBold().FontSize(11);
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.RelativeColumn();
                            c.ConstantColumn(70);
                            c.ConstantColumn(80);
                            c.ConstantColumn(45);
                        });
                        t.Header(h =>
                        {
                            foreach (var titel in new[] { "Inventar-Nr.", "Gerät", "geprüft bis", "Zustand", "da" })
                            {
                                h.Cell().BorderBottom(1).PaddingBottom(2).Text(titel).SemiBold().FontSize(9);
                            }
                        });
                        foreach (var z in fach.OrderBy(z => z.Inventarnummer, StringComparer.Ordinal))
                        {
                            IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                            t.Cell().Element(Zelle).Text(z.Inventarnummer).SemiBold();
                            t.Cell().Element(Zelle).Text(z.Bezeichnung);
                            t.Cell().Element(Zelle).Text(PlakettenPdf.GeprueftBis(z.NaechstePruefung));
                            t.Cell().Element(Zelle).Text(z.Zustand);
                            t.Cell().Element(Zelle).AlignCenter().Width(10).Height(10).Border(0.75f);
                        }
                    });
                }
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Stand {d.Stand:dd.MM.yyyy, HH:mm} Uhr · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
