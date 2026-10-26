using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using static Fevad.Infrastructure.Identitaet.Rollen;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>
/// Alle Rechte mit Bereich und Art (<see cref="Katalog"/>) und ihre Vorbelegung je Rolle. Neue Rechte nur hier eintragen.
/// Welche Rolle ein Recht tatsächlich hat, steht in der Datenbank (Verwaltung → Rollen); die Vorbelegung gilt beim ersten
/// Start und für jedes neu eingeführte Recht einmal. Abgeleitete Policies (<see cref="Abgeleitet"/>) stehen nicht in der
/// Matrix, sie folgen aus einem Recht und seinem Abteilungsbezug.
/// </summary>
public static class Berechtigungen
{
    /// <summary>Recht: Mitglieder lesen.</summary>
    public const string MitgliederLesen = "Mitglieder.Lesen";

    /// <summary>Neue Mitglieder anlegen; mit Abteilungsbezug nur in diesen Abteilungen.</summary>
    public const string MitgliederAnlegen = "Mitglieder.Anlegen";
    /// <summary>Recht: Mitglieder bearbeiten, übertreten, austreten.</summary>
    public const string MitgliederBearbeiten = "Mitglieder.Bearbeiten";

    /// <summary>Abgeleitet: Zugang zur Mitgliederliste, wer Mitglieder.Lesen für irgendeine Abteilung hat. Was er sieht, regelt der Datenbereich.</summary>
    public const string MitgliederSehen = "Mitglieder.Sehen";

    /// <summary>Beruf und Arbeitgeber mit Kontaktdaten sehen und pflegen (Tagesverfügbarkeit); bearbeiten nur zusammen mit Mitglieder.Bearbeiten.</summary>
    public const string MitgliederArbeitgeber = "Mitglieder.Arbeitgeber";

    /// <summary>Aufnahme- und Übertrittsanträge ausfüllen, drucken und entscheiden; mit Abteilungsbezug nur für diese Zielabteilungen.</summary>
    public const string AntraegeBearbeiten = "Antraege.Bearbeiten";

    /// <summary>Abgeleitet: Zugang zu den Anträgen, wer Antraege.Bearbeiten für irgendeine Abteilung hat.</summary>
    public const string AntraegeSehen = "Antraege.Sehen";

    /// <summary>Anträge rückwirkend erfassen: Aufnahme, Übertritt oder Altersabteilung mit einem Beginn vor heute (Altdaten, Nacherfassung).</summary>
    public const string AntraegeNacherfassen = "Antraege.Nacherfassen";

    /// <summary>
    /// Bereich der Jugend- und Kinderfeuerwehr betreten. Ihre Daten sind vom übrigen Personal abgeschirmt: Rechte „für alle
    /// Abteilungen“ umfassen Jugend- und Kinderfeuerwehr nur mit diesem Recht; ein Recht ausdrücklich für die Jugendfeuerwehr
    /// (Jugendwart) zählt ebenso.
    /// </summary>
    public const string JugendZugang = "Jugend.Zugang";

    /// <summary>Abgeleitet: Zugang zur Jugend- oder Kinderfeuerwehr und Mitglieder.Lesen dort (Jugendabzeichen, Jugendfristen).</summary>
    public const string JugendLesen = "Jugend.Lesen";

    /// <summary>Abgeleitet: Mitglieder.Bearbeiten für die Jugend- oder Kinderfeuerwehr.</summary>
    public const string JugendBearbeiten = "Jugend.Bearbeiten";

    /// <summary>
    /// Notfallliste der Jugend- und Kinderfeuerwehr (V46): Heimweg, Abholberechtigte, Erziehungsberechtigte, Notfallkontakt und Stand der
    /// Notfallmappe, als Seite und PDF für Dienste und Fahrten. Keine Gesundheitsangaben (Entscheidung E3).
    /// </summary>
    public const string JugendNotfallliste = "Jugend.Notfallliste";

    /// <summary>
    /// Betreuer-Register der Jugend- und Kinderfeuerwehr mit dem Stand der erweiterten Führungszeugnisse (V47): eintragen, einsehen vermerken,
    /// Trägerbescheinigung drucken. Das Ergebnis sehen nur Kommandant und Jugendwart.
    /// </summary>
    public const string JugendBetreuer = "Jugend.Betreuer";

    /// <summary>Abgeleitet: <see cref="JugendBetreuer"/> für mindestens eine Jugend- oder Kinderfeuerwehr.</summary>
    public const string JugendBetreuerSehen = "Jugend.BetreuerSehen";

    /// <summary>
    /// Abgeleitet: Veranstaltungen der Jugend (V48) planen, wer <see cref="DiensteBearbeiten"/> für mindestens eine Jugend- oder
    /// Kinderfeuerwehr hat. Welche Veranstaltungen er sieht, regeln die Zielabteilungen der Dienste.
    /// </summary>
    public const string JugendVeranstaltungenSehen = "Jugend.VeranstaltungenSehen";

    /// <summary>Abgeleitet: <see cref="JugendNotfallliste"/> für mindestens eine Jugend- oder Kinderfeuerwehr; schützt Seite und Menüpunkt.</summary>
    public const string JugendNotfalllisteSehen = "Jugend.NotfalllisteSehen";
    /// <summary>Recht: Laufbahn lesen.</summary>
    public const string LaufbahnLesen = "Laufbahn.Lesen";
    /// <summary>Recht: Beförderungen, Ehrungen, Befreiungen eintragen.</summary>
    public const string LaufbahnBearbeiten = "Laufbahn.Bearbeiten";
    /// <summary>Recht: Regeln und Ehrungsarten pflegen.</summary>
    public const string LaufbahnRegeln = "Laufbahn.Regeln";

    /// <summary>Grund einer Befreiung sehen (Krankheit, Mutterschutz sind Gesundheitsdaten); alle anderen sehen nur „befreit“.</summary>
    public const string LaufbahnBefreiungsgrund = "Laufbahn.BefreiungsgrundLesen";

    /// <summary>Empfänger der Erinnerung an Übertritte, Ehrungen und Beförderungen (keine Seitenrechte).</summary>
    public const string ErinnerungLaufbahn = "Fristen.ErinnerungLaufbahn";
    /// <summary>Recht: Erinnerung Jugendfeuerwehr.</summary>
    public const string ErinnerungJugend = "Fristen.ErinnerungJugend";
    /// <summary>Recht: Atemschutz lesen (Tauglichkeit, Fristen).</summary>
    public const string AtemschutzLesen = "Atemschutz.Lesen";
    /// <summary>Recht: Atemschutz-Einsatzstatus sehen, nur einsatzbereit ja/nein und Fristen, ohne Vorsorgen (Führungskräfte, V19).</summary>
    public const string AtemschutzStatusLesen = "Atemschutz.StatusLesen";
    /// <summary>Recht: Atemschutz eintragen.</summary>
    public const string AtemschutzBearbeiten = "Atemschutz.Bearbeiten";

