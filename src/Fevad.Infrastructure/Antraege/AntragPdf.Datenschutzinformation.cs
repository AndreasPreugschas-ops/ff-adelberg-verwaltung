using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure.Antraege;

/// <summary>
/// Datenschutzinformation nach Art. 13 DSGVO als Anhang jedes Antrags (Entwurf 06 vom 22.10.2026, vom Datenschutzbeauftragten
/// zu prüfen). Platzhalter in eckigen Klammern kommen aus den Text-Parametern und bleiben, bis der Datenschutzbeauftragte sie freigibt.
/// </summary>
public static partial class AntragPdf
{
    private static void Datenschutzinformation(PageDescriptor seite, Rahmen rahmen, bool jugend)
    {
        Seitenrahmen(seite);
        seite.DefaultTextStyle(t => t.FontSize(7.5f).LineHeight(1.1f));
        seite.Header().Element(c => Briefkopf(c, rahmen.Kopf with { Abteilung = null }, "Datenschutzinformation nach Art. 13 DSGVO"));
        seite.Content().MultiColumn(spalten =>
        {
            spalten.Columns(2);
            spalten.Spacing(16);
            spalten.Content().Column(text =>
            {
                text.Spacing(3);
                foreach (var (titel, absaetze) in Abschnitte(rahmen, jugend))
                {
                    // Überschrift nie allein am Spaltenende: sie bleibt beim ersten Absatz.
                    text.Item().ShowEntire().Column(anfang =>
                    {
                        anfang.Spacing(3);
                        anfang.Item().PaddingTop(4).Text(titel).FontSize(8.5f).SemiBold().FontColor(Rot);
                        anfang.Item().Element(c => Absatz(c, absaetze[0]));
                    });
                    foreach (var absatz in absaetze.Skip(1))
                    {
                        text.Item().ShowEntire().Element(c => Absatz(c, absatz));
                    }
                }
            });
        });
        seite.Footer().Element(c => Fusszeile(c, "Bitte bewahren Sie diese Information auf"));
    }

    /// <summary>Absatz mit **fett**; beginnt er mit „- “, wird er ein Aufzählungspunkt.</summary>
    private static void Absatz(IContainer c, string absatz)
    {
        var punkt = absatz.StartsWith("- ", StringComparison.Ordinal);
        var teile = (punkt ? absatz[2..] : absatz).Split("**");
        if (!punkt)
        {
            c.Text(t => Teile(t, teile));
            return;
        }

        c.Row(zeile =>
        {
            zeile.ConstantItem(8).Text("•");
            zeile.RelativeItem().Text(t => Teile(t, teile));
        });
    }

    private static void Teile(TextDescriptor t, string[] teile)
    {
        for (var i = 0; i < teile.Length; i++)
        {
            var span = t.Span(teile[i]);
            if (i % 2 == 1)
            {
                span.SemiBold();
            }
        }
    }

