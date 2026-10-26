using System.Globalization;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Gemeinde;

/// <summary>Schreiben der Gemeinde aus FEVAD (V27): Vorlagen-Entwurf, Anhörung und Bescheid an Mitglieder, Dienstausweis zum Ausschneiden.</summary>
public static class GemeindePdf
{
    static GemeindePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo Deutsch = new("de-DE");

    private static string Datum(DateOnly? tag) => tag?.ToString("dd.MM.yyyy", Deutsch) ?? "–";

    /// <summary>Vorlagen-Entwurf für Gemeinderat oder Bürgermeister.</summary>
    /// <param name="kopf">Briefkopf der Gemeinde.</param>
    /// <param name="v">Der Vorgang.</param>
    /// <param name="heute">Datum des Entwurfs.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Vorlage(AntragPdf.Kopfdaten kopf, Gemeindevorgang v, DateOnly heute) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, kopf, v.Zustaendig == Gemeindevorlagen.Buergermeister ? "Entscheidungsvorlage" : "Sitzungsvorlage"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                if (v.Stand == GemeindevorgangStand.Entwurf)
                {
                    spalte.Item().Background(Colors.Grey.Lighten3).Padding(4).Text("Entwurf der Feuerwehr – vom Hauptamt zu prüfen.").SemiBold();
                }