    /// <summary>Atemschutznachweise für das Kommando bestätigen; jeder Nachweis braucht dazu die Bestätigung des Atemschutzwarts (Andi, 25.10.2026).</summary>
    public const string AtemschutzBestaetigenKommando = "Atemschutz.BestaetigenKommando";

    /// <summary>Atemschutznachweise als Atemschutzgerätewart bestätigen.</summary>
    public const string AtemschutzBestaetigenWart = "Atemschutz.BestaetigenWart";
    /// <summary>Recht: Lehrgänge und Fristen lesen.</summary>
    public const string AusbildungLesen = "Ausbildung.Lesen";
    /// <summary>Recht: Lehrgänge und Nachweise eintragen.</summary>
    public const string AusbildungBearbeiten = "Ausbildung.Bearbeiten";

    /// <summary>Wiederkehrende Nachweise ohne Gesundheitsbezug eintragen, z. B. Führerscheinkontrolle oder UVV-Unterweisung.</summary>
    public const string AusbildungNachweise = "Ausbildung.Nachweise";
    /// <summary>Recht: Kataloge und Fristen pflegen.</summary>
    public const string AusbildungKataloge = "Ausbildung.Kataloge";
    /// <summary>Chronik der Feuerwehr lesen (intern, V56).</summary>
    public const string ChronikLesen = "Chronik.Lesen";
    /// <summary>Chronik führen und Einträge zur Veröffentlichung freigeben (Schriftführung, F25).</summary>
    public const string ChronikBearbeiten = "Chronik.Bearbeiten";
    /// <summary>Jubiläumsvorschau mit Mitgliedsjahren und runden Geburtstagen sehen.</summary>
    public const string Jubilaeen = "Chronik.Jubilaeen";
    /// <summary>Serienbriefe und Serienmails aus Vorlagen an Mitglieder oder Eltern schreiben (V56); welche Mitglieder, regelt der Datenbereich.</summary>
    public const string Serienbriefe = "Vorlagen.Serienbriefe";
    /// <summary>Dienstpflicht-Vorgänge (Abwesenheit, Beurlaubung, Ordnungsmaßnahmen) und Fachberater-Kennzeichen in der Akte führen (V57).</summary>
    public const string Dienstpflicht = "Mitglieder.Dienstpflicht";
    /// <summary>Briefvorlagen anlegen und ändern (Schriftführung, V56).</summary>
    public const string VorlagenBearbeiten = "Vorlagen.Bearbeiten";
    /// <summary>Lehrgangswünsche priorisieren, entscheiden und als Herbstmeldung an den Kreisbrandmeister geben (V54).</summary>
    public const string Lehrgangsmeldung = "Ausbildung.Lehrgangsmeldung";

    /// <summary>Abteilungen (Art, Unterabteilung, aktiv) und Funktionen der Feuerwehr pflegen.</summary>
    public const string StammdatenOrganisation = "Stammdaten.Organisation";

    /// <summary>Empfänger der wöchentlichen Fristerinnerung (keine Seitenrechte).</summary>
    public const string ErinnerungPersonal = "Fristen.ErinnerungPersonal";
    /// <summary>Recht: Fristerinnerung Atemschutz.</summary>
    public const string ErinnerungAtemschutz = "Fristen.ErinnerungAtemschutz";
    /// <summary>Recht: Anwesenheit und Rückmeldungen lesen.</summary>
    public const string AnwesenheitLesen = "Dienste.AnwesenheitLesen";

    /// <summary>Abgeleitet: Anwesenheitslisten aufrufen, wer Dienste.AnwesenheitLesen für irgendeine Abteilung hat (prüft Datenbereich).</summary>
    public const string AnwesenheitSehen = "Dienste.AnwesenheitSehen";
    /// <summary>Recht: Dienste planen, Anwesenheit erfassen.</summary>
    public const string DiensteBearbeiten = "Dienste.Bearbeiten";

    /// <summary>Einstellungen des Dienstplans, z. B. bis wann vor Beginn Mitglieder selbst zu- oder absagen können.</summary>
    public const string DiensteEinstellungen = "Dienste.Einstellungen";

    /// <summary>Gemeldete Abwesenheiten mit Grund sehen und anzeigepflichtige zur Kenntnis nehmen (Satzung § 5 Abs. 6, V37).</summary>
    public const string AbwesenheitenLesen = "Dienste.Abwesenheiten";
    /// <summary>Recht: Einsatzberichte lesen.</summary>
    public const string EinsaetzeLesen = "Einsaetze.Lesen";
    /// <summary>Recht: Einsatzberichte schreiben.</summary>
    public const string EinsaetzeBearbeiten = "Einsaetze.Bearbeiten";
    /// <summary>Recht: Einsatzberichte freigeben.</summary>
    public const string EinsaetzeFreigeben = "Einsaetze.Freigeben";

    /// <summary>Kostenersatz-Sätze (Fahrzeuge, Personal, Ausrüstung) nach der Kostenersatzsatzung pflegen.</summary>
    public const string EinsaetzeKostensaetze = "Einsaetze.Kostensaetze";

    /// <summary>Anschrift, Telefon, Kennzeichen und Versicherung der Beteiligten sehen und eintragen; ohne nur Rolle und Name.</summary>
    public const string EinsaetzeBeteiligte = "Einsaetze.Beteiligte";

    /// <summary>Kostenersatz-Vorgänge mit Schuldnern und Aufstellung lesen.</summary>
    public const string KostenersatzLesen = "Kostenersatz.Lesen";
    /// <summary>Kostenersatz-Vorgang bearbeiten und eine Empfehlung der Feuerwehr abgeben.</summary>
    public const string KostenersatzEmpfehlen = "Kostenersatz.Empfehlen";
    /// <summary>Über den Kostenersatz entscheiden; nur die Gemeinde (Andi, 25.10.2026: die Feuerwehr kann nur empfehlen).</summary>
    public const string KostenersatzEntscheiden = "Kostenersatz.Entscheiden";
    /// <summary>Forderungen aus Kostenersatzbescheiden ohne Schuldnernamen sehen und Zahlungsrückmeldungen der Gemeindekasse einspielen (V23, V26).</summary>
    public const string KostenersatzForderungen = "Kostenersatz.Forderungen";
    /// <summary>Stundensatz nach § 34 Abs. 5 FwG kalkulieren und als Anlage zur Gemeinderatsvorlage ausgeben (V24).</summary>
    public const string KostenersatzKalkulation = "Kostenersatz.Kalkulation";

    /// <summary>Vorgänge „Entscheidung der Gemeinde nötig“ mit Vorlagen-Entwurf lesen und vorbereiten (V27).</summary>
    public const string GemeindeVorgaenge = "Gemeinde.Vorgaenge";
    /// <summary>Vorgänge der Gemeinde vorlegen und die Entscheidung eintragen; nur die Gemeinde (V27).</summary>
    public const string GemeindeEntscheidungen = "Gemeinde.Entscheidungen";
    /// <summary>Bescheide des Bürgermeisters an Mitglieder und Bewerber mit Anhörung vorbereiten und führen (V27).</summary>
    public const string GemeindeMitgliederbescheide = "Gemeinde.Mitgliederbescheide";
    /// <summary>Dienstausweise ausstellen, drucken und die Rückgabe vermerken (V27).</summary>
    public const string Dienstausweise = "Mitglieder.Dienstausweise";

