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
public static class AntragPdf
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private const string Rot = "#A50D26";
    private const string Leer = " ";

    static AntragPdf()
    {
        // Community-Lizenz: kostenfrei für gemeinnützige Organisationen und Umsätze unter 1 Mio. USD.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Was das Formular außer dem Antrag braucht. <paramref name="Abteilung"/> ist die Zielabteilung, falls bekannt.</summary>
    public sealed record Rahmen(string Organisation, string? Abteilung, Laufbahnregeln Regeln, int VolljaehrigAb, DateOnly Heute, Datenschutztexte Datenschutz);

    /// <summary>
    /// Angaben für die Datenschutzhinweise (Textbausteine A bis C, Entwurf vom 23.10.2026, vom Datenschutzbeauftragten zu prüfen).
    /// Fristen kommen aus den Parametern des Löschkonzepts, damit Formular und Löschlauf nicht auseinanderlaufen.
    /// </summary>
    public sealed record Datenschutztexte(
        string Verantwortlicher, string Beauftragter, string FotoKanaele, int AntraegeLoeschenMonate, int ElternBisAlter, int ElternNachAustrittMonate);

    public static string Titel(AntragArt art) => art switch
    {
        AntragArt.JugendEintritt => "Antrag auf Aufnahme in die Jugendfeuerwehr",
        AntragArt.UebertrittEinsatz => "Antrag auf Übertritt in die Einsatzabteilung",
        AntragArt.EinsatzEintritt => "Antrag auf Aufnahme in die Einsatzabteilung",
        _ => "Erklärung bei Eintritt der Volljährigkeit",
    };

    public static string Dateiname(AntragArt art, Antrag? antrag) =>
        $"{art switch
        {
            AntragArt.JugendEintritt => "Aufnahme-Jugendfeuerwehr",
            AntragArt.UebertrittEinsatz => "Uebertritt-Einsatzabteilung",
            AntragArt.EinsatzEintritt => "Aufnahme-Einsatzabteilung",
            _ => "Volljaehrigkeit",
        }}{(antrag is null ? "-blanko" : "-" + Dateinamenteil(antrag.Nachname))}.pdf";

    public static byte[] Erzeugen(AntragArt art, Antrag? antrag, Rahmen rahmen) =>
        Document.Create(dokument => dokument.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(1.8f, Unit.Centimetre);
            seite.DefaultTextStyle(t => t.FontSize(10));

            seite.Header().Column(kopf =>
            {
                kopf.Item().Text(rahmen.Organisation + (rahmen.Abteilung is null ? "" : $" – {rahmen.Abteilung}")).FontSize(9).FontColor(Colors.Grey.Darken1);
                kopf.Item().PaddingBottom(6).Text(Titel(art)).FontSize(17).Bold().FontColor(Rot);
            });

            seite.Content().Column(inhalt =>
            {
                inhalt.Spacing(8);
                var alter = antrag is null ? (int?)null : Mitglied.Alter(antrag.Geburtsdatum, rahmen.Heute);

                inhalt.Item().Element(Abschnitt(art == AntragArt.JugendEintritt ? "Angaben zum Kind" : "Angaben zur Person"));
                inhalt.Item().Element(Felder(
                [
                    ("Name", antrag?.Nachname), ("Vorname", antrag?.Vorname),
                    ("Geburtsdatum", Datum(antrag?.Geburtsdatum)), ("Geburtsort", antrag?.Geburtsort),
                    ("Geschlecht", antrag?.Geschlecht switch { Geschlecht.Maennlich => "männlich", Geschlecht.Weiblich => "weiblich", Geschlecht.Divers => "divers", _ => null }),
                    ("Straße, Hausnummer", antrag?.Strasse), ("PLZ, Ort", antrag is null ? null : $"{antrag.Plz} {antrag.Ort}".Trim()),
                    ("Telefon", antrag?.Telefon), ("Mobil", antrag?.Mobil), ("E-Mail", antrag?.Email),
                ]));
                inhalt.Item().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken2));
                    t.Span("Datenschutz. ").SemiBold();
                    t.Span(Kurzinformation(rahmen.Datenschutz));
                });

                if (Antragsregeln.UnterschriftEltern(art, alter, rahmen.VolljaehrigAb))
                {
                    inhalt.Item().Element(Abschnitt(art == AntragArt.EinsatzEintritt ? "Erziehungsberechtigte (nur bei Minderjährigen)" : "Erziehungsberechtigte"));
                    inhalt.Item().Row(zeile =>
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
                    inhalt.Item().Text($"Von den Erziehungsberechtigten speichern wir Name, Anschrift und Erreichbarkeit, damit wir sie bei Diensten, Ausfahrten und Notfällen erreichen. " +
                        $"Diese Angaben löschen wir mit dem {rahmen.Datenschutz.ElternBisAlter}. Geburtstag des Kindes, spätestens {(rahmen.Datenschutz.ElternNachAustrittMonate == 1 ? "einen Monat" : $"{rahmen.Datenschutz.ElternNachAustrittMonate} Monate")} nach dem Austritt. " +
                        "Die Rechte nach der Datenschutzinformation können auch die Erziehungsberechtigten für das Kind geltend machen.")
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                }

                switch (art)
                {
                    case AntragArt.JugendEintritt:
                        inhalt.Item().Element(Abschnitt("Antrag und Erklärung"));
                        inhalt.Item().Text($"Wir beantragen die Aufnahme unseres Kindes in die Jugendfeuerwehr{AbDatum(antrag?.GewuenschtAb)}. " +
                            "Wir sind damit einverstanden, dass es an Übungen, Ausbildungen, Zeltlagern, Wettbewerben und sonstigen Veranstaltungen der Jugendfeuerwehr teilnimmt. " +
                            "Die Feuerwehrsatzung und die Jugendordnung erkennen wir an. Während des Dienstes besteht Versicherungsschutz über die Unfallkasse Baden-Württemberg.");
                        inhalt.Item().Text($"Aufgenommen werden Kinder und Jugendliche ab {rahmen.Regeln.JugendAufnahmeAbAlter} bis zum vollendeten {rahmen.Regeln.JugendAufnahmeBisAlter}. Lebensjahr; die Jugendfeuerwehr endet mit {rahmen.Regeln.JugendEndeAlter}.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        break;

                    case AntragArt.UebertrittEinsatz:
                        inhalt.Item().Element(Abschnitt("Antrag und Erklärung"));
                        inhalt.Item().Text($"Ich beantrage den Übertritt aus der Jugendfeuerwehr in die Einsatzabteilung{AbDatum(antrag?.GewuenschtAb)}. " +
                            "Alle Ausbildungen, Abzeichen und Dienstzeiten aus der Jugendfeuerwehr werden übernommen.");
                        inhalt.Item().Text($"Wir als Erziehungsberechtigte stimmen dem Übertritt und dem Dienst in der Einsatzabteilung bis zur Volljährigkeit zu. " +
                            $"Am Einsatzdienst nimmt unser Kind erst ab {rahmen.Regeln.EinsatzdienstAbAlter} Jahren teil; bis dahin an Übungen und Ausbildung.");
                        inhalt.Item().Element(Ankreuzen(Eignungserklaerung, null));
                        inhalt.Item().Text($"Die Probezeit beträgt {rahmen.Regeln.ProbezeitMonate} Monate. Über eine Verkürzung oder einen Verzicht beim Übertritt aus der Jugendfeuerwehr entscheidet der Feuerwehrausschuss.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        break;

                    case AntragArt.EinsatzEintritt:
                        inhalt.Item().Element(Abschnitt("Bisherige Feuerwehrzugehörigkeit"));
                        inhalt.Item().Text(VorgeschichteHinweis).FontSize(8).FontColor(Colors.Grey.Darken2);
                        inhalt.Item().Element(JaNein("Ich war bereits Mitglied einer Feuerwehr (auch Jugendfeuerwehr, Werk- oder Berufsfeuerwehr).", antrag?.FrueherFeuerwehr, antrag is null));
                        inhalt.Item().Element(Felder(
                        [
                            ("Feuerwehr", antrag?.FrueherFeuerwehrName),
                            ("von – bis", antrag?.FrueherVon is null && antrag?.FrueherBis is null ? null : $"{Datum(antrag?.FrueherVon)} – {Datum(antrag?.FrueherBis)}"),
                            ("Letzter Dienstgrad", antrag?.FrueherDienstgrad),
                            .. antrag is null ? [] : new[] { ("Grund des Ausscheidens", antrag.FrueherAustrittsgrund is { } grund ? Anzeigetext(grund) : null) },
                        ]));
                        if (antrag is null)
                        {
                            // Auswahl statt Freitext, damit keine Gesundheitsangaben aufs Papier kommen.
                            inhalt.Item().DefaultTextStyle(x => x.FontSize(8.5f)).Inlined(auswahl =>
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
                        inhalt.Item().Element(Felder([("Lehrgänge und Qualifikationen (bitte Nachweise beifügen)", antrag?.FrueherLehrgaenge)], einspaltig: true, hoch: true));
                        inhalt.Item().Element(JaNein("Ich bin derzeit Mitglied einer anderen Feuerwehr.", antrag?.AndereFeuerwehrAktiv, antrag is null));
                        if (antrag?.AndereFeuerwehrAktiv != false)
                        {
                            inhalt.Item().Element(Felder([("Welche Feuerwehr", antrag?.AndereFeuerwehrName)], einspaltig: true));
                        }

                        inhalt.Item().Element(JaNein("Ich bin aus einer Feuerwehr ausgeschlossen worden.", antrag?.FrueherAusgeschlossen, antrag is null));

                        inhalt.Item().Element(Abschnitt("Antrag und Erklärung"));
                        inhalt.Item().Text($"Ich beantrage die Aufnahme in die Einsatzabteilung{AbDatum(antrag?.GewuenschtAb)}. Ich erkläre, dass ich " +
                            "die Fähigkeit zur Bekleidung öffentlicher Ämter besitze und die Angaben zur Vorgeschichte vollständig und richtig sind. " +
                            "Ich verpflichte mich, am Dienst nach der Feuerwehrsatzung teilzunehmen und die Unfallverhütungsvorschriften zu beachten.");
                        inhalt.Item().Element(Ankreuzen(Eignungserklaerung, null));
                        inhalt.Item().Text($"Die Aufnahme erfolgt auf Probe ({rahmen.Regeln.ProbezeitMonate} Monate). Aufgenommen wird ab {rahmen.Regeln.AufnahmeEinsatzAbAlter} Jahren, Einsatzdienst ab {rahmen.Regeln.EinsatzdienstAbAlter}; " +
                            "Minderjährige brauchen die Zustimmung der Erziehungsberechtigten. Über die Aufnahme entscheidet der Feuerwehrausschuss.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        break;

                    case AntragArt.Volljaehrigkeit:
                        inhalt.Item().Element(Abschnitt("Erklärung"));
                        var volljaehrig = antrag is null ? null : Datum(Laufbahnhinweise.Geburtstag(antrag.Geburtsdatum, rahmen.VolljaehrigAb));
                        inhalt.Item().Text($"Ich bin seit dem {volljaehrig ?? "____________"} volljährig und bestätige meine Mitgliedschaft in der Einsatzabteilung. " +
                            "Die Feuerwehrsatzung erkenne ich an; meine Aufnahme und mein bisheriger Dienst gelten fort. Die oben stehenden Angaben sind aktuell; Änderungen habe ich eingetragen.");
                        inhalt.Item().Text("Die Zustimmung meiner Erziehungsberechtigten ist nicht mehr nötig. Ihre Kontaktdaten werden nach der Datenschutzinformation gelöscht.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        inhalt.Item().Element(Felder([("Änderungen (Anschrift, Telefon, E-Mail)", null)], einspaltig: true, hoch: true));
                        break;
                }

                inhalt.Item().Element(Abschnitt("Datenschutz und Fotos"));
                // Den Erhalt bestätigt die Person selbst; FEVAD kreuzt nichts vor.
                inhalt.Item().Element(Ankreuzen("Die Datenschutzinformation nach Art. 13 DSGVO habe ich erhalten.", null));
                inhalt.Item().Element(Ankreuzen(
                    $"Ich bin damit einverstanden, dass Fotos, auf denen ich (bzw. mein Kind) zu erkennen bin, im Rahmen der Öffentlichkeitsarbeit der Feuerwehr " +
                    $"({rahmen.Datenschutz.FotoKanaele}) veröffentlicht werden. Die Einwilligung ist freiwillig. Ohne sie entstehen keine Nachteile, auch nicht für die Aufnahme. " +
                    "Ich kann sie jederzeit mit Wirkung für die Zukunft widerrufen, formlos beim Kommandanten (Art. 6 Abs. 1 lit. a, Art. 7 Abs. 3 DSGVO). " +
                    "Bereits gedruckte Veröffentlichungen können nicht zurückgeholt werden.",
                    antrag?.FotoEinwilligung));

                inhalt.Item().PaddingTop(10).Element(Unterschriften(art, alter, rahmen.VolljaehrigAb));

                inhalt.Item().PaddingTop(10).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(vermerk =>
                {
                    vermerk.Item().Text("Vermerke der Feuerwehr").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                    vermerk.Item().PaddingTop(4).Text("Eingegangen am: ____________      Feuerwehrausschuss am: ____________").FontSize(9);
                    vermerk.Item().PaddingTop(6).Text(art == AntragArt.Volljaehrigkeit
                        ? "Erledigt am: ____________"
                        : "Angenommen ab: ____________      Probezeit bis: ____________").FontSize(9);
                    vermerk.Item().PaddingTop(10).Text("Unterschrift Kommandant/in: ________________________________").FontSize(9);
                });
            });

            seite.Footer().AlignRight().Text(t =>
            {
                t.Span(antrag is null ? "Blanko-Formular" : $"Erstellt am {rahmen.Heute.ToString("dd.MM.yyyy", Deutsch)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.Span(" · Seite ").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();

    private const string VorgeschichteHinweis =
        "Die Angaben zu einer früheren Feuerwehr brauchen wir, um Vordienstzeiten, Lehrgänge und Dienstgrad anzuerkennen (§ 3 Feuerwehrsatzung). " +
        "Bitte beim Austrittsgrund und bei den Lehrgängen keine Angaben zur Gesundheit machen.";

    /// <summary>Baustein E: nur auf Papier, FEVAD speichert dazu nichts.</summary>
    private const string Eignungserklaerung =
        "Ich erkläre, dass mir keine gesundheitlichen Gründe bekannt sind, die dem Feuerwehrdienst entgegenstehen.";

    /// <summary>Baustein A: Kurzinformation nach Art. 13 DSGVO unter den Personalangaben.</summary>
    public static string Kurzinformation(Datenschutztexte ds) =>
        $"Verantwortliche Stelle: {ds.Verantwortlicher}. Datenschutzbeauftragter: {ds.Beauftragter}. " +
        "Wir verarbeiten Ihre Angaben, um über die Aufnahme zu entscheiden und Ihre Mitgliedschaft zu führen (Art. 6 Abs. 1 lit. e DSGVO i. V. m. Feuerwehrgesetz Baden-Württemberg und Feuerwehrsatzung). " +
        "Ohne die Pflichtangaben können wir den Antrag nicht bearbeiten; Festnetz, Mobilnummer und E-Mail sind freiwillig, soweit eine Erreichbarkeit gegeben ist. " +
        "Den Antrag sehen nur Kommandant, Stellvertreter, Schriftführer und bei der Jugendfeuerwehr der Jugendwart. " +
        $"Wird der Antrag abgelehnt oder zurückgezogen, löschen wir die Angaben nach {Monate(ds.AntraegeLoeschenMonate)}. " +
        "Ihre Rechte (Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Beschwerde beim Landesbeauftragten für den Datenschutz Baden-Württemberg) " +
        "und weitere Einzelheiten finden Sie in der beiliegenden Datenschutzinformation.";

    public static string Anzeigetext(Ausscheidensgrund grund) => grund switch
    {
        Ausscheidensgrund.Umzug => "Umzug",
        Ausscheidensgrund.Wechsel => "Wechsel in eine andere Feuerwehr",
        Ausscheidensgrund.JugendfeuerwehrEnde => "Ende der Jugendfeuerwehr ohne Übertritt",
        Ausscheidensgrund.Beruf => "berufliche Gründe",
        Ausscheidensgrund.Persoenlich => "persönliche Gründe",
        _ => "sonstiger Grund",
    };

    private static string Monate(int monate) => monate == 1 ? "einem Monat" : $"{monate} Monaten";

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

    private static Action<IContainer> Unterschriften(AntragArt art, int? alter, int volljaehrigAb) => c =>
        c.Column(spalte =>
        {
            var linien = new List<string>();
            if (Antragsregeln.UnterschriftPerson(art))
            {
                linien.Add(art == AntragArt.UebertrittEinsatz ? "Unterschrift Jugendliche(r)" : "Unterschrift Antragsteller/in");
            }

            if (Antragsregeln.UnterschriftEltern(art, alter, volljaehrigAb))
            {
                linien.Add("Unterschrift 1. Erziehungsberechtigte(r)");
                linien.Add("Unterschrift 2. Erziehungsberechtigte(r)");
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

            if (Antragsregeln.UnterschriftEltern(art, alter, volljaehrigAb))
            {
                spalte.Item().PaddingTop(4).Text("Bei gemeinsamem Sorgerecht unterschreiben beide Erziehungsberechtigte.").FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });

    private static string? Datum(DateOnly? datum) => datum?.ToString("dd.MM.yyyy", Deutsch);

    private static string Dateinamenteil(string text) =>
        new(text.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss")
            .Where(char.IsAsciiLetterOrDigit).ToArray());
}
