using System.Globalization;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Dankschreiben an einen Spender oder Sponsor (V08). Die Kassen sind Sondervermögen der Gemeinde und stellen keine
/// Zuwendungsbestätigung aus; das Schreiben sagt das ausdrücklich und verweist für eine Bestätigung an die Gemeinde.
/// </summary>
public static class DankschreibenPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    static DankschreibenPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Inhalt des Schreibens.</summary>
    /// <param name="Kopf">Anschriften und Logo für den Kopf.</param>
    /// <param name="Kasse">Name der Kasse.</param>
    /// <param name="Zuwendung">Die Zuwendung mit Geber und Anschrift.</param>
    /// <param name="Datum">Datum des Schreibens.</param>
    /// <param name="Unterzeichner">Name unter dem Schreiben; leer lässt nur die Linie.</param>
    /// <param name="Gemeinde">Name der Gemeinde als Verantwortliche.</param>
    /// <param name="Datenschutzbeauftragter">Kontakt des Datenschutzbeauftragten aus dem Parameter.</param>
    /// <param name="LoeschenNachJahren">Aufbewahrung im Spendenregister in Jahren nach dem Jahr der Zuwendung (L43).</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, string Kasse, Zuwendung Zuwendung, DateOnly Datum, string? Unterzeichner, string Gemeinde, string Datenschutzbeauftragter,
        int LoeschenNachJahren);

    /// <summary>Dateiname aus Geber und Eingang.</summary>
    /// <param name="d">Inhalt des Schreibens.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(Daten d) =>
        $"Dankschreiben {string.Concat(d.Zuwendung.Geber.Select(z => char.IsLetterOrDigit(z) || z is ' ' or '-' ? z : '_')).Trim()} {d.Zuwendung.Eingang:yyyy-MM-dd}.pdf";

    /// <summary>Erzeugt das Schreiben als PDF.</summary>
    /// <param name="d">Inhalt des Schreibens.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var z = d.Zuwendung;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, z.Sponsoring ? "Dank für Ihre Unterstützung" : "Dank für Ihre Spende"));
            seite.Content().PaddingTop(10).Column(spalte =>
            {
                spalte.Spacing(8);
                spalte.Item().Column(anschrift =>
                {
                    anschrift.Item().Text(z.Geber);
                    foreach (var zeile in (z.Anschrift ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        anschrift.Item().Text(zeile);
                    }
                });
                spalte.Item().PaddingTop(16).AlignRight().Text(d.Datum.ToString("dd.MM.yyyy", Deutsch));
                spalte.Item().PaddingTop(8).Text("Sehr geehrte Damen und Herren,");
                var gabe = z.Art == ZuwendungArt.Geld
                    ? $"Ihre {(z.Sponsoring ? "Zahlung" : "Spende")} über {Euro(z.Betrag)}"
                    : $"Ihre {(z.Sponsoring ? "Leistung" : "Sachspende")} ({z.Sache ?? "Sache"}, Wert etwa {Euro(z.Betrag)})";
                spalte.Item().Text(t =>
                {
                    t.Span($"für {gabe} vom {z.Eingang.ToString("dd.MM.yyyy", Deutsch)} an die {d.Kasse} bedanken wir uns herzlich");
                    t.Span(string.IsNullOrWhiteSpace(z.Zweck) ? "." : $". Sie kommt {(z.Zweckgebunden ? "wie gewünscht" : "insbesondere")} folgendem Zweck zugute: {z.Zweck}.");
                });
                spalte.Item().Text("Mit Ihrer Unterstützung helfen Sie den ehrenamtlichen Angehörigen unserer Feuerwehr, die rund um die Uhr für die Sicherheit in unserer Gemeinde da sind.");
                spalte.Item().PaddingTop(4).Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(9));
                    t.Span("Hinweis: ").SemiBold();
                    t.Span(z.Sponsoring
                        ? "Dieses Schreiben ist kein Beleg im steuerlichen Sinn. Für Ihre Leistung erhalten Sie die vereinbarte Gegenleistung; eine Zuwendungsbestätigung ist dafür nicht möglich."
                        : $"Die {d.Kasse} ist ein Sondervermögen der Gemeinde und stellt keine Zuwendungsbestätigung (Spendenquittung) aus. "
                            + "Benötigen Sie eine Bestätigung für das Finanzamt, wenden Sie sich bitte an die Gemeindeverwaltung.");
                });
                spalte.Item().PaddingTop(8).Text("Mit freundlichen Grüßen");
                spalte.Item().PaddingTop(30).Width(220).Column(c =>
                {
                    c.Item().BorderBottom(0.75f).Height(4);
                    c.Item().PaddingTop(2).Text(d.Unterzeichner ?? d.Kopf.Feuerwehr).FontSize(9);
                    c.Item().Text($"für die {d.Kasse}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                });
                spalte.Item().PaddingTop(24).Text(Datenschutzhinweis(d)).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();

    /// <summary>Information nach Art. 13 DSGVO für den Geber, Text abgestimmt mit den Datenschutz-Unterlagen (Information für Dritte).</summary>
    /// <param name="d">Inhalt des Schreibens.</param>
    /// <returns>Der Hinweis für den Fuß des Schreibens.</returns>
    public static string Datenschutzhinweis(Daten d) =>
        $"Datenschutz: Die {d.Gemeinde} verarbeitet Ihren Namen, Ihre Anschrift und die Angaben zu Ihrer Zuwendung, weil der Gemeinderat über die Annahme "
        + "beschließen und der Rechtsaufsicht jährlich berichten muss (Art. 6 Abs. 1 lit. c DSGVO i. V. m. § 78 Abs. 4 GemO). Der Beschluss kann in öffentlicher "
        + "Sitzung gefasst werden; im Bericht stehen Name, Betrag und Zweck, nicht die Anschrift. "
        + $"Wir löschen die Angaben {d.LoeschenNachJahren} Jahre nach Ablauf des Jahres der Zuwendung. Sie haben die Rechte nach Art. 15 bis 21 DSGVO und können sich "
        + $"beim Landesbeauftragten für den Datenschutz und die Informationsfreiheit Baden-Württemberg beschweren. Datenschutzbeauftragter: {d.Datenschutzbeauftragter}.";

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);
}