    /// <summary>Spaltenzuordnung des Finanzexports an die Kämmerei einrichten (V23).</summary>
    public const string FinanzexportEinrichten = "Finanzexport.Einrichten";

    /// <summary>Bankverbindungen der Mitglieder sehen und pflegen, Auszahlungsliste mit IBAN und Überweisungsdatei erzeugen.</summary>
    public const string KasseBankdaten = "Kasse.Bankdaten";

    /// <summary>Kassenbuch, Konten und Wirtschaftsplan lesen; mit Abteilungsbezug nur die Kassen dieser Abteilungen (Jugendwart: JF-Kasse).</summary>
    public const string KasseLesen = "Kasse.Lesen";

    /// <summary>Abgeleitet: Zugang zu den Kassen, wer Kasse.Lesen für irgendeine Abteilung hat.</summary>
    public const string KasseSehen = "Kasse.Sehen";

    /// <summary>Buchen, Belege hochladen, festschreiben, stornieren und den Wirtschaftsplan aufstellen (Kassenwart, verfügt allein).</summary>
    public const string KasseBuchen = "Kasse.Buchen";

    /// <summary>Ausgaben mit Beleg vorbereiten, die der Kassenwart zahlt und bucht (Jugendwart für die JF-Kasse).</summary>
    public const string KasseVorbereiten = "Kasse.Vorbereiten";

    /// <summary>Anordnung einer vorbereiteten Ausgabe vermerken; nur Dokumentation, keine Voraussetzung der Zahlung.</summary>
    public const string KasseAnordnen = "Kasse.Anordnen";

    /// <summary>Kassen, Konten mit Anfangsbeständen, Gliederung und Einstellungen der Kassen pflegen.</summary>
    public const string KasseEinrichten = "Kasse.Einrichten";

    /// <summary>Kassenprüfung anlegen, Beanstandungen erfassen und die Prüfung bestätigen (gewählte Rechnungsprüfer, V05).</summary>
    public const string KassePruefen = "Kasse.Pruefen";

    /// <summary>Dienstunfälle mit Verletzungsangaben lesen (Gesundheitsdaten, Unfallanzeige § 193 SGB VII).</summary>
    public const string UnfaelleLesen = "Unfaelle.Lesen";

    /// <summary>Dienstunfall erfassen; wer nur dieses Recht hat, sieht den Unfall nach dem Speichern nicht mehr.</summary>
    public const string UnfaelleAnlegen = "Unfaelle.Anlegen";

    /// <summary>Dienstunfälle bearbeiten, melden und abschließen.</summary>
    public const string UnfaelleBearbeiten = "Unfaelle.Bearbeiten";

    /// <summary>Gemeldete Sachschäden an privatem Eigentum (§ 17 FwG) mit Fotos und Beträgen lesen.</summary>
    public const string SachschaedenLesen = "Sachschaeden.Lesen";

    /// <summary>Sachschäden an die Gemeinde weiterleiten, Entscheidung und Erstattung eintragen.</summary>
    public const string SachschaedenBearbeiten = "Sachschaeden.Bearbeiten";
    /// <summary>Recht: Fahrzeuge und Geräte lesen.</summary>
    public const string TechnikLesen = "Technik.Lesen";
    /// <summary>Recht: Prüfungen eintragen.</summary>
    public const string TechnikPruefen = "Technik.Pruefen";
    /// <summary>Recht: Dokumente lesen.</summary>
    public const string DokumenteLesen = "Dokumente.Lesen";
    /// <summary>Recht: Vertrauliche Dokumente lesen.</summary>
    public const string DokumenteVertraulichLesen = "Dokumente.VertraulichLesen";
    /// <summary>Recht: Dokumente ablegen und ändern.</summary>
    public const string DokumenteVerwalten = "Dokumente.Verwalten";

    /// <summary>Dokumente samt allen Versionen und Dateien endgültig löschen oder alte Versionen entfernen.</summary>
    public const string DokumenteEndgueltigLoeschen = "Dokumente.EndgueltigLoeschen";
    /// <summary>Recht: Sitzungen und Wahlen lesen.</summary>
    public const string GremienLesen = "Gremien.Lesen";
    /// <summary>Recht: Sitzungen und Wahlen pflegen.</summary>
    public const string GremienBearbeiten = "Gremien.Bearbeiten";
    /// <summary>Recht: Wahlergebnis in Funktionen übernehmen.</summary>
    public const string WahlenUebernehmen = "Gremien.WahlenUebernehmen";
    /// <summary>
    /// Recht: Niederschriften der Hauptversammlung und die für Mitglieder freigegebenen Kassenberichte (Jahresabschluss, Prüfbericht) lesen
    /// (V09; alle Mitglieder, Andi 25.10.2026).
    /// </summary>
    public const string VersammlungLesen = "Gremien.VersammlungLesen";
    /// <summary>Recht: Abrechnungen lesen.</summary>
    public const string EntschaedigungLesen = "Entschaedigung.Lesen";
    /// <summary>Recht: Abrechnung vorbereiten.</summary>
    public const string EntschaedigungAbrechnen = "Entschaedigung.Abrechnen";
    /// <summary>Recht: Abrechnung abschließen.</summary>
    public const string EntschaedigungAbschliessen = "Entschaedigung.Abschliessen";
    /// <summary>Recht: Sätze pflegen.</summary>
    public const string EntschaedigungSaetze = "Entschaedigung.Saetze";
    /// <summary>Recht: Jahresstatistik und Jahresbericht.</summary>
    public const string StatistikLesen = "Statistik.Lesen";
    /// <summary>Recht: Führungskennzahlen mit Personenbezug lesen: Einsatzbereitschaft, Zeiten, Beteiligung, Abgangsprognose (V52).</summary>
    public const string KennzahlenLesen = "Fuehrung.Kennzahlen";
    /// <summary>Recht: Fahrzeuge, Geräte, Gerätearten pflegen.</summary>
    public const string TechnikBearbeiten = "Technik.Bearbeiten";
    /// <summary>Recht: Mängel melden.</summary>
    public const string MaengelMelden = "Technik.MaengelMelden";
    /// <summary>Recht: Beschaffungen lesen.</summary>
    public const string BeschaffungLesen = "Beschaffung.Lesen";
    /// <summary>Recht: Beschaffungen anlegen und bearbeiten.</summary>
    public const string BeschaffungBearbeiten = "Beschaffung.Bearbeiten";
    /// <summary>Recht: Beschaffungen freigeben.</summary>
    public const string BeschaffungFreigeben = "Beschaffung.Freigeben";
    /// <summary>Recht: Kleiderkammer lesen.</summary>
    public const string KleiderkammerLesen = "Kleiderkammer.Lesen";
    /// <summary>Recht: Ausgaben und Artikel pflegen.</summary>
    public const string KleiderkammerBearbeiten = "Kleiderkammer.Bearbeiten";
    /// <summary>Recht: Systemverwaltung.</summary>
    public const string Verwaltung = "Verwaltung";

