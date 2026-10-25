using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;

namespace Fevad.Infrastructure.Persistenz.Seed;

/// <summary>
/// Standardwerte für Kataloge und Parameter (BW, Satzung Adelberg 2015/2019).
/// Werden nur angelegt, wenn der Code noch fehlt; Änderungen in der Oberfläche bleiben erhalten.
/// </summary>
public static class Stammdaten
{
    /// <summary>
    /// Fünf Einsatzabteilungen mit neutralem Namen; umbenannt werden sie unter Stammdaten → Abteilungen und Funktionen,
    /// nicht gebrauchte bleiben dort inaktiv.
    /// </summary>
    public static IReadOnlyList<Abteilung> Abteilungen =>
    [
        new() { Code = AbteilungCode.Einsatz, Bezeichnung = "Einsatzabteilung 1", Kurzbezeichnung = "EA 1", Art = AbteilungArt.Einsatz, Sortierung = 10 },
        new() { Code = $"{AbteilungCode.Einsatz}2", Bezeichnung = "Einsatzabteilung 2", Kurzbezeichnung = "EA 2", Art = AbteilungArt.Einsatz, Sortierung = 11 },
        new() { Code = $"{AbteilungCode.Einsatz}3", Bezeichnung = "Einsatzabteilung 3", Kurzbezeichnung = "EA 3", Art = AbteilungArt.Einsatz, Sortierung = 12 },
        new() { Code = $"{AbteilungCode.Einsatz}4", Bezeichnung = "Einsatzabteilung 4", Kurzbezeichnung = "EA 4", Art = AbteilungArt.Einsatz, Sortierung = 13 },
        new() { Code = $"{AbteilungCode.Einsatz}5", Bezeichnung = "Einsatzabteilung 5", Kurzbezeichnung = "EA 5", Art = AbteilungArt.Einsatz, Sortierung = 14 },
        new() { Code = AbteilungCode.Jugend, Bezeichnung = "Jugendfeuerwehr", Art = AbteilungArt.Jugend, Sortierung = 20 },

        // Steht nicht in der Satzung von 2015; erst nach einer Satzungsänderung aktivieren. Hängt an der Jugendfeuerwehr.
        new() { Code = AbteilungCode.Kinder, Bezeichnung = "Kinderfeuerwehr", Art = AbteilungArt.Kinder, Sortierung = 25, Aktiv = false },
        new() { Code = AbteilungCode.Alters, Bezeichnung = "Altersabteilung", Art = AbteilungArt.Alters, Sortierung = 30 },
    ];

    /// <summary>Feuerwehr und Gemeinde; die Anschrift des Rathauses trägt die Verwaltung unter Stammdaten ein.</summary>
    public static Organisation Organisation => new()
    {
        FeuerwehrName = "Freiwillige Feuerwehr Adelberg",
        FeuerwehrStrasse = "Kirchstraße 5",
        FeuerwehrPlz = "73099",
        FeuerwehrOrt = "Adelberg",
        GemeindeName = "Gemeinde Adelberg",
        GemeindeVertretung = "vertreten durch den Bürgermeister",
        GemeindePlz = "73099",
        GemeindeOrt = "Adelberg",

        // Angabe der Kämmerei (Andi 25.10.2026): § 2b UStG gilt für Adelberg ab Januar 2027.
        UmsatzsteuerParagraph2bAb = new(2027, 1, 1),
    };

    public static IReadOnlyList<Dienstgrad> Dienstgrade =>
    [
        new() { Code = "FMP", Bezeichnung = "Feuerwehrmann/-frau in der Probezeit", Kurzbezeichnung = "FM(P)", Sortierung = 10 },
        new() { Code = "FM", Bezeichnung = "Feuerwehrmann/-frau", Kurzbezeichnung = "FM", Sortierung = 20 },
        new() { Code = "OFM", Bezeichnung = "Oberfeuerwehrmann/-frau", Kurzbezeichnung = "OFM", Sortierung = 30 },
        new() { Code = "HFM", Bezeichnung = "Hauptfeuerwehrmann/-frau", Kurzbezeichnung = "HFM", Sortierung = 40 },
        new() { Code = "LM", Bezeichnung = "Löschmeister/in", Kurzbezeichnung = "LM", Sortierung = 50 },
        new() { Code = "OLM", Bezeichnung = "Oberlöschmeister/in", Kurzbezeichnung = "OLM", Sortierung = 60 },
        new() { Code = "HLM", Bezeichnung = "Hauptlöschmeister/in", Kurzbezeichnung = "HLM", Sortierung = 70 },
        new() { Code = "BM", Bezeichnung = "Brandmeister/in", Kurzbezeichnung = "BM", Sortierung = 80 },
        new() { Code = "OBM", Bezeichnung = "Oberbrandmeister/in", Kurzbezeichnung = "OBM", Sortierung = 90 },
        new() { Code = "HBM", Bezeichnung = "Hauptbrandmeister/in", Kurzbezeichnung = "HBM", Sortierung = 100 },
        new() { Code = "LHBM", Bezeichnung = "Leitende/r Hauptbrandmeister/in", Kurzbezeichnung = "LHBM", Sortierung = 110 },
    ];

