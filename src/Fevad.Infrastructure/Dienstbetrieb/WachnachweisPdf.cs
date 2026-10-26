using System.Globalization;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Leistungsnachweis einer Brandsicherheitswache (V25, F24) als Grundlage für die Rechnung der Gemeindekasse (E16: die Rechnung stellt die
/// Gemeinde, nicht FEVAD). Die Wachen stehen ohne Namen auf dem Nachweis, nur mit ihren Zeiten; der Veranstalter bestätigt mit Unterschrift.
/// </summary>
public static class WachnachweisPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static WachnachweisPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Zeiten einer Wache.</summary>
    /// <param name="Von">Beginn, formatiert „HH:MM“.</param>
    /// <param name="Bis">Ende, formatiert „HH:MM“.</param>
    /// <param name="Stunden">Abgerechnete Stunden nach Rundung.</param>
    public sealed record Zeile(string Von, string Bis, decimal Stunden);

    /// <summary>Inhalt des Nachweises.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Titel">Titel des Dienstes, z. B. „Sonnwendfeuer“.</param>
    /// <param name="Datum">Datum und Uhrzeit, formatiert.</param>
    /// <param name="Ort">Ort der Wache.</param>
    /// <param name="Auftraggeber">Veranstalter mit Anschrift.</param>
    /// <param name="Kontakt">Ansprechpartner des Veranstalters.</param>
    /// <param name="Wachen">Zeiten je Wache.</param>
    /// <param name="Personenstunden">Summe der abgerechneten Stunden.</param>
    /// <param name="Satz">Bezeichnung und Betrag des Kostensatzes, z. B. „Brandsicherheitswache 16,50 € je Stunde“; leer ohne Satz.</param>
    /// <param name="Betrag">Personenstunden mal Satz.</param>
    /// <param name="Vorlaeufig">Der Satz ist ein Platzhalter; die Satzung liegt noch nicht vor (Platzhalter-Sperre, F3).</param>
    /// <param name="AnteilKasse">Vorschlag Anteil der Kameradschaftskasse; leer bei 0 %.</param>
    /// <param name="Bemerkung">Bericht oder Besonderheiten.</param>
    /// <param name="Erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Titel, string Datum, string? Ort, string? Auftraggeber, string? Kontakt,
        IReadOnlyList<Zeile> Wachen, decimal Personenstunden, string? Satz, decimal? Betrag, bool Vorlaeufig, decimal? AnteilKasse, string? Bemerkung,
        string Erstellt);

    /// <summary>Erzeugt den Nachweis auf A4 hoch.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(10));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Leistungsnachweis Brandsicherheitswache"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span(d.Titel).SemiBold();
                    t.Span($" · {d.Datum}" + (string.IsNullOrWhiteSpace(d.Ort) ? "" : $" · {d.Ort}"));
                });
                spalte.Item().Text($"Auftraggeber (Veranstalter): {d.Auftraggeber ?? "nicht eingetragen"}");
                if (!string.IsNullOrWhiteSpace(d.Kontakt))
                {
                    spalte.Item().Text($"Ansprechpartner: {d.Kontakt}");
                }

                spalte.Item().Text("Rechtsgrundlage: § 2 Abs. 2 Nr. 2 FwG; Kostenersatz nach § 34 FwG und der Kostenersatzsatzung der Gemeinde.").FontSize(9);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "Wache", "von", "bis", "Stunden" })
                        {
                            h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                        }
                    });
                    var nummer = 0;
                    foreach (var z in d.Wachen)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
                        t.Cell().Element(Zelle).Text($"Wache {++nummer}");
                        t.Cell().Element(Zelle).Text(z.Von);
                        t.Cell().Element(Zelle).Text(z.Bis);
                        t.Cell().Element(Zelle).Text(z.Stunden.ToString("0.##", Deutsch));
                    }
                });
                spalte.Item().Text($"Eingesetzte Wachen: {d.Wachen.Count} · Personenstunden: {d.Personenstunden.ToString("0.##", Deutsch)}").SemiBold();
                if (d.Satz is not null && d.Betrag is { } betrag)
                {
                    spalte.Item().Text($"Kostensatz: {d.Satz} · Betrag: {betrag.ToString("C", Deutsch)}");
                }
                else
                {
                    spalte.Item().Text("Kein Kostensatz gewählt: die Gemeindekasse setzt den Betrag nach der Satzung fest.");
                }

                if (d.Vorlaeufig)
                {
                    spalte.Item().Text("Vorläufig: Der Kostensatz ist ein Platzhalter, die Kostenersatzsatzung der Gemeinde liegt noch nicht vor.")
                        .FontColor(Colors.Red.Darken2);
                }

                if (d.AnteilKasse is { } anteil)
                {
                    spalte.Item().Text($"Vorschlag Anteil Kameradschaftskasse: {anteil.ToString("C", Deutsch)}").FontSize(9);
                }

                if (!string.IsNullOrWhiteSpace(d.Bemerkung))
                {
                    spalte.Item().Text($"Bemerkung: {d.Bemerkung}");
                }

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Datum, Unterschrift Wachführer").FontSize(8);
                    });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Datum, Unterschrift Veranstalter (Leistung erhalten)").FontSize(8);
                    });
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Erstellt am {d.Erstellt} · Die Rechnung stellt die Gemeindekasse · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();
}