    /// <summary>Löschnachweise und Fristen des Löschkonzepts einsehen, Löschlauf von Hand anstoßen.</summary>
    public const string DatenschutzLoeschlauf = "Datenschutz.Loeschlauf";

    /// <summary>Altdaten aus CSV- oder XLSX-Dateien importieren (V68): Personen und Lehrgänge anlegen oder ergänzen.</summary>
    public const string DatenImportieren = "Verwaltung.Import";

    /// <summary>Benutzerkonten anlegen und Rollen mit ihren Rechten pflegen.</summary>
    public const string BenutzerVerwalten = "Benutzer.Verwalten";

    /// <summary>
    /// Zweite Freigabe (Vier-Augen-Prinzip, F18): Rechte auf Gesundheits- oder Jugenddaten wirken erst, wenn eine andere Person als die,
    /// die sie vergeben hat, sie freigibt.
    /// </summary>
    public const string RechteFreigeben = "Benutzer.RechteFreigeben";

    /// <summary>Leseprotokoll der Atemschutzdaten und Befreiungsgründe einsehen (F17): wer hat wann wessen Gesundheitsdaten geöffnet.</summary>
    public const string DatenschutzLeseprotokoll = "Datenschutz.Leseprotokoll";

    /// <summary>Was ein Recht im Bereich erlaubt; danach ordnet die Rechtematrix ihre Spalten.</summary>
    public enum Aktion
    {
        /// <summary>Lesen.</summary>
        [Display(Name = "lesen")] Lesen,
        /// <summary>Anlegen.</summary>
        [Display(Name = "anlegen")] Anlegen,
        /// <summary>Bearbeiten.</summary>
        [Display(Name = "bearbeiten")] Bearbeiten,
        /// <summary>Löschen.</summary>
        [Display(Name = "löschen")] Loeschen,
        /// <summary>Sonderrecht außerhalb von Lesen und Bearbeiten.</summary>
        [Display(Name = "Sonderrecht")] Sonderrecht,
        /// <summary>Empfang einer Erinnerung, ohne Seitenrechte.</summary>
        [Display(Name = "Erinnerung")] Erinnerung,
    }

    /// <summary>Ein Recht im Katalog.</summary>
    /// <param name="Name">Name des Rechts und der Policy.</param>
    /// <param name="Bereich">Bereich in der Rechtematrix.</param>
    /// <param name="Aktion">Art des Rechts.</param>
    /// <param name="Bezeichnung">Bezeichnung in der Oberfläche.</param>
    /// <param name="Gesundheitsdaten">Schutzschranke: Vergabe nur mit ausdrücklicher Bestätigung (Art. 9 DSGVO).</param>
    /// <param name="Abteilungsbezug">Lässt sich auf einzelne Abteilungen beschränken.</param>
    public sealed record Recht(string Name, string Bereich, Aktion Aktion, string Bezeichnung, bool Gesundheitsdaten = false, bool Abteilungsbezug = false);

