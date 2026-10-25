using System.Globalization;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Kostenaufstellung als Grundlage für den Kostenersatzbescheid. Den Bescheid erlässt die Gemeinde (§ 34 FwG);
/// FEVAD liefert Einsatzdaten, Kostenpflichtige und die berechneten Posten nach der Kostenersatzsatzung.
/// </summary>
public static class KostenersatzPdf
{
    static KostenersatzPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    public sealed record Kostenpflichtiger(string Name, string? Anschrift, string? Kennzeichen, string? Versicherung, string? Versicherungsnummer);

    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, string Gemeinde, string? GemeindeAnschrift, Einsatz Einsatz, string? Einsatzort,
        IReadOnlyList<Kostenpflichtiger> Kostenpflichtige, Kostenaufstellung Kosten, int RundungMinuten);

    public static string Dateiname(Einsatz e) => $"Kostenersatz-{e.Einsatznummer.Replace('/', '-')}.pdf";

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    private static string Zeit(DateTimeOffset? zeit) =>
        zeit is { } z ? TimeZoneInfo.ConvertTime(z, Ortszeit.Berlin).ToString("dd.MM.yyyy, HH:mm 'Uhr'", Deutsch) : "–";

    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Kostenaufstellung zum Kostenersatz"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"Grundlage für den Kostenersatzbescheid der {d.Gemeinde} nach § 34 Feuerwehrgesetz Baden-Württemberg und der Kostenersatzsatzung. "
                    + "Dies ist kein Bescheid.").FontColor(Colors.Grey.Darken2);

                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(130); c.RelativeColumn(); });
                    void Zeile(string etikett, string? wert)
                    {
                        t.Cell().PaddingVertical(1).Text(etikett).FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(1).Text(string.IsNullOrWhiteSpace(wert) ? "–" : wert);
                    }

                    var e = d.Einsatz;
                    Zeile("Einsatznummer", e.Einsatznummer);
                    Zeile("Stichwort", string.Join(" – ", new[] { e.Stichwort, e.StichwortBeschreibung }.Where(x => !string.IsNullOrWhiteSpace(x))));
                    Zeile("Einsatzort", d.Einsatzort);
                    Zeile("Alarmiert", Zeit(e.Alarmiert));
                    Zeile("Einsatzbereit", Zeit(e.EinsatzbereitschaftHergestellt ?? e.Einsatzende));
                    Zeile("Rechtsgrundlage", e.Rechtsgrundlage == Rechtsgrundlage.Para2Abs2
                        ? "§ 2 Abs. 2 FwG (weitere Aufgabe, kostenersatzpflichtig)"
                        : "§ 2 Abs. 1 FwG (Pflichtaufgabe; Kostenersatz nur in den Fällen des § 34 FwG)");
                });

                spalte.Item().PaddingTop(4).Text("Kostenpflichtige").SemiBold();
                if (d.Kostenpflichtige.Count == 0)
                {
                    spalte.Item().Text("Im Einsatzbericht ist kein Verursacher oder Fahrzeughalter eingetragen.").FontColor(Colors.Red.Darken1);
                }

                foreach (var k in d.Kostenpflichtige)
                {
                    spalte.Item().Text(t =>
                    {
                        t.Span(k.Name).SemiBold();
                        if (!string.IsNullOrWhiteSpace(k.Anschrift)) t.Span($", {k.Anschrift}");
                        if (!string.IsNullOrWhiteSpace(k.Kennzeichen)) t.Span($" · Kennzeichen {k.Kennzeichen}");
                        if (!string.IsNullOrWhiteSpace(k.Versicherung)) t.Span($" · Versicherung {k.Versicherung}{(string.IsNullOrWhiteSpace(k.Versicherungsnummer) ? "" : $", Nr. {k.Versicherungsnummer}")}");
                    });
                }

                spalte.Item().PaddingTop(4).Text("Posten").SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Posten").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Menge").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Satz").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                    });
                    foreach (var p in d.Kosten.Posten)
                    {
                        t.Cell().PaddingVertical(1.5f).Text(p.Bezeichnung);
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text($"{p.Menge.ToString("0.##", Deutsch)} {Einheit(p.Einheit)}");
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(p.Satz));
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(p.Betrag));
                    }

                    t.Cell().ColumnSpan(3).BorderTop(0.5f).PaddingTop(2).Text("Summe").SemiBold();
                    t.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Euro(d.Kosten.Summe)).SemiBold();
                });

                spalte.Item().Text($"Angefangene Einheiten sind auf {d.RundungMinuten} Minuten aufgerundet. Fahrzeuge vom Ausrücken bis zur Einsatzbereitschaft, "
                    + "Personal von der Alarmierung bis zur Einsatzbereitschaft.").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                foreach (var h in d.Kosten.Hinweise)
                {
                    spalte.Item().Text($"Hinweis: {h}").FontColor(Colors.Red.Darken1);
                }
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Für die {d.Gemeinde}{(d.GemeindeAnschrift is { } a ? $", {a}" : "")} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static string Einheit(Kosteneinheit einheit) => einheit switch
    {
        Kosteneinheit.Stunde => "Std.",
        Kosteneinheit.Tag => "Tage",
        Kosteneinheit.Stueck => "Stück",
        _ => "",
    };
}