    private static IEnumerable<(string Titel, string[] Absaetze)> Abschnitte(Rahmen rahmen, bool jugend)
    {
        var ds = rahmen.Datenschutz;
        var g = ds.Gemeinde;
        var feuerwehr = rahmen.Kopf.Feuerwehr;
        var erreichbar = string.Join(" · ", new[] { g.Telefon, g.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));

        yield return ("Worum geht es?",
        [
            $"Die {feuerwehr} verwaltet ihre Personal-, Ausbildungs- und Einsatzdaten mit der Verwaltungssoftware FEVAD. " +
            "Damit Sie wissen, was mit Ihren Daten geschieht, informieren wir Sie hier.",
        ]);
        yield return ("1. Wer ist verantwortlich?",
        [
            $"Die Feuerwehr ist eine Einrichtung der Gemeinde. Verantwortlich für Ihre Daten ist deshalb die **{g.Name}**" +
            $"{(string.IsNullOrWhiteSpace(g.Vertretung) ? "" : ", " + g.Vertretung)}, {g.Anschrift ?? AnschriftFehlt}" +
            $"{(erreichbar.Length == 0 ? "" : " · " + erreichbar)}.",
        ]);
        yield return ("2. Datenschutzbeauftragter", [ds.Beauftragter]);
        yield return ("3. Wofür verarbeiten wir Ihre Daten?",
        [
            "- Führung des Personalstands (Aufnahme, Probezeit, Abteilung, Dienstgrad, Funktionen, Wahlen, Austritt)",
            "- Planung und Nachweis von Übungen, Unterricht und Lehrgängen, Anwesenheit",
            "- Dokumentation von Einsätzen (wer war dabei, mit welchem Fahrzeug, wie lange)",
            "- Arbeitsschutz: Nachweis der Atemschutztauglichkeit und -fortbildung, Kontrolle der Fahrerlaubnis für Maschinisten, Ausgabe von Schutzausrüstung",
            "- Berechnung der Entschädigung nach der Entschädigungssatzung (Übermittlung an die Gemeindekasse)",
            "- Ehrungen und Beförderungen",
            "- Bereitstellung Ihrer Erreichbarkeit für die Alarmierung über das Alarmierungssystem",
            "- Erreichbarkeit für dienstliche Informationen, z. B. Erinnerung an ablaufende Fristen per E-Mail",
        ]);
        yield return ("4. Rechtsgrundlage",
        [
            "Die Verarbeitung ist zur Erfüllung der Aufgaben der Gemeinde als Trägerin der Feuerwehr erforderlich: Art. 6 Abs. 1 lit. c und e DSGVO " +
            "in Verbindung mit dem Feuerwehrgesetz Baden-Württemberg, dem Landesdatenschutzgesetz Baden-Württemberg und der Feuerwehrsatzung. " +
            "Für den Arbeitsschutz gelten zusätzlich die DGUV Vorschrift 49 und die Feuerwehr-Dienstvorschrift 7.",
            $"Die Atemschutztauglichkeit ist ein Gesundheitsdatum. Wir verarbeiten sie auf Grundlage von {ds.Art9Grundlage}.",
            "Fotos für die Öffentlichkeitsarbeit veröffentlichen wir nur mit Ihrer Einwilligung (Art. 6 Abs. 1 lit. a DSGVO). Sie ist freiwillig und jederzeit widerrufbar.",
        ]);
        yield return ("5. Welche Daten?",
        [
            "- **Stammdaten:** Name, Geburtsdatum, Geschlecht, Anschrift, Telefonnummern, E-Mail, Personalnummer; Beruf, Arbeitgeber mit dienstlicher Erreichbarkeit und Arbeitsort (für die Tagesverfügbarkeit)",
            "- **Dienstliche Daten:** Abteilung, Status, Ein- und Austrittsdatum, Dienstgrade, Funktionen, Lehrgänge und Qualifikationen, Fahrerlaubnisklassen (ohne Kopie des Führerscheins), Anwesenheit, Einsatzteilnahmen, ausgegebene Schutzausrüstung mit Größen, Ehrungen",
            "- **Atemschutz:** nur Datum der Untersuchung, Ergebnis (geeignet, befristet geeignet, nicht geeignet) und nächste Fälligkeit, dazu Übungen und Atemschutzeinsätze. **Befunde und Diagnosen speichern wir nicht.**",
            "- **Zugangsdaten**, wenn Sie ein Benutzerkonto haben: Benutzername, E-Mail, Zeitpunkt der letzten Anmeldung. Das Passwort wird nur als nicht umkehrbarer Hash gespeichert. Änderungen an Daten werden mit Zeitpunkt und Bearbeiter protokolliert.",
        ]);
        yield return ("6. Wer bekommt Ihre Daten?",
        [
            "- **Innerhalb der Feuerwehr** nur die Funktionsträger, die sie für ihre Aufgabe brauchen (z. B. Kommandant, Gruppenführer, Gerätewart, Atemschutzgerätewart, Kassier, Schriftführer). " +
            "Die Atemschutztauglichkeit sehen nur Kommandant, Stellvertreter und Atemschutzgerätewart.",
            "- **Gemeindeverwaltung:** Hauptamt (Personalstand, Ehrungen), Kämmerei bzw. Gemeindekasse (Entschädigung, Kostenersatz).",
            $"- **Alarmierungssystem:** Name, Mobilnummer und Qualifikationskürzel gehen an das Alarmierungssystem, das {ds.AlarmierungBetreiber} betrieben wird.",
            $"- **Technische Dienstleister**, die im Auftrag und nach Weisung der Gemeinde handeln (Art. 28 DSGVO): {ds.Auftragsverarbeiter}. Server und Sicherungen stehen in Deutschland bzw. der EU.",
            "- **Weitere Stellen nur im Einzelfall** und soweit gesetzlich vorgesehen: Kreisbrandmeister und Landesfeuerwehrschule (Lehrgänge), Unfallkasse Baden-Württemberg (Dienstunfall), " +
            "Kreisfeuerwehrverband bzw. Land (Ehrungsanträge). Statistiken gehen nur ohne Namen hinaus.",
            "Eine Übermittlung in Länder außerhalb der EU findet nicht statt.",
        ]);
        yield return ("7. Wie lange?",
        [
            $"Wir löschen Ihre Daten, sobald sie nicht mehr gebraucht werden. Nach dem Austritt löschen wir Kontaktdaten nach {Monate(ds.KontaktdatenNachAustrittMonate)}. " +
            $"Ein Restdatensatz (Name, Geburtsdatum, Dienstzeiten, Dienstgrade, Lehrgänge, Ehrungen) bleibt {Jahre(ds.RestdatensatzJahre)} erhalten, damit wir Dienstzeiten bescheinigen können, " +
            $"z. B. beim Wechsel in eine andere Feuerwehr. Einsatzberichte bewahren wir {Jahre(ds.EinsatzberichteJahre)} auf, Daten aus der Alarmierung höchstens {MonateAkkusativ(ds.AlarmdatenMonate)}. " +
            $"Wird ein Antrag abgelehnt oder zurückgezogen, löschen wir ihn nach {Monate(ds.AntraegeLoeschenMonate)}. Die genauen Fristen stehen im Löschkonzept der Gemeinde, das Sie beim Hauptamt einsehen können.",
        ]);
        yield return ("8. Ihre Rechte",
        [
            "Sie haben das Recht auf **Auskunft** (Art. 15 DSGVO), **Berichtigung** (Art. 16), **Löschung** (Art. 17) und **Einschränkung** der Verarbeitung (Art. 18), " +
            "soweit keine Aufbewahrungspflicht entgegensteht, sowie auf **Widerspruch** aus Gründen Ihrer besonderen Situation (Art. 21).",
            "Wenden Sie sich dafür an den Kommandanten oder an das Hauptamt der Gemeinde. Mit einem Benutzerkonto können Sie Ihre Daten auch selbst in FEVAD einsehen und herunterladen: " +
            "„Mein Konto“ → „Meine gespeicherten Daten“.",
        ]);
        yield return ("9. Beschwerderecht",
        [
            "Sie können sich bei der Aufsichtsbehörde beschweren: Der Landesbeauftragte für den Datenschutz und die Informationsfreiheit Baden-Württemberg, " +
            "Lautenschlagerstraße 20, 70173 Stuttgart, www.baden-wuerttemberg.datenschutz.de.",
        ]);
        yield return ("10. Müssen Sie die Daten angeben?",
        [
            "Die Angaben zu Person, Erreichbarkeit, Ausbildung und Tauglichkeit sind für den Feuerwehrdienst erforderlich. Ohne sie können wir Sie nicht aufnehmen bzw. nicht für die entsprechende " +
            "Funktion einteilen (z. B. ohne gültige Tauglichkeit kein Atemschutzeinsatz). Freiwillig sind Angaben wie Festnetznummer, Beruf und Arbeitgeber.",
            "Eine automatisierte Entscheidung im Sinne von Art. 22 DSGVO findet nicht statt. FEVAD zeigt Fristen an; über den Einsatz entscheidet immer die Führungskraft.",
        ]);

        if (jugend)
        {
            yield return ("11. Ergänzung für die Jugendfeuerwehr (an die Erziehungsberechtigten)",
            [
                "- Zusätzlich speichern wir von bis zu zwei Erziehungsberechtigten: Verhältnis zum Kind, Name, Anschrift, Telefon, Mobilnummer und E-Mail. " +
                "Die unterschriebene Einverständniserklärung zur Aufnahme bewahren wir in Papierform auf.",
                "- Die Daten der Jugend- und Kinderfeuerwehr werden getrennt vom übrigen Personal geführt. Sie sieht nur, wer dafür eigens berechtigt ist, " +
                "in der Regel der Jugendwart, der Kommandant und sein Stellvertreter. Andere Funktionsträger der Feuerwehr sehen sie nicht.",
                $"- Daten der Erziehungsberechtigten löschen wir mit dem {ds.ElternBisAlter}. Geburtstag, spätestens {MonateAkkusativ(ds.KontaktdatenNachAustrittMonate)} nach dem Austritt.",
                "- Die Rechte nach Abschnitt 8 können bei Minderjährigen auch die Erziehungsberechtigten geltend machen.",
                "- **Fotos** veröffentlichen wir nur mit gesonderter Einwilligung (im Antrag „ja“ oder „nein“). Wir speichern, ob und seit wann sie vorliegt; " +
                "ein Widerruf ist jederzeit formlos beim Kommandanten möglich und wird mit Datum vermerkt.",
            ]);
        }
    }

    private static string Jahre(int jahre) => jahre == 1 ? "ein Jahr" : $"{jahre} Jahre";
}
