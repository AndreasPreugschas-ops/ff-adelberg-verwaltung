using System.Globalization;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Entschaedigung;

/// <summary>
/// Jahresbescheinigung der Entschädigung für ein Mitglied (V34): alle Posten abgeschlossener Abrechnungen eines Kalenderjahrs mit Summen
/// je steuerlicher Art und den Freibeträgen als Hinweis. Maßgeblich ist das Jahr des Abschlusses der Abrechnung, weil danach ausgezahlt wird.
/// Keine Steuerberatung.
/// </summary>
public static class Jahresbescheinigung
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static Jahresbescheinigung()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Ein Posten der Bescheinigung.</summary>
    /// <param name="Datum">Tag des Einsatzes, Dienstes oder Lehrgangs.</param>
    /// <param name="Bezeichnung">Lesbarer Text.</param>
    /// <param name="Steuerart">Steuerliche Einordnung.</param>
    /// <param name="Betrag">Betrag in Euro.</param>
    /// <param name="Abrechnung">Bezeichnung der Abrechnung.</param>
    public sealed record Zeile(DateOnly Datum, string Bezeichnung, Steuerart Steuerart, decimal Betrag, string Abrechnung);

    /// <summary>Freibeträge, die als Hinweis genannt werden; jeweils null, wenn nicht hinterlegt.</summary>
    /// <param name="AufwandMonat">§ 3 Nr. 12 EStG je Monat.</param>
    /// <param name="UebungsleiterJahr">§ 3 Nr. 26 EStG je Jahr.</param>
    /// <param name="SonstigeFreigrenze">§ 22 Nr. 3 EStG je Jahr.</param>
    public sealed record Freibetraege(decimal? AufwandMonat, decimal? UebungsleiterJahr, decimal? SonstigeFreigrenze);

    /// <summary>Inhalt der Bescheinigung.</summary>
    /// <param name="Kopf">Anschrift und Logo der Feuerwehr.</param>
    /// <param name="Name">Name des Mitglieds.</param>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Jahr">Kalenderjahr.</param>
    /// <param name="Zeilen">Die Posten.</param>
    /// <param name="Freibetraege">Freibeträge für die Hinweise.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Name, string Personalnummer, int Jahr, IReadOnlyList<Zeile> Zeilen, Freibetraege Freibetraege);

    /// <summary>Lädt die Posten eines Mitglieds aus Abrechnungen, die im Jahr abgeschlossen wurden.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="jahr">Kalenderjahr des Abschlusses.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Posten nach Datum.</returns>
    public static async Task<List<Zeile>> ZeilenAsync(AppDbContext db, Guid mitgliedId, int jahr, CancellationToken ct = default)
    {
        var von = Ortszeit.TagesbeginnUtc(new DateOnly(jahr, 1, 1));
        var bis = Ortszeit.TagesbeginnUtc(new DateOnly(jahr + 1, 1, 1));
        return await db.AbrechnungsPosten.AsNoTracking()
            .Where(p => p.MitgliedId == mitgliedId && p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen
                && p.Abrechnung.AbgeschlossenAm >= von && p.Abrechnung.AbgeschlossenAm < bis)
            .OrderBy(p => p.Datum)
            .Select(p => new Zeile(p.Datum, p.Bezeichnung, p.Steuerart, p.Betrag, p.Abrechnung!.Bezeichnung))
            .ToListAsync(ct);
    }

    /// <summary>Jahre, in denen für das Mitglied Abrechnungen abgeschlossen wurden, neueste zuerst.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Jahre.</returns>
    public static async Task<List<int>> JahreAsync(AppDbContext db, Guid mitgliedId, CancellationToken ct = default)
    {
        var zeitpunkte = await db.AbrechnungsPosten.AsNoTracking()
            .Where(p => p.MitgliedId == mitgliedId && p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen && p.Abrechnung.AbgeschlossenAm != null)
            .Select(p => p.Abrechnung!.AbgeschlossenAm!.Value)
            .Distinct()
            .ToListAsync(ct);
        return zeitpunkte.Select(z => Ortszeit.Datum(z).Year).Distinct().OrderDescending().ToList();
    }

    /// <summary>Hinweis zur steuerlichen Behandlung einer Art, mit hinterlegtem Freibetrag.</summary>
    /// <param name="art">Die Steuerart.</param>
    /// <param name="f">Die Freibeträge.</param>
    /// <returns>Der Hinweistext.</returns>
    public static string Hinweis(Steuerart art, Freibetraege f) => art switch
    {
        Steuerart.Aufwandsentschaedigung => f.AufwandMonat is { } m
            ? $"steuerfrei bis {Euro(m)} je Monat ({Euro(m * 12)} im Jahr), zusammen mit anderen Aufwandsentschädigungen aus öffentlichen Kassen"
            : "steuerfrei bis zum Freibetrag nach § 3 Nr. 12 EStG",
        Steuerart.Uebungsleiter => f.UebungsleiterJahr is { } u
            ? $"steuerfrei bis {Euro(u)} im Jahr, zusammen mit anderen Ausbilder- und Übungsleitertätigkeiten"
            : "steuerfrei bis zum Freibetrag nach § 3 Nr. 26 EStG",
        Steuerart.Verdienstausfall => "Ersatz für entgangenen Verdienst, grundsätzlich steuerpflichtig",
        Steuerart.Auslagenersatz => "Ersatz belegter Auslagen und Fahrkosten, steuerfrei",
        Steuerart.Sonstiges => f.SonstigeFreigrenze is { } s
            ? $"steuerfrei, wenn alle sonstigen Leistungen im Jahr unter {Euro(s)} bleiben (Freigrenze)"
            : "Freigrenze nach § 22 Nr. 3 EStG",
        _ => "",
    };

    /// <summary>Dateiname ohne Namen des Mitglieds.</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(int jahr) => $"Entschaedigung-Jahresbescheinigung-{jahr}.pdf";

    /// <summary>Erzeugt die Bescheinigung als PDF.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF.</returns>
    public static byte[] Pdf(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Jahresbescheinigung Entschädigung {d.Jahr}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span("Für: ").SemiBold();
                    t.Span($"{d.Name}, Personalnummer {d.Personalnummer}");
                });
                spalte.Item().Text($"Entschädigung nach § 16 FwG aus Abrechnungen, die {d.Jahr} abgeschlossen und danach ausgezahlt wurden.").FontSize(9);

                spalte.Item().PaddingTop(4).Text("Summen je steuerlicher Art").SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.ConstantColumn(80);
                        c.RelativeColumn(3);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Art").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                        h.Cell().BorderBottom(0.5f).PaddingLeft(8).Text("Hinweis").SemiBold();
                    });
                    foreach (var g in d.Zeilen.GroupBy(z => z.Steuerart).OrderBy(g => g.Key))
                    {
                        t.Cell().PaddingVertical(1.5f).Text(Text(g.Key));
                        t.Cell().PaddingVertical(1.5f).AlignRight().Text(Euro(g.Sum(z => z.Betrag)));
                        t.Cell().PaddingVertical(1.5f).PaddingLeft(8).Text(Hinweis(g.Key, d.Freibetraege)).FontSize(8.5f);
                    }

                    t.Cell().BorderTop(0.5f).PaddingTop(2).Text("Gesamt").SemiBold();
                    t.Cell().BorderTop(0.5f).PaddingTop(2).AlignRight().Text(Euro(d.Zeilen.Sum(z => z.Betrag))).SemiBold();
                    t.Cell().BorderTop(0.5f);
                });

                spalte.Item().PaddingTop(4).Text("Einzelposten").SemiBold();
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.ConstantColumn(70);
                    });
                    t.Header(h =>
                    {
                        h.Cell().BorderBottom(0.5f).Text("Datum").SemiBold();
                        h.Cell().BorderBottom(0.5f).Text("Was").SemiBold();
                        h.Cell().BorderBottom(0.5f).Text("Art").SemiBold();
                        h.Cell().BorderBottom(0.5f).AlignRight().Text("Betrag").SemiBold();
                    });
                    foreach (var z in d.Zeilen)
                    {
                        t.Cell().PaddingVertical(1).Text(z.Datum.ToString("dd.MM.yyyy", Deutsch)).FontSize(8.5f);
                        t.Cell().PaddingVertical(1).Text(z.Bezeichnung).FontSize(8.5f);
                        t.Cell().PaddingVertical(1).Text(Kurztext(z.Steuerart)).FontSize(8.5f);
                        t.Cell().PaddingVertical(1).AlignRight().Text(Euro(z.Betrag)).FontSize(8.5f);
                    }
                });

                spalte.Item().PaddingTop(10).Text(
                    "Diese Übersicht der Feuerwehr ist keine Steuerberatung. Ob und wie die Beträge in der Steuererklärung anzugeben sind, "
                    + "klärt bitte mit dem Finanzamt oder einer steuerberatenden Stelle. Die Freibeträge gelten je Person für alle "
                    + "Tätigkeiten zusammen.").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    /// <summary>Langer Text einer Art, z. B. „Aufwandsentschädigung (§ 3 Nr. 12 EStG)“.</summary>
    /// <param name="art">Die Steuerart.</param>
    /// <returns>Der Text.</returns>
    public static string Text(Steuerart art) => art switch
    {
        Steuerart.Aufwandsentschaedigung => "Aufwandsentschädigung (§ 3 Nr. 12 EStG)",
        Steuerart.Verdienstausfall => "Verdienstausfall (§ 24 Nr. 1a EStG)",
        Steuerart.Uebungsleiter => "Ausbilder, Übungsleiter (§ 3 Nr. 26 EStG)",
        Steuerart.Auslagenersatz => "Auslagen, Reisekosten (§ 3 Nr. 13 EStG)",
        Steuerart.Sonstiges => "Sonstiges (§ 22 Nr. 3 EStG)",
        _ => art.ToString(),
    };

    private static string Kurztext(Steuerart art) => art switch
    {
        Steuerart.Aufwandsentschaedigung => "Aufwand",
        Steuerart.Verdienstausfall => "Verdienstausfall",
        Steuerart.Uebungsleiter => "Übungsleiter",
        Steuerart.Auslagenersatz => "Auslagen",
        Steuerart.Sonstiges => "Sonstiges",
        _ => art.ToString(),
    };

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";
}
