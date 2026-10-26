using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Ausgaben des Einsatzberichts (V18): Bericht als PDF für Gemeinde, Polizei und Versicherung, Anwesenheitsliste, leeres Papierformular
/// für den Ausfall und der Vorschlag einer Pressefassung ohne Personenbezug. Welche Personendaten in den Bericht kommen, entscheidet
/// der Aufrufer (Teilnehmer, Beteiligte); ohne Auswahl enthält er keine.
/// </summary>
public static class EinsatzberichtPdf
{
    static EinsatzberichtPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Inhalt des Berichts.</summary>
    /// <param name="Kopf">Briefkopf der Feuerwehr.</param>
    /// <param name="Einsatz">Der Einsatz mit Fahrzeugen, Teilnahmen, Organisationen, Beteiligten, Einsatzleiter und Kategorie.</param>
    /// <param name="MitTeilnehmern">Teilnehmerliste aufnehmen.</param>
    /// <param name="MitBeteiligten">Beteiligte (Geschädigte, Verursacher) mit Anschrift und Versicherung aufnehmen.</param>
    /// <param name="Stand">Stand des Berichts, z. B. „freigegeben am 03.11.2026, 19:12 Uhr“.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, Einsatz Einsatz, bool MitTeilnehmern, bool MitBeteiligten, string Stand);

    /// <summary>Dateiname des Berichts.</summary>
    /// <param name="e">Der Einsatz.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Einsatz e) => $"Einsatzbericht-{e.Einsatznummer.Replace('/', '-')}.pdf";

    /// <summary>Dateiname der Anwesenheitsliste.</summary>
    /// <param name="e">Der Einsatz.</param>
    /// <returns>Der Dateiname.</returns>
    public static string DateinameAnwesenheit(Einsatz e) => $"Anwesenheit-{e.Einsatznummer.Replace('/', '-')}.pdf";

    private static string Zeit(DateTimeOffset? zeit) =>
        zeit is { } z ? TimeZoneInfo.ConvertTime(z, Ortszeit.Berlin).ToString("dd.MM.yyyy, HH:mm 'Uhr'", Deutsch) : "–";

    private static string Uhrzeit(DateTimeOffset? zeit) =>
        zeit is { } z ? TimeZoneInfo.ConvertTime(z, Ortszeit.Berlin).ToString("HH:mm", Deutsch) : "";

    private static string Name(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();

    private static string Ort(Einsatz e) =>
        string.Join(", ", new[] { $"{e.Strasse} {e.Hausnummer}".Trim(), $"{e.Plz} {e.Ort}".Trim(), e.Ortsteil, e.Objekt }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static IContainer Zelle(IContainer c) => c.BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingRight(4);

    private static void Fusszeile(PageDescriptor seite, string text) =>
        seite.Footer().AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
            t.Span(text + " · Seite ");
            t.CurrentPageNumber();
            t.Span(" von ");
            t.TotalPages();
        });

    /// <summary>Erzeugt den Einsatzbericht.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Bericht(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var e = d.Einsatz;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, $"Einsatzbericht {e.Einsatznummer}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"Stand: {d.Stand}").FontColor(Colors.Grey.Darken2);
                spalte.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                    void Zeile(string etikett, string? wert)
                    {
                        t.Cell().PaddingVertical(1).Text(etikett).FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(1).Text(string.IsNullOrWhiteSpace(wert) ? "–" : wert);
                    }