    /// <summary>
    /// Funktionen nach FwG BW, Feuerwehrsatzung, Dienstvorschriften und UVV. Voraussetzungen sind Vorschläge nach
    /// VwV Feuerwehrausbildung und FwDV 2; in den Stammdaten anpassbar.
    /// </summary>
    public static IReadOnlyList<Funktion> Funktionen =>
    [
        new() { Code = "KDT", Bezeichnung = "Kommandant/in", MitWahl = true, BeschlussDurch = "Gemeinderat", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Kommandant, Grundlage = FwgKommandant, Voraussetzungen = "ZF|VF; LDF", Sortierung = 10 },
        new() { Code = "STV", Bezeichnung = "Stellvertretende/r Kommandant/in", MitWahl = true, BeschlussDurch = "Gemeinderat", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Stellvertreter, Grundlage = FwgKommandant, Voraussetzungen = "ZF|VF; LDF", Sortierung = 20 },
        new() { Code = "ABTKDT", Bezeichnung = "Abteilungskommandant/in", MitWahl = true, BeschlussDurch = "Gemeinderat", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Fuehrungskraft, Grundlage = FwgKommandant, Voraussetzungen = "GF|ZF|VF", Abteilungsart = AbteilungArt.Einsatz, Sortierung = 22 },
        new() { Code = "ABTSTV", Bezeichnung = "Stellvertretende/r Abteilungskommandant/in", MitWahl = true, BeschlussDurch = "Gemeinderat", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Fuehrungskraft, Grundlage = FwgKommandant, Voraussetzungen = "GF|ZF|VF", Abteilungsart = AbteilungArt.Einsatz, Sortierung = 24 },
        new() { Code = "ZF", Bezeichnung = "Zugführer/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Fuehrungskraft, Grundlage = "FwDV 3, Feuerwehrsatzung", Voraussetzungen = "ZF|VF", Abteilungsart = AbteilungArt.Einsatz, Sortierung = 26 },
        new() { Code = "GF", Bezeichnung = "Gruppenführer/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Fuehrungskraft, Grundlage = "FwDV 3, Feuerwehrsatzung", Voraussetzungen = "GF|ZF|VF", Abteilungsart = AbteilungArt.Einsatz, Sortierung = 30 },
        new() { Code = "GW", Bezeichnung = "Gerätewart/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Geraetewart, Grundlage = "DGUV Vorschrift 49, Feuerwehrsatzung", Voraussetzungen = "GW", Sortierung = 40 },
        new() { Code = "AGW", Bezeichnung = "Atemschutzgerätewart/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Atemschutzgeraetewart, Grundlage = "FwDV 7, DGUV Vorschrift 49", Voraussetzungen = "AGW", Sortierung = 50 },
        new() { Code = "SIB", Bezeichnung = "Sicherheitsbeauftragte/r", BeschlussDurch = "Feuerwehrkommandant", Grundlage = "§ 22 SGB VII, DGUV Vorschrift 1 § 20, DGUV Vorschrift 49", Voraussetzungen = "SIB", Sortierung = 52 },
        new() { Code = "EHF", Bezeichnung = "Ersthelfer/in", BeschlussDurch = "Feuerwehrkommandant", Grundlage = "DGUV Vorschrift 1 § 26", Voraussetzungen = "EH", Sortierung = 54 },
        new() { Code = "AUSB", Bezeichnung = "Ausbilder/in", BeschlussDurch = "Feuerwehrkommandant", Grundlage = "FwDV 2, Feuerwehrsatzung", Voraussetzungen = "AUSB", Sortierung = 56 },
        new() { Code = "JW", Bezeichnung = "Jugendfeuerwehrwart/in", MitWahl = true, MitBeschluss = false, AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Jugendwart, Grundlage = "Feuerwehrsatzung (Jugendfeuerwehr), Jugendordnung", Voraussetzungen = "JFW|JGL", Abteilungsart = AbteilungArt.Jugend, Sortierung = 60 },
        new() { Code = "JWSTV", Bezeichnung = "Stellvertretende/r Jugendfeuerwehrwart/in", MitWahl = true, MitBeschluss = false, AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Jugendwart, Grundlage = "Feuerwehrsatzung (Jugendfeuerwehr), Jugendordnung", Voraussetzungen = "JGL|JFW", Abteilungsart = AbteilungArt.Jugend, Sortierung = 62 },
        new() { Code = "KFW", Bezeichnung = "Kinderfeuerwehrwart/in", BeschlussDurch = "Feuerwehrkommandant", Grundlage = "Feuerwehrsatzung (Kinderfeuerwehr)", Voraussetzungen = "JGL|JFW", Abteilungsart = AbteilungArt.Kinder, Sortierung = 64 },
        new() { Code = "AAL", Bezeichnung = "Leiter/in der Altersabteilung", MitWahl = true, BeschlussDurch = "Feuerwehrausschuss", AmtszeitJahre = 5, Grundlage = "Feuerwehrsatzung (Altersabteilung)", Abteilungsart = AbteilungArt.Alters, Sortierung = 66 },
        new() { Code = "KAS", Bezeichnung = "Kassier/in", MitWahl = true, MitBeschluss = false, VorgeschlageneRolle = Rollen.Kassier, Grundlage = "FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung", Sortierung = 70 },
        new() { Code = "SF", Bezeichnung = "Schriftführer/in", MitWahl = true, MitBeschluss = false, VorgeschlageneRolle = Rollen.Schriftfuehrer, Grundlage = "Feuerwehrsatzung", Sortierung = 80 },
        new() { Code = "KW", Bezeichnung = "Kleiderwart/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Kleiderwart, Grundlage = "Feuerwehrsatzung", Sortierung = 90 },
        new() { Code = "KP", Bezeichnung = "Kassenprüfer/in", MitWahl = true, MitBeschluss = false, Grundlage = "FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung", Sortierung = 100 },
        new() { Code = "AUS", Bezeichnung = "Mitglied Feuerwehrausschuss", MitWahl = true, MitBeschluss = false, AmtszeitJahre = 5, Grundlage = "FwG BW, Feuerwehrsatzung (Feuerwehrausschuss)", Sortierung = 110 },
    ];

    private const string FwgKommandant = "FwG BW § 8, Feuerwehrsatzung (Wahl, Bestellung durch den Gemeinderat)";

    public static IReadOnlyList<Qualifikation> Qualifikationen =>
    [
        new() { Code = "TM1", Bezeichnung = "Truppmann Teil 1", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Sortierung = 10 },
        new() { Code = "TM2", Bezeichnung = "Truppmann Teil 2", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TM1", Ebene = AusbildungsEbene.Standort, Sortierung = 20 },
        new() { Code = "SPRF", Bezeichnung = "Sprechfunker", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TM1", Ebene = AusbildungsEbene.Kreis, Sortierung = 30 },
        new() { Code = "AGT", Bezeichnung = "Atemschutzgeräteträger", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TM1", MindestalterJahre = 18, Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "AGT", Sortierung = 40 },
        new() { Code = "MA", Bezeichnung = "Maschinist", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TM1", MindestalterJahre = 18, Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "MA", Sortierung = 50 },
        new() { Code = "TF", Bezeichnung = "Truppführer", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TM2", Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "TF", Sortierung = 60 },
        new() { Code = "GF", Bezeichnung = "Gruppenführer", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TF", Ebene = AusbildungsEbene.Land, Fe2Funktion = "GF", Sortierung = 70 },
        new() { Code = "ZF", Bezeichnung = "Zugführer", Art = QualifikationArt.Lehrgang, Voraussetzungen = "GF", Ebene = AusbildungsEbene.Land, Fe2Funktion = "ZF", Sortierung = 80 },
        new() { Code = "GW", Bezeichnung = "Gerätewart", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Sortierung = 90 },
        new() { Code = "AGW", Bezeichnung = "Atemschutzgerätewart", Art = QualifikationArt.Lehrgang, Voraussetzungen = "AGT", Ebene = AusbildungsEbene.Land, Sortierung = 100 },
        new() { Code = "FS-C", Bezeichnung = "Fahrerlaubnis Klasse C", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Fe2Funktion = "C", Sortierung = 110 },
        new() { Code = "FS-CE", Bezeichnung = "Fahrerlaubnis Klasse CE", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Sortierung = 120 },
        new() { Code = "FS-B", Bezeichnung = "Fahrerlaubnis Klasse B", Art = QualifikationArt.Fahrerlaubnis, Sortierung = 105 },
        new() { Code = "FS-C1", Bezeichnung = "Fahrerlaubnis Klasse C1", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Sortierung = 108 },
        new() { Code = "FW-FS", Bezeichnung = "Feuerwehr-Fahrberechtigung (bis 4,75 t / 7,5 t)", Art = QualifikationArt.Fahrerlaubnis, Sortierung = 125 },
        // Weitere Lehrgänge nach FwDV 2 und dem Lehrgangsangebot in BW (Kreis bzw. Landesfeuerwehrschule). Die
        // Voraussetzungen sind Vorschläge und im Katalog änderbar; die alte VwV Feuerwehrausbildung gilt seit 2018 nicht mehr.
        new() { Code = "TH", Bezeichnung = "Technische Hilfeleistung", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Voraussetzungen = "TM1", Sortierung = 62 },
        new() { Code = "MOTS", Bezeichnung = "Motorsägenführer", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Voraussetzungen = "TM1", MindestalterJahre = 18, Sortierung = 64 },
        new() { Code = "ABC1", Bezeichnung = "ABC-Einsatz", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Voraussetzungen = "TM2; AGT", Sortierung = 66 },
        new() { Code = "VF", Bezeichnung = "Verbandsführer", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Voraussetzungen = "ZF", Sortierung = 82 },
        new() { Code = "LDF", Bezeichnung = "Leiter einer Feuerwehr", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Voraussetzungen = "ZF", Sortierung = 84 },
        new() { Code = "AUSB", Bezeichnung = "Ausbilder in der Feuerwehr", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Voraussetzungen = "GF", Sortierung = 86 },
        new() { Code = "JGL", Bezeichnung = "Jugendgruppenleiter", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Voraussetzungen = "TM1", Sortierung = 94 },
        new() { Code = "JFW", Bezeichnung = "Jugendfeuerwehrwart", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Voraussetzungen = "JGL|GF", Sortierung = 96 },
        new() { Code = "SIB", Bezeichnung = "Sicherheitsbeauftragter (UKBW)", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Sortierung = 98 },
        new() { Code = "EH", Bezeichnung = "Erste-Hilfe-Ausbildung", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Standort, GueltigkeitMonate = 24, Sortierung = 130 },

        // Abzeichen: Richtlinie Leistungsabzeichen BW (Neufassung 2026), DJF-Grundsätze Jugendflamme, JF BW Kinderfunke.
        new() { Code = "LA-B", Bezeichnung = "Leistungsabzeichen Bronze", Kurzbezeichnung = "LA Bronze", Art = QualifikationArt.Abzeichen, Voraussetzungen = "TM1", Sortierung = 200 },
        new() { Code = "LA-S", Bezeichnung = "Leistungsabzeichen Silber", Kurzbezeichnung = "LA Silber", Art = QualifikationArt.Abzeichen, VorstufeAbstandJahre = 1, Voraussetzungen = "TM1; AGT|SPRF", Sortierung = 210 },
        new() { Code = "LA-G", Bezeichnung = "Leistungsabzeichen Gold", Kurzbezeichnung = "LA Gold", Art = QualifikationArt.Abzeichen, VorstufeAbstandJahre = 1, Voraussetzungen = "TF", Sortierung = 220 },
        new() { Code = "GP-MA", Bezeichnung = "Geschicklichkeitsprüfung Maschinisten", Kurzbezeichnung = "Geschick. MA", Art = QualifikationArt.Abzeichen, Voraussetzungen = Qualifikation.CodeMaschinist, Sortierung = 230 },
        new() { Code = "KF1", Bezeichnung = "Kinderfunke Stufe 1", Art = QualifikationArt.Abzeichen, MindestalterJahre = 6, HoechstalterJahre = 8, AlterNachJahrgang = true, Sortierung = 300 },
        new() { Code = "KF2", Bezeichnung = "Kinderfunke Stufe 2", Art = QualifikationArt.Abzeichen, MindestalterJahre = 8, HoechstalterJahre = 10, AlterNachJahrgang = true, Sortierung = 310 },
        new() { Code = Qualifikation.CodeJugendflamme1, Bezeichnung = "Jugendflamme Stufe I", Kurzbezeichnung = "JFL I", Art = QualifikationArt.Abzeichen, MindestalterJahre = 10, AlterNachJahrgang = true, Sortierung = 320 },
        new() { Code = Qualifikation.CodeJugendflamme2, Bezeichnung = "Jugendflamme Stufe II", Kurzbezeichnung = "JFL II", Art = QualifikationArt.Abzeichen, MindestalterJahre = 13, AlterNachJahrgang = true, Sortierung = 330 },
        new() { Code = Qualifikation.CodeJugendflamme3, Bezeichnung = "Jugendflamme Stufe III", Kurzbezeichnung = "JFL III", Art = QualifikationArt.Abzeichen, MindestalterJahre = 15, AlterNachJahrgang = true, Voraussetzungen = "EH", Sortierung = 340 },
        new() { Code = Qualifikation.CodeLeistungsspange, Bezeichnung = "Leistungsspange DJF", Kurzbezeichnung = "Leistungsspange", Art = QualifikationArt.Abzeichen, MindestalterJahre = 15, AlterNachJahrgang = true, MindestJugendMonate = 12, Sortierung = 350 },
    ];

    /// <summary>Stufenfolgen der Abzeichen (Code, Vorstufe); werden nur gesetzt, solange keine Vorstufe eingetragen ist.</summary>
    public static IReadOnlyList<(string Code, string Vorstufe)> Stufenfolgen =>
    [
        ("LA-S", "LA-B"),
        ("LA-G", "LA-S"),
        (Qualifikation.CodeJugendflamme2, Qualifikation.CodeJugendflamme1),
        (Qualifikation.CodeJugendflamme3, Qualifikation.CodeJugendflamme2),
    ];

    public static IReadOnlyList<Nachweisart> Nachweisarten =>
    [
        new() { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", IntervallMonate = 6, Pflicht = NachweisPflicht.MitFahrerlaubnis, Sortierung = 10, Beschreibung = "Sichtkontrolle des Führerscheins durch die Wehrführung (Halterpflicht der Gemeinde)." },
        new() { Code = "UVV", Bezeichnung = "Jährliche Unterweisung (UVV)", IntervallMonate = 12, Pflicht = NachweisPflicht.AlleEinsatzkraefte, Sortierung = 20, Beschreibung = "Unterweisung nach DGUV Vorschrift 49." },
    ];

    public static IReadOnlyList<StatistikKategorie> StatistikKategorien =>
    [
        new() { Code = "BR", Bezeichnung = "Brand", Sortierung = 10, ExpositionPflicht = true },
        new() { Code = "TH", Bezeichnung = "Technische Hilfe", Sortierung = 20 },
        new() { Code = "RD", Bezeichnung = "Rettungsdienstlicher Notfalleinsatz", Sortierung = 30 },
        new() { Code = "SO", Bezeichnung = "Sonstige", Sortierung = 40 },
        new() { Code = "FA", Bezeichnung = "Fehlalarm", Sortierung = 50 },
    ];

    /// <summary>Gefahrstoffgruppen für den Expositionsblock (KoAtEx, DGUV Information 205-035).</summary>
    public static IReadOnlyList<Gefahrstoffgruppe> Gefahrstoffgruppen =>
    [
        new() { Code = "RAUCH", Bezeichnung = "Brandrauch", Sortierung = 10 },
        new() { Code = "ASBEST", Bezeichnung = "Asbest", Sortierung = 20 },
        new() { Code = "KMF", Bezeichnung = "Künstliche Mineralfasern", Sortierung = 30 },
        new() { Code = "CHEM", Bezeichnung = "Chemikalie", Sortierung = 40 },
        new() { Code = "BIO", Bezeichnung = "Biostoffe", Sortierung = 50 },
        new() { Code = "SONST", Bezeichnung = "Sonstiger Gefahrstoff", Sortierung = 90 },
    ];

    public static IReadOnlyList<Dokumentkategorie> Dokumentkategorien =>
    [
        new() { Code = "SAT", Bezeichnung = "Satzung und Ordnungen", Sortierung = 10 },
        new() { Code = "DA", Bezeichnung = "Dienstanweisung", Sortierung = 20 },
        new() { Code = "PROT", Bezeichnung = "Protokoll", Sortierung = 30 },
        new() { Code = "MB", Bezeichnung = "Merkblatt", Sortierung = 40 },
        new() { Code = "FORM", Bezeichnung = "Formular und Vorlage", Sortierung = 50 },
        new() { Code = "SON", Bezeichnung = "Sonstiges", Sortierung = 90 },
    ];

    public static IReadOnlyList<Parameter> Parameter =>
    [
        new() { Schluessel = ParameterSchluessel.ProbezeitMonate, Wert = "12", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.AltersgrenzeEinsatzdienst, Wert = "65", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.AltersabteilungAbAlter, Wert = "55", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.FristWarnungTage, Wert = "90", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.PruefungWarnungTage, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.InventarisierungAbWertEuro, Wert = "150", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag, mit der Kämmerei abstimmen" },
        new() { Schluessel = ParameterSchluessel.DirektabschreibungBisWertEuro, Wert = "1000", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag nach GemHVO BW, mit der Kämmerei abstimmen" },
        new() { Schluessel = ParameterSchluessel.AtemschutzUebungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.AlarmdatenAufbewahrungMonate, Wert = "6", GueltigAb = new(2021, 8, 1), Quelle = "IM BW, Hinweise Datenschutz Alarmierungssysteme 08/2021" },
        new() { Schluessel = ParameterSchluessel.DokumentMaxMegabyte, Wert = "20", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.HauptversammlungEinladungsfristTage, Wert = "14", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg § 14 Abs. 3" },
        new() { Schluessel = ParameterSchluessel.AusschussEinladungsfristTage, Wert = "3", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg § 13 Abs. 4" },
        new() { Schluessel = ParameterSchluessel.AmtszeitWarnungTage, Wert = "180", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.NachweisWarnungTage, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.AtemschutzWarnungTage, Wert = "60", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.ErinnerungIntervallTage, Wert = "7", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.ErinnerungAbStunde, Wert = "7", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.BelastungsuebungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.EinsatzuebungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.AtemschutzUnterweisungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.VorsorgeIntervallMonate, Wert = "36", GueltigAb = new(2026, 1, 1), Quelle = "DGUV G 26.3 bis 50 Jahre; mit AMR 14.2 und Betriebsarzt abgleichen" },
        new() { Schluessel = ParameterSchluessel.VorsorgeIntervallAbAlterMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "DGUV G 26.3 ab 50 Jahre; mit AMR 14.2 und Betriebsarzt abgleichen" },
        new() { Schluessel = ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, Wert = "50", GueltigAb = new(2026, 1, 1), Quelle = "DGUV G 26.3" },
        new() { Schluessel = ParameterSchluessel.EinsatzAlsEinsatzuebungAbMinuten, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 7: Einsatz unter Atemschutz ersetzt die Einsatzübung (Festlegung FF Adelberg, vorläufig)" },
        new() { Schluessel = ParameterSchluessel.AtemschutzSelbstmeldungHoechstTage, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (Andi 25.10.2026)" },
        new() { Schluessel = ParameterSchluessel.DienstSerieMaxMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.EinsatzbeteiligteLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L3 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzRundungMinuten, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Muster Feuerwehrkostenersatzsatzung Göppingen § 6 Abs. 2, mit Adelberger Satzung abgleichen" },
        new() { Schluessel = ParameterSchluessel.DienstRueckmeldungSchlussStunden, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KalenderVorschauMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KalenderRueckblickMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.RueckmeldungLoeschenMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.LoeschlaufAbStunde, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, Wert = "6", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L18 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L6 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AnschriftenVerlaufLoeschenNachMonate, Wert = "24", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L34 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.PruefungGesamtlaufBeimStart, Wert = "1", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (Andi 23.10.2026)" },
        new() { Schluessel = ParameterSchluessel.AntraegeLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L32 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AntragsentwuerfeLoeschenNachMonate, Wert = "6", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L33 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.VolljaehrigkeitAlter, Wert = "18", GueltigAb = new(2000, 1, 1), Quelle = "§ 2 BGB" },
        new() { Schluessel = ParameterSchluessel.VolljaehrigkeitErinnernMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.AntraegeVorlaufMonate, Wert = "6", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (Andi 24.10.2026)" },
        new() { Schluessel = ParameterSchluessel.DatenschutzBeauftragter, Wert = "[Name / Stelle, Kontakt]", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Freigabe durch den Datenschutzbeauftragten" },
        new() { Schluessel = ParameterSchluessel.FotoKanaele, Wert = "Homepage, Amtsblatt, Presse, Social-Media-Kanäle der Feuerwehr [anpassen]", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Freigabe durch den Datenschutzbeauftragten" },
        new() { Schluessel = ParameterSchluessel.Art9Grundlage, Wert = "Art. 9 Abs. 2 lit. [b oder h, vom Datenschutzbeauftragten festzulegen] DSGVO", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Freigabe durch den Datenschutzbeauftragten" },
        new() { Schluessel = ParameterSchluessel.AlarmierungBetreiber, Wert = "[von der Gemeinde selbst / von einem Dienstleister, Namen eintragen]", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Freigabe durch den Datenschutzbeauftragten" },
        new() { Schluessel = ParameterSchluessel.Auftragsverarbeiter, Wert = "[Hoster des Servers, Anbieter der Datensicherung, E-Mail-Versand: Namen eintragen]", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Freigabe durch den Datenschutzbeauftragten" },
        new() { Schluessel = ParameterSchluessel.RestdatensatzAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L7 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.EinsatzberichteAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L2 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, Wert = "40", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L8: KoAtEx (DGUV, Januar 2025), § 10a GefStoffV; Festlegung Andi 25.10.2026" },
        new() { Schluessel = ParameterSchluessel.PruefhinweiseLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L31" },
        new() { Schluessel = ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, Wert = "18", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L15, Volljährigkeit nach § 2 BGB" },
        new() { Schluessel = ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, Wert = "36", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L19 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AenderungsprotokollVerdichtenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L19, Verdichtung nach der Zehn-Jahres-Simulation (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.StartseiteVorschauTage, Wert = "28", GueltigAb = new(2000, 1, 1), Quelle = "FEVAD, Zehn-Jahres-Simulation (Startseite)" },
        new() { Schluessel = ParameterSchluessel.UnfallAnzeigefristTage, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "§ 193 Abs. 4 SGB VII" },
        new() { Schluessel = ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L38 (Vorschlag Datenschutz-Thread, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.BankverbindungLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L37 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.UnfaelleLoeschenNachJahren, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L36, angelehnt an DGUV Vorschrift 1 § 24 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, Wert = "6", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L5 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AusstattungLoeschenNachRueckgabeMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L14 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L10a (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.KleiderkammerAussonderungVorlaufMonate, Wert = "6", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.EinsatznummerFormat, Wert = Einsatznummer.StandardFormat, GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.LeistungsabzeichenBelastungsuebungMonate, Wert = "12", GueltigAb = new(2026, 3, 12), Quelle = "Richtlinie Leistungsabzeichen BW, Neufassung 2026" },
        new() { Schluessel = ParameterSchluessel.LeistungsspangeWarnungTage, Wert = "365", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.StandortfortbildungStunden, Wert = "40", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 2 / VwV Feuerwehrausbildung BW" },
        new() { Schluessel = ParameterSchluessel.UmsatzsteuerRegelsatzProzent, Wert = "19", GueltigAb = new(2021, 1, 1), Quelle = "§ 12 Abs. 1 UStG" },
        new() { Schluessel = ParameterSchluessel.UmsatzsteuerErmaessigterSatzProzent, Wert = "7", GueltigAb = new(2021, 1, 1), Quelle = "§ 12 Abs. 2 UStG" },
        new() { Schluessel = ParameterSchluessel.KleinunternehmerVorjahrEuro, Wert = "25000", GueltigAb = new(2025, 1, 1), Quelle = "§ 19 Abs. 1 UStG i. d. F. Jahressteuergesetz 2024" },
        new() { Schluessel = ParameterSchluessel.KleinunternehmerLaufendesJahrEuro, Wert = "100000", GueltigAb = new(2025, 1, 1), Quelle = "§ 19 Abs. 1 UStG i. d. F. Jahressteuergesetz 2024" },
        new() { Schluessel = ParameterSchluessel.KameradschaftskasseUmsatzgrenzeEuro, Wert = "45000", GueltigAb = new(2000, 1, 1), Quelle = "Angabe Kämmerei Adelberg (Andi 25.10.2026); R 4.1 Abs. 5 KStR, LFV-Ratgeber Kameradschaftskassen 04/2026" },
        new() { Schluessel = ParameterSchluessel.KoerperschaftsteuerFreibetragEuro, Wert = "5000", GueltigAb = new(2009, 1, 1), Quelle = "§ 24 KStG" },
        .. Entschaedigungsbeispiele,
    ];

    private const string Beispielquelle = Entschaedigung.Abrechnungen.PlatzhalterQuelle + ": Beispielwert aus der FwES Bad Überkingen 2022, durch die Entschädigungssatzung Adelberg ersetzen";

    /// <summary>
    /// Die Adelberger Sätze sind noch nicht bekannt. Die Beispielwerte zeigen, wie die Abrechnung rechnet;
    /// solange einer davon verwendet wird, lässt sich keine Abrechnung abschließen.
    /// </summary>
    private static IEnumerable<Parameter> Entschaedigungsbeispiele =>
    [
        Beispiel(Entschaedigungssaetze.Schluessel(Entschaedigungssaetze.VorgangEinsatz, "SatzStunde"), "15"),
        Beispiel(Entschaedigungssaetze.Schluessel(Entschaedigungssaetze.VorgangEinsatz, "RundungMinuten"), "60"),
        Beispiel(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Sonderdienst), "SatzStunde"), "12"),
        Beispiel(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Sonderdienst), "RundungMinuten"), "60"),
        Beispiel(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "SatzTag"), "42"),
        Beispiel(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "SatzHalberTag"), "21"),
        Beispiel(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "HalberTagUnterStunden"), "5"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "KDT", "700"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "STV", "400"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "GW", "200"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "AGW", "200"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "SF", "150"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "KAS", "100"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "KW", "100"),
        Beispiel(Entschaedigungssaetze.PauschalePraefix + "JW", "250"),
    ];

    private static Parameter Beispiel(string schluessel, string wert) =>
        new() { Schluessel = schluessel, Wert = wert, GueltigAb = new(2000, 1, 1), Quelle = Beispielquelle };

    /// <summary>
    /// Gerätearten mit Prüfplan nach DGUV Grundsatz 305-002 (05/2021, Kap. III Tab. 1). Intervalle für Fahrzeuge,
    /// Elektro und Sonstiges nach StVZO Anlage VIII bzw. DGUV Vorschrift 3 sind Vorschläge und vor Gebrauch zu prüfen.
    /// </summary>
    public static IReadOnlyList<Geraeteart> Geraetearten =>
    [
        Art("LEITER", "Tragbare Leiter", "DGUV 305-002 Tab. 1, DIN EN 1147", null, 10,
            Position(Pruefart.Funktionspruefung, 12, null, "Holme und Sprossen ohne Risse oder Verformung", "Beschläge und Gelenke gängig", "Fußpunkte und Kennzeichnung vorhanden"),
            Position(Pruefart.Belastungspruefung, 24, null, "Belastungsprüfung bestanden", "Bleibende Durchbiegung [mm]")),
        Art("HALTEGURT", "Feuerwehr-Haltegurt", "DGUV 305-002 Tab. 1, DIN 14927", 10, 20,
            Position(Pruefart.Sichtpruefung, 12, null, "Gurtband ohne Schnitte, Scheuerstellen oder Verfärbung", "Nähte intakt", "Schnalle und Karabiner funktionsfähig")),
        Art("LEINE", "Feuerwehrleine", "DGUV 305-002 Tab. 1, DIN 14920", 20, 30,
            Position(Pruefart.Sichtpruefung, 12, null, "Leine ohne Beschädigung", "Kennzeichnung lesbar", "Länge [m]")),
        Art("SPRUNGPOLSTER", "Sprungpolster", "DGUV 305-002 Tab. 1, DIN 14151-3", null, 40,
            Position(Pruefart.Funktionspruefung, 12, null, "Aufbau und Füllung in Ordnung", "Hülle ohne Beschädigung"),
            Position(Pruefart.Hersteller, 60, "Hersteller oder autorisierte Person", "Sicherheitshauptprüfung bestanden")),
        Art("DRUCKSCHLAUCH", "Druckschlauch", "DGUV 305-002 Tab. 1, DIN 14811", null, 50,
            Position(Pruefart.NachGebrauch, 0, null, "Druckprüfung nach Wäsche bestanden", "Prüfdruck [bar]")),
        Art("SAUGSCHLAUCH", "Saugschlauch", "DGUV 305-002 Tab. 1", null, 60,
            Position(Pruefart.Funktionspruefung, 12, null, "Sichtprüfung ohne Befund", "Unterdruckprüfung bestanden", "Unterdruck [bar]")),
        Art("PUMPE", "Feuerlöschkreiselpumpe / Tragkraftspritze", "DGUV 305-002 Tab. 1, DIN EN 1028, DIN 14466", null, 70,
            Position(Pruefart.Funktionspruefung, 12, null, "Sichtprüfung ohne Befund", "Trockensaugprüfung bestanden", "Schließdruckprüfung bestanden", "Unterdruck nach Trockensaugen [bar]", "Betriebsstunden [h]")),
        Art("ARMATUR", "Wasserführende Armatur", "DGUV 305-002 Tab. 1", null, 80,
            Position(Pruefart.Funktionspruefung, 12, null, "Dichtungen vorhanden und dicht", "Kupplungen gängig", "Absperrorgane funktionsfähig")),
        Art("HEBEKISSEN", "Hebekissen", "DGUV 305-002 Tab. 1, DIN EN 13731", null, 90,
            Position(Pruefart.Funktionspruefung, 12, null, "Kissen ohne Beschädigung", "Schläuche und Steuerorgan dicht"),
            Position(Pruefart.Belastungspruefung, 60, "Hersteller oder autorisierte Person", "5-Jahres-Prüfung bestanden")),
        Art("HYDRAULIK", "Hydraulisches Rettungsgerät", "DGUV 305-002 Tab. 1, DIN EN 13204", null, 100,
            Position(Pruefart.Funktionspruefung, 12, null, "Sicht- und Funktionsprüfung ohne Befund", "Schläuche und Kupplungen dicht", "Ölstand Aggregat in Ordnung"),
            Position(Pruefart.Belastungspruefung, 36, "Befähigte Person mit Hersteller-Zusatzausbildung", "Funktions- und Belastungsprüfung bestanden")),
        Art("ELEKTRO", "Ortsveränderliches elektrisches Betriebsmittel", "DGUV Vorschrift 3 (Vorschlag)", null, 110,
            Position(Pruefart.Elektro, 12, "Elektrofachkraft", "Sichtprüfung ohne Befund", "Schutzleiterwiderstand [Ohm]", "Isolationswiderstand [MOhm]")),
        Art("STROMERZEUGER", "Stromerzeuger", "DGUV 305-002 Tab. 2 / DGUV Vorschrift 3 (Vorschlag)", null, 120,
            Position(Pruefart.Funktionspruefung, 12, null, "Probelauf ohne Befund", "Isolationsüberwachung in Ordnung", "Betriebsstunden [h]"),
            Position(Pruefart.Elektro, 12, "Elektrofachkraft", "Elektrische Prüfung bestanden")),
        Art("MOTORSAEGE", "Motorsäge", "UVV, Herstellerangaben (Vorschlag)", null, 130,
            Position(Pruefart.Uvv, 12, null, "Kettenbremse wirksam", "Kettenfangbolzen vorhanden", "Kette geschärft und gespannt")),
        Art("FEUERLOESCHER", "Tragbarer Feuerlöscher", "DIN 14406-4 (Vorschlag)", null, 140,
            Position(Pruefart.Hersteller, 24, "Sachkundiger nach DIN 14406-4", "Instandhaltung durchgeführt")),
        Art("SONSTIGES", "Sonstiges Gerät", "nach Herstellerangabe", null, 900,
            Position(Pruefart.Sichtpruefung, 12, null, "Sichtprüfung ohne Befund")),
        Art("FZG-LKW", "Fahrzeug über 3,5 t", "StVZO Anlage VIII, DGUV 305-002 Kap. IV (Vorschlag)", null, 950, true,
            Position(Pruefart.Hauptuntersuchung, 12, "Prüforganisation (TÜV, DEKRA)", "HU ohne Mängel"),
            Position(Pruefart.Uvv, 12, null, "Betriebssicherheitsprüfung bestanden", "Kilometerstand [km]")),
        Art("FZG-PKW", "Fahrzeug bis 3,5 t", "StVZO Anlage VIII, DGUV 305-002 Kap. IV (Vorschlag)", null, 960, true,
            Position(Pruefart.Hauptuntersuchung, 24, "Prüforganisation (TÜV, DEKRA)", "HU ohne Mängel"),
            Position(Pruefart.Uvv, 12, null, "Betriebssicherheitsprüfung bestanden", "Kilometerstand [km]")),
    ];

    /// <summary>
    /// Gerätearten für persönliche Schutzausrüstung, die die Kleiderkammer als Einzelstück führt. Prüfung und
    /// Nutzungsdauer richten sich nach Herstellerangabe; die Werte sind Vorschläge und vor Gebrauch zu prüfen.
    /// </summary>
    public static IReadOnlyList<Geraeteart> PsaGeraetearten =>
    [
        Art("PSA-HELM", "Feuerwehrhelm", "DIN EN 443, Herstellerangaben (Vorschlag)", null, 200,
            Position(Pruefart.Sichtpruefung, 12, null, "Helmschale ohne Risse, Verformung oder Hitzeschäden", "Innenausstattung und Kinnriemen intakt", "Visier und Nackenschutz in Ordnung", "Herstelldatum im Helm lesbar")),
        Art("PSA-EJ", "Einsatzjacke", "DIN EN 469, HuPF Teil 2, Herstellerangaben (Vorschlag)", null, 210,
            Position(Pruefart.Sichtpruefung, 12, null, "Obermaterial ohne Risse, Löcher oder Verfärbung", "Nähte und Reflexstreifen intakt", "Verschlüsse funktionsfähig", "Membran ohne sichtbaren Schaden")),
        Art("PSA-EH", "Einsatzhose", "DIN EN 469, HuPF Teil 2, Herstellerangaben (Vorschlag)", null, 220,
            Position(Pruefart.Sichtpruefung, 12, null, "Obermaterial ohne Risse, Löcher oder Verfärbung", "Nähte und Reflexstreifen intakt", "Verschlüsse und Hosenträger funktionsfähig")),
    ];

    /// <summary>Kleiderkammer-Artikel. Geräteart-Code macht den Artikel zum Einzelstück.</summary>
    public static IReadOnlyList<(Artikel Artikel, string? Geraeteart)> Artikel =>
    [
        (new() { Code = "HELM", Bezeichnung = "Feuerwehrhelm", Kategorie = ArtikelKategorie.Kopfschutz, Groessenart = Groessenart.Kopf, Norm = "DIN EN 443", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 10 }, "PSA-HELM"),
        (new() { Code = "EJ", Bezeichnung = "Einsatzjacke", Kategorie = ArtikelKategorie.Einsatzkleidung, Groessenart = Groessenart.Jacke, Norm = "DIN EN 469", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 20 }, "PSA-EJ"),
        (new() { Code = "EH", Bezeichnung = "Einsatzhose", Kategorie = ArtikelKategorie.Einsatzkleidung, Groessenart = Groessenart.Hose, Norm = "DIN EN 469", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 30 }, "PSA-EH"),
        (new() { Code = "HG", Bezeichnung = "Feuerwehr-Haltegurt", Kategorie = ArtikelKategorie.Absturzsicherung, Groessenart = Groessenart.Einheitsgroesse, Norm = "DIN 14927", Sortierung = 40 }, "HALTEGURT"),
        (new() { Code = "HSH", Bezeichnung = "Feuerwehrschutzhandschuhe", Kategorie = ArtikelKategorie.Handschutz, Groessenart = Groessenart.Handschuhe, Norm = "DIN EN 659", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 50 }, null),
        (new() { Code = "STF", Bezeichnung = "Feuerwehrschutzschuhe", Kategorie = ArtikelKategorie.Fussschutz, Groessenart = Groessenart.Schuhe, Norm = "DIN EN 15090", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 60 }, null),
        (new() { Code = "FSH", Bezeichnung = "Flammschutzhaube", Kategorie = ArtikelKategorie.Kopfschutz, Groessenart = Groessenart.Einheitsgroesse, Norm = "DIN EN 13911", GrundausstattungArt = AbteilungArt.Einsatz, Sortierung = 70 }, null),
        (new() { Code = "TSH", Bezeichnung = "T-Shirt", Kategorie = ArtikelKategorie.Dienstkleidung, Groessenart = Groessenart.Oberteil, Sortierung = 80 }, null),
        (new() { Code = "PLS", Bezeichnung = "Poloshirt", Kategorie = ArtikelKategorie.Dienstkleidung, Groessenart = Groessenart.Oberteil, Sortierung = 90 }, null),
    ];

    /// <summary>Prüfvorlage (Code der Geräteart) für neu angelegte Stammfahrzeuge.</summary>
    public static IReadOnlyDictionary<string, string> FahrzeugPruefvorlagen => new Dictionary<string, string>
    {
        ["Florian Adelberg 43"] = "FZG-LKW",
        ["Florian Adelberg 21"] = "FZG-LKW",
        ["Florian Adelberg 19"] = "FZG-PKW",
    };

    /// <summary>Fahrzeuge der FF Adelberg (öffentliche Angaben, Stand 2026).</summary>
    public static IReadOnlyList<Fahrzeug> Fahrzeuge =>
    [
        new() { Funkrufname = "Florian Adelberg 43", Typ = "HLF 10", Hersteller = "Mercedes-Benz Atego", Aufbau = "Ziegler", Baujahr = 2023, IndienstAm = new(2023, 11, 30) },
        new() { Funkrufname = "Florian Adelberg 21", Typ = "TLF 8/18", Hersteller = "Mercedes-Benz Unimog 1300 L", Aufbau = "Ziegler", Baujahr = 1986 },
        new() { Funkrufname = "Florian Adelberg 19", Typ = "MTW", Hersteller = "Mercedes-Benz Sprinter", Baujahr = 1999, Sitzplaetze = 9 },
    ];

    private static Geraeteart Art(string code, string bezeichnung, string grundlage, int? aussonderungJahre, int sortierung, params PruefplanPosition[] plan) =>
        Art(code, bezeichnung, grundlage, aussonderungJahre, sortierung, false, plan);

    private static Geraeteart Art(string code, string bezeichnung, string grundlage, int? aussonderungJahre, int sortierung, bool fuerFahrzeuge, params PruefplanPosition[] plan) =>
        new()
        {
            Code = code,
            Bezeichnung = bezeichnung,
            Pruefgrundlage = grundlage,
            AussonderungNachJahren = aussonderungJahre,
            FuerFahrzeuge = fuerFahrzeuge,
            Sortierung = sortierung,
            Pruefplan = [.. plan],
        };

    private static PruefplanPosition Position(Pruefart art, int intervallMonate, string? pruefer, params string[] punkte) =>
        new()
        {
            Pruefart = art,
            IntervallMonate = intervallMonate,
            Pruefer = pruefer,
            Checkliste = Pruefpunkt.AlsJson(Pruefpunkt.AusZeilen(string.Join('\n', punkte))),
        };
}