    /// <summary>Alle Rechte mit Bereich, Art und Bezeichnung.</summary>
    public static readonly IReadOnlyList<Recht> Katalog =
    [
        new(MitgliederLesen, "Mitglieder", Aktion.Lesen, "Mitglieder lesen", Abteilungsbezug: true),
        new(MitgliederAnlegen, "Mitglieder", Aktion.Anlegen, "Mitglieder anlegen", Abteilungsbezug: true),
        new(MitgliederBearbeiten, "Mitglieder", Aktion.Bearbeiten, "Mitglieder bearbeiten, übertreten, austreten", Abteilungsbezug: true),
        new(MitgliederArbeitgeber, "Mitglieder", Aktion.Sonderrecht, "Beruf und Arbeitgeber"),
        new(AntraegeBearbeiten, "Mitglieder", Aktion.Sonderrecht, "Aufnahme- und Übertrittsanträge", Abteilungsbezug: true),
        new(AntraegeNacherfassen, "Mitglieder", Aktion.Sonderrecht, "Anträge rückwirkend erfassen (Beginn vor heute)"),
        new(JugendZugang, "Mitglieder", Aktion.Sonderrecht, "Bereich Jugend- und Kinderfeuerwehr betreten"),
        new(JugendNotfallliste, "Mitglieder", Aktion.Lesen, "Notfallliste der Jugend: Heimweg, Abholberechtigte, Eltern", Abteilungsbezug: true),
        new(JugendBetreuer, "Mitglieder", Aktion.Sonderrecht, "Betreuer der Jugend und Führungszeugnisse", Abteilungsbezug: true),
        new(LaufbahnLesen, "Laufbahn", Aktion.Lesen, "Laufbahn lesen"),
        new(LaufbahnBearbeiten, "Laufbahn", Aktion.Bearbeiten, "Beförderungen, Ehrungen, Befreiungen eintragen"),
        new(LaufbahnRegeln, "Laufbahn", Aktion.Sonderrecht, "Regeln und Ehrungsarten pflegen"),
        new(LaufbahnBefreiungsgrund, "Laufbahn", Aktion.Sonderrecht, "Grund einer Befreiung sehen", Gesundheitsdaten: true),
        new(ErinnerungLaufbahn, "Laufbahn", Aktion.Erinnerung, "Erinnerung Übertritte, Ehrungen, Beförderungen"),
        new(ErinnerungJugend, "Laufbahn", Aktion.Erinnerung, "Erinnerung Jugendfeuerwehr"),
        new(AusbildungLesen, "Ausbildung", Aktion.Lesen, "Lehrgänge und Fristen lesen"),
        new(AusbildungBearbeiten, "Ausbildung", Aktion.Bearbeiten, "Lehrgänge und Nachweise eintragen"),
        new(AusbildungNachweise, "Ausbildung", Aktion.Sonderrecht, "Nur wiederkehrende Nachweise eintragen"),
        new(AusbildungKataloge, "Ausbildung", Aktion.Sonderrecht, "Kataloge und Fristen pflegen"),
        new(ChronikLesen, "Chronik", Aktion.Lesen, "Chronik der Feuerwehr lesen"),
        new(ChronikBearbeiten, "Chronik", Aktion.Bearbeiten, "Chronik führen und Einträge zur Veröffentlichung freigeben"),
        new(Jubilaeen, "Chronik", Aktion.Sonderrecht, "Jubiläumsvorschau mit runden Geburtstagen"),
        new(Dienstpflicht, "Mitglieder", Aktion.Sonderrecht, "Dienstpflicht-Vorgänge und Fachberater in der Akte"),
        new(Serienbriefe, "Vorlagen", Aktion.Sonderrecht, "Serienbriefe und Serienmails an Mitglieder oder Eltern"),
        new(VorlagenBearbeiten, "Vorlagen", Aktion.Bearbeiten, "Briefvorlagen anlegen und ändern"),
        new(Lehrgangsmeldung, "Ausbildung", Aktion.Sonderrecht, "Lehrgangswünsche entscheiden und an den Kreisbrandmeister melden"),
        new(ErinnerungPersonal, "Ausbildung", Aktion.Erinnerung, "Fristerinnerung Lehrgänge und Nachweise"),
        new(AtemschutzLesen, "Atemschutz", Aktion.Lesen, "Atemschutz lesen (Tauglichkeit, Fristen)", Gesundheitsdaten: true),
        new(AtemschutzStatusLesen, "Atemschutz", Aktion.Lesen, "Atemschutz-Einsatzstatus und Fristen sehen (ohne Vorsorgen)", Gesundheitsdaten: true),
        new(AtemschutzBearbeiten, "Atemschutz", Aktion.Bearbeiten, "Atemschutz eintragen", Gesundheitsdaten: true),
        new(AtemschutzBestaetigenKommando, "Atemschutz", Aktion.Sonderrecht, "Atemschutznachweise bestätigen (Kommando)"),
        new(AtemschutzBestaetigenWart, "Atemschutz", Aktion.Sonderrecht, "Atemschutznachweise bestätigen (Atemschutzwart)"),
        new(ErinnerungAtemschutz, "Atemschutz", Aktion.Erinnerung, "Fristerinnerung Atemschutz", Gesundheitsdaten: true),
        new(AnwesenheitLesen, "Dienste", Aktion.Lesen, "Anwesenheit und Rückmeldungen lesen", Abteilungsbezug: true),
        new(DiensteBearbeiten, "Dienste", Aktion.Bearbeiten, "Dienste planen, Anwesenheit erfassen", Abteilungsbezug: true),
        new(DiensteEinstellungen, "Dienste", Aktion.Sonderrecht, "Einstellungen des Dienstplans (Rückmeldeschluss, Serien, Kalender)"),
        new(AbwesenheitenLesen, "Dienste", Aktion.Lesen, "Gemeldete Abwesenheiten mit Grund sehen und zur Kenntnis nehmen", Gesundheitsdaten: true),
        new(EinsaetzeLesen, "Einsätze", Aktion.Lesen, "Einsatzberichte lesen"),
        new(EinsaetzeBearbeiten, "Einsätze", Aktion.Bearbeiten, "Einsatzberichte schreiben"),
        new(EinsaetzeFreigeben, "Einsätze", Aktion.Sonderrecht, "Einsatzberichte freigeben"),
        new(EinsaetzeKostensaetze, "Einsätze", Aktion.Sonderrecht, "Kostenersatz-Sätze pflegen"),
        new(EinsaetzeBeteiligte, "Einsätze", Aktion.Sonderrecht, "Beteiligte: Anschrift, Kennzeichen, Versicherung"),
        new(KostenersatzLesen, "Kostenersatz", Aktion.Lesen, "Kostenersatz-Vorgänge mit Schuldnern lesen"),
        new(KostenersatzEmpfehlen, "Kostenersatz", Aktion.Bearbeiten, "Vorgang bearbeiten und Empfehlung der Feuerwehr abgeben"),
        new(KostenersatzEntscheiden, "Kostenersatz", Aktion.Sonderrecht, "Über den Kostenersatz entscheiden (Gemeinde)"),
        new(KostenersatzForderungen, "Kostenersatz", Aktion.Lesen, "Forderungen ohne Schuldnernamen sehen und Zahlungsrückmeldungen einspielen"),
        new(KostenersatzKalkulation, "Kostenersatz", Aktion.Sonderrecht, "Stundensatz nach § 34 Abs. 5 FwG kalkulieren"),
        new(GemeindeVorgaenge, "Gemeinde", Aktion.Bearbeiten, "Vorgänge für die Gemeinde mit Vorlagen-Entwurf vorbereiten"),
        new(GemeindeEntscheidungen, "Gemeinde", Aktion.Sonderrecht, "Vorgänge vorlegen und die Entscheidung der Gemeinde eintragen"),
        new(GemeindeMitgliederbescheide, "Gemeinde", Aktion.Sonderrecht, "Bescheide des Bürgermeisters an Mitglieder mit Anhörung führen"),
        new(Dienstausweise, "Mitglieder", Aktion.Sonderrecht, "Dienstausweise ausstellen, drucken und Rückgabe vermerken"),
        new(FinanzexportEinrichten, "Verwaltung", Aktion.Sonderrecht, "Spaltenzuordnung des Finanzexports an die Kämmerei einrichten"),
        new(KasseBankdaten, "Entschädigung", Aktion.Sonderrecht, "Bankverbindungen sehen und pflegen, Überweisungsdatei"),
        new(KasseLesen, "Kasse", Aktion.Lesen, "Kassenbuch und Wirtschaftsplan lesen", Abteilungsbezug: true),
        new(KasseVorbereiten, "Kasse", Aktion.Anlegen, "Ausgaben vorbereiten", Abteilungsbezug: true),
        new(KasseBuchen, "Kasse", Aktion.Bearbeiten, "Buchen, festschreiben, Wirtschaftsplan aufstellen", Abteilungsbezug: true),
        new(KasseAnordnen, "Kasse", Aktion.Sonderrecht, "Anordnung vermerken (nur Dokumentation)", Abteilungsbezug: true),
        new(KasseEinrichten, "Kasse", Aktion.Sonderrecht, "Kassen, Konten und Gliederung einrichten"),
        new(KassePruefen, "Kasse", Aktion.Sonderrecht, "Kassenprüfung: Beanstandungen erfassen, Prüfung bestätigen"),
        new(UnfaelleLesen, "Unfälle", Aktion.Lesen, "Dienstunfälle lesen", Gesundheitsdaten: true),
        new(UnfaelleAnlegen, "Unfälle", Aktion.Anlegen, "Dienstunfall erfassen", Gesundheitsdaten: true),
        new(UnfaelleBearbeiten, "Unfälle", Aktion.Bearbeiten, "Dienstunfälle bearbeiten und an die Unfallkasse melden", Gesundheitsdaten: true),
        new(SachschaedenLesen, "Unfälle", Aktion.Lesen, "Sachschäden an privatem Eigentum lesen"),
        new(SachschaedenBearbeiten, "Unfälle", Aktion.Bearbeiten, "Sachschäden weiterleiten und Entscheidung der Gemeinde eintragen"),
        new(TechnikLesen, "Technik", Aktion.Lesen, "Fahrzeuge und Geräte lesen"),
        new(MaengelMelden, "Technik", Aktion.Anlegen, "Mängel melden"),
        new(TechnikBearbeiten, "Technik", Aktion.Bearbeiten, "Fahrzeuge, Geräte, Gerätearten pflegen"),
        new(TechnikPruefen, "Technik", Aktion.Sonderrecht, "Prüfungen eintragen"),
        new(BeschaffungLesen, "Beschaffung", Aktion.Lesen, "Beschaffungen lesen"),
        new(BeschaffungBearbeiten, "Beschaffung", Aktion.Bearbeiten, "Beschaffungen anlegen und bearbeiten"),
        new(BeschaffungFreigeben, "Beschaffung", Aktion.Sonderrecht, "Beschaffungen freigeben"),
        new(KleiderkammerLesen, "Kleiderkammer", Aktion.Lesen, "Kleiderkammer lesen"),
        new(KleiderkammerBearbeiten, "Kleiderkammer", Aktion.Bearbeiten, "Ausgaben und Artikel pflegen"),
        new(DokumenteLesen, "Dokumente", Aktion.Lesen, "Dokumente lesen"),
        new(DokumenteVertraulichLesen, "Dokumente", Aktion.Sonderrecht, "Vertrauliche Dokumente lesen"),
        new(DokumenteVerwalten, "Dokumente", Aktion.Bearbeiten, "Dokumente ablegen und ändern"),
        new(DokumenteEndgueltigLoeschen, "Dokumente", Aktion.Loeschen, "Dokumente endgültig löschen"),
        new(GremienLesen, "Ausschuss", Aktion.Lesen, "Sitzungen und Wahlen lesen"),
        new(GremienBearbeiten, "Ausschuss", Aktion.Bearbeiten, "Sitzungen und Wahlen pflegen"),
        new(WahlenUebernehmen, "Ausschuss", Aktion.Sonderrecht, "Wahlergebnis in Funktionen übernehmen"),
        new(VersammlungLesen, "Ausschuss", Aktion.Lesen, "Hauptversammlung: Niederschriften und freigegebene Kassenberichte lesen"),
        new(EntschaedigungLesen, "Entschädigung", Aktion.Lesen, "Abrechnungen lesen"),
        new(EntschaedigungAbrechnen, "Entschädigung", Aktion.Bearbeiten, "Abrechnung vorbereiten"),
        new(EntschaedigungAbschliessen, "Entschädigung", Aktion.Sonderrecht, "Abrechnung abschließen"),
        new(EntschaedigungSaetze, "Entschädigung", Aktion.Sonderrecht, "Sätze pflegen"),
        new(StatistikLesen, "Auswertungen", Aktion.Lesen, "Jahresstatistik und Jahresbericht"),
        new(KennzahlenLesen, "Auswertungen", Aktion.Lesen, "Führungskennzahlen mit Beteiligung je Person und Abgangsprognose"),
        new(StammdatenOrganisation, "Stammdaten", Aktion.Bearbeiten, "Abteilungen und Funktionen pflegen"),
        new(Verwaltung, "Verwaltung", Aktion.Sonderrecht, "Systemverwaltung"),
        new(BenutzerVerwalten, "Verwaltung", Aktion.Sonderrecht, "Benutzer und Rollen verwalten"),
        new(DatenschutzLoeschlauf, "Verwaltung", Aktion.Sonderrecht, "Löschkonzept und Löschlauf"),
        new(RechteFreigeben, "Verwaltung", Aktion.Sonderrecht, "Rechte auf Gesundheits- und Jugenddaten freigeben (zweite Person)"),
        new(DatenschutzLeseprotokoll, "Verwaltung", Aktion.Lesen, "Leseprotokoll der Gesundheitsdaten einsehen"),
        new(DatenImportieren, "Verwaltung", Aktion.Sonderrecht, "Altdaten importieren"),
    ];