                    Zeile("Einsatznummer", e.Einsatznummer + (string.IsNullOrWhiteSpace(e.LeitstellenNummer) ? "" : $" (Leitstelle {e.LeitstellenNummer})"));
                    Zeile("Stichwort", string.Join(" – ", new[] { e.Stichwort, e.StichwortBeschreibung }.Where(x => !string.IsNullOrWhiteSpace(x))));
                    Zeile("Einsatzart", e.StatistikKategorie?.Bezeichnung);
                    Zeile("Einsatzort", Ort(e));
                    Zeile("Meldung", e.Meldung);
                    Zeile("Alarmiert", Zeit(e.Alarmiert));
                    Zeile("Ausgerückt", Zeit(e.Ausgerueckt));
                    Zeile("Eintreffen", Zeit(e.EintreffenEinsatzort));
                    Zeile("Einsatzende", Zeit(e.Einsatzende));
                    Zeile("Einsatzbereit", Zeit(e.EinsatzbereitschaftHergestellt));
                    Zeile("Einsatzleiter", e.Einsatzleiter is { } l ? $"{l.Vorname} {l.Nachname}" : null);
                    Zeile("Einsatzkräfte", e.Teilnahmen.Count.ToString(Deutsch) + (e.Teilnahmen.Count(x => x.Atemschutz) is > 0 and var agt ? $", davon {agt} unter Atemschutz" : ""));
                    Zeile("Personen", $"gerettet {e.Gerettete}, verletzt {e.Verletzte}, tot {e.Tote}; Feuerwehr verletzt {e.VerletzteFeuerwehr}");
                    if (e.TiereGerettet + e.TiereTot > 0)
                    {
                        Zeile("Tiere", $"gerettet {e.TiereGerettet}, tot {e.TiereTot}");
                    }

                    if (e.Gefahrstoff)
                    {
                        Zeile("Gefahrstoff", string.Join(", ", new[] { e.GefahrstoffBezeichnung, e.UnNummer is null ? null : "UN " + e.UnNummer, e.GefahrstoffMenge }.Where(x => !string.IsNullOrWhiteSpace(x))));
                    }

