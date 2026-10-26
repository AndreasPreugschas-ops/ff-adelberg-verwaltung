using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Notbetrieb;

/// <summary>
/// Papier-Notfallsatz (R18) für das Gerätehaus, monatlich neu gedruckt: Erreichbarkeit der Einsatzabteilung mit Notfallkontakt und
/// Atemschutz-Einsetzbarkeit, Prüfstand der Fahrzeuge und der nicht grünen Geräte, dazu eine leere Anwesenheitsliste. Deckt einen Ausfall
/// von Internet oder Server ab, bis über den Notbetrieb Stufe 2 (V67) entschieden ist. Atemschutz nur als ja/nein, nie mit Grund.
/// </summary>
public static class NotfallsatzPdf
{
    static NotfallsatzPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Eine Person der Einsatzabteilung.</summary>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Telefon">Mobil oder Festnetz.</param>
    /// <param name="Notfallkontakt">Notfallkontakt mit Telefon.</param>
    /// <param name="Atemschutz">„ja“, „nein“ oder leer, wenn kein Geräteträger.</param>
    public sealed record Person(string Name, string? Telefon, string? Notfallkontakt, string? Atemschutz);

    /// <summary>Ein Fahrzeug oder Gerät mit Prüfstand.</summary>
    /// <param name="Nummer">Funkrufname oder Inventarnummer.</param>
    /// <param name="Bezeichnung">Typ oder Bezeichnung.</param>
    /// <param name="Zustand">Zustand, z. B. „einsatzbereit“.</param>
    /// <param name="NaechstePruefung">Nächste Prüfung, formatiert.</param>
    /// <param name="Hinweis">Was fällig oder überfällig ist.</param>
    public sealed record Technik(string Nummer, string Bezeichnung, string Zustand, string NaechstePruefung, string? Hinweis);

    /// <summary>Inhalt des Notfallsatzes.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Stand">Zeitpunkt des Drucks, schon in Ortszeit formatiert.</param>
    /// <param name="Personen">Einsatzabteilung.</param>
    /// <param name="Fahrzeuge">Fahrzeuge im Dienst.</param>
    /// <param name="Geraete">Geräte, die nicht grün oder nicht einsatzbereit sind.</param>
    /// <param name="GeraeteInOrdnung">Anzahl der übrigen Geräte.</param>
    /// <param name="LeereZeilen">Freie Zeilen am Ende der Anwesenheitsliste.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Stand, IReadOnlyList<Person> Personen, IReadOnlyList<Technik> Fahrzeuge,
        IReadOnlyList<Technik> Geraete, int GeraeteInOrdnung, int LeereZeilen = 8);

    /// <summary>Erzeugt den Notfallsatz auf A4 hoch: Erreichbarkeit, Technik, Anwesenheitsliste.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument =>
        {
            Seite(dokument, d, "Notfallsatz: Erreichbarkeit", spalte =>
            {
                spalte.Item().Text("Für den Ausfall von Internet oder FEVAD. Alarmierung läuft unabhängig davon über die Leitstelle. "
                    + "„AGT“: als Atemschutzgeräteträger am Druckdatum einsatzbereit.");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2f);
                        c.ConstantColumn(34);
                    });
                    Kopfzeile(t, "Name", "Telefon", "Notfallkontakt", "AGT");
                    foreach (var p in d.Personen)
                    {
                        t.Cell().Element(Zelle).Text(p.Name);
                        t.Cell().Element(Zelle).Text(p.Telefon ?? "");
                        t.Cell().Element(Zelle).Text(p.Notfallkontakt ?? "");
                        t.Cell().Element(Zelle).Text(p.Atemschutz ?? "");
                    }
                });
            });

            Seite(dokument, d, "Notfallsatz: Fahrzeuge und Geräte", spalte =>
            {
                spalte.Item().Text("Fahrzeuge").SemiBold();
                spalte.Item().Element(c => Techniktabelle(c, d.Fahrzeuge));
                spalte.Item().PaddingTop(6).Text("Geräte mit fälliger Prüfung oder Mangel").SemiBold();
                if (d.Geraete.Count == 0)
                {
                    spalte.Item().Text("Keine.");
                }
                else
                {
                    spalte.Item().Element(c => Techniktabelle(c, d.Geraete));
                }

                spalte.Item().Text($"Alle übrigen {d.GeraeteInOrdnung} Geräte waren am Druckdatum geprüft und einsatzbereit.");
            });

            Seite(dokument, d, "Anwesenheitsliste", spalte =>
            {
                spalte.Item().Row(r =>
                {
                    foreach (var feld in new[] { "Datum", "Beginn", "Ende", "Anlass (Einsatz, Übung, Dienst)" })
                    {
                        r.RelativeItem().PaddingRight(8).Column(c =>
                        {
                            c.Item().Height(18);
                            c.Item().LineHorizontal(0.5f);
                            c.Item().Text(feld).FontSize(7.5f);
                        });
                    }
                });
                spalte.Item().Text("Anwesende ankreuzen; nach dem Ausfall in FEVAD nachtragen (Dienste und Einsätze sind rückdatierbar).");
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2f);
                        c.ConstantColumn(40);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1.4f);
                    });
                    Kopfzeile(t, "Name", "da", "Funktion / Fahrzeug", "Bemerkung");
                    foreach (var name in d.Personen.Select(p => p.Name).Concat(Enumerable.Repeat("", d.LeereZeilen)))
                    {
                        t.Cell().Element(Zelle).Height(15).Text(name);
                        t.Cell().Element(Zelle).AlignCenter().AlignMiddle().Width(9).Height(9).Border(0.75f);
                        t.Cell().Element(Zelle);
                        t.Cell().Element(Zelle);
                    }
                });
                spalte.Item().PaddingTop(16).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f);
                    c.Item().Text("Unterschrift Einsatzleiter / Leitung des Dienstes").FontSize(7.5f);
                });
            });
        }).AlsPdf();

    private static void Seite(IDocumentContainer dokument, Daten d, string titel, Action<ColumnDescriptor> inhalt) =>
        dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Size(PageSizes.A4);
            seite.DefaultTextStyle(t => t.FontSize(8.5f));
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, titel));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                spalte.Item().Text($"Stand {d.Stand}").FontColor(Colors.Grey.Darken2);
                inhalt(spalte);
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                t.Span("Vertraulich: nur im Gerätehaus aufbewahren, ältere Fassung beim Austausch vernichten · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        });

    private static void Techniktabelle(IContainer container, IReadOnlyList<Technik> zeilen) =>
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1f);
                c.RelativeColumn(1.6f);
                c.RelativeColumn(1f);
                c.RelativeColumn(1f);
                c.RelativeColumn(1.8f);
            });
            Kopfzeile(t, "Nummer", "Bezeichnung", "Zustand", "Nächste Prüfung", "Hinweis");
            foreach (var z in zeilen)
            {
                t.Cell().Element(Zelle).Text(z.Nummer);
                t.Cell().Element(Zelle).Text(z.Bezeichnung);
                t.Cell().Element(Zelle).Text(z.Zustand);
                t.Cell().Element(Zelle).Text(z.NaechstePruefung);
                t.Cell().Element(Zelle).Text(z.Hinweis ?? "");
            }
        });

    private static void Kopfzeile(TableDescriptor t, params string[] spalten) =>
        t.Header(h =>
        {
            foreach (var s in spalten)
            {
                h.Cell().BorderBottom(0.5f).PaddingVertical(2).PaddingRight(4).Text(s).SemiBold();
            }
        });

    private static IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(4);
}