                spalte.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Gremium: {v.Zustaendig}");
                    r.ConstantItem(170).AlignRight().Text($"Stand: {Datum(heute)}");
                });
                spalte.Item().Text(v.Betreff).FontSize(12).SemiBold();
                Abschnitt(spalte, "Sachverhalt", v.Sachverhalt);
                Abschnitt(spalte, "Rechtsgrundlage", v.Rechtsgrundlage);
                Abschnitt(spalte, "Beschlussvorschlag", v.Beschlussvorschlag);
                if (v.Ergebnis is { } e)
                {
                    Abschnitt(spalte, "Entscheidung", $"{Anzeigetext(e)} am {Datum(v.EntschiedenAm)}" + (string.IsNullOrWhiteSpace(v.Entscheidungsvermerk) ? "" : $"\n{v.Entscheidungsvermerk}"));
                }
            });
            seite.Footer().Element(Fuss);
        })).GeneratePdf();

    /// <summary>Inhalt eines Schreibens an ein Mitglied.</summary>
    /// <param name="Kopf">Briefkopf der Gemeinde.</param>
    /// <param name="Anhoerung">true: Anhörung, false: Bescheid.</param>
    /// <param name="Entwurf">Noch nicht erlassen; das PDF trägt den Vermerk „Entwurf“.</param>
    /// <param name="Datum">Datum des Schreibens.</param>
    /// <param name="Bescheid">Der Bescheid.</param>
    /// <param name="Text">Anschreiben der Anhörung bzw. Rechtsbehelfsbelehrung, Platzhalter eingesetzt.</param>
    /// <param name="Buergermeister">Name des Bürgermeisters für die Unterschriftszeile.</param>
    public sealed record Schreiben(AntragPdf.Kopfdaten Kopf, bool Anhoerung, bool Entwurf, DateOnly Datum, Mitgliederbescheid Bescheid, string Text, string? Buergermeister);

    /// <summary>Anhörung oder Bescheid an ein Mitglied.</summary>
    /// <param name="s">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Mitgliederbescheid(Schreiben s) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            var b = s.Bescheid;
            AntragPdf.Seitenrahmen(seite);
            seite.Header().Element(c => AntragPdf.Briefkopf(c, s.Kopf, s.Anhoerung ? "Anhörung" : "Bescheid"));
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(8);
                if (s.Entwurf)
                {
                    spalte.Item().Background(Colors.Red.Lighten4).Padding(4)
                        .Text("ENTWURF – noch nicht erlassen, nicht versenden.").SemiBold().FontColor(Colors.Red.Darken3);
                }

                spalte.Item().Row(r =>
                {
                    r.RelativeItem().Column(a =>
                    {
                        a.Item().Text(b.EmpfaengerName).SemiBold();
                        foreach (var zeile in (b.EmpfaengerAnschrift ?? "[Anschrift fehlt]").Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            a.Item().Text(zeile);
                        }
                    });
                    r.ConstantItem(170).AlignRight().Text($"Datum: {Datum(s.Datum)}");
                });
                spalte.Item().PaddingTop(6).Text($"{Anzeigetext(b.Art)} – Freiwillige Feuerwehr").SemiBold();
                if (s.Anhoerung)
                {
                    spalte.Item().Text(s.Text);
                    Abschnitt(spalte, "Sachverhalt", b.Sachverhalt);
                }
                else
                {
                    spalte.Item().Text(b.Tenor ?? "[Entscheidungssatz fehlt]").SemiBold();
                    Abschnitt(spalte, "Sachverhalt", b.Sachverhalt);
                    Abschnitt(spalte, "Begründung", b.Begruendung);
                    Abschnitt(spalte, "Rechtsbehelfsbelehrung", s.Text);
                }

                spalte.Item().PaddingTop(28).Text("______________________________");
                spalte.Item().Text($"{s.Buergermeister ?? "Bürgermeister"} (Unterschrift, Dienstsiegel)").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
            });
            seite.Footer().Element(Fuss);
        })).GeneratePdf();

    /// <summary>Daten eines Dienstausweises.</summary>
    /// <param name="Gemeinde">Name der Gemeinde.</param>
    /// <param name="Feuerwehr">Name der Feuerwehr.</param>
    /// <param name="Logo">Logo oder null.</param>
    /// <param name="LogoSvg">Das Logo ist SVG.</param>
    /// <param name="Name">Vor- und Nachname.</param>
    /// <param name="Geburtsdatum">Geburtsdatum.</param>
    /// <param name="Ausweis">Der Ausweis.</param>
    /// <param name="Lichtbild">Lichtbild (JPEG oder PNG) oder null; wird nicht gespeichert.</param>
    /// <param name="Buergermeister">Name des Bürgermeisters.</param>
    public sealed record Ausweisdaten(string Gemeinde, string Feuerwehr, byte[]? Logo, bool LogoSvg, string Name, DateOnly Geburtsdatum,
        Dienstausweis Ausweis, byte[]? Lichtbild, string? Buergermeister);

    /// <summary>Dienstausweis im Scheckkartenformat (85,6 × 54 mm), Vorder- und Rückseite zum Ausschneiden und Laminieren.</summary>
    /// <param name="d">Inhalt.</param>
    /// <returns>Das PDF als Bytes.</returns>
    public static byte[] Dienstausweis(Ausweisdaten d) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            AntragPdf.Seitenrahmen(seite);
            seite.Content().Column(spalte =>
            {
                spalte.Spacing(10);
                spalte.Item().Text("Dienstausweis – an den Linien ausschneiden, Vorder- und Rückseite zusammenkleben oder laminieren.").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                // Vorder- und Rückseite untereinander; nebeneinander wären zwei Karten breiter als der Satzspiegel.
                spalte.Item().Column(r =>
                {
                    r.Spacing(6, Unit.Millimetre);
                    r.Item().Width(85.6f, Unit.Millimetre).Height(54, Unit.Millimetre).Border(0.5f).BorderColor(Colors.Grey.Darken1).Padding(2, Unit.Millimetre).Row(karte =>
                    {
                        karte.Spacing(2, Unit.Millimetre);
                        karte.ConstantItem(25, Unit.Millimetre).Column(links =>
                        {
                            var bild = links.Item().Height(32, Unit.Millimetre).Border(0.5f).BorderColor(Colors.Grey.Lighten1);
                            if (d.Lichtbild is { Length: > 0 } foto)
                            {
                                bild.Image(foto).FitArea();
                            }
                            else
                            {
                                bild.AlignCenter().AlignMiddle().Text("Lichtbild").FontSize(7).FontColor(Colors.Grey.Medium);
                            }

                            if (d.Logo is { Length: > 0 } logo)
                            {
                                var platz = links.Item().PaddingTop(1, Unit.Millimetre).Height(12, Unit.Millimetre).AlignCenter();
                                if (d.LogoSvg)
                                {
                                    platz.Svg(System.Text.Encoding.UTF8.GetString(logo)).FitArea();
                                }
                                else
                                {
                                    platz.Image(logo).FitArea();
                                }
                            }
                        });
                        karte.RelativeItem().Column(rechts =>
                        {
                            rechts.Spacing(1);
                            rechts.Item().Text("DIENSTAUSWEIS").FontSize(9).Bold().FontColor(Colors.Red.Darken2);
                            rechts.Item().Text(d.Feuerwehr).FontSize(7).SemiBold();
                            rechts.Item().PaddingTop(2, Unit.Millimetre).Text(d.Name).FontSize(9).SemiBold();
                            rechts.Item().Text($"geb. {Datum(d.Geburtsdatum)}").FontSize(7);
                            rechts.Item().PaddingTop(2, Unit.Millimetre).Text($"Nr. {d.Ausweis.Nummer}").FontSize(7);
                            rechts.Item().Text($"gültig bis {Datum(d.Ausweis.GueltigBis)}").FontSize(7).SemiBold();
                        });
                    });
                    r.Item().Width(85.6f, Unit.Millimetre).Height(54, Unit.Millimetre).Border(0.5f).BorderColor(Colors.Grey.Darken1).Padding(3, Unit.Millimetre).Column(rueck =>
                    {
                        rueck.Spacing(2);
                        rueck.Item().Text($"Der Inhaber dieses Ausweises ist Angehöriger der {d.Feuerwehr}.").FontSize(7);
                        rueck.Item().Text("Der Ausweis ist Eigentum der Gemeinde und beim Ausscheiden aus der Feuerwehr unaufgefordert zurückzugeben. Verlust ist sofort zu melden.").FontSize(6.5f);
                        rueck.Item().Text($"Ausgestellt am {Datum(d.Ausweis.AusgestelltAm)}").FontSize(7);
                        rueck.Item().PaddingTop(5, Unit.Millimetre).Text("______________________").FontSize(7);
                        rueck.Item().Text($"{d.Buergermeister ?? "Bürgermeister"}, {d.Gemeinde}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        })).GeneratePdf();

    private static void Abschnitt(ColumnDescriptor spalte, string titel, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        spalte.Item().PaddingTop(4).Text(titel).SemiBold();
        spalte.Item().Text(text);
    }

    private static string Anzeigetext(GemeindeErgebnis e) => e switch
    {
        GemeindeErgebnis.Zugestimmt => "Zugestimmt",
        GemeindeErgebnis.Geaendert => "Mit Änderungen zugestimmt",
        _ => "Abgelehnt",
    };

    private static string Anzeigetext(MitgliederbescheidArt art) => art switch
    {
        MitgliederbescheidArt.AblehnungAufnahme => "Ablehnung der Aufnahme",
        MitgliederbescheidArt.FeststellungBeendigung => "Feststellung der Beendigung des Feuerwehrdienstes",
        MitgliederbescheidArt.Geldbusse => "Geldbuße wegen Verletzung der Dienstpflichten",
        _ => "Entlassung aus der Einsatzabteilung",
    };

    private static void Fuss(IContainer c) => c.AlignCenter().Text(t =>
    {
        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
        t.Span("Seite ");
        t.CurrentPageNumber();
        t.Span(" von ");
        t.TotalPages();
    });
}
