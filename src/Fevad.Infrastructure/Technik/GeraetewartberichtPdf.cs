using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Technik;

/// <summary>Jahresbericht des Gerätewarts als PDF für Hauptversammlung und Haushaltsanmeldung (V64).</summary>
public static class GeraetewartberichtPdf
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    static GeraetewartberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt das PDF auf A4 hoch.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <param name="b">Der Bericht.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung, formatiert.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(AntragPdf.Kopfdaten kopf, Geraetewartbericht b, string erstellt) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.DefaultTextStyle(t => t.FontSize(9));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, $"Jahresbericht Gerätewart {b.Jahr}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text($"Fahrzeuge und Geräte {b.Jahr}").FontSize(13).SemiBold();

                Ueberschrift(spalte, "Kennzahlen");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                    });
                    Zeile(t, $"Prüfquote am {b.Stichtag.ToString("dd.MM.yyyy", Deutsch)} (ohne überfällige Prüfung)", b.Pruefquote is { } q ? $"{q.ToString("0.0", Deutsch)} %" : "–");
                    Zeile(t, "Geräte und Fahrzeuge mit Prüfplan / davon überfällig", $"{b.Pruefgegenstaende} / {b.Ueberfaellig}");
                    Zeile(t, "Prüfungen im Jahr / davon durch Fremdfirmen / nicht bestanden", $"{b.Pruefungen} / {b.PruefungenExtern} / {b.NichtBestanden}");
                    Zeile(t, "Mängel gemeldet / behoben", $"{b.MaengelGemeldet} / {b.MaengelBehoben}");
                    Zeile(t, "Mängel offen am Jahresende / davon außer Betrieb", $"{b.MaengelOffen} / {b.AusserBetrieb}");
                    Zeile(t, "Fahrzeuge im Dienst", b.FahrzeugeImDienst.ToString(Deutsch));
                    Zeile(t, "Geräte im Bestand / Zugänge / Abgänge",
                        $"{b.Bestand.Sum(x => x.Bestand)} / {b.Bestand.Sum(x => x.Zugaenge)} / {b.Bestand.Sum(x => x.Abgaenge)}");
                });
                if (b.FahrzeugZugaenge.Count > 0)
                {
                    spalte.Item().Text($"In Dienst gestellt: {string.Join(", ", b.FahrzeugZugaenge)}");
                }

                if (b.FahrzeugAbgaenge.Count > 0)
                {
                    spalte.Item().Text($"Außer Dienst gestellt: {string.Join(", ", b.FahrzeugAbgaenge)}");
                }

                Ueberschrift(spalte, "Kosten je Fahrzeug");
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
                    Kopf(t, 1, "Fahrzeug", "Fahrten", "km", "Liter", "Treibstoff", "Reparaturen", "Summe");
                    foreach (var k in b.Kosten)
                    {
                        t.Cell().Element(Zelle).Text($"{k.Funkrufname} ({k.Typ}{(k.Baujahr is { } bj ? $", {bj}" : "")})");
                        t.Cell().Element(Zelle).AlignRight().Text(k.Fahrten.ToString(Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(k.Kilometer.ToString("N0", Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(k.Liter.ToString("N0", Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(Euro(k.Treibstoff));
                        t.Cell().Element(Zelle).AlignRight().Text(Euro(k.Reparaturen));
                        t.Cell().Element(Zelle).AlignRight().Text(Euro(k.Summe)).SemiBold();
                    }

                    t.Cell().ColumnSpan(4).PaddingTop(2).Text("Summe").SemiBold();
                    t.Cell().PaddingTop(2).AlignRight().Text(Euro(b.Kosten.Sum(k => k.Treibstoff))).SemiBold();
                    t.Cell().PaddingTop(2).AlignRight().Text(Euro(b.Kosten.Sum(k => k.Reparaturen))).SemiBold();
                    t.Cell().PaddingTop(2).AlignRight().Text(Euro(b.Kosten.Sum(k => k.Summe))).SemiBold();
                });

                Ueberschrift(spalte, "Bestand je Geräteart");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    Kopf(t, 1, "Geräteart", "Bestand", "Zugänge", "Abgänge");
                    foreach (var z in b.Bestand)
                    {
                        t.Cell().Element(Zelle).Text(z.Geraeteart);
                        t.Cell().Element(Zelle).AlignRight().Text(z.Bestand.ToString(Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(z.Zugaenge == 0 ? "" : z.Zugaenge.ToString(Deutsch));
                        t.Cell().Element(Zelle).AlignRight().Text(z.Abgaenge == 0 ? "" : z.Abgaenge.ToString(Deutsch));
                    }
                });

                Ueberschrift(spalte, "Ausblick Ersatzbeschaffung");
                if (b.Ausblick.Count == 0)
                {
                    spalte.Item().Text("Keine Ersatzbeschaffung absehbar.");
                }
                else
                {
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(40);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });
                        Kopf(t, 3, "Jahr", "Gegenstand", "Grund", "Kosten");
                        foreach (var a in b.Ausblick)
                        {
                            t.Cell().Element(Zelle).Text(a.Jahr.ToString(CultureInfo.InvariantCulture));
                            t.Cell().Element(Zelle).Text(a.Gegenstand);
                            t.Cell().Element(Zelle).Text(a.Grund);
                            t.Cell().Element(Zelle).AlignRight().Text(a.Kosten is { } k ? Euro(k) : "");
                        }
                    });
                }

                Ueberschrift(spalte, "Haushalt");
                if (b.Kuerzungen.Count == 0)
                {
                    spalte.Item().Text($"Keine Kürzungen der Gemeinde in der Haushaltsanmeldung {b.Jahr} erfasst.");
                }
                else
                {
                    spalte.Item().Text($"Kürzungen der Gemeinde in der Haushaltsanmeldung {b.Jahr}:");
                    foreach (var k in b.Kuerzungen)
                    {
                        spalte.Item().PaddingLeft(8).Text($"{k.Bezeichnung}: {Text(k.Ergebnis)}, angemeldet {Euro(k.Angemeldet)}, beschlossen {Euro(k.Beschlossen)}");
                    }
                }

                spalte.Item().Text(b.AnmeldungFolgejahr is { } f ? $"Haushaltsanmeldung {b.Jahr + 1}: {f}." : $"Für {b.Jahr + 1} gibt es noch keine Haushaltsanmeldung.");

                spalte.Item().PaddingTop(30).Width(220).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f);
                    c.Item().Text("Gerätewart").FontSize(8);
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
        })).AlsPdf();

    private static IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(4);

    private static void Ueberschrift(ColumnDescriptor spalte, string text) => spalte.Item().PaddingTop(8).Text(text).FontSize(11).SemiBold();

    // Die ersten Spalten (Texte) linksbündig, die übrigen (Zahlen) rechtsbündig.
    private static void Kopf(TableDescriptor t, int links, params string[] titel) => t.Header(h =>
    {
        for (var i = 0; i < titel.Length; i++)
        {
            var zelle = h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4);
            (i < links ? zelle : zelle.AlignRight()).Text(titel[i]).SemiBold();
        }
    });

    private static void Zeile(TableDescriptor t, string text, string wert)
    {
        t.Cell().Element(Zelle).Text(text);
        t.Cell().Element(Zelle).AlignRight().Text(wert).SemiBold();
    }

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .OfType<DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();
}
