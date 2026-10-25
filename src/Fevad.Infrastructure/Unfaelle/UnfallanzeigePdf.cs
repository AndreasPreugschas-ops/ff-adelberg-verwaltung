using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Unfaelle;

/// <summary>
/// Angaben für die Unfallanzeige nach § 193 SGB VII, geordnet wie das Formular der Unfallkasse Baden-Württemberg.
/// Das Blatt dient zum Übertragen ins UKBW-Formular bzw. Online-Verfahren und zur Unterschrift der Gemeinde;
/// rechtsverbindlich ist die Anzeige an die Unfallkasse.
/// </summary>
public static class UnfallanzeigePdf
{
    static UnfallanzeigePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Zeuge auf der Unfallanzeige.</summary>
    /// <param name="Name">Name.</param>
    /// <param name="Kontakt">Kontakt.</param>
    /// <param name="Augenzeuge">Hat den Unfall selbst gesehen.</param>
    public sealed record Zeuge(string Name, string? Kontakt, bool Augenzeuge);

    /// <summary>Inhalt der Unfallanzeige.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Gemeinde">Name der Gemeinde (Unternehmen im Sinne der Unfallversicherung).</param>
    /// <param name="GemeindeAnschrift">Anschrift der Gemeinde.</param>
    /// <param name="Name">Name der verletzten Person.</param>
    /// <param name="Anschrift">Anschrift der verletzten Person.</param>
    /// <param name="Geburtsdatum">Geburtsdatum.</param>
    /// <param name="Geschlecht">Geschlecht als Text.</param>
    /// <param name="Arbeitgeber">Arbeitgeber der verletzten Person.</param>
    /// <param name="Krankenkasse">Krankenkasse.</param>
    /// <param name="Unfall">Der Unfall.</param>
    /// <param name="Bezug">Einsatz oder Dienst, bei dem der Unfall geschah, als Text.</param>
    /// <param name="Zeugen">Zeugen und Ersthelfer.</param>
    /// <param name="AnzeigeBis">Letzter Tag der Anzeigefrist.</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, string Gemeinde, string? GemeindeAnschrift,
        string Name, string? Anschrift, DateOnly Geburtsdatum, string? Geschlecht, string? Arbeitgeber, string? Krankenkasse,
        Unfall Unfall, string? Bezug, IReadOnlyList<Zeuge> Zeugen, DateOnly? AnzeigeBis);

    /// <summary>Ohne Namen der verletzten Person, weil die Datei oft per E-Mail weitergeht.</summary>
    public static string Dateiname(Unfall u) =>
        $"Unfallanzeige-{TimeZoneInfo.ConvertTime(u.Zeitpunkt, Ortszeit.Berlin):yyyy-MM-dd}-{u.Id.ToString("N")[^6..]}.pdf";

    /// <summary>Erzeugt das Blatt der Unfallanzeige als PDF.</summary>
    /// <param name="d">Inhalt der Anzeige.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Angaben zur Unfallanzeige (Dienstunfall)"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(6);
                var u = d.Unfall;
                var zeit = TimeZoneInfo.ConvertTime(u.Zeitpunkt, Ortszeit.Berlin);

                spalte.Item().Text(d.AnzeigeBis is { } bis
                        ? $"Anzeigepflichtig nach § 193 SGB VII: bis spätestens {bis:dd.MM.yyyy} an die Unfallkasse Baden-Württemberg."
                        : "Nach den bisherigen Angaben nicht anzeigepflichtig (nicht tödlich, höchstens drei Tage arbeitsunfähig). Für die Akte und eine spätere Anzeige.")
                    .FontSize(9).FontColor(Colors.Grey.Darken2);

                void Abschnitt(string titel, params (string Etikett, string? Wert)[] zeilen)
                {
                    spalte.Item().PaddingTop(4).Text(titel).SemiBold().FontSize(11);
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                        foreach (var (etikett, wert) in zeilen)
                        {
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Text(etikett).FontColor(Colors.Grey.Darken1);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Text(string.IsNullOrWhiteSpace(wert) ? "–" : wert);
                        }
                    });
                }

                Abschnitt("Unternehmen",
                    ("Unternehmerin", d.Gemeinde),
                    ("Anschrift", d.GemeindeAnschrift),
                    ("Einrichtung", d.Kopf.Feuerwehr));
                Abschnitt("Versicherte Person",
                    ("Name", d.Name),
                    ("Anschrift", d.Anschrift),
                    ("Geburtsdatum", d.Geburtsdatum.ToString("dd.MM.yyyy")),
                    ("Geschlecht", d.Geschlecht),
                    ("Arbeitgeber (Hauptberuf)", d.Arbeitgeber),
                    ("Krankenkasse", d.Krankenkasse),
                    ("Versichert als", "ehrenamtliche Angehörige bzw. ehrenamtlicher Angehöriger der Freiwilligen Feuerwehr (§ 2 Abs. 1 Nr. 12 SGB VII)"));
                Abschnitt("Unfall",
                    ("Zeitpunkt", zeit.ToString("dd.MM.yyyy, HH:mm 'Uhr'")),
                    ("Unfallort", u.Ort),
                    ("Anlass", Text(u.Anlass) + (d.Bezug is null ? "" : $" ({d.Bezug})")),
                    ("Tätigkeit beim Unfall", u.Taetigkeit),
                    ("Hergang", u.Hergang),
                    ("Verletzte Körperteile", u.VerletzteKoerperteile),
                    ("Art der Verletzung", u.Verletzungsart?.Bezeichnung),
                    ("Tödlicher Unfall", u.Toedlich ? "ja" : "nein"),
                    ("Zuerst Kenntnis genommen", u.ZuerstKenntnis),
                    ("Erstbehandlung", u.Erstbehandlung),
                    ("Dienst eingestellt", u.DienstEingestelltAm is { } e ? TimeZoneInfo.ConvertTime(e, Ortszeit.Berlin).ToString("dd.MM.yyyy, HH:mm 'Uhr'") : "nein"),
                    ("Mehr als drei Tage arbeitsunfähig", u.MehrAlsDreiTageArbeitsunfaehig ? "ja (voraussichtlich)" : "nein"));

                spalte.Item().PaddingTop(4).Text("Zeugen und Ersthelfer").SemiBold().FontSize(11);
                if (d.Zeugen.Count == 0)
                {
                    spalte.Item().Text("keine angegeben");
                }

                foreach (var z in d.Zeugen)
                {
                    spalte.Item().Text($"{z.Name}{(string.IsNullOrWhiteSpace(z.Kontakt) ? "" : ", " + z.Kontakt)} – {(z.Augenzeuge ? "Augenzeuge" : "hinzugekommen")}");
                }

                if (u.Status != UnfallStatus.Erfasst)
                {
                    Abschnitt("Meldung", ("Gemeldet am", u.GemeldetAm?.ToString("dd.MM.yyyy")), ("Aktenzeichen UKBW", u.Aktenzeichen));
                }

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Unterschrift(string text) => r.RelativeItem().Column(c =>
                    {
                        c.Item().BorderBottom(0.75f).Height(24);
                        c.Item().PaddingTop(2).Text(text).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });

                    Unterschrift("Ort, Datum");
                    r.ConstantItem(20);
                    Unterschrift("Feuerwehrkommandant");
                    r.ConstantItem(20);
                    Unterschrift($"für die Unternehmerin ({d.Gemeinde})");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Vertraulich: enthält Gesundheitsdaten (Art. 9 DSGVO) · Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();
}
