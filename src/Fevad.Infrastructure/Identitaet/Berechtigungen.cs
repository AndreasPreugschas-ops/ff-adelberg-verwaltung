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
    public const string MitgliederLesen = "Mitglieder.Lesen";

    /// <summary>Neue Mitglieder anlegen; mit Abteilungsbezug nur in diesen Abteilungen.</summary>
    public const string MitgliederAnlegen = "Mitglieder.Anlegen";
    public const string MitgliederBearbeiten = "Mitglieder.Bearbeiten";

    /// <summary>Abgeleitet: Zugang zur Mitgliederliste, wer Mitglieder.Lesen für irgendeine Abteilung hat. Was er sieht, regelt der Datenbereich.</summary>
    public const string MitgliederSehen = "Mitglieder.Sehen";

    /// <summary>Beruf und Arbeitgeber mit Kontaktdaten sehen und pflegen (Tagesverfügbarkeit); bearbeiten nur zusammen mit Mitglieder.Bearbeiten.</summary>
    public const string MitgliederArbeitgeber = "Mitglieder.Arbeitgeber";

    /// <summary>Aufnahme- und Übertrittsanträge ausfüllen, drucken und entscheiden; mit Abteilungsbezug nur für diese Zielabteilungen.</summary>
    public const string AntraegeBearbeiten = "Antraege.Bearbeiten";

    /// <summary>Abgeleitet: Zugang zu den Anträgen, wer Antraege.Bearbeiten für irgendeine Abteilung hat.</summary>
    public const string AntraegeSehen = "Antraege.Sehen";

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
    public const string LaufbahnLesen = "Laufbahn.Lesen";
    public const string LaufbahnBearbeiten = "Laufbahn.Bearbeiten";
    public const string LaufbahnRegeln = "Laufbahn.Regeln";

    /// <summary>Grund einer Befreiung sehen (Krankheit, Mutterschutz sind Gesundheitsdaten); alle anderen sehen nur „befreit“.</summary>
    public const string LaufbahnBefreiungsgrund = "Laufbahn.BefreiungsgrundLesen";

    /// <summary>Empfänger der Erinnerung an Übertritte, Ehrungen und Beförderungen (keine Seitenrechte).</summary>
    public const string ErinnerungLaufbahn = "Fristen.ErinnerungLaufbahn";
    public const string ErinnerungJugend = "Fristen.ErinnerungJugend";
    public const string AtemschutzLesen = "Atemschutz.Lesen";
    public const string AtemschutzBearbeiten = "Atemschutz.Bearbeiten";
    public const string AusbildungLesen = "Ausbildung.Lesen";
    public const string AusbildungBearbeiten = "Ausbildung.Bearbeiten";

    /// <summary>Wiederkehrende Nachweise ohne Gesundheitsbezug eintragen, z. B. Führerscheinkontrolle oder UVV-Unterweisung.</summary>
    public const string AusbildungNachweise = "Ausbildung.Nachweise";
    public const string AusbildungKataloge = "Ausbildung.Kataloge";

    /// <summary>Abteilungen (Art, Unterabteilung, aktiv) und Funktionen der Feuerwehr pflegen.</summary>
    public const string StammdatenOrganisation = "Stammdaten.Organisation";

    /// <summary>Empfänger der wöchentlichen Fristerinnerung (keine Seitenrechte).</summary>
    public const string ErinnerungPersonal = "Fristen.ErinnerungPersonal";
    public const string ErinnerungAtemschutz = "Fristen.ErinnerungAtemschutz";
    public const string AnwesenheitLesen = "Dienste.AnwesenheitLesen";

    /// <summary>Abgeleitet: Anwesenheitslisten aufrufen, wer Dienste.AnwesenheitLesen für irgendeine Abteilung hat (prüft Datenbereich).</summary>
    public const string AnwesenheitSehen = "Dienste.AnwesenheitSehen";
    public const string DiensteBearbeiten = "Dienste.Bearbeiten";

    /// <summary>Einstellungen des Dienstplans, z. B. bis wann vor Beginn Mitglieder selbst zu- oder absagen können.</summary>
    public const string DiensteEinstellungen = "Dienste.Einstellungen";
    public const string EinsaetzeLesen = "Einsaetze.Lesen";
    public const string EinsaetzeBearbeiten = "Einsaetze.Bearbeiten";
    public const string EinsaetzeFreigeben = "Einsaetze.Freigeben";

    /// <summary>Kostenersatz-Sätze (Fahrzeuge, Personal, Ausrüstung) nach der Kostenersatzsatzung pflegen.</summary>
    public const string EinsaetzeKostensaetze = "Einsaetze.Kostensaetze";

    /// <summary>Anschrift, Telefon, Kennzeichen und Versicherung der Beteiligten sehen und eintragen; ohne nur Rolle und Name.</summary>
    public const string EinsaetzeBeteiligte = "Einsaetze.Beteiligte";
    public const string TechnikLesen = "Technik.Lesen";
    public const string TechnikPruefen = "Technik.Pruefen";
    public const string DokumenteLesen = "Dokumente.Lesen";
    public const string DokumenteVertraulichLesen = "Dokumente.VertraulichLesen";
    public const string DokumenteVerwalten = "Dokumente.Verwalten";

    /// <summary>Dokumente samt allen Versionen und Dateien endgültig löschen oder alte Versionen entfernen.</summary>
    public const string DokumenteEndgueltigLoeschen = "Dokumente.EndgueltigLoeschen";
    public const string GremienLesen = "Gremien.Lesen";
    public const string GremienBearbeiten = "Gremien.Bearbeiten";
    public const string WahlenUebernehmen = "Gremien.WahlenUebernehmen";
    public const string EntschaedigungLesen = "Entschaedigung.Lesen";
    public const string EntschaedigungAbrechnen = "Entschaedigung.Abrechnen";
    public const string EntschaedigungAbschliessen = "Entschaedigung.Abschliessen";
    public const string EntschaedigungSaetze = "Entschaedigung.Saetze";
    public const string StatistikLesen = "Statistik.Lesen";
    public const string TechnikBearbeiten = "Technik.Bearbeiten";
    public const string MaengelMelden = "Technik.MaengelMelden";
    public const string BeschaffungLesen = "Beschaffung.Lesen";
    public const string BeschaffungBearbeiten = "Beschaffung.Bearbeiten";
    public const string BeschaffungFreigeben = "Beschaffung.Freigeben";
    public const string KleiderkammerLesen = "Kleiderkammer.Lesen";
    public const string KleiderkammerBearbeiten = "Kleiderkammer.Bearbeiten";
    public const string Verwaltung = "Verwaltung";

    /// <summary>Löschnachweise und Fristen des Löschkonzepts einsehen, Löschlauf von Hand anstoßen.</summary>
    public const string DatenschutzLoeschlauf = "Datenschutz.Loeschlauf";

    /// <summary>Benutzerkonten anlegen und Rollen mit ihren Rechten pflegen.</summary>
    public const string BenutzerVerwalten = "Benutzer.Verwalten";

    /// <summary>Was ein Recht im Bereich erlaubt; danach ordnet die Rechtematrix ihre Spalten.</summary>
    public enum Aktion
    {
        [Display(Name = "lesen")] Lesen,
        [Display(Name = "anlegen")] Anlegen,
        [Display(Name = "bearbeiten")] Bearbeiten,
        [Display(Name = "löschen")] Loeschen,
        [Display(Name = "Sonderrecht")] Sonderrecht,
        [Display(Name = "Erinnerung")] Erinnerung,
    }

    /// <param name="Gesundheitsdaten">Schutzschranke: Vergabe nur mit ausdrücklicher Bestätigung (Art. 9 DSGVO).</param>
    /// <param name="Abteilungsbezug">Lässt sich auf einzelne Abteilungen beschränken.</param>
    public sealed record Recht(string Name, string Bereich, Aktion Aktion, string Bezeichnung, bool Gesundheitsdaten = false, bool Abteilungsbezug = false);

    public static readonly IReadOnlyList<Recht> Katalog =
    [
        new(MitgliederLesen, "Mitglieder", Aktion.Lesen, "Mitglieder lesen", Abteilungsbezug: true),
        new(MitgliederAnlegen, "Mitglieder", Aktion.Anlegen, "Mitglieder anlegen", Abteilungsbezug: true),
        new(MitgliederBearbeiten, "Mitglieder", Aktion.Bearbeiten, "Mitglieder bearbeiten, übertreten, austreten", Abteilungsbezug: true),
        new(MitgliederArbeitgeber, "Mitglieder", Aktion.Sonderrecht, "Beruf und Arbeitgeber"),
        new(AntraegeBearbeiten, "Mitglieder", Aktion.Sonderrecht, "Aufnahme- und Übertrittsanträge", Abteilungsbezug: true),
        new(JugendZugang, "Mitglieder", Aktion.Sonderrecht, "Bereich Jugend- und Kinderfeuerwehr betreten"),
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
        new(ErinnerungPersonal, "Ausbildung", Aktion.Erinnerung, "Fristerinnerung Lehrgänge und Nachweise"),
        new(AtemschutzLesen, "Atemschutz", Aktion.Lesen, "Atemschutz lesen (Tauglichkeit, Fristen)", Gesundheitsdaten: true),
        new(AtemschutzBearbeiten, "Atemschutz", Aktion.Bearbeiten, "Atemschutz eintragen", Gesundheitsdaten: true),
        new(ErinnerungAtemschutz, "Atemschutz", Aktion.Erinnerung, "Fristerinnerung Atemschutz", Gesundheitsdaten: true),
        new(AnwesenheitLesen, "Dienste", Aktion.Lesen, "Anwesenheit und Rückmeldungen lesen", Abteilungsbezug: true),
        new(DiensteBearbeiten, "Dienste", Aktion.Bearbeiten, "Dienste planen, Anwesenheit erfassen", Abteilungsbezug: true),
        new(DiensteEinstellungen, "Dienste", Aktion.Sonderrecht, "Einstellungen des Dienstplans (Rückmeldeschluss, Serien, Kalender)"),
        new(EinsaetzeLesen, "Einsätze", Aktion.Lesen, "Einsatzberichte lesen"),
        new(EinsaetzeBearbeiten, "Einsätze", Aktion.Bearbeiten, "Einsatzberichte schreiben"),
        new(EinsaetzeFreigeben, "Einsätze", Aktion.Sonderrecht, "Einsatzberichte freigeben"),
        new(EinsaetzeKostensaetze, "Einsätze", Aktion.Sonderrecht, "Kostenersatz-Sätze pflegen"),
        new(EinsaetzeBeteiligte, "Einsätze", Aktion.Sonderrecht, "Beteiligte: Anschrift, Kennzeichen, Versicherung"),
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
        new(EntschaedigungLesen, "Entschädigung", Aktion.Lesen, "Abrechnungen lesen"),
        new(EntschaedigungAbrechnen, "Entschädigung", Aktion.Bearbeiten, "Abrechnung vorbereiten"),
        new(EntschaedigungAbschliessen, "Entschädigung", Aktion.Sonderrecht, "Abrechnung abschließen"),
        new(EntschaedigungSaetze, "Entschädigung", Aktion.Sonderrecht, "Sätze pflegen"),
        new(StatistikLesen, "Auswertungen", Aktion.Lesen, "Jahresstatistik und Jahresbericht"),
        new(StammdatenOrganisation, "Stammdaten", Aktion.Bearbeiten, "Abteilungen und Funktionen pflegen"),
        new(Verwaltung, "Verwaltung", Aktion.Sonderrecht, "Systemverwaltung"),
        new(BenutzerVerwalten, "Verwaltung", Aktion.Sonderrecht, "Benutzer und Rollen verwalten"),
        new(DatenschutzLoeschlauf, "Verwaltung", Aktion.Sonderrecht, "Löschkonzept und Löschlauf"),
    ];

    /// <summary>Policies, die aus einem Recht folgen: (Recht, nur Jugend- und Kinderfeuerwehr zählt).</summary>
    public static readonly IReadOnlyDictionary<string, (string Recht, bool NurJugend)> Abgeleitet = new Dictionary<string, (string, bool)>
    {
        [MitgliederSehen] = (MitgliederLesen, false),
        [JugendLesen] = (MitgliederLesen, true),
        [JugendBearbeiten] = (MitgliederBearbeiten, true),
        [AnwesenheitSehen] = (AnwesenheitLesen, false),
        [AntraegeSehen] = (AntraegeBearbeiten, false),
    };

    /// <summary>Rechte mit Abteilungsbezug, die Jugend- und Kinderfeuerwehr nur mit <see cref="JugendZugang"/> umfassen.</summary>
    public static readonly IReadOnlySet<string> MitJugendbezug = new HashSet<string>
    {
        MitgliederLesen, MitgliederAnlegen, MitgliederBearbeiten, AnwesenheitLesen, DiensteBearbeiten, AntraegeBearbeiten,
    };

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
        // Daten der Jugendfeuerwehr sehen nur Kommandant, Stellvertreter und der Jugendwart (über seine Rechte für die JF).
        [JugendZugang] = [Kommandant, Stellvertreter],
        [LaufbahnLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [LaufbahnBearbeiten] = [Kommandant, Stellvertreter],
        [LaufbahnRegeln] = [Administrator, Kommandant, Stellvertreter],
        [LaufbahnBefreiungsgrund] = [Kommandant, Stellvertreter],
        [ErinnerungLaufbahn] = [Kommandant, Stellvertreter],
        [ErinnerungJugend] = [Kommandant, Stellvertreter, Jugendwart],
        [AnwesenheitLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [AtemschutzLesen] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AtemschutzBearbeiten] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [AusbildungLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Atemschutzgeraetewart, Geraetewart],
        [AusbildungBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        // Der Gerätewart prüft Führerscheine und unterweist, Lehrgänge trägt er nicht ein (Andi, 2026-10-22).
        [AusbildungNachweise] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart],
        [AusbildungKataloge] = [Administrator, Kommandant, Stellvertreter],
        [StammdatenOrganisation] = [Administrator, Kommandant, Stellvertreter],
        [ErinnerungPersonal] = [Kommandant, Stellvertreter],
        [ErinnerungAtemschutz] = [Kommandant, Stellvertreter, Atemschutzgeraetewart],
        [DiensteBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [DiensteEinstellungen] = [Administrator, Kommandant, Stellvertreter],
        [EinsaetzeLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, GemeindeLesen],
        [EinsaetzeBearbeiten] = [Kommandant, Stellvertreter, Fuehrungskraft],
        [EinsaetzeFreigeben] = [Kommandant, Stellvertreter],
        // Sätze aus der Kostenersatzsatzung; der Kassier kann es in der Rechtematrix zusätzlich bekommen (braucht dann auch Einsätze lesen).
        [EinsaetzeKostensaetze] = [Kommandant, Stellvertreter],
        // Löschkonzept/VVT: die Führungskraft braucht Versicherungsdaten für den Bericht nicht; die Gemeinde für den Kostenersatzbescheid schon.
        [EinsaetzeBeteiligte] = [Kommandant, Stellvertreter, GemeindeLesen],
        [TechnikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart],
        [TechnikPruefen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        // Dienstanweisungen und Satzung lesen alle; Ausschussprotokolle u. Ä. nur die Führung.
        [DokumenteLesen] = Rollen.Alle.Except([Erziehungsberechtigt]).ToArray(),
        [DokumenteVertraulichLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer],
        [DokumenteVerwalten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        [DokumenteEndgueltigLoeschen] = [Kommandant, Stellvertreter],
        [GremienLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, Kassier],
        [GremienBearbeiten] = [Kommandant, Stellvertreter, Schriftfuehrer],
        // Das Wahlergebnis ändert Funktionen und Amtszeiten der Mitglieder.
        [WahlenUebernehmen] = [Kommandant, Stellvertreter],
        // Entschädigung zahlt die Gemeinde: Kassier bereitet vor, Kommandant schließt ab, die Gemeinde liest und exportiert.
        [EntschaedigungLesen] = [Kommandant, Stellvertreter, Kassier, GemeindeLesen],
        [EntschaedigungAbrechnen] = [Kommandant, Stellvertreter, Kassier],
        [EntschaedigungAbschliessen] = [Kommandant, Stellvertreter],
        [EntschaedigungSaetze] = [Kommandant, Stellvertreter],
        [StatistikLesen] = [Kommandant, Stellvertreter, Fuehrungskraft, Schriftfuehrer, GemeindeLesen],
        [TechnikBearbeiten] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart],
        // Jedes aktive Mitglied darf einen Mangel melden, z. B. per QR-Code am Gerät.
        [MaengelMelden] = [Administrator, Kommandant, Stellvertreter, Fuehrungskraft, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Jugendwart, Kassier, Schriftfuehrer, Mitglied],
        [BeschaffungLesen] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart, Kassier, GemeindeLesen],
        [BeschaffungBearbeiten] = [Kommandant, Stellvertreter, Geraetewart, Atemschutzgeraetewart, Kleiderwart],
        [BeschaffungFreigeben] = [Kommandant, Stellvertreter],
        [KleiderkammerLesen] = [Kommandant, Stellvertreter, Kleiderwart, Geraetewart],
        [KleiderkammerBearbeiten] = [Kommandant, Stellvertreter, Kleiderwart],
        [Verwaltung] = [Administrator],
        [DatenschutzLoeschlauf] = [Administrator, Kommandant, Stellvertreter],
        [BenutzerVerwalten] = [Administrator],
    };

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