    /// <summary>Policies, die aus einem Recht folgen: (Recht, nur Jugend- und Kinderfeuerwehr zählt).</summary>
    public static readonly IReadOnlyDictionary<string, (string Recht, bool NurJugend)> Abgeleitet = new Dictionary<string, (string, bool)>
    {
        [MitgliederSehen] = (MitgliederLesen, false),
        [JugendLesen] = (MitgliederLesen, true),
        [JugendBearbeiten] = (MitgliederBearbeiten, true),
        [AnwesenheitSehen] = (AnwesenheitLesen, false),
        [AntraegeSehen] = (AntraegeBearbeiten, false),
        [KasseSehen] = (KasseLesen, false),
        [JugendNotfalllisteSehen] = (JugendNotfallliste, true),
        [JugendBetreuerSehen] = (JugendBetreuer, true),
        [JugendVeranstaltungenSehen] = (DiensteBearbeiten, true),
    };

    /// <summary>Rechte mit Abteilungsbezug, die Jugend- und Kinderfeuerwehr nur mit <see cref="JugendZugang"/> umfassen.</summary>
    public static readonly IReadOnlySet<string> MitJugendbezug = new HashSet<string>
    {
        MitgliederLesen, MitgliederAnlegen, MitgliederBearbeiten, AnwesenheitLesen, DiensteBearbeiten, AntraegeBearbeiten, JugendNotfallliste, JugendBetreuer,
    };

    /// <summary>Rechte auf Daten der Jugend- und Kinderfeuerwehr, die ohne Abteilungsangabe schon Jugenddaten öffnen.</summary>
    public static readonly IReadOnlySet<string> Jugenddaten = new HashSet<string> { JugendZugang, JugendNotfallliste, JugendBetreuer };

    /// <summary>
    /// Braucht die Vergabe dieses Rechts eine zweite Freigabe (F18)? Ja bei Rechten auf Gesundheitsdaten, auf Jugenddaten, bei Rechten mit
    /// Bezug zu Mitgliedern ausdrücklich für eine Jugend- oder Kinderabteilung und beim Freigaberecht selbst.
    /// </summary>
    /// <param name="recht">Name des Rechts.</param>
    /// <param name="abteilungen">Abteilungen, auf die das Recht beschränkt ist; leer heißt alle.</param>
    /// <param name="jugendabteilungen">Schlüssel der Jugend- und Kinderabteilungen.</param>
    /// <returns>true, wenn die Vergabe erst nach der zweiten Freigabe wirkt.</returns>
    public static bool Schutzbeduerftig(string recht, IEnumerable<Guid> abteilungen, IReadOnlySet<Guid> jugendabteilungen) =>
        Katalog.Any(r => r.Name == recht && r.Gesundheitsdaten)
        || Jugenddaten.Contains(recht)
        || recht == RechteFreigeben
        || (MitJugendbezug.Contains(recht) && abteilungen.Any(jugendabteilungen.Contains));

