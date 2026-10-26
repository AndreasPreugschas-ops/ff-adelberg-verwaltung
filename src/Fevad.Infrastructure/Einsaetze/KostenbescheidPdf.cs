using System.Globalization;
using Fevad.Infrastructure.Antraege;
using Fevad.Domain.Einsaetze;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Anhörung und Kostenersatzbescheid der Gemeinde als PDF (V22): Tenor mit Betrag, Begründung, Zahlungshinweis mit Fälligkeit und Kassenzeichen,
/// Rechtsbehelfsbelehrung und die Berechnung als Anlage. Die Texte sind Textbausteine des Hauptamts mit bereits eingesetzten Platzhaltern.
/// Unterschrieben wird auf Papier.
/// </summary>
public static class KostenbescheidPdf
{
    static KostenbescheidPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Inhalt des Schreibens.</summary>
    /// <param name="Kopf">Briefkopf der Gemeinde.</param>
    /// <param name="Anhoerung">true: Anhörungsschreiben, false: Bescheid.</param>
    /// <param name="Entwurf">Der Bescheid ist noch nicht festgesetzt; das PDF trägt den Vermerk „Entwurf“.</param>
    /// <param name="Datum">Datum des Schreibens.</param>
    /// <param name="Empfaenger">Name des Schuldners.</param>
    /// <param name="Anschrift">Zustellanschrift.</param>
    /// <param name="Kassenzeichen">Kassenzeichen.</param>
    /// <param name="Betreff">Betreffzeile mit Einsatz.</param>
    /// <param name="Betrag">Festgesetzter bzw. voraussichtlicher Betrag.</param>
    /// <param name="Haupttext">Anschreiben (Anhörung) bzw. Begründung (Bescheid).</param>
    /// <param name="Zahlung">Zahlungshinweis; nur im Bescheid.</param>
    /// <param name="Rechtsbehelf">Rechtsbehelfsbelehrung; nur im Bescheid.</param>
    /// <param name="Gesamtschuld">Hinweis auf die gesamtschuldnerische Haftung oder null.</param>
    /// <param name="Kosten">Die Berechnung.</param>
    public sealed record Daten(
        AntragPdf.Kopfdaten Kopf, bool Anhoerung, bool Entwurf, DateOnly Datum, string Empfaenger, string? Anschrift, string? Kassenzeichen,
        string Betreff, decimal Betrag, string Haupttext, string? Zahlung, string? Rechtsbehelf, string? Gesamtschuld, Kostenaufstellung Kosten);

    /// <summary>Dateiname.</summary>
    /// <param name="anhoerung">Anhörung statt Bescheid.</param>
    /// <param name="einsatznummer">Einsatznummer.</param>
    /// <param name="empfaenger">Name des Schuldners; nur der erste Bestandteil geht in den Dateinamen.</param>
    /// <returns>Der Dateiname.</returns>
    public static string Dateiname(bool anhoerung, string einsatznummer, string empfaenger)
    {
        var name = new string(empfaenger.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault("Schuldner").Where(char.IsLetterOrDigit).ToArray());
        return $"{(anhoerung ? "Anhoerung" : "Kostenbescheid")}-{einsatznummer.Replace('/', '-')}-{name}.pdf";
    }

    private static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    /// <summary>Erzeugt das PDF.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Erzeugen(Daten d) =>
        Document.Create(dokument =>
        {
            dokument.Page(seite =>
            {
                AntragPdf.Seitenrahmen(seite);
                seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, d.Anhoerung ? "Anhörung zum Kostenersatz" : "Kostenersatzbescheid"));
                seite.Content().Column(spalte =>
                {
                    spalte.Spacing(8);
                    if (d.Entwurf)
                    {
                        spalte.Item().Background(Colors.Red.Lighten4).Padding(4)
                            .Text("ENTWURF – noch nicht festgesetzt, nicht versenden.").SemiBold().FontColor(Colors.Red.Darken3);
                    }

                    spalte.Item().Row(r =>
                    {
                        r.RelativeItem().Column(a =>
                        {
                            a.Item().Text(d.Empfaenger).SemiBold();
                            foreach (var zeile in (d.Anschrift ?? "[Anschrift fehlt]").Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                a.Item().Text(zeile);
                            }
                        });
                        r.ConstantItem(170).Column(a =>
                        {
                            a.Item().AlignRight().Text($"Datum: {d.Datum.ToString("dd.MM.yyyy", Deutsch)}");
                            a.Item().AlignRight().Text($"Kassenzeichen: {d.Kassenzeichen ?? "[fehlt]"}");
                        });
                    });

                    spalte.Item().PaddingTop(6).Text(d.Betreff).SemiBold();
                    if (!d.Anhoerung)
                    {
                        spalte.Item().Text(t =>
                        {
                            t.Span("Für den oben genannten Einsatz wird Kostenersatz in Höhe von ");
                            t.Span(Euro(d.Betrag)).SemiBold();
                            t.Span(" festgesetzt. Die Berechnung ist Bestandteil dieses Bescheids.");
                        });
                        if (d.Gesamtschuld is { } g)
                        {
                            spalte.Item().Text(g);
                        }

                        spalte.Item().PaddingTop(4).Text("Begründung").SemiBold();
                    }

                    spalte.Item().Text(d.Haupttext);
                    if (d.Zahlung is { } z)
                    {
                        spalte.Item().PaddingTop(4).Text("Zahlung").SemiBold();
                        spalte.Item().Text(z);
                    }

                    if (d.Rechtsbehelf is { } rb)
                    {
                        spalte.Item().PaddingTop(4).Text("Rechtsbehelfsbelehrung").SemiBold();
                        spalte.Item().Text(rb);
                    }

                    spalte.Item().PaddingTop(28).Text("______________________________");
                    spalte.Item().Text($"{d.Kopf.Feuerwehr} (Unterschrift, Dienstsiegel)").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    spalte.Item().Text("Anlage: Berechnung des Kostenersatzes").FontSize(8.5f);
                });
                seite.Footer().Element(Fuss);
            });

            dokument.Page(seite =>
            {
                AntragPdf.Seitenrahmen(seite);
                seite.Header().Element(c => AntragPdf.Briefkopf(c, d.Kopf, "Anlage: Berechnung des Kostenersatzes"));
                seite.Content().Column(spalte =>
                {
                    spalte.Spacing(8);
                    spalte.Item().Text(d.Betreff).SemiBold();
                    spalte.Item().Element(c => KostenersatzPdf.Posten(c, d.Kosten));
                    foreach (var h in d.Kosten.Hinweise)
                    {
                        spalte.Item().Text($"Hinweis: {h}").FontColor(Colors.Red.Darken1);
                    }
                });
                seite.Footer().Element(Fuss);
            });
        }).GeneratePdf();

    private static void Fuss(IContainer c) => c.AlignCenter().Text(t =>
    {
        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
        t.Span("Seite ");
        t.CurrentPageNumber();
        t.Span(" von ");
        t.TotalPages();
    });
}
