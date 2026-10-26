using System.Globalization;
using System.Text;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Entschaedigung;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Atemschutz;

/// <summary>
/// Expositionsnachweis je Atemschutzgeräteträger (KoAtEx) nach § 14 Abs. 3 GefStoffV als PDF und CSV: Einsätze und Einsatzübungen
/// unter Atemschutz mit Dauer, Pressluftatmer, Maske und Gefahrstoff. Kein ZED-Format; aufbewahrt 40 Jahre (L8).
/// Der Dateiname enthält nur die Personalnummer, keinen Namen.
/// </summary>
public static class Expositionsnachweis
{
    static Expositionsnachweis()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Inhalt des Nachweises für eine Person.</summary>
    /// <param name="Kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="Name">Name des Geräteträgers.</param>
    /// <param name="Personalnummer">Personalnummer, auch für den Dateinamen.</param>
    /// <param name="Geburtsdatum">Geburtsdatum.</param>
    /// <param name="Eintraege">Die aufgeführten Atemschutznachweise.</param>
    /// <param name="Erstellt">Zeitpunkt der Erstellung (UTC).</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Name, string Personalnummer, DateOnly Geburtsdatum,
        IReadOnlyList<AtemschutzNachweis> Eintraege, DateTimeOffset Erstellt);

    /// <summary>Dateiname ohne Namen der Person.</summary>
    /// <param name="personalnummer">Personalnummer der Person.</param>
    /// <param name="endung">Dateiendung ohne Punkt, z. B. <c>pdf</c> oder <c>csv</c>.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(string personalnummer, string endung) => $"Expositionsnachweis-{personalnummer}.{endung}";

    private static string Text(AtemschutzNachweisArt art) => art switch
    {
        AtemschutzNachweisArt.Einsatz => "Einsatz",
        AtemschutzNachweisArt.Belastungsuebung => "Belastungsübung",
        AtemschutzNachweisArt.Einsatzuebung => "Einsatzübung",
        AtemschutzNachweisArt.Exposition => "Exposition ohne Atemschutzgerät",
        _ => "Unterweisung",
    };

    /// <summary>Erzeugt den Nachweis als CSV-Datei.</summary>
    /// <param name="d">Inhalt des Nachweises.</param>
    /// <returns>Die CSV-Datei als Bytes.</returns>
    public static byte[] Csv(Daten d)
    {
        var csv = new StringBuilder();
        AbrechnungExport.CsvZeile(csv, "Personalnummer", "Datum", "Art", "Minuten", "Pressluftatmer", "Maske", "Gerätetyp", "Gefahrstoffe", "Ort", "Tätigkeit");
        foreach (var e in d.Eintraege)
        {
            AbrechnungExport.CsvZeile(csv, d.Personalnummer, e.Datum.ToString("dd.MM.yyyy", Deutsch), Text(e.Art), e.Minuten?.ToString(Deutsch) ?? "",
                e.PaGeraet ?? "", e.Maske ?? "", e.Geraetetyp ?? "", e.Exposition ?? "", e.Ort ?? "", e.Taetigkeit ?? "");
        }

        return AbrechnungExport.Utf8MitBom(csv);
    }

    /// <summary>Erzeugt den Nachweis als PDF.</summary>
    /// <param name="d">Inhalt des Nachweises.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Pdf(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Expositionsnachweis Atemschutz"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"{d.Name}, Personalnummer {d.Personalnummer}, geboren am {d.Geburtsdatum:dd.MM.yyyy}");
                spalte.Item().Text("Tätigkeiten unter Atemschutz mit möglicher Exposition gegenüber Gefahrstoffen (§ 14 Abs. 3 GefStoffV). "
                    + "Aufzubewahren 40 Jahre nach der letzten Exposition; bei Ausscheiden erhält die Person eine Kopie.").FontSize(9);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(58);
                        c.ConstantColumn(62);
                        c.ConstantColumn(36);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                    });
                    t.Header(h =>
                    {
                        foreach (var titel in new[] { "Datum", "Art", "Min.", "Pressluftatmer", "Maske", "Gefahrstoffe", "Ort, Tätigkeit" })
                        {
                            h.Cell().BorderBottom(1).PaddingBottom(3).Text(titel).SemiBold().FontSize(9);
                        }
                    });
                    foreach (var e in d.Eintraege)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
                        t.Cell().Element(Zelle).Text(e.Datum.ToString("dd.MM.yyyy")).FontSize(9);
                        t.Cell().Element(Zelle).Text(Text(e.Art)).FontSize(9);
                        t.Cell().Element(Zelle).AlignRight().Text(e.Minuten?.ToString() ?? "–").FontSize(9);
                        t.Cell().Element(Zelle).Text(e.PaGeraet ?? e.Geraetetyp ?? "–").FontSize(9);
                        t.Cell().Element(Zelle).Text(e.Maske ?? "–").FontSize(9);
                        t.Cell().Element(Zelle).Text(e.Exposition ?? "–").FontSize(9);
                        t.Cell().Element(Zelle).Text(string.Join(", ", new[] { e.Ort, e.Taetigkeit }.Where(x => x is not null)) is { Length: > 0 } x ? x : "–").FontSize(9);
                    }
                });
                if (d.Eintraege.Count == 0)
                {
                    spalte.Item().Text("Keine Einträge.").Italic();
                }

                spalte.Item().Text($"Summe: {d.Eintraege.Count} Einträge, {d.Eintraege.Sum(e => e.Minuten ?? 0)} Minuten unter Atemschutz.");
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Erstellt am {TimeZoneInfo.ConvertTime(d.Erstellt, Ortszeit.Berlin):dd.MM.yyyy, HH:mm} Uhr · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