    /// <summary>
    /// Erweitert diese Änderung ein Recht so, dass sie eine zweite Freigabe braucht? Neu vergeben und schutzbedürftig, oder die Abteilungen
    /// kommen um eine Jugend- oder Kinderabteilung dazu.
    /// </summary>
    /// <param name="recht">Name des Rechts.</param>
    /// <param name="bisher">Bisherige Abteilungen; null, wenn die Rolle das Recht noch nicht hatte.</param>
    /// <param name="neu">Gewünschte Abteilungen; leer heißt alle.</param>
    /// <param name="jugendabteilungen">Schlüssel der Jugend- und Kinderabteilungen.</param>
    /// <returns>true, wenn die Änderung erst nach der zweiten Freigabe wirken darf.</returns>
    public static bool BrauchtFreigabe(string recht, IReadOnlyCollection<Guid>? bisher, IReadOnlyCollection<Guid> neu, IReadOnlySet<Guid> jugendabteilungen) =>
        bisher is null
            ? Schutzbeduerftig(recht, neu, jugendabteilungen)
            : MitJugendbezug.Contains(recht) && !Jugenddaten.Contains(recht) && neu.Any(a => jugendabteilungen.Contains(a) && !bisher.Contains(a));

    /// <summary>Rechte, die der Administrator immer behält, damit sich niemand aussperrt.</summary>
    public static readonly string[] Unentziehbar = [Verwaltung, BenutzerVerwalten];

    /// <summary>Vorbelegung mit Abteilungsbezug: (Rolle, Recht, Codes der Abteilungen; Unterabteilungen zählen mit).</summary>
    public static readonly IReadOnlyList<(string Rolle, string Recht, string[] Abteilungen)> VorbelegungAbteilungen =
    [
        (Jugendwart, MitgliederLesen, ["JF"]),
        (Jugendwart, MitgliederAnlegen, ["JF"]),
        (Jugendwart, MitgliederBearbeiten, ["JF"]),
        (Jugendwart, AnwesenheitLesen, ["JF"]),
        (Jugendwart, DiensteBearbeiten, ["JF"]),
        (Jugendwart, AntraegeBearbeiten, ["JF"]),
        // Kasse der Jugendfeuerwehr: der Jugendwart liest und bereitet Ausgaben vor, der Kassenwart bucht (Andi, 25.10.2026).
        (Jugendwart, KasseLesen, ["JF"]),
        (Jugendwart, KasseVorbereiten, ["JF"]),
        (Jugendwart, JugendNotfallliste, ["JF"]),
        (Jugendwart, JugendBetreuer, ["JF"]),
        // Betreuer der Jugendfeuerwehr: Anwesenheit und Notfallliste für Dienste und Fahrten, sonst nichts (V46).
        (BetreuerJugend, AnwesenheitLesen, ["JF"]),
        (BetreuerJugend, JugendNotfallliste, ["JF"]),
    ];

