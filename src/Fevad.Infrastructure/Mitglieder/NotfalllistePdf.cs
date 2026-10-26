using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Mitglieder;

/// <summary>
/// Notfallliste der Jugend- und Kinderfeuerwehr (V46) für Dienste, Fahrten und Lager: wer wie heimkommt, wer abholen darf, wie die Eltern
/// und der Notfallkontakt erreichbar sind und ob die Notfallmappe auf Papier vorliegt. Gesundheitsangaben stehen nur in der Mappe (E3).
/// </summary>
public static class NotfalllistePdf
{
    static NotfalllistePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Eine Zeile der Liste, für die Anzeige aufbereitet.</summary>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Geburtsdatum">Geburtsdatum mit Alter.</param>
    /// <param name="Abteilung">Abteilung.</param>
    /// <param name="Heimweg">Abholregelung.</param>
    /// <param name="Abholberechtigte">Wer abholen darf.</param>
    /// <param name="Eltern">Erziehungsberechtigte mit Telefon, je Person eine Zeile.</param>
    /// <param name="Notfallkontakt">Notfallkontakt mit Telefon.</param>
    /// <param name="Notfallmappe">Stand der Notfallmappe, z. B. „liegt vor (12.03.2026)“.</param>
    public sealed record Zeile(string Name, string Geburtsdatum, string? Abteilung, string Heimweg, string? Abholberechtigte,
        IReadOnlyList<string> Eltern, string? Notfallkontakt, string Notfallmappe);

    /// <summary>Inhalt des PDFs.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Titel">Titel, z. B. „Notfallliste Jugendfeuerwehr“.</param>
    /// <param name="Stand">Zeitpunkt der Erstellung, schon in Ortszeit formatiert.</param>
    /// <param name="Zeilen">Die Kinder und Jugendlichen.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Titel, string Stand, IReadOnlyList<Zeile> Zeilen);

    /// <summary>Erzeugt die Notfallliste quer auf A4.</summary>
    /// <param name="d">Inhalt des PDFs.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Size(PageSizes.A4.Landscape());
            seite.DefaultTextStyle(t => t.FontSize(8.5f));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, d.Titel));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text($"Stand {d.Stand}. Allergien, Medikamente und Schwimmfähigkeit stehen nur in der Notfallmappe auf Papier. "
                    + "Die Liste nach der Veranstaltung vernichten.");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1f);
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "Name", "Geboren", "Heimweg", "Abholberechtigte", "Erziehungsberechtigte", "Notfallkontakt", "Notfallmappe" })
                        {
                            h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
                        }
                    });
                    foreach (var z in d.Zeilen)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(4);
                        t.Cell().Element(Zelle).Column(c =>
                        {
                            c.Item().Text(z.Name).SemiBold();
                            if (z.Abteilung is not null)
                            {
                                c.Item().Text(z.Abteilung).FontColor(Colors.Grey.Darken1);
                            }
                        });
                        t.Cell().Element(Zelle).Text(z.Geburtsdatum);
                        t.Cell().Element(Zelle).Text(z.Heimweg);
                        t.Cell().Element(Zelle).Text(z.Abholberechtigte ?? "");
                        t.Cell().Element(Zelle).Column(c =>
                        {
                            foreach (var e in z.Eltern)
                            {
                                c.Item().Text(e);
                            }
                        });
                        t.Cell().Element(Zelle).Text(z.Notfallkontakt ?? "");
                        t.Cell().Element(Zelle).Text(z.Notfallmappe);
                    }
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
}
