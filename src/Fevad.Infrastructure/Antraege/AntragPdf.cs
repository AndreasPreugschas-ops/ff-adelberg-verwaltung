using System.Globalization;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Antraege;

/// <summary>
/// Antragsformulare als PDF zum Unterschreiben: ausgefüllt aus einem <see cref="Antrag"/> oder blanko zum Ausfüllen von Hand.
/// Rechtsverbindlich ist das unterschriebene Papier; FEVAD speichert keine Unterschriften.
/// </summary>
public static partial class AntragPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private const string Rot = "#A50D26";
    private const string Leer = " ";

    static AntragPdf()
    {
        // Community-Lizenz: kostenfrei für gemeinnützige Organisationen und Umsätze unter 1 Mio. USD.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Was das Formular außer dem Antrag braucht.</summary>
    public sealed record Rahmen(Kopfdaten Kopf, Laufbahnregeln Regeln, int VolljaehrigAb, DateOnly Heute, Datenschutztexte Datenschutz);

    /// <summary>Briefkopf aus den Stammdaten; <paramref name="Abteilung"/> ist die Zielabteilung, falls bekannt.</summary>
    public sealed record Kopfdaten(string Feuerwehr, string? FeuerwehrAnschrift, string? Abteilung, string? AbteilungAnschrift, byte[]? Logo, string? LogoInhaltstyp = null);

    /// <summary>Die Gemeinde als Verantwortliche; fehlende Angaben erscheinen als Platzhalter.</summary>
    public sealed record Verantwortliche(string Name, string? Vertretung, string? Anschrift, string? Telefon, string? Email);

    /// <summary>
    /// Angaben für die Datenschutzhinweise (Textbausteine A bis C, Entwurf vom 23.10.2026) und die Datenschutzinformation
    /// (Entwurf 06 vom 22.10.2026), beide vom Datenschutzbeauftragten zu prüfen. Fristen kommen aus den Parametern des
    /// Löschkonzepts, damit Formular und Löschlauf nicht auseinanderlaufen.
    /// </summary>
    public sealed record Datenschutztexte(
        Verantwortliche Gemeinde, string Beauftragter, string FotoKanaele, string Art9Grundlage, string AlarmierungBetreiber, string Auftragsverarbeiter,
        int AntraegeLoeschenMonate, int ElternBisAlter, int KontaktdatenNachAustrittMonate, int RestdatensatzJahre, int EinsatzberichteJahre, int AlarmdatenMonate,
        int KontoNachDeaktivierungMonate = 6, int AtemschutznachweiseJahre = 40, int AnwesenheitenJahre = 6);

    /// <summary>Überschrift des Formulars je Antragsart.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>Die Überschrift, z. B. „Antrag auf Aufnahme in die Jugendfeuerwehr“.</returns>
    public static string Titel(AntragArt art) => art switch
    {
        AntragArt.JugendEintritt => "Antrag auf Aufnahme in die Jugendfeuerwehr",
        AntragArt.UebertrittEinsatz => "Antrag auf Übertritt in die Einsatzabteilung",
        AntragArt.EinsatzEintritt => "Antrag auf Aufnahme in die Einsatzabteilung",
        AntragArt.Altersabteilung => "Antrag auf Wechsel in die Altersabteilung",
        AntragArt.Ausscheiden => "Antrag auf Ausscheiden aus der Feuerwehr",
        _ => "Erklärung bei Eintritt der Volljährigkeit",
    };

    /// <summary>Dateiname des PDFs aus Antragsart und Namen.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <param name="antrag">Der Antrag; ohne Antrag entsteht der Name eines leeren Formulars.</param>
    /// <returns>Der Dateiname mit Endung „.pdf“.</returns>
    public static string Dateiname(AntragArt art, Antrag? antrag) =>
        $"{art switch
        {
            AntragArt.JugendEintritt => "Aufnahme-Jugendfeuerwehr",
            AntragArt.UebertrittEinsatz => "Uebertritt-Einsatzabteilung",
            AntragArt.EinsatzEintritt => "Aufnahme-Einsatzabteilung",
            AntragArt.Altersabteilung => "Wechsel-Altersabteilung",
            AntragArt.Ausscheiden => "Ausscheiden",
            _ => "Volljaehrigkeit",
        }}{(antrag is null ? "-blanko" : "-" + Dateinamenteil(antrag.Nachname))}.pdf";

    /// <summary>Formular und dahinter immer die Datenschutzinformation nach Art. 13 DSGVO, bei Minderjährigen mit Ergänzung für die Jugendfeuerwehr.</summary>
    public static byte[] Erzeugen(AntragArt art, Antrag? antrag, Rahmen rahmen) =>
        Document.Create(dokument =>
        {
            dokument.Page(seite => Formular(seite, art, antrag, rahmen));
            dokument.Page(seite => Datenschutzinformation(seite, rahmen, art is AntragArt.JugendEintritt or AntragArt.UebertrittEinsatz
                || (art == AntragArt.Ausscheiden && Minderjaehrig(antrag, rahmen))));
        }).GeneratePdf();

    /// <summary>Blanko gilt als möglicherweise minderjährig, damit die Zeilen für die Erziehungsberechtigten da sind.</summary>
    private static bool Minderjaehrig(Antrag? antrag, Rahmen rahmen) =>
        antrag is null || Mitglied.Alter(antrag.Geburtsdatum, antrag.GewuenschtAb ?? rahmen.Heute) < rahmen.VolljaehrigAb;

    internal static void Seitenrahmen(PageDescriptor seite)
    {
        seite.Size(PageSizes.A4);
        seite.Margin(1.8f, Unit.Centimetre);
        seite.MarginTop(1.4f, Unit.Centimetre);
        seite.DefaultTextStyle(t => t.FontSize(10));
    }

    /// <summary>Feuerwehr mit Anschrift, darunter die Zielabteilung; rechts das Logo, falls hinterlegt.</summary>
    internal static void Briefkopf(IContainer c, Kopfdaten kopf, string titel) =>
        c.Column(spalte =>
        {
            spalte.Item().Row(zeile =>
            {
                zeile.RelativeItem().AlignBottom().Column(text =>
                {
                    text.Item().Text(kopf.Feuerwehr).FontSize(10).SemiBold();
                    text.Item().Text(kopf.FeuerwehrAnschrift ?? "[Anschrift der Feuerwehr fehlt]").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    if (kopf.Abteilung is not null)
                    {
                        text.Item().Text(kopf.Abteilung + (kopf.AbteilungAnschrift is null ? "" : $", {kopf.AbteilungAnschrift}")).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    }
                });
                if (kopf.Logo is { Length: > 0 } logo)
                {
                    var platz = zeile.ConstantItem(70).Height(52).AlignRight().AlignMiddle();
                    if (kopf.LogoInhaltstyp == SvgInhaltstyp)
                    {
                        platz.Svg(System.Text.Encoding.UTF8.GetString(logo)).FitArea();
                    }
                    else
                    {
                        platz.Image(logo).FitArea();
                    }
                }
            });
            spalte.Item().PaddingTop(6).PaddingBottom(6).Text(titel).FontSize(17).Bold().FontColor(Rot);
        });

    /// <summary>MIME-Typ von SVG-Logos.</summary>
    public const string SvgInhaltstyp = "image/svg+xml";

    /// <summary>Lässt sich die SVG-Datei für das PDF lesen? Geprüft beim Hochladen, damit kein Formular daran scheitert.</summary>
    public static bool SvgLesbar(string svg)
    {
        try
        {
            using var bild = SvgImage.FromText(svg);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Formular(PageDescriptor seite, AntragArt art, Antrag? antrag, Rahmen rahmen)
    {
        Seitenrahmen(seite);
        seite.Header().Element(c => Briefkopf(c, rahmen.Kopf, Titel(art)));

        seite.Content().Column(inhalt =>
        {
            inhalt.Spacing(8);

            // Jeder Abschnitt bleibt auf einer Seite: lieber Platz frei lassen als einen Satz auf die nächste Seite reißen.
            Block(inhalt, block =>
            {
                block.Item().Element(Abschnitt(art == AntragArt.JugendEintritt ? "Angaben zum Kind" : "Angaben zur Person"));
                block.Item().Element(Felder(
                [
                    ("Name", antrag?.Nachname), ("Vorname", antrag?.Vorname),
                    ("Geburtsdatum", Datum(antrag?.Geburtsdatum)), ("Geburtsort", antrag?.Geburtsort),
                    ("Geschlecht", antrag?.Geschlecht switch { Geschlecht.Maennlich => "männlich", Geschlecht.Weiblich => "weiblich", Geschlecht.Divers => "divers", _ => null }),
                    ("Straße, Hausnummer", antrag?.Strasse), ("PLZ, Ort", antrag is null ? null : $"{antrag.Plz} {antrag.Ort}".Trim()),
                    ("Telefon", antrag?.Telefon), ("Mobil", antrag?.Mobil), ("E-Mail", antrag?.Email),
                ]));
                block.Item().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken2));
                    t.Span("Datenschutz. ").SemiBold();
                    t.Span(Kurzinformation(rahmen.Datenschutz, rahmen.Kopf.Feuerwehr));
                });
            });

            if (Antragsregeln.UnterschriftEltern(art))
            {
                Block(inhalt, block =>
                {
                    block.Item().Element(Abschnitt("Erziehungsberechtigte"));
                    block.Item().Row(zeile =>
                    {
                        zeile.Spacing(12);
                        for (var nummer = 1; nummer <= 2; nummer++)
                        {
                            var e = antrag?.Erziehungsberechtigte.FirstOrDefault(x => x.Nummer == nummer);
                            zeile.RelativeItem().Column(spalte =>
                            {
                                spalte.Item().Text($"{nummer}. Erziehungsberechtigte(r)").SemiBold();
                                spalte.Item().Element(Felder(
                                [
                                    ("Name, Vorname", e is null ? null : $"{e.Nachname}, {e.Vorname}"),
                                    ("Verhältnis", e?.Verhaeltnis switch { Sorgeverhaeltnis.Mutter => "Mutter", Sorgeverhaeltnis.Vater => "Vater", Sorgeverhaeltnis.Vormund => "Vormund", Sorgeverhaeltnis.Sonstige => "sonstige", _ => null }),
                                    ("Anschrift", e is null ? null : string.Join(", ", new[] { e.Strasse, $"{e.Plz} {e.Ort}".Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                                    ("Telefon / Mobil", e is null ? null : string.Join(" / ", new[] { e.Telefon, e.Mobil }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                                    ("E-Mail", e?.Email),
                                ], einspaltig: true));
                            });
                        }
                    });
                    block.Item().Text($"Von den Erziehungsberechtigten speichern wir Name, Anschrift und Erreichbarkeit, damit wir sie bei Diensten, Ausfahrten und Notfällen erreichen. " +
                        $"Diese Angaben löschen wir mit dem {rahmen.Datenschutz.ElternBisAlter}. Geburtstag des Kindes, spätestens {MonateAkkusativ(rahmen.Datenschutz.KontaktdatenNachAustrittMonate)} nach dem Austritt. " +
                        "Die Rechte nach der Datenschutzinformation können auch die Erziehungsberechtigten für das Kind geltend machen.")
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                });
            }

            switch (art)
            {
                case AntragArt.JugendEintritt:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Antrag und Erklärung"));
                        block.Item().Text($"Wir beantragen die Aufnahme unseres Kindes in die Jugendfeuerwehr{AbDatum(antrag?.GewuenschtAb)}. " +
                            "Wir sind damit einverstanden, dass es an Übungen, Ausbildungen, Zeltlagern, Wettbewerben und sonstigen Veranstaltungen der Jugendfeuerwehr teilnimmt. " +
                            "Die Feuerwehrsatzung und die Jugendordnung erkennen wir an. Während des Dienstes besteht Versicherungsschutz über die Unfallkasse Baden-Württemberg.");
                        block.Item().Text($"Aufgenommen werden Kinder und Jugendliche ab {rahmen.Regeln.JugendAufnahmeAbAlter} bis zum vollendeten {rahmen.Regeln.JugendAufnahmeBisAlter}. Lebensjahr; die Jugendfeuerwehr endet mit {rahmen.Regeln.JugendEndeAlter}.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    break;

                case AntragArt.UebertrittEinsatz:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Antrag und Erklärung"));
                        block.Item().Text($"Ich beantrage den Übertritt aus der Jugendfeuerwehr in die Einsatzabteilung{AbDatum(antrag?.GewuenschtAb)}. " +
                            "Alle Ausbildungen, Abzeichen und Dienstzeiten aus der Jugendfeuerwehr werden übernommen.");
                        block.Item().Text($"Wir als Erziehungsberechtigte stimmen dem Übertritt und dem Dienst in der Einsatzabteilung bis zur Volljährigkeit zu. " +
                            $"Am Einsatzdienst nimmt unser Kind erst ab {rahmen.Regeln.EinsatzdienstAbAlter} Jahren teil; bis dahin an Übungen und Ausbildung.");
                        block.Item().Element(Ankreuzen(Eignungserklaerung, null));
                        block.Item().Text($"Die Probezeit beträgt {rahmen.Regeln.ProbezeitMonate} Monate. Über eine Verkürzung oder einen Verzicht beim Übertritt aus der Jugendfeuerwehr entscheidet der Feuerwehrausschuss.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    break;

                case AntragArt.EinsatzEintritt:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Bisherige Feuerwehrzugehörigkeit"));
                        block.Item().Text(VorgeschichteHinweis).FontSize(8).FontColor(Colors.Grey.Darken2);
                        block.Item().Element(JaNein("Ich war bereits Mitglied einer Feuerwehr (auch Jugendfeuerwehr, Werk- oder Berufsfeuerwehr).", antrag?.FrueherFeuerwehr, antrag is null));
                        block.Item().Element(Felder(
                        [
                            ("Feuerwehr", antrag?.FrueherFeuerwehrName),
                            ("von – bis", antrag?.FrueherVon is null && antrag?.FrueherBis is null ? null : $"{Datum(antrag?.FrueherVon)} – {Datum(antrag?.FrueherBis)}"),
                            ("Letzter Dienstgrad", antrag?.FrueherDienstgrad),
                            .. antrag is null ? [] : new[] { ("Grund des Ausscheidens", antrag.FrueherAustrittsgrund is { } grund ? Anzeigetext(grund) : null) },
                        ]));
                        if (antrag is null)
                        {
                            // Auswahl statt Freitext, damit keine Gesundheitsangaben aufs Papier kommen.
                            block.Item().DefaultTextStyle(x => x.FontSize(8.5f)).Inlined(auswahl =>
                            {
                                auswahl.Spacing(10);
                                auswahl.Item().Text("Grund ankreuzen:");
                                foreach (var grund in Enum.GetValues<Ausscheidensgrund>())
                                {
                                    auswahl.Item().Row(option =>
                                    {
                                        option.Spacing(3);
                                        option.AutoItem().PaddingTop(1).Width(8).Height(8).Element(k => Kaestchen(k, false));
                                        option.AutoItem().Text(Anzeigetext(grund));
                                    });
                                }
                            });
                        }
                        block.Item().Element(Felder([("Lehrgänge und Qualifikationen (bitte Nachweise beifügen)", antrag?.FrueherLehrgaenge)], einspaltig: true, hoch: true));
                        block.Item().Element(JaNein("Ich bin derzeit Mitglied einer anderen Feuerwehr.", antrag?.AndereFeuerwehrAktiv, antrag is null));
                        if (antrag?.AndereFeuerwehrAktiv != false)
                        {
                            block.Item().Element(Felder([("Welche Feuerwehr", antrag?.AndereFeuerwehrName)], einspaltig: true));
                        }

                        block.Item().Element(JaNein("Ich bin aus einer Feuerwehr ausgeschlossen worden.", antrag?.FrueherAusgeschlossen, antrag is null));
                    });

                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Antrag und Erklärung"));
                        block.Item().Text($"Ich beantrage die Aufnahme in die Einsatzabteilung{AbDatum(antrag?.GewuenschtAb)}. Ich erkläre, dass ich " +
                            "die Fähigkeit zur Bekleidung öffentlicher Ämter besitze und die Angaben zur Vorgeschichte vollständig und richtig sind. " +
                            "Ich verpflichte mich, am Dienst nach der Feuerwehrsatzung teilzunehmen und die Unfallverhütungsvorschriften zu beachten.");
                        block.Item().Element(Ankreuzen(Eignungserklaerung, null));
                        block.Item().Text($"Die Aufnahme erfolgt auf Probe ({rahmen.Regeln.ProbezeitMonate} Monate). Ohne Übertritt aus der Jugendfeuerwehr wird aufgenommen, wer volljährig ist; " +
                            $"Jugendliche kommen ab {rahmen.Regeln.AufnahmeEinsatzAbAlter} über den Übertritt aus der Jugendfeuerwehr. Über die Aufnahme entscheidet der Feuerwehrausschuss.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    break;

                case AntragArt.Altersabteilung:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Antrag"));
                        block.Item().Text($"Ich beantrage, aus der Einsatzabteilung{(antrag?.Abteilung is { } bisher ? $" ({bisher.Bezeichnung})" : "")} in die Altersabteilung " +
                            $"übernommen zu werden{AbDatum(antrag?.GewuenschtAb)}. Die Dienstkleidung bleibt mir überlassen.");
                        block.Item().Text($"Feuerwehrsatzung § 6 Abs. 2: Wer das {rahmen.Regeln.AltersabteilungAbAlter}. Lebensjahr vollendet hat, kann auf Antrag in die Altersabteilung " +
                            "übernommen werden; darüber entscheidet der Feuerwehrausschuss. Angehörige der Altersabteilung, die die gesundheitlichen und fachlichen " +
                            "Anforderungen erfüllen, können zu Übungen und Einsätzen herangezogen werden (§ 6 Abs. 5).")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    break;

                case AntragArt.Ausscheiden:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Antrag auf Entlassung"));
                        block.Item().Text($"Ich beantrage meine Entlassung aus dem Feuerwehrdienst{(antrag?.Abteilung is { } bisher ? $" in der {bisher.Bezeichnung}" : "")} " +
                            $"mit Ablauf des {(antrag?.GewuenschtAb is { } bis ? Datum(bis) : "____________")}. Grund:");
                        // Auswahl statt Freitext, damit keine Gesundheitsangaben aufs Papier kommen.
                        foreach (var grund in Enum.GetValues<Entlassungsgrund>())
                        {
                            block.Item().Element(Ankreuzen(Anzeigetext(grund), antrag?.Entlassungsgrund == grund));
                        }

                        block.Item().Text("Gesundheitliche Gründe bitte als persönliche Gründe ankreuzen, ohne Einzelheiten.").FontSize(8).FontColor(Colors.Grey.Darken2);
                        if (antrag is null || antrag.Abteilung?.Art == AbteilungArt.Einsatz)
                        {
                            block.Item().Element(JaNein("Aus der Einsatzabteilung: Ich möchte in die Altersabteilung übernommen werden (§ 6 Abs. 1).",
                                antrag?.InAltersabteilung, antrag is null));
                        }

                        block.Item().Text("Den Antrag bitte über den Abteilungskommandanten beim Feuerwehrkommandanten einreichen (Feuerwehrsatzung § 4 Abs. 3); " +
                            "die Entlassung spricht der Bürgermeister aus. Bei persönlichen oder beruflichen Gründen und beim Umzug geht es ohne gegenteilige Erklärung " +
                            "in der Altersabteilung weiter, die Dienstkleidung bleibt. Sonst ist die ausgegebene Ausrüstung zurückzugeben. Auf Antrag gibt es eine " +
                            "Bescheinigung über die Zugehörigkeit zur Feuerwehr (§ 4 Abs. 6).")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    break;

                case AntragArt.Volljaehrigkeit:
                    Block(inhalt, block =>
                    {
                        block.Item().Element(Abschnitt("Erklärung"));
                        var volljaehrig = antrag is null ? null : Datum(Laufbahnhinweise.Geburtstag(antrag.Geburtsdatum, rahmen.VolljaehrigAb));
                        block.Item().Text($"Ich bin seit dem {volljaehrig ?? "____________"} volljährig und bestätige meine Mitgliedschaft in der Einsatzabteilung. " +
                            "Die Feuerwehrsatzung erkenne ich an; meine Aufnahme und mein bisheriger Dienst gelten fort. Die oben stehenden Angaben sind aktuell; Änderungen habe ich eingetragen.");
                        block.Item().Text("Die Zustimmung meiner Erziehungsberechtigten ist nicht mehr nötig. Ihre Kontaktdaten werden nach der Datenschutzinformation gelöscht.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        block.Item().Element(Felder([("Änderungen (Anschrift, Telefon, E-Mail)", null)], einspaltig: true, hoch: true));
                    });
                    break;
            }

            // Einwilligung und Unterschrift gehören zusammen.
            Block(inhalt, block =>
            {
                var fotos = Antragsregeln.MitFotos(art);
                block.Item().Element(Abschnitt(fotos ? "Datenschutz und Fotos" : "Datenschutz"));
                // Den Erhalt bestätigt die Person selbst; FEVAD kreuzt nichts vor.
                block.Item().Element(Ankreuzen("Die Datenschutzinformation nach Art. 13 DSGVO habe ich erhalten.", null));
                if (!fotos)
                {
                    block.Item().PaddingTop(10).Element(Unterschriften(art, Minderjaehrig(antrag, rahmen)));
                    return;
                }

                // Ja oder nein, beides gleichwertig: ein Nein steht der Aufnahme nicht entgegen.
                var person = art == AntragArt.JugendEintritt ? "unser Kind" : art == AntragArt.UebertrittEinsatz ? "ich bzw. unser Kind" : "ich";
                block.Item().Column(fotos =>
                {
                    fotos.Spacing(3);
                    fotos.Item().Text($"Fotos für die Öffentlichkeitsarbeit ({rahmen.Datenschutz.FotoKanaele}): Die Einwilligung ist freiwillig. " +
                        "Ob Sie zustimmen oder nicht, hat keinen Einfluss auf die Aufnahme. Ein Widerruf ist jederzeit mit Wirkung für die Zukunft möglich, " +
                        "formlos beim Kommandanten (Art. 6 Abs. 1 lit. a, Art. 7 Abs. 3 DSGVO); bereits gedruckte Veröffentlichungen können nicht zurückgeholt werden.")
                        .FontSize(9);
                    fotos.Item().Element(Ankreuzen($"Ja, ich bin einverstanden, dass Fotos veröffentlicht werden, auf denen {person} zu erkennen {(person == "ich" ? "bin" : "ist")}.", antrag?.FotoEinwilligung == true));
                    fotos.Item().Element(Ankreuzen("Nein, ich bin nicht einverstanden.", antrag?.FotoEinwilligung == false));
                });

                block.Item().PaddingTop(10).Element(Unterschriften(art, false));
            });

            inhalt.Item().ShowEntire().PaddingTop(10).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(vermerk =>
            {
                vermerk.Item().Text("Vermerke der Feuerwehr").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                vermerk.Item().PaddingTop(4).Text("Eingegangen am: ____________      Feuerwehrausschuss am: ____________").FontSize(9);
                vermerk.Item().PaddingTop(6).Text(art switch
                {
                    AntragArt.Volljaehrigkeit => "Erledigt am: ____________",
                    AntragArt.Altersabteilung => "In der Altersabteilung ab: ____________",
                    AntragArt.Ausscheiden => "Entlassen mit Ablauf des: ____________      Bescheid Bürgermeister am: ____________",
                    _ => "Angenommen ab: ____________      Probezeit bis: ____________",
                }).FontSize(9);
                vermerk.Item().PaddingTop(10).Text("Unterschrift Kommandant/in: ________________________________").FontSize(9);
            });
        });

        seite.Footer().Element(c => Fusszeile(c, antrag is null ? "Blanko-Formular" : $"Erstellt am {rahmen.Heute.ToString("dd.MM.yyyy", Deutsch)}"));
    }

    private static void Block(ColumnDescriptor inhalt, Action<ColumnDescriptor> fuellen) =>
        inhalt.Item().ShowEntire().Column(block =>
        {
            block.Spacing(8);
            fuellen(block);
        });

    private static void Fusszeile(IContainer c, string links) =>
        c.AlignRight().Text(t =>
        {
            t.Span(links).FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Span(" · Seite ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Span(" von ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });

    private const string VorgeschichteHinweis =
        "Die Angaben zu einer früheren Feuerwehr brauchen wir, um Vordienstzeiten, Lehrgänge und Dienstgrad anzuerkennen (§ 3 Feuerwehrsatzung). " +
        "Bitte beim Austrittsgrund und bei den Lehrgängen keine Angaben zur Gesundheit machen.";

    /// <summary>Baustein E: nur auf Papier, FEVAD speichert dazu nichts.</summary>
    private const string Eignungserklaerung =
        "Ich erkläre, dass mir keine gesundheitlichen Gründe bekannt sind, die dem Feuerwehrdienst entgegenstehen.";

    /// <summary>Baustein A: Kurzinformation unter den Personalangaben; alles Weitere steht in der angehängten Datenschutzinformation.</summary>
    public static string Kurzinformation(Datenschutztexte ds, string feuerwehr) =>
        $"Verantwortlich ist die {ds.Gemeinde.Name} ({feuerwehr}), {ds.Gemeinde.Anschrift ?? AnschriftFehlt}. " +
        "Wir verarbeiten Ihre Angaben, um über Ihren Antrag (Aufnahme, Übertritt, Wechsel oder Ausscheiden) zu entscheiden und die Mitgliedschaft zu führen (Art. 6 Abs. 1 lit. e DSGVO i. V. m. Feuerwehrgesetz Baden-Württemberg und Feuerwehrsatzung). " +
        $"Wird der Antrag abgelehnt oder zurückgezogen, löschen wir die Angaben nach {Monate(ds.AntraegeLoeschenMonate)}. " +
        "Alles Weitere, auch zu Ihren Rechten, steht in der Datenschutzinformation nach Art. 13 DSGVO am Ende dieses Formulars.";

    /// <summary>Text eines früheren Austrittsgrunds für das Formular.</summary>
    /// <param name="grund">Grund des Ausscheidens aus der früheren Feuerwehr.</param>
    /// <returns>Der Anzeigetext.</returns>
    public static string Anzeigetext(Ausscheidensgrund grund) => grund switch
    {
        Ausscheidensgrund.Umzug => "Umzug",
        Ausscheidensgrund.Wechsel => "Wechsel in eine andere Feuerwehr",
        Ausscheidensgrund.JugendfeuerwehrEnde => "Ende der Jugendfeuerwehr ohne Übertritt",
        Ausscheidensgrund.Beruf => "berufliche Gründe",
        Ausscheidensgrund.Persoenlich => "persönliche Gründe",
        _ => "sonstiger Grund",
    };

    /// <summary>Text eines Entlassungsgrunds mit Satzungsfundstelle für das Formular.</summary>
    /// <param name="grund">Grund des Ausscheidens.</param>
    /// <returns>Der Anzeigetext.</returns>
    public static string Anzeigetext(Entlassungsgrund grund) => grund switch
    {
        Entlassungsgrund.AustrittProbezeit => "Austritt während oder mit Ablauf der Probezeit (§ 4 Abs. 1 Nr. 2)",
        Entlassungsgrund.PersoenlichBeruflich => "Der Dienst ist aus persönlichen oder beruflichen Gründen nicht mehr möglich (§ 4 Abs. 2 Nr. 2)",
        Entlassungsgrund.Wohnung => "Ich verlege meine Wohnung in eine andere Gemeinde (§ 4 Abs. 2 Nr. 3)",
        Entlassungsgrund.Arbeitsstaette => "Ich wohne nicht in der Gemeinde und verlege meine Arbeitsstätte in eine andere Gemeinde (§ 4 Abs. 2 Nr. 4)",
        Entlassungsgrund.JugendAustritt => "Austritt aus der Jugendfeuerwehr (§ 7 Abs. 3 Nr. 2)",
        _ => "Wir als Erziehungsberechtigte nehmen unsere Zustimmung zurück (§ 7 Abs. 3 Nr. 3)",
    };

    private const string AnschriftFehlt = "[Anschrift in den Stammdaten eintragen]";

    private static string Monate(int monate) => monate == 1 ? "einem Monat" : $"{monate} Monaten";

    private static string MonateAkkusativ(int monate) => monate == 1 ? "einen Monat" : $"{monate} Monate";

    private static string AbDatum(DateOnly? ab) => ab is { } d ? $" zum {Datum(d)}" : " zum ____________";

    private static Action<IContainer> Abschnitt(string titel) => c =>
        c.PaddingTop(6).BorderBottom(1).BorderColor(Rot).PaddingBottom(2).Text(titel).FontSize(12).SemiBold().FontColor(Rot);

    /// <summary>Beschriftete Felder; leere Werte bleiben als Schreiblinie stehen.</summary>
    private static Action<IContainer> Felder(IReadOnlyList<(string Bezeichnung, string? Wert)> felder, bool einspaltig = false, bool hoch = false) => c =>
        c.Table(tabelle =>
        {
            tabelle.ColumnsDefinition(spalten =>
            {
                for (var i = 0; i < (einspaltig ? 1 : 2); i++)
                {
                    spalten.RelativeColumn();
                }
            });

            foreach (var (bezeichnung, wert) in felder)
            {
                tabelle.Cell().PaddingRight(8).PaddingBottom(4).Column(feld =>
                {
                    feld.Item().Text(bezeichnung).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    feld.Item().MinHeight(hoch ? 36 : 15).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                        .Text(string.IsNullOrWhiteSpace(wert) ? Leer : wert);
                });
            }
        });

    /// <summary>Gezeichnetes Kästchen; die Schrift hat keine Zeichen für ☐ und ☒.</summary>
    private static void Kaestchen(IContainer c, bool gesetzt)
    {
        var kasten = c.Border(0.75f).BorderColor(Colors.Black).AlignCenter().AlignMiddle();
        if (gesetzt)
        {
            kasten.Text("X").FontSize(7).Bold().LineHeight(1);
        }
    }

    private static Action<IContainer> Ankreuzen(string text, bool? gesetzt) => c =>
        c.Row(zeile =>
        {
            zeile.ConstantItem(16).PaddingTop(2).Width(9).Height(9).Element(k => Kaestchen(k, gesetzt == true));
            zeile.RelativeItem().Text(text);
        });

    /// <summary>Frage mit „ja“ und „nein“; im Blanko-Formular oder ohne Angabe beide Kästchen leer.</summary>
    private static Action<IContainer> JaNein(string frage, bool? antwort, bool offen) => c =>
        c.Row(zeile =>
        {
            zeile.RelativeItem().Text(frage);
            zeile.ConstantItem(90).AlignRight().Row(auswahl =>
            {
                auswahl.Spacing(3);
                auswahl.AutoItem().PaddingTop(2).Width(9).Height(9).Element(k => Kaestchen(k, !offen && antwort == true));
                auswahl.AutoItem().PaddingRight(8).Text("ja");
                auswahl.AutoItem().PaddingTop(2).Width(9).Height(9).Element(k => Kaestchen(k, !offen && antwort == false));
                auswahl.AutoItem().Text("nein");
            });
        });

    private static Action<IContainer> Unterschriften(AntragArt art, bool minderjaehrig) => c =>
        c.Column(spalte =>
        {
            var linien = new List<string>();
            if (Antragsregeln.UnterschriftPerson(art))
            {
                linien.Add(art == AntragArt.UebertrittEinsatz ? "Unterschrift Jugendliche(r)" : "Unterschrift Antragsteller/in");
            }

            var eltern = Antragsregeln.ElternUnterschreiben(art, minderjaehrig);
            if (eltern)
            {
                var zusatz = Antragsregeln.UnterschriftEltern(art) ? "" : " (bei Minderjährigen)";
                linien.Add($"Unterschrift 1. Erziehungsberechtigte(r){zusatz}");
                linien.Add($"Unterschrift 2. Erziehungsberechtigte(r){zusatz}");
            }

            spalte.Item().Text("Ort, Datum: ______________________________").FontSize(9);
            spalte.Item().PaddingTop(18).Row(zeile =>
            {
                zeile.Spacing(14);
                foreach (var linie in linien)
                {
                    zeile.RelativeItem().BorderTop(0.75f).BorderColor(Colors.Black).PaddingTop(2).Text(linie).FontSize(8);
                }
            });

            if (eltern)
            {
                spalte.Item().PaddingTop(4).Text("Bei gemeinsamem Sorgerecht unterschreiben beide Erziehungsberechtigte.").FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });

    private static string? Datum(DateOnly? datum) => datum?.ToString("dd.MM.yyyy", Deutsch);

    private static string Dateinamenteil(string text) =>
        new(text.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss")
            .Where(char.IsAsciiLetterOrDigit).ToArray());
}