    /// <summary>Vorbelegung ohne Abteilungsbezug (gilt für alle Abteilungen).</summary>
    private static readonly Dictionary<string, string[]> Zuordnung = new()
    {
        // Der Jugendwart bekommt die Mitgliederrechte nur für die Jugendfeuerwehr (VorbelegungAbteilungen).
        [MitgliederLesen] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart, Kassier],
        [MitgliederAnlegen] = [Administrator, Kommandant, Stellvertreter],
        [MitgliederBearbeiten] = [Administrator, Kommandant, Stellvertreter],
        [MitgliederArbeitgeber] = [Kommandant, Stellvertreter, Fuehrungskraft],
        // Der Jugendwart bekommt Anträge nur für die Jugendfeuerwehr (VorbelegungAbteilungen).
        [AntraegeBearbeiten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        // Nacherfassung von Altdaten: ein Beginn vor heute braucht dieses Recht zusätzlich (Andi, 2026-10-25).
        [AntraegeNacherfassen] = [Kommandant, Stellvertreter, Schriftfuehrer, Jugendwart],
        // Daten der Jugendfeuerwehr sehen nur Kommandant, Stellvertreter und der Jugendwart (über seine Rechte für die JF).
        [JugendZugang] = [Kommandant, Stellvertreter],
        // Kommandant und Stellvertreter für alle Abteilungen; Jugendwart und Betreuer nur für die Jugendfeuerwehr (VorbelegungAbteilungen).
        [JugendNotfallliste] = [Kommandant, Stellvertreter],
        // Ergebnis der Führungszeugnisse nur Kommandant und Jugendwart (V47, E10).
        [JugendBetreuer] = [Kommandant],
        [LaufbahnLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [LaufbahnBearbeiten] = [Kommandant, Stellvertreter],
        [LaufbahnRegeln] = [Administrator, Kommandant, Stellvertreter],
        [LaufbahnBefreiungsgrund] = [Kommandant, Stellvertreter],
        [ErinnerungLaufbahn] = [Kommandant, Stellvertreter],
        [ErinnerungJugend] = [Kommandant, Stellvertreter, Jugendwart],
        [AnwesenheitLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [AtemschutzLesen] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AtemschutzStatusLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Atemschutzgeraetewart],
        [AtemschutzBearbeiten] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        // Jeder Nachweis braucht beide Bestätigungen; dieselbe Person füllt nie beide Felder.
        [AtemschutzBestaetigenKommando] = [Kommandant, Stellvertreter],
        [AtemschutzBestaetigenWart] = [Atemschutzgeraetewart],
        [AusbildungLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart],
        [AusbildungBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        // Gerätewarte sehen von sich aus keine Personaldaten (Andi, 2026-10-25). Soll der Gerätewart Führerscheine prüfen und
        // unterweisen, bekommt er Ausbildung.Lesen und Ausbildung.Nachweise in der Rechtematrix.
        [AusbildungNachweise] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [AusbildungKataloge] = [Administrator, Kommandant, Stellvertreter],
        [Lehrgangsmeldung] = [Kommandant, Stellvertreter],
        // Die Chronik ist intern, aber für alle Angehörigen lesbar (F25); Geburtstage sehen nur Kommandant und Schriftführung.
        [ChronikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Jugendwart, Kassier, Schriftfuehrer, Mitglied],
        [ChronikBearbeiten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        [Jubilaeen] = [Kommandant, Schriftfuehrer],
        // Der Jugendwart schreibt Elternbriefe; er sieht über den Datenbereich nur die Jugend- und Kinderfeuerwehr.
        // Ordnungsmaßnahmen sind Sache des Kommandanten (Satzung § 5); sonst sieht sie niemand.
        [Dienstpflicht] = [Kommandant, Stellvertreter],
        [Serienbriefe] = [Kommandant, Stellvertreter, Schriftfuehrer, Jugendwart],
        [VorlagenBearbeiten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        [StammdatenOrganisation] = [Administrator, Kommandant, Stellvertreter],
        [ErinnerungPersonal] = [Kommandant, Stellvertreter],
        [ErinnerungAtemschutz] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [DiensteBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [DiensteEinstellungen] = [Administrator, Kommandant, Stellvertreter],
        [AbwesenheitenLesen] = [Kommandant, Stellvertreter],
        [EinsaetzeLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, GemeindeLesen, Hauptamt],
        [EinsaetzeBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [EinsaetzeFreigeben] = [Kommandant, Stellvertreter],
        // Sätze aus der Kostenersatzsatzung; der Kassier kann es in der Rechtematrix zusätzlich bekommen (braucht dann auch Einsätze lesen).
        [EinsaetzeKostensaetze] = [Kommandant, Stellvertreter],
        // Löschkonzept/VVT: die Führungskraft braucht Versicherungsdaten für den Bericht nicht; die Gemeinde für den Kostenersatzbescheid schon.
        [EinsaetzeBeteiligte] = [Kommandant, Stellvertreter, GemeindeLesen, Hauptamt],
        // Die Feuerwehr empfiehlt, das Hauptamt entscheidet und erlässt den Bescheid (V21).
        [KostenersatzLesen] = [Kommandant, Stellvertreter, GemeindeLesen, Hauptamt],
        [KostenersatzEmpfehlen] = [Kommandant, Stellvertreter],
        [KostenersatzEntscheiden] = [Hauptamt],
        // Das Sachgebiet Abgaben sieht nur den Zahlungsstatus (V26).
        [KostenersatzForderungen] = [Kommandant, Hauptamt, Kaemmerei, Abgaben],
        [FinanzexportEinrichten] = [Administrator, Kaemmerei],
        [KostenersatzKalkulation] = [Kommandant, Stellvertreter, Hauptamt, Kaemmerei],
        // V27: die Feuerwehr bereitet vor, das Hauptamt legt vor und trägt die Entscheidung ein; Bescheide und Ausweise stellt der Bürgermeister aus.
        [GemeindeVorgaenge] = [Kommandant, Stellvertreter, Hauptamt],
        [GemeindeEntscheidungen] = [Hauptamt],
        [GemeindeMitgliederbescheide] = [Kommandant, Hauptamt],
        [Dienstausweise] = [Kommandant, Stellvertreter, Hauptamt],
        // Unfallanzeige mit Gesundheitsdaten: lesen und melden nur Kommandant und Stellvertreter; Führungskräfte erfassen an der Einsatzstelle.
        [UnfaelleLesen] = [Kommandant, Stellvertreter],
        [UnfaelleAnlegen] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [UnfaelleBearbeiten] = [Kommandant, Stellvertreter],
        [SachschaedenLesen] = [Kommandant, Stellvertreter, GemeindeLesen],
        [SachschaedenBearbeiten] = [Kommandant, Stellvertreter],
        // Inventar für die Anlagenbuchhaltung; Technik enthält keine Personaldaten.
        [TechnikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Hauptamt, Kaemmerei],
        [TechnikPruefen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        // Dienstanweisungen und Satzung lesen alle; Ausschussprotokolle u. Ä. nur die Führung.
        [DokumenteLesen] = Rollen.Alle.Except([Erziehungsberechtigt, BetreuerJugend]).ToArray(),
        [DokumenteVertraulichLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [DokumenteVerwalten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        [DokumenteEndgueltigLoeschen] = [Kommandant, Stellvertreter],
        [GremienLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Kassier],
        [GremienBearbeiten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        // Das Wahlergebnis ändert Funktionen und Amtszeiten der Mitglieder.
        [WahlenUebernehmen] = [Kommandant, Stellvertreter],
        // Die Niederschrift der Hauptversammlung dürfen alle Mitglieder lesen (Andi, 25.10.2026), Eltern nicht.
        [VersammlungLesen] = Rollen.Alle.Except([Erziehungsberechtigt, BetreuerJugend]).ToArray(),
        // Entschädigung zahlt die Gemeinde: Kassier bereitet vor, Kommandant schließt ab, die Gemeinde liest und exportiert.
        // Hauptamt und Kämmerei sehen Personen und Summen (Andi, 25.10.2026), nie Gesundheits- oder Jugenddaten.
        [EntschaedigungLesen] = [Kommandant, Stellvertreter, Kassier, GemeindeLesen, Hauptamt, Kaemmerei],
        [EntschaedigungAbrechnen] = [Kommandant, Stellvertreter, Kassier],
        [EntschaedigungAbschliessen] = [Kommandant, Stellvertreter],
        [EntschaedigungSaetze] = [Kommandant, Stellvertreter],
        // Bankdaten nur Kassier und Kommandant (Datenschutz-Unterlagen, offene DSB-Frage); nicht die Gemeinde, nicht der Stellvertreter.
        [KasseBankdaten] = [Kommandant, Kassier],
        // Der Kassenwart führt beide Kassen und verfügt allein; das Hauptamt liest Personen und Summen (Andi, 25.10.2026).
        // Die Rechnungsprüfer lesen alles für ihre Amtszeit; die Rolle ergibt sich aus der Funktion, nicht aus der Benutzerverwaltung.
        [KasseLesen] = [Kommandant, Stellvertreter, Kassier, GemeindeLesen, Hauptamt, Kaemmerei, Kassenpruefer],
        [KasseVorbereiten] = [Kommandant, Stellvertreter, Kassier],
        [KasseBuchen] = [Kassier],
        [KasseAnordnen] = [Kommandant, Stellvertreter],
        [KasseEinrichten] = [Kommandant, Kassier],
        [KassePruefen] = [Kassenpruefer],
        [KennzahlenLesen] = [Kommandant, Stellvertreter],
        [StatistikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, GemeindeLesen, Hauptamt, Kaemmerei],
        [TechnikBearbeiten] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        // Jedes aktive Mitglied darf einen Mangel melden, z. B. per QR-Code am Gerät.
        [MaengelMelden] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Jugendwart, Kassier, Schriftfuehrer, Mitglied],
        [BeschaffungLesen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Kassier, GemeindeLesen, Hauptamt, Kaemmerei],
        [BeschaffungBearbeiten] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart],
        [BeschaffungFreigeben] = [Kommandant, Stellvertreter],
        [KleiderkammerLesen] = [Kommandant, Stellvertreter, Kleiderwart, Geraetewart],
        [KleiderkammerBearbeiten] = [Kommandant, Stellvertreter, Kleiderwart],
        [Verwaltung] = [Administrator],
        [DatenschutzLoeschlauf] = [Administrator, Kommandant, Stellvertreter],
        [BenutzerVerwalten] = [Administrator],
        [RechteFreigeben] = [Kommandant, Stellvertreter],
        [DatenschutzLeseprotokoll] = [Administrator, Kommandant],
        [DatenImportieren] = [Administrator, Kommandant],
    };

    /// <summary>Vorbelegung: Recht und die Rollen, die es beim ersten Start bekommen.</summary>
    public static IReadOnlyDictionary<string, string[]> Vorbelegung => Zuordnung;

    /// <summary>Jede Policy prüft gegen die Rechte in der Datenbank (<see cref="Rechteverzeichnis"/>).</summary>
    public static void Registrieren(AuthorizationOptions optionen)
    {
        foreach (var recht in Katalog)
        {
            optionen.AddPolicy(recht.Name, p => p.AddRequirements(new RechtAnforderung(recht.Name, Abgeleitet: false, NurJugend: false)));
        }

        foreach (var (policy, (recht, nurJugend)) in Abgeleitet)
        {
            optionen.AddPolicy(policy, p => p.AddRequirements(new RechtAnforderung(recht, Abgeleitet: true, nurJugend)));
        }
    }
}
