using System.Globalization;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
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
/// Arbeitgeberbescheinigungen nach § 15 FwG (V33): ermittelt die Zeiten eines Mitglieds aus freigegebenen Einsätzen, Diensten und
/// Lehrgangsanträgen, stellt die Bescheinigung mit Prüfcode aus und erzeugt das PDF mit Unterschriftsfeld. Dazu die Vorab-Mitteilung
/// über einen Lehrgang. Auf der Bescheinigung stehen nur Zeiten und die Art, keine Einsatzorte oder Stichworte.
/// </summary>
public sealed class Arbeitgeberbescheinigungen(AppDbContext db, TimeProvider zeit)
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static Arbeitgeberbescheinigungen()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Was in die Bescheinigung aufgenommen wird.</summary>
    /// <param name="Einsaetze">Freigegebene Einsätze.</param>
    /// <param name="Lehrgaenge">Lehrgänge aus Diensten und eigenen Anträgen.</param>
    /// <param name="Dienste">Übungen und sonstige Dienste.</param>
    public sealed record Auswahl(bool Einsaetze, bool Lehrgaenge, bool Dienste);

    /// <summary>Inhalt des PDFs.</summary>
    /// <param name="Kopf">Anschrift und Logo der Feuerwehr.</param>
    /// <param name="Feuerwehr">Name der Feuerwehr.</param>
    /// <param name="Gemeinde">Name der Gemeinde, die erstattet.</param>
    /// <param name="Name">Vor- und Nachname des Mitglieds.</param>
    /// <param name="Bescheinigung">Die ausgestellte Bescheinigung.</param>
    /// <param name="Pruefadresse">Adresse der Prüfseite oder null, wenn FEVAD nicht öffentlich erreichbar ist.</param>
    /// <param name="Lehrgang">Bei einer Lehrgangsmitteilung: Bezeichnung und Ort.</param>
    public sealed record PdfDaten(AntragPdf.Kopfdaten Kopf, string Feuerwehr, string Gemeinde, string Name, Arbeitgeberbescheinigung Bescheinigung,
        string? Pruefadresse, string? Lehrgang = null);

    /// <summary>Ermittelt die Zeiten eines Mitglieds im Zeitraum, nach Beginn sortiert.</summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <param name="auswahl">Was aufgenommen wird.</param>
    /// <param name="ruhezeitStunden">Ruhezeit nach Einsätzen in Stunden oder null.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<List<Bescheinigungszeile>> ZeilenAsync(Guid mitgliedId, DateOnly von, DateOnly bis, Auswahl auswahl, int? ruhezeitStunden, CancellationToken ct = default)
    {
        var utcVon = Ortszeit.TagesbeginnUtc(von);
        var utcBis = Ortszeit.TagesbeginnUtc(bis.AddDays(1));
        var zeilen = new List<Bescheinigungszeile>();

        if (auswahl.Einsaetze)
        {
            var teilnahmen = await db.EinsatzTeilnahmen.AsNoTracking()
                .Where(t => t.MitgliedId == mitgliedId && t.Einsatz!.Status == EinsatzStatus.Freigegeben
                    && t.Einsatz.Alarmiert >= utcVon && t.Einsatz.Alarmiert < utcBis)
                .Select(t => new { t.Von, t.Bis, t.Einsatz!.Alarmiert, t.Einsatz.EinsatzbereitschaftHergestellt, t.Einsatz.Einsatzende })
                .ToListAsync(ct);
            foreach (var t in teilnahmen)
            {
                // Freigestellt ist bis zur Rückkehr, also bis zur Einsatzbereitschaft, wenn sie erfasst ist.
                var beginn = t.Von ?? t.Alarmiert;
                if ((t.Bis ?? t.EinsatzbereitschaftHergestellt ?? t.Einsatzende) is { } ende && ende > beginn)
                {
                    zeilen.Add(new(Ortszeit.Datum(beginn), "Einsatz", beginn, ende, ruhezeitStunden is > 0 and var h ? ende.AddHours(h) : null));
                }
            }
        }

        if (auswahl.Lehrgaenge || auswahl.Dienste)
        {
            var anwesenheiten = await db.Anwesenheiten.AsNoTracking()
                .Where(a => a.MitgliedId == mitgliedId && a.Status == AnwesenheitStatus.Anwesend && a.Dienst!.Status == DienstStatus.Durchgefuehrt
                    && a.Dienst.Beginn >= utcVon && a.Dienst.Beginn < utcBis)
                .Select(a => new { a.Von, a.Bis, a.Dienst!.Art, a.Dienst.Beginn, a.Dienst.Ende })
                .ToListAsync(ct);
            foreach (var a in anwesenheiten)
            {
                var lehrgang = a.Art == DienstArt.Lehrgang;
                var beginn = a.Von ?? a.Beginn;
                if ((lehrgang ? auswahl.Lehrgaenge : auswahl.Dienste) && (a.Bis ?? a.Ende) is { } ende && ende > beginn)
                {
                    zeilen.Add(new(Ortszeit.Datum(beginn), lehrgang ? "Lehrgang" : "Dienst", beginn, ende, null));
                }
            }
        }

        if (auswahl.Lehrgaenge)
        {
            // Auswärtige Lehrgänge stehen oft nur im eigenen Antrag; Tage, die schon als Dienst erfasst sind, nicht doppelt.
            var tage = await db.AntragTage.AsNoTracking()
                .Where(t => t.Von >= utcVon && t.Von < utcBis && db.Entschaedigungsantraege.Any(a => a.Id == t.EntschaedigungsantragId
                    && a.MitgliedId == mitgliedId && a.Anlass == AntragAnlass.Lehrgang && (a.Stand == AntragStand.Eingereicht || a.Stand == AntragStand.Genehmigt)))
                .ToListAsync(ct);
            foreach (var t in tage.Where(t => !zeilen.Any(z => z.Anlass == "Lehrgang" && z.Beginn < t.Bis && t.Von < z.Ende)))
            {
                zeilen.Add(new(Ortszeit.Datum(t.Von), "Lehrgang", t.Von, t.Bis, null));
            }
        }

        return zeilen.OrderBy(z => z.Beginn).ToList();
    }

    /// <summary>Stellt eine Bescheinigung über Feuerwehrdienst aus und speichert sie mit Prüfcode.</summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <param name="auswahl">Was aufgenommen wird.</param>
    /// <param name="arbeitgeber">Arbeitgeber.</param>
    /// <param name="ruhezeitStunden">Ruhezeit nach Einsätzen oder null.</param>
    /// <param name="ausgestelltVon">Konto, das ausstellt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Bescheinigung.</returns>
    /// <exception cref="InvalidOperationException">Im Zeitraum gibt es nichts zu bescheinigen.</exception>
    public async Task<Arbeitgeberbescheinigung> AusstellenAsync(Guid mitgliedId, DateOnly von, DateOnly bis, Auswahl auswahl, string arbeitgeber,
        int? ruhezeitStunden, Guid? ausgestelltVon, CancellationToken ct = default)
    {
        var zeilen = await ZeilenAsync(mitgliedId, von, bis, auswahl, ruhezeitStunden, ct);
        if (zeilen.Count == 0)
        {
            throw new InvalidOperationException("Im Zeitraum gibt es keine freigegebenen Einsätze, Lehrgänge oder Dienste zu bescheinigen.");
        }

        var bescheinigung = new Arbeitgeberbescheinigung
        {
            MitgliedId = mitgliedId, Art = BescheinigungArt.Teilnahme, Von = von, Bis = bis, Arbeitgeber = arbeitgeber.Trim(),
            Pruefcode = await NeuerCodeAsync(ct), Zeilen = ZeilenText(zeilen), Stunden = zeilen.Sum(z => z.Stunden),
            RuhezeitStunden = ruhezeitStunden is > 0 ? ruhezeitStunden : null, AusgestelltAm = zeit.GetUtcNow(), AusgestelltVon = ausgestelltVon,
        };
        db.Arbeitgeberbescheinigungen.Add(bescheinigung);
        await db.SaveChangesAsync(ct);
        return bescheinigung;
    }

    /// <summary>Stellt die Vorab-Mitteilung über einen Lehrgang aus.</summary>
    /// <param name="teilnahme">Die Lehrgangsteilnahme mit Qualifikation; Von muss gesetzt sein.</param>
    /// <param name="arbeitgeber">Arbeitgeber.</param>
    /// <param name="ausgestelltVon">Konto, das ausstellt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Mitteilung.</returns>
    public async Task<Arbeitgeberbescheinigung> MitteilungAsync(Lehrgangsteilnahme teilnahme, string arbeitgeber, Guid? ausgestelltVon, CancellationToken ct = default)
    {
        var von = teilnahme.Von ?? throw new InvalidOperationException("Für den Lehrgang ist noch kein Termin eingetragen.");
        var bis = teilnahme.Bis ?? von;
        var tage = new List<string>();
        for (var tag = von; tag <= bis; tag = tag.AddDays(1))
        {
            tage.Add($"{tag:dd.MM.yyyy}|Lehrgang||||");
        }

        var mitteilung = new Arbeitgeberbescheinigung
        {
            MitgliedId = teilnahme.MitgliedId, Art = BescheinigungArt.Lehrgangsmitteilung, Von = von, Bis = bis, Arbeitgeber = arbeitgeber.Trim(),
            Pruefcode = await NeuerCodeAsync(ct), Zeilen = string.Join('\n', tage), AusgestelltAm = zeit.GetUtcNow(), AusgestelltVon = ausgestelltVon,
        };
        db.Arbeitgeberbescheinigungen.Add(mitteilung);
        await db.SaveChangesAsync(ct);
        return mitteilung;
    }

    private async Task<string> NeuerCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = Arbeitgeberbescheinigung.NeuerPruefcode();
            if (!await db.Arbeitgeberbescheinigungen.AnyAsync(b => b.Pruefcode == code, ct))
            {
                return code;
            }
        }
    }

    /// <summary>Zeilen als gespeicherter Text „Datum|Anlass|Beginn|Ende|Stunden|Ruhezeit bis“ in Ortszeit.</summary>
    /// <param name="zeilen">Die Zeilen.</param>
    /// <returns>Der Text.</returns>
    public static string ZeilenText(IEnumerable<Bescheinigungszeile> zeilen) => string.Join('\n', zeilen.Select(z =>
        $"{z.Datum:dd.MM.yyyy}|{z.Anlass}|{Uhrzeit(z.Beginn, z.Datum)}|{Uhrzeit(z.Ende, z.Datum)}|{z.Stunden.ToString("0.##", Deutsch)}|{(z.RuheBis is { } r ? Uhrzeit(r, z.Datum) : "")}"));

    /// <summary>Liest gespeicherte Zeilen für die Anzeige.</summary>
    /// <param name="text">Gespeicherter Text.</param>
    /// <returns>Je Zeile Datum, Anlass, Beginn, Ende, Stunden und Ruhezeit bis als Text.</returns>
    public static IReadOnlyList<string[]> ZeilenLesen(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(z => z.Split('|')).Where(t => t.Length == 6).ToList();

    /// <summary>Uhrzeit in Ortszeit, mit Datum, wenn sie auf einen anderen Tag fällt.</summary>
    private static string Uhrzeit(DateTimeOffset zeitpunkt, DateOnly tag)
    {
        var lokal = TimeZoneInfo.ConvertTime(zeitpunkt, Ortszeit.Berlin);
        return DateOnly.FromDateTime(lokal.DateTime) == tag ? lokal.ToString("HH:mm", Deutsch) : lokal.ToString("dd.MM. HH:mm", Deutsch);
    }

    /// <summary>Dateiname ohne Namen des Mitglieds.</summary>
    /// <param name="b">Die Bescheinigung.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Arbeitgeberbescheinigung b) =>
        (b.Art == BescheinigungArt.Lehrgangsmitteilung ? "Lehrgangsmitteilung-" : "Arbeitgeberbescheinigung-") + b.Pruefcode + ".pdf";

    /// <summary>Erzeugt das PDF mit Prüfcode und Unterschriftsfeld.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF.</returns>
    public static byte[] Pdf(PdfDaten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var b = d.Bescheinigung;
            var mitteilung = b.Art == BescheinigungArt.Lehrgangsmitteilung;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf,
                mitteilung ? "Mitteilung über die Teilnahme an einem Lehrgang (§ 15 FwG)" : "Bescheinigung über Feuerwehrdienst (§ 15 FwG)"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Text(t =>
                {
                    t.Span("An: ").SemiBold();
                    t.Span(b.Arbeitgeber);
                });

                if (mitteilung)
                {
                    spalte.Item().Text($"{d.Name} nimmt als Mitglied der {d.Feuerwehr} vom {b.Von:dd.MM.yyyy} bis {b.Bis:dd.MM.yyyy} an folgendem "
                        + $"Aus- oder Fortbildungslehrgang teil: {d.Lehrgang}.");
                    spalte.Item().Text("Für die Dauer des Lehrgangs besteht nach § 15 Abs. 1 Feuerwehrgesetz Baden-Württemberg Anspruch auf Freistellung "
                        + "von der Arbeitsleistung. Diese Mitteilung erfolgt rechtzeitig vor Beginn des Lehrgangs. Private Arbeitgeber erhalten das "
                        + $"fortgezahlte Arbeitsentgelt auf Antrag von der {d.Gemeinde} erstattet (§ 15 Abs. 2 FwG).").FontSize(9.5f);
                }
                else
                {
                    spalte.Item().Text($"Hiermit wird bescheinigt, dass {d.Name} als Mitglied der {d.Feuerwehr} im Zeitraum vom {b.Von:dd.MM.yyyy} "
                        + $"bis {b.Bis:dd.MM.yyyy} zu folgenden Zeiten Feuerwehrdienst geleistet hat:");
                    spalte.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.ConstantColumn(55);
                            if (b.RuhezeitStunden is not null)
                            {
                                c.RelativeColumn(2);
                            }
                        });
                        t.Header(h =>
                        {
                            h.Cell().BorderBottom(0.5f).Text("Datum").SemiBold();
                            h.Cell().BorderBottom(0.5f).Text("Art").SemiBold();
                            h.Cell().BorderBottom(0.5f).Text("Beginn").SemiBold();
                            h.Cell().BorderBottom(0.5f).Text("Ende").SemiBold();
                            h.Cell().BorderBottom(0.5f).AlignRight().Text("Stunden").SemiBold();
                            if (b.RuhezeitStunden is not null)
                            {
                                h.Cell().BorderBottom(0.5f).PaddingLeft(6).Text("Ruhezeit bis").SemiBold();
                            }
                        });
                        foreach (var z in ZeilenLesen(b.Zeilen))
                        {
                            t.Cell().PaddingVertical(1.5f).Text(z[0]);
                            t.Cell().PaddingVertical(1.5f).Text(z[1]);
                            t.Cell().PaddingVertical(1.5f).Text(z[2]);
                            t.Cell().PaddingVertical(1.5f).Text(z[3]);
                            t.Cell().PaddingVertical(1.5f).AlignRight().Text(z[4]);
                            if (b.RuhezeitStunden is not null)
                            {
                                t.Cell().PaddingVertical(1.5f).PaddingLeft(6).Text(z[5]);
                            }
                        }
                    });
                    spalte.Item().Text($"Summe: {b.Stunden.ToString("0.##", Deutsch)} Stunden"
                        + (b.RuhezeitStunden is { } r ? $", zuzüglich einer Ruhezeit von {r} Stunden nach Einsätzen" : "")).SemiBold();
                    spalte.Item().Text("Ehrenamtlich tätige Angehörige der Gemeindefeuerwehr sind nach § 15 Abs. 1 Feuerwehrgesetz Baden-Württemberg "
                        + "für die Dauer der Teilnahme an Einsätzen und an Aus- und Fortbildungsveranstaltungen von der Arbeitsleistung freigestellt, "
                        + "nach Einsätzen einschließlich einer angemessenen Ruhezeit. Private Arbeitgeber erhalten das fortgezahlte Arbeitsentgelt "
                        + $"auf Antrag von der {d.Gemeinde} erstattet (§ 15 Abs. 2 FwG); bitte diese Bescheinigung dem Antrag beifügen.").FontSize(9.5f);
                }

                spalte.Item().PaddingTop(6).Border(0.5f).Padding(6).Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("Prüfcode: ").SemiBold();
                        t.Span(b.Pruefcode).LetterSpacing(0.08f).SemiBold();
                    });
                    c.Item().Text(d.Pruefadresse is { } adresse
                        ? $"Die Angaben lassen sich mit dem Prüfcode unter {adresse} vergleichen."
                        : $"Die Angaben lassen sich mit dem Prüfcode bei der {d.Feuerwehr} erfragen.").FontSize(8.5f);
                });

                spalte.Item().PaddingTop(30).Row(r =>
                {
                    void Feld(string titel, string unter) => r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(titel).SemiBold();
                        c.Item().PaddingTop(24).BorderBottom(0.5f).Height(2);
                        c.Item().PaddingTop(2).Text(unter).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });
                    Feld("Ort, Datum", $"ausgestellt am {TimeZoneInfo.ConvertTime(b.AusgestelltAm, Ortszeit.Berlin):dd.MM.yyyy}");
                    r.ConstantItem(30);
                    Feld("Unterschrift", "Feuerwehrkommandant oder Stellvertreter");
                });
            });
            seite.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Seite ");
                t.CurrentPageNumber();
                t.Span(" von ");
                t.TotalPages();
            });
        })).AlsPdf();
}
