using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Fevad.Domain.Haushalt;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Haushalt;

/// <summary>
/// Haushaltsanmeldung der Feuerwehr als PDF für Kämmerei und Gemeinderat (V29): Summen mit Vorjahresvergleich, Positionen je Haushaltsstelle
/// nach Dringlichkeit, Investitionsprogramm mit Raten und Verpflichtungsermächtigungen sowie die Begründungen.
/// </summary>
public static class HaushaltPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static HaushaltPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Inhalt des PDFs.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Anmeldung">Anmeldung mit Positionen und Haushaltsstellen.</param>
    /// <param name="Vorjahr">Anmeldung des Vorjahres oder null; ihre Ansätze dienen dem Vergleich.</param>
    /// <param name="Erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, Haushaltsanmeldung Anmeldung, Haushaltsanmeldung? Vorjahr, string Erstellt);

    /// <summary>Erzeugt das PDF auf A4 hoch.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d)
    {
        var a = d.Anmeldung;
        var positionen = a.Angemeldet.ToList();
        var vorjahr = d.Vorjahr?.Angemeldet.GroupBy(p => p.HaushaltsstelleId).ToDictionary(g => g.Key, g => g.Sum(Haushaltsueberwachung.Ansatz))
            ?? new Dictionary<Guid, decimal>();
        return Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(9));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Haushaltsanmeldung Feuerwehr {a.Jahr}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text($"Anmeldung der Feuerwehr zum Haushaltsplan {a.Jahr}").FontSize(13).SemiBold();
                var stand = new List<string> { $"Stand: {Text(a.Status)}" };
                if (a.BeratenAm is { } beraten)
                {
                    stand.Add($"im Feuerwehrausschuss beraten am {beraten.ToString("dd.MM.yyyy", Deutsch)}{(a.Ausschussbeschluss is { } b ? $" ({b})" : "")}");
                }

                if (a.FreigegebenAm is { } frei)
                {
                    stand.Add($"freigegeben am {frei.ToString("dd.MM.yyyy", Deutsch)}");
                }

                spalte.Item().Text(string.Join(" · ", stand));

                // Summen mit Vorjahresvergleich.
                spalte.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    Kopfzeile(t, "Übersicht", $"Anmeldung {a.Jahr}", d.Vorjahr is null ? "" : $"Ansatz {a.Jahr - 1}");
                    foreach (var teil in Enum.GetValues<Haushaltsteil>())
                    {
                        var ids = positionen.Where(p => p.Haushaltsstelle?.Teil == teil && p.Haushaltsstelle?.Einzahlung != true).Select(p => p.HaushaltsstelleId).ToHashSet();
                        var vj = d.Vorjahr?.Angemeldet.Where(p => p.Haushaltsstelle?.Teil == teil && p.Haushaltsstelle?.Einzahlung != true).Sum(Haushaltsueberwachung.Ansatz);
                        Zeile(t, teil == Haushaltsteil.Ergebnishaushalt ? "Ergebnishaushalt (Aufwand)" : "Finanzhaushalt (Auszahlungen im Planjahr)",
                            Euro(a.Summe(teil)), vj is null ? "" : Euro(vj.Value), fett: true);
                    }

                    Zeile(t, "Erwartete Zuwendungen (Einzahlungen)", Euro(a.Zuwendungen), "");
                });

                // Positionen je Haushaltsstelle.
                foreach (var teil in Enum.GetValues<Haushaltsteil>())
                {
                    var gruppen = positionen.Where(p => p.Haushaltsstelle?.Teil == teil)
                        .GroupBy(p => p.Haushaltsstelle!).OrderBy(g => g.Key.Sortierung).ToList();
                    if (gruppen.Count == 0)
                    {
                        continue;
                    }

                    spalte.Item().PaddingTop(8).Text(Text(teil)).FontSize(11).SemiBold();
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.1f);
                        });
                        Kopfzeile(t, "Haushaltsstelle / Position", "Dringlichkeit", $"{a.Jahr}", d.Vorjahr is null ? "" : $"Ansatz {a.Jahr - 1}");
                        foreach (var g in gruppen)
                        {
                            var s = g.Key;
                            t.Cell().ColumnSpan(2).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).PaddingBottom(2)
                                .Text($"{s.Produkt} · {s.Sachkonto} {s.Bezeichnung}{(s.Innenauftrag is { } ia ? $" ({ia})" : "")}").SemiBold();
                            t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).AlignRight().Text(Euro(g.Sum(p => p.BetragPlanjahr))).SemiBold();
                            t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).AlignRight()
                                .Text(d.Vorjahr is null ? "" : Euro(vorjahr.GetValueOrDefault(s.Id)));
                            foreach (var p in g.OrderByDescending(p => p.Pflicht).ThenByDescending(p => p.Prioritaet).ThenBy(p => p.Bezeichnung))
                            {
                                t.Cell().PaddingLeft(8).PaddingVertical(1).Text($"{p.Bezeichnung}{(p.Menge != 1 ? $" ({p.Menge.ToString("0.##", Deutsch)} × {Euro(p.Einzelpreis)})" : "")}");
                                t.Cell().PaddingVertical(1).Text(Text(p.Prioritaet) + (p.Pflicht ? ", Pflicht" : ""));
                                t.Cell().PaddingVertical(1).AlignRight().Text(Euro(p.BetragPlanjahr));
                                t.Cell().PaddingVertical(1).AlignRight().Text(p.Ergebnis is { } e ? Text(e) : "");
                            }
                        }
                    });
                }

                // Investitionsprogramm.
                var investitionen = positionen.Where(p => p.Haushaltsstelle?.Teil == Haushaltsteil.Finanzhaushalt && p.Haushaltsstelle?.Einzahlung != true)
                    .OrderByDescending(p => p.Prioritaet).ToList();
                if (investitionen.Count > 0)
                {
                    spalte.Item().PaddingTop(8).Text("Investitionsprogramm").FontSize(11).SemiBold();
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.6f);
                            for (var i = 0; i < 6; i++)
                            {
                                c.RelativeColumn(1);
                            }
                        });
                        Kopfzeile(t, "Maßnahme", $"{a.Jahr}", $"{a.Jahr + 1}", $"{a.Jahr + 2}", $"{a.Jahr + 3}", "VE", "Zuwendung");
                        foreach (var p in investitionen)
                        {
                            t.Cell().PaddingVertical(1).Text(p.Bezeichnung);
                            foreach (var betrag in new[] { p.BetragPlanjahr, p.RateFolgejahr1, p.RateFolgejahr2, p.RateFolgejahr3, p.Verpflichtungsermaechtigung, p.Zuwendung })
                            {
                                t.Cell().PaddingVertical(1).AlignRight().Text(betrag is null or 0 ? "–" : Euro(betrag.Value));
                            }
                        }
                    });
                    if (investitionen.Any(p => p.Folgekosten > 0))
                    {
                        spalte.Item().Text($"Jährliche Folgekosten der Investitionen: {Euro(investitionen.Sum(p => p.Folgekosten ?? 0))}").FontSize(8);
                    }
                }

                // Begründungen.
                var begruendet = positionen.Where(p => !string.IsNullOrWhiteSpace(p.Begruendung) || !string.IsNullOrWhiteSpace(p.FolgenBeiAblehnung)
                        || !string.IsNullOrWhiteSpace(p.Rechtsgrundlage) || !string.IsNullOrWhiteSpace(p.Bedarfsplan))
                    .OrderBy(p => p.Haushaltsstelle?.Sortierung).ThenByDescending(p => p.Prioritaet).ToList();
                if (begruendet.Count > 0)
                {
                    spalte.Item().PaddingTop(8).Text("Begründungen").FontSize(11).SemiBold();
                    foreach (var p in begruendet)
                    {
                        spalte.Item().Column(c =>
                        {
                            c.Item().Text($"{p.Bezeichnung} ({Euro(p.BetragPlanjahr)})").SemiBold();
                            Absatz(c, "Begründung", p.Begruendung);
                            Absatz(c, "Rechtsgrundlage", p.Rechtsgrundlage);
                            Absatz(c, "Bedarfsplan", p.Bedarfsplan);
                            Absatz(c, "Folgen bei Ablehnung", p.FolgenBeiAblehnung);
                        });
                    }
                }

                spalte.Item().PaddingTop(30).Width(220).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f);
                    c.Item().Text("Feuerwehrkommandant").FontSize(8);
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
    }

    private static void Kopfzeile(TableDescriptor t, params string[] titel) => t.Header(h =>
    {
        for (var i = 0; i < titel.Length; i++)
        {
            var zelle = h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4);
            (i == 0 ? zelle : zelle.AlignRight()).Text(titel[i]).SemiBold();
        }
    });

    private static void Zeile(TableDescriptor t, string text, string betrag, string vergleich, bool fett = false)
    {
        IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(4);
        var erste = t.Cell().Element(Zelle).Text(text);
        var zweite = t.Cell().Element(Zelle).AlignRight().Text(betrag);
        t.Cell().Element(Zelle).AlignRight().Text(vergleich);
        if (fett)
        {
            erste.SemiBold();
            zweite.SemiBold();
        }
    }

    private static void Absatz(ColumnDescriptor c, string titel, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            c.Item().Text(t =>
            {
                t.Span(titel + ": ").Italic();
                t.Span(text.Trim());
            });
        }
    }

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .OfType<DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();
}
