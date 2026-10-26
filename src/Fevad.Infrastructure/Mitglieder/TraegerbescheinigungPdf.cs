using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Mitglieder;

/// <summary>
/// Bestätigung des Trägers für den Antrag auf ein erweitertes Führungszeugnis (V47): Die Person ist ehrenamtlich in der Kinder- und
/// Jugendarbeit tätig, das Zeugnis ist nach § 72a SGB VIII nötig (§ 30a Abs. 1 Nr. 2 BZRG), und die Gebührenbefreiung für Ehrenamtliche
/// wird beantragt. Geburtsdatum und Anschrift trägt die Person selbst ein, FEVAD speichert sie für Helfer nicht.
/// </summary>
public static class TraegerbescheinigungPdf
{
    static TraegerbescheinigungPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Inhalt der Bescheinigung.</summary>
    /// <param name="Kopf">Briefkopf.</param>
    /// <param name="Name">Vor- und Nachname der Person.</param>
    /// <param name="Traeger">Träger, z. B. „Gemeinde Adelberg“.</param>
    /// <param name="Feuerwehr">Name der Feuerwehr.</param>
    /// <param name="Abteilung">Jugend- oder Kinderfeuerwehr.</param>
    /// <param name="Aufgabe">Aufgabe der Person.</param>
    /// <param name="Datum">Ausstellungsdatum, schon formatiert.</param>
    public sealed record Daten(AntragPdf.Kopfdaten Kopf, string Name, string Traeger, string Feuerwehr, string Abteilung, string Aufgabe, string Datum);

    /// <summary>Erzeugt die Bescheinigung auf einer A4-Seite.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Bestätigung zur Beantragung eines erweiterten Führungszeugnisses"));
            seite.Content().PaddingTop(10).Column(spalte =>
            {
                spalte.Spacing(10);
                spalte.Item().Text("zur Vorlage bei der Meldebehörde bzw. im Online-Portal des Bundesamts für Justiz");
                spalte.Item().Text(t =>
                {
                    t.Span("Hiermit wird bestätigt, dass ");
                    t.Span(d.Name).SemiBold();
                    t.Span($" für die {d.Feuerwehr} (Träger: {d.Traeger}) in der {d.Abteilung} ehrenamtlich in der Kinder- und Jugendarbeit "
                        + $"tätig ist oder tätig werden soll (Aufgabe: {d.Aufgabe}).");
                });
                spalte.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("geboren am:");
                        c.Item().PaddingTop(14).LineHorizontal(0.5f);
                    });
                    r.ConstantItem(20);
                    r.RelativeItem(2).Column(c =>
                    {
                        c.Item().Text("wohnhaft in:");
                        c.Item().PaddingTop(14).LineHorizontal(0.5f);
                    });
                });
                spalte.Item().Text("Für diese Tätigkeit ist nach § 72a des Achten Buches Sozialgesetzbuch (SGB VIII) die Vorlage eines erweiterten "
                    + "Führungszeugnisses nach § 30a Abs. 1 Nr. 2 des Bundeszentralregistergesetzes (BZRG) erforderlich.");
                spalte.Item().Text("Die Tätigkeit wird ehrenamtlich und unentgeltlich ausgeübt. Es wird daher die Befreiung von der Gebühr für das "
                    + "Führungszeugnis beantragt (Merkblatt des Bundesamts für Justiz zur Gebührenbefreiung).");
                spalte.Item().Text("Das Führungszeugnis wird an die antragstellende Person geschickt. Sie legt es dem Kommandanten oder dem Jugendwart "
                    + "zur Einsicht vor; die Feuerwehr vermerkt nur, dass und wann sie es eingesehen hat, das Datum des Zeugnisses und ob eine "
                    + "einschlägige Eintragung vorliegt (§ 72a Abs. 5 SGB VIII), und gibt es zurück.");
                spalte.Item().PaddingTop(30).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text($"Ort, Datum ({d.Datum})").FontSize(8);
                    });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Unterschrift Kommandant oder Jugendwart, Stempel").FontSize(8);
                    });
                });
            });
            seite.Footer().AlignCenter().Text($"{d.Feuerwehr} · Bestätigung nach § 72a SGB VIII").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).AlsPdf();
}
