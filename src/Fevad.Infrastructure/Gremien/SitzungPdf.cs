using System.Globalization;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Gremien;

/// <summary>Niederschrift einer Sitzung und Anwesenheitsliste zum Ausdruck als PDF (V55; Satzung § 12 Abs. 2, § 13 Abs. 7, § 14 Abs. 5).</summary>
public static class SitzungPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static SitzungPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Ein Wahlgang in Kurzform.</summary>
    /// <param name="Funktion">Gewählte Funktion.</param>
    /// <param name="Wahlgang">Nummer des Wahlgangs.</param>
    /// <param name="Ergebnis">Gewählte mit Stimmen, z. B. „Muster, Max (31 Stimmen)“.</param>
    public sealed record Wahlzeile(string Funktion, int Wahlgang, string Ergebnis);

    /// <summary>Inhalt der Niederschrift.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Titel">Titel der Sitzung.</param>
    /// <param name="Art">Art der Sitzung.</param>
    /// <param name="Zeit">Datum, Beginn und Ende, formatiert.</param>
    /// <param name="Ort">Ort.</param>
    /// <param name="Protokollfuehrer">Name des Protokollführers.</param>
    /// <param name="Anwesend">Namen der Anwesenden.</param>
    /// <param name="Entschuldigt">Namen der entschuldigt Fehlenden.</param>
    /// <param name="Beschlussfaehigkeit">Satz zur Beschlussfähigkeit, z. B. „31 von 48 Stimmberechtigten anwesend: beschlussfähig“.</param>
    /// <param name="Einladung">Satz zur Einladung, z. B. „Eingeladen am 01.03.2027 (Frist 14 Tage eingehalten)“.</param>
    /// <param name="Tagesordnung">Tagesordnung.</param>
    /// <param name="Protokoll">Text der Niederschrift.</param>
    /// <param name="Beschluesse">Beschlüsse der Sitzung.</param>
    /// <param name="Wahlen">Wahlgänge.</param>
    /// <param name="Erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Titel, string Art, string Zeit, string? Ort, string? Protokollfuehrer,
        IReadOnlyList<string> Anwesend, IReadOnlyList<string> Entschuldigt, string? Beschlussfaehigkeit, string? Einladung, string? Tagesordnung,
        string? Protokoll, IReadOnlyList<Beschluss> Beschluesse, IReadOnlyList<Wahlzeile> Wahlen, string Erstellt);

    /// <summary>Erzeugt die Niederschrift auf A4 hoch.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Niederschrift(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(10));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Niederschrift {d.Art}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text(d.Titel).FontSize(13).SemiBold();
                spalte.Item().Text(d.Zeit + (string.IsNullOrWhiteSpace(d.Ort) ? "" : $" · {d.Ort}"));
                if (d.Einladung is not null)
                {
                    spalte.Item().Text(d.Einladung).FontSize(9);
                }

                spalte.Item().Text($"Protokollführung: {d.Protokollfuehrer ?? "nicht eingetragen"}").FontSize(9);
                Abschnitt(spalte, $"Anwesend ({d.Anwesend.Count})", d.Anwesend.Count == 0 ? "nicht erfasst" : string.Join(", ", d.Anwesend));
                if (d.Entschuldigt.Count > 0)
                {
                    Abschnitt(spalte, $"Entschuldigt ({d.Entschuldigt.Count})", string.Join(", ", d.Entschuldigt));
                }

                if (d.Beschlussfaehigkeit is not null)
                {
                    spalte.Item().Text(d.Beschlussfaehigkeit).SemiBold();
                }

                if (!string.IsNullOrWhiteSpace(d.Tagesordnung))
                {
                    Abschnitt(spalte, "Tagesordnung", d.Tagesordnung.Trim());
                }

                if (!string.IsNullOrWhiteSpace(d.Protokoll))
                {
                    Abschnitt(spalte, "Verlauf", d.Protokoll.Trim());
                }

                if (d.Beschluesse.Count > 0)
                {
                    spalte.Item().PaddingTop(6).Text("Beschlüsse").SemiBold();
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(48);
                            c.ConstantColumn(40);
                            c.RelativeColumn(3);
                            c.RelativeColumn(1.4f);
                        });
                        t.Header(h =>
                        {
                            foreach (var s in new[] { "Nr.", "TOP", "Beschluss", "Ergebnis" })
                            {
                                h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
                            }
                        });
                        foreach (var b in d.Beschluesse.OrderBy(b => b.Nummer))
                        {
                            IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingRight(4);
                            t.Cell().Element(Zelle).Text(b.Kennung);
                            t.Cell().Element(Zelle).Text(b.Tagesordnungspunkt ?? "");
                            t.Cell().Element(Zelle).Text(b.Gegenstand);
                            t.Cell().Element(Zelle).Text(Ergebnis(b));
                        }
                    });
                }

                if (d.Wahlen.Count > 0)
                {
                    spalte.Item().PaddingTop(6).Text("Wahlen").SemiBold();
                    foreach (var w in d.Wahlen)
                    {
                        spalte.Item().Text($"{w.Funktion}{(w.Wahlgang > 1 ? $" ({w.Wahlgang}. Wahlgang)" : "")}: {w.Ergebnis}");
                    }
                }

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Vorsitzender").FontSize(8);
                    });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Schriftführer / Protokollführung").FontSize(8);
                    });
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Erstellt am {d.Erstellt} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    /// <summary>Anwesenheitsliste zum Unterschreiben, eine Zeile je Eingeladenem.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <param name="titel">Titel und Zeit der Sitzung.</param>
    /// <param name="namen">Eingeladene, „Nachname, Vorname“.</param>
    /// <param name="stimmberechtigt">Namen der Stimmberechtigten; sie bekommen ein Kreuz in der Spalte.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Anwesenheitsliste(AntragPdf.Kopfdaten kopf, string titel, IReadOnlyList<string> namen, IReadOnlySet<string> stimmberechtigt,
        string erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(10));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, "Anwesenheitsliste"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text(titel).SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);
                        c.RelativeColumn(2);
                        c.ConstantColumn(70);
                        c.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "Nr.", "Name", "stimmber.", "Unterschrift" })
                        {
                            h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                        }
                    });
                    var nr = 0;
                    foreach (var n in namen)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(7);
                        t.Cell().Element(Zelle).Text((++nr).ToString(Deutsch));
                        t.Cell().Element(Zelle).Text(n);
                        t.Cell().Element(Zelle).Text(stimmberechtigt.Contains(n) ? "x" : "");
                        t.Cell().Element(Zelle).Text("");
                    }

                    for (var i = 0; i < 5; i++)
                    {
                        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(7);
                        t.Cell().Element(Zelle).Text((++nr).ToString(Deutsch));
                        t.Cell().Element(Zelle).Text("");
                        t.Cell().Element(Zelle).Text("");
                        t.Cell().Element(Zelle).Text("");
                    }
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Erstellt am {erstellt} · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static string Ergebnis(Beschluss b)
    {
        var stimmen = b.JaStimmen is null ? "" : $" ({b.JaStimmen}/{b.NeinStimmen ?? 0}/{b.Enthaltungen ?? 0})";
        return b.Ergebnis switch
        {
            BeschlussErgebnis.Angenommen => "angenommen" + stimmen,
            BeschlussErgebnis.Abgelehnt => "abgelehnt" + stimmen,
            BeschlussErgebnis.Vertagt => "vertagt",
            _ => "zur Kenntnis genommen",
        };
    }

    private static void Abschnitt(ColumnDescriptor spalte, string ueberschrift, string text)
    {
        spalte.Item().PaddingTop(4).Text(ueberschrift).SemiBold();
        spalte.Item().Text(text);
    }
}