                    Zeile("Schadenart", e.Schadenart);
                });

                void Abschnitt(string titel, string? text)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        spalte.Item().Text(titel).SemiBold();
                        spalte.Item().Text(text);
                    }
                }

                Abschnitt("Lage beim Eintreffen", e.Lagebericht);
                Abschnitt("Maßnahmen", e.Massnahmen);
                Abschnitt("Eingesetzte Mittel", e.EingesetzteMittel);

                if (e.Fahrzeuge.Count > 0)
                {
                    spalte.Item().Text("Fahrzeuge").SemiBold();
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Header(h =>
                        {
                            foreach (var s in new[] { "Fahrzeug", "ausgerückt", "eingetroffen", "zurück", "km" })
                            {
                                h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                            }
                        });
                        foreach (var f in e.Fahrzeuge.OrderBy(f => f.Fahrzeug?.Funkrufname))
                        {
                            t.Cell().Element(Zelle).Text(f.Fahrzeug?.Funkrufname ?? "");
                            t.Cell().Element(Zelle).Text(Uhrzeit(f.Ausgerueckt));
                            t.Cell().Element(Zelle).Text(Uhrzeit(f.Eingetroffen));
                            t.Cell().Element(Zelle).Text(Uhrzeit(f.Zurueck));
                            t.Cell().Element(Zelle).Text(f.Kilometer?.ToString(Deutsch) ?? "");
                        }
                    });
                }

                if (e.Organisationen.Count > 0)
                {
                    spalte.Item().Text("Weitere Stellen am Einsatzort").SemiBold();
                    foreach (var o in e.Organisationen)
                    {
                        spalte.Item().Text(string.Join(", ", new[] { Name(o.Art), o.Bezeichnung, o.Aktenzeichen is null ? null : "Az. " + o.Aktenzeichen }
                            .Where(x => !string.IsNullOrWhiteSpace(x))));
                    }
                }

                if (d.MitTeilnehmern && e.Teilnahmen.Count > 0)
                {
                    spalte.Item().Text("Teilnehmer").SemiBold();
                    spalte.Item().Element(c => Teilnehmertabelle(c, e, unterschrift: false));
                }

                if (d.MitBeteiligten && e.Beteiligte.Count > 0)
                {
                    spalte.Item().Text("Beteiligte").SemiBold();
                    foreach (var b in e.Beteiligte)
                    {
                        spalte.Item().Text(string.Join(", ", new[] { Name(b.Rolle), b.Name, b.Anschrift, b.Kennzeichen, b.Versicherung, b.Versicherungsnummer }
                            .Where(x => !string.IsNullOrWhiteSpace(x))));
                    }
                }

                if (!d.MitTeilnehmern || !d.MitBeteiligten)
                {
                    spalte.Item().PaddingTop(6).Text("Dieser Auszug enthält "
                        + (d.MitTeilnehmern || d.MitBeteiligten ? (d.MitTeilnehmern ? "keine Beteiligten." : "keine Teilnehmerliste.") : "keine Teilnehmerliste und keine Beteiligten.")
                        + " Vollständige Angaben bei der Feuerwehr auf Anfrage mit Begründung.").FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });
            Fusszeile(seite, d.MitTeilnehmern || d.MitBeteiligten ? "Vertraulich: enthält personenbezogene Daten" : $"Einsatzbericht {e.Einsatznummer}");
        })).AlsPdf();

    private static void Teilnehmertabelle(IContainer container, Einsatz e, bool unterschrift) =>
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(0.7f);
                c.RelativeColumn(0.7f);
                c.RelativeColumn(0.5f);
                if (unterschrift)
                {
                    c.RelativeColumn(0.8f);
                    c.RelativeColumn(1.6f);
                }
            });
            t.Header(h =>
            {
                foreach (var s in unterschrift
                    ? new[] { "Name", "Fahrzeug", "Funktion", "von", "bis", "AGT", "bestätigt", "Unterschrift" }
                    : ["Name", "Fahrzeug", "Funktion", "von", "bis", "AGT"])
                {
                    h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                }
            });
            foreach (var x in e.Teilnahmen.OrderBy(x => x.Mitglied?.Nachname, StringComparer.CurrentCulture).ThenBy(x => x.Mitglied?.Vorname, StringComparer.CurrentCulture))
            {
                t.Cell().Element(Zelle).Text(x.Mitglied is { } m ? $"{m.Nachname}, {m.Vorname}" : "");
                t.Cell().Element(Zelle).Text(x.EinsatzFahrzeug?.Fahrzeug?.Funkrufname ?? "");
                t.Cell().Element(Zelle).Text(x.Funktion ?? "");
                t.Cell().Element(Zelle).Text(Uhrzeit(x.Von));
                t.Cell().Element(Zelle).Text(Uhrzeit(x.Bis));
                t.Cell().Element(Zelle).Text(x.Atemschutz ? "ja" : "");
                if (unterschrift)
                {
                    t.Cell().Element(Zelle).Text(x.Bestaetigt ? "ja" : "");
                    t.Cell().Element(Zelle).Text("");
                }
            }
        });

    /// <summary>Anwesenheitsliste des Einsatzes zum Ausdrucken und Abzeichnen.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <param name="e">Der Einsatz mit Teilnahmen, Mitgliedern und Fahrzeugen.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Anwesenheitsliste(AntragPdf.Kopfdaten kopf, Einsatz e) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, $"Anwesenheitsliste Einsatz {e.Einsatznummer}"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text($"{e.Stichwort} · {Zeit(e.Alarmiert)} · {e.Teilnahmen.Count} Einsatzkräfte");
                spalte.Item().Element(c => Teilnehmertabelle(c, e, unterschrift: true));
            });
            Fusszeile(seite, "Vertraulich: enthält personenbezogene Daten");
        })).AlsPdf();

    /// <summary>Leeres Papierformular für den Ausfall von FEVAD; nacherfasst wird später im Bericht.</summary>
    /// <param name="kopf">Briefkopf.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Papierformular(AntragPdf.Kopfdaten kopf) =>
        Document.Create(dokument =>
        {
            dokument.Page(seite =>
            {
                AntragPdf.Seitenrahmen(seite);
                seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, "Einsatzbericht (Papier, zur Nacherfassung)"));
                seite.Content().Column(spalte =>
                {
                    spalte.Spacing(4);
                    spalte.Item().Text("Nur wenn FEVAD nicht erreichbar ist. Nach dem Einsatz in FEVAD nacherfassen und dieses Blatt danach vernichten.")
                        .FontColor(Colors.Grey.Darken2);
                    foreach (var feld in new[]
                    {
                        "Einsatznummer / Leitstelle", "Stichwort", "Einsatzort (Straße, Hausnummer, Ortsteil)", "Meldung",
                        "Alarmiert (Datum, Uhrzeit)", "Ausgerückt", "Eintreffen", "Einsatzende", "Einsatzbereit", "Einsatzleiter",
                        "Personen gerettet / verletzt / tot", "Feuerwehrangehörige verletzt", "Weitere Stellen (Polizei, RD, …)",
                    })
                    {
                        spalte.Item().Height(22).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).Text(feld).FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    foreach (var (titel, zeilen) in new[] { ("Lage beim Eintreffen", 4), ("Maßnahmen", 6) })
                    {
                        spalte.Item().PaddingTop(8).Text(titel).SemiBold();
                        for (var i = 0; i < zeilen; i++)
                        {
                            spalte.Item().Height(16).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium);
                        }
                    }
                });
                Fusszeile(seite, "Papierformular Einsatzbericht");
            });
            dokument.Page(seite =>
            {
                AntragPdf.Seitenrahmen(seite);
                seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, "Fahrzeuge und Einsatzkräfte"));
                seite.Content().Column(spalte =>
                {
                    spalte.Spacing(6);
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Header(h =>
                        {
                            foreach (var s in new[] { "Fahrzeug", "ausgerückt", "eingetroffen", "zurück", "km" })
                            {
                                h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                            }
                        });
                        for (var i = 0; i < 25; i++)
                        {
                            t.Cell().Height(16).BorderBottom(0.25f).BorderColor(Colors.Grey.Medium);
                        }
                    });
                    spalte.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(0.7f); c.RelativeColumn(0.7f); c.RelativeColumn(0.5f); c.RelativeColumn(1.6f); });
                        t.Header(h =>
                        {
                            foreach (var s in new[] { "Name", "Fahrzeug", "Funktion", "von", "bis", "AGT", "Unterschrift" })
                            {
                                h.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(s).SemiBold();
                            }
                        });
                        for (var i = 0; i < 7 * 22; i++)
                        {
                            t.Cell().Height(16).BorderBottom(0.25f).BorderColor(Colors.Grey.Medium);
                        }
                    });
                });
                Fusszeile(seite, "Papierformular Einsatzbericht");
            });
        }).AlsPdf();

    /// <summary>
    /// Vorschlag für die Pressefassung: Datum, Uhrzeit, Stichwort, Ort ohne Straße und Hausnummer, Fahrzeuge und Zahl der Einsatzkräfte;
    /// keine Namen, keine Kennzeichen, keine Angaben zu Verletzten über die Zahl hinaus.
    /// </summary>
    /// <param name="e">Der Einsatz mit Fahrzeugen.</param>
    /// <param name="feuerwehr">Name der Feuerwehr.</param>
    /// <returns>Der Vorschlag.</returns>
    public static string Pressevorschlag(Einsatz e, string feuerwehr)
    {
        var lokal = TimeZoneInfo.ConvertTime(e.Alarmiert, Ortszeit.Berlin);
        var ort = string.Join(", ", new[] { e.Ort, e.Ortsteil }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        var art = string.IsNullOrWhiteSpace(e.StichwortBeschreibung) ? e.Stichwort : e.StichwortBeschreibung;
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Am {lokal.ToString("dddd, dd.MM.yyyy", Deutsch)} um {lokal.ToString("HH:mm", Deutsch)} Uhr wurde die {feuerwehr}");
        text.Append(string.IsNullOrWhiteSpace(art) ? " alarmiert" : $" zu einem Einsatz „{art}“ alarmiert");
        text.Append(string.IsNullOrWhiteSpace(ort) ? "." : $" ({ort}).");
        var fahrzeuge = e.Fahrzeuge.Select(f => f.Fahrzeug?.Funkrufname).Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
        if (fahrzeuge.Count > 0 || e.Teilnahmen.Count > 0)
        {
            text.Append(" Im Einsatz waren ");
            text.Append(e.Teilnahmen.Count > 0 ? $"{e.Teilnahmen.Count} Einsatzkräfte" : "Einsatzkräfte");
            text.Append(fahrzeuge.Count > 0 ? $" mit {string.Join(", ", fahrzeuge)}." : ".");
        }

        if (e.Einsatzende is { } ende)
        {
            text.Append(CultureInfo.InvariantCulture, $" Der Einsatz endete um {TimeZoneInfo.ConvertTime(ende, Ortszeit.Berlin).ToString("HH:mm", Deutsch)} Uhr.");
        }

        if (e.Verletzte > 0)
        {
            text.Append(e.Verletzte == 1 ? " Eine Person wurde verletzt." : $" {e.Verletzte} Personen wurden verletzt.");
        }

        return text.ToString();
    }
}
