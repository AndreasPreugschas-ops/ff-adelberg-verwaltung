using Fevad.Domain.Unfaelle;
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

    /// <summary>Frühere Bezeichnungen des Seeds; unverändert gebliebene werden auf die Schreibweise der VwV umgestellt (F21).</summary>
    public static IReadOnlyDictionary<string, string> DienstgradeFruehereBezeichnung { get; } = new Dictionary<string, string>
    {
        ["FMP"] = "Feuerwehrmann/-frau in der Probezeit",
        ["FM"] = "Feuerwehrmann/-frau",
        ["OFM"] = "Oberfeuerwehrmann/-frau",
        ["HFM"] = "Hauptfeuerwehrmann/-frau",
        ["LM"] = "Löschmeister/in",
        ["OLM"] = "Oberlöschmeister/in",
        ["HLM"] = "Hauptlöschmeister/in",
        ["BM"] = "Brandmeister/in",
        ["OBM"] = "Oberbrandmeister/in",
        ["HBM"] = "Hauptbrandmeister/in",
        ["LHBM"] = "Leitende/r Hauptbrandmeister/in",
    };

    /// <summary>Dienstgrade 1:1 nach Anlage 2 der VwV Feuerwehrbekleidung und Dienstgrade BW vom 27.09.2022 in Rangfolge (F21).</summary>
    public static IReadOnlyList<Dienstgrad> Dienstgrade =>
    [
        new() { Code = "FMP", Bezeichnung = "Feuerwehrfrau/-mann in der Probezeit", Kurzbezeichnung = "FM(P)", Sortierung = 10 },
        new() { Code = "FM", Bezeichnung = "Feuerwehrfrau/-mann", Kurzbezeichnung = "FM", Sortierung = 20 },
        new() { Code = "OFM", Bezeichnung = "Oberfeuerwehrfrau/-mann", Kurzbezeichnung = "OFM", Sortierung = 30 },
        new() { Code = "HFM", Bezeichnung = "Hauptfeuerwehrfrau/-mann", Kurzbezeichnung = "HFM", Sortierung = 40 },
        new() { Code = "LM", Bezeichnung = "Löschmeister/-in", Kurzbezeichnung = "LM", Sortierung = 50 },
        new() { Code = "OLM", Bezeichnung = "Oberlöschmeister/-in", Kurzbezeichnung = "OLM", Sortierung = 60 },
        new() { Code = "HLM", Bezeichnung = "Hauptlöschmeister/-in", Kurzbezeichnung = "HLM", Sortierung = 70 },
        new() { Code = "BM", Bezeichnung = "Brandmeister/-in", Kurzbezeichnung = "BM", Sortierung = 80 },
        new() { Code = "OBM", Bezeichnung = "Oberbrandmeister/-in", Kurzbezeichnung = "OBM", Sortierung = 90 },
        new() { Code = "HBM", Bezeichnung = "Hauptbrandmeister/-in", Kurzbezeichnung = "HBM", Sortierung = 100 },
        new() { Code = "LHBM", Bezeichnung = "Leitende/r Hauptbrandmeister/-in", Kurzbezeichnung = "LHBM", Sortierung = 110 },
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
        new() { Code = "JSPR", Bezeichnung = "Jugendsprecher/in", MitWahl = true, MitBeschluss = false, Grundlage = "Jugendordnung (Wahl durch die Jugendgruppe, V50)", Abteilungsart = AbteilungArt.Jugend, Sortierung = 65 },
        new() { Code = "AAL", Bezeichnung = "Leiter/in der Altersabteilung", MitWahl = true, BeschlussDurch = "Feuerwehrausschuss", AmtszeitJahre = 5, Grundlage = "Feuerwehrsatzung (Altersabteilung)", Abteilungsart = AbteilungArt.Alters, Sortierung = 66 },
        new() { Code = "KAS", Bezeichnung = "Kassier/in", MitWahl = true, MitBeschluss = false, VorgeschlageneRolle = Rollen.Kassier, Grundlage = "FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung", Sortierung = 70 },
        new() { Code = "SF", Bezeichnung = "Schriftführer/in", MitWahl = true, MitBeschluss = false, VorgeschlageneRolle = Rollen.Schriftfuehrer, Grundlage = "Feuerwehrsatzung", Sortierung = 80 },
        new() { Code = "KW", Bezeichnung = "Kleiderwart/in", BeschlussDurch = "Feuerwehrkommandant", VorgeschlageneRolle = Rollen.Kleiderwart, Grundlage = "Feuerwehrsatzung", Sortierung = 90 },
        new() { Code = "KP", Bezeichnung = "Kassenprüfer/in", MitWahl = true, MitBeschluss = false, AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Kassenpruefer, Grundlage = "FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung", Sortierung = 100 },
        new() { Code = "AUS", Bezeichnung = "Mitglied Feuerwehrausschuss", MitWahl = true, MitBeschluss = false, AmtszeitJahre = 5, Grundlage = "FwG BW, Feuerwehrsatzung (Feuerwehrausschuss)", Sortierung = 110 },
    ];

    private const string FwgKommandant = "FwG BW § 8, Feuerwehrsatzung (Wahl, Bestellung durch den Gemeinderat)";

    /// <summary>Bezeichnung der Feuerwehr-Fahrberechtigung bis 4,75 t (§ 2 Abs. 10a StVG); früher eine Zeile für 4,75 t und 7,5 t.</summary>
    public const string FwFahrberechtigungKlein = "Feuerwehr-Fahrberechtigung bis 4,75 t";

    /// <summary>Zulässige Gesamtmasse je Fahrerlaubnis-Code für ältere Datenbanken ohne Angabe (V59).</summary>
    public static IReadOnlyDictionary<string, int> FahrerlaubnisMasse => new Dictionary<string, int>
    {
        ["FS-B"] = 3500,
        ["FS-C1"] = 7500,
        ["FW-FS"] = 4750,
    };

    /// <summary>Vorbelegter Qualifikationskatalog: Lehrgänge, Fahrerlaubnisse, Abzeichen.</summary>
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
        new() { Code = "FS-B", Bezeichnung = "Fahrerlaubnis Klasse B", Art = QualifikationArt.Fahrerlaubnis, FahrzeugmasseBisKg = 3500, Sortierung = 105 },
        new() { Code = "FS-C1", Bezeichnung = "Fahrerlaubnis Klasse C1", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, FahrzeugmasseBisKg = 7500, Sortierung = 108 },
        new() { Code = "FW-FS", Bezeichnung = FwFahrberechtigungKlein, Art = QualifikationArt.Fahrerlaubnis, FahrzeugmasseBisKg = 4750, Sortierung = 125 },
        new() { Code = "FW-FS75", Bezeichnung = "Feuerwehr-Fahrberechtigung bis 7,5 t", Art = QualifikationArt.Fahrerlaubnis, FahrzeugmasseBisKg = 7500, Sortierung = 126 },
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
        new() { Code = Qualifikation.CodeLeistungsspange, Bezeichnung = "Leistungsspange DJF", Kurzbezeichnung = "Leistungsspange", Art = QualifikationArt.Abzeichen, MindestalterJahre = 15, HoechstalterJahre = 18, AlterNachJahrgang = true, MindestJugendMonate = 12, Sortierung = 350 },
    ];

    /// <summary>Stufenfolgen der Abzeichen (Code, Vorstufe); werden nur gesetzt, solange keine Vorstufe eingetragen ist.</summary>
    public static IReadOnlyList<(string Code, string Vorstufe)> Stufenfolgen =>
    [
        ("LA-S", "LA-B"),
        ("LA-G", "LA-S"),
        (Qualifikation.CodeJugendflamme2, Qualifikation.CodeJugendflamme1),
        (Qualifikation.CodeJugendflamme3, Qualifikation.CodeJugendflamme2),
    ];

    /// <summary>
    /// Jugendflamme-Regelwerke (V50a, Andi 25.10.2026: „füge beide Regeln ein“). A nach Heilbronn 2017 und Baden-Baden, B nach
    /// Neckar-Odenwald 2026 (DJF-Änderung Ende 2025); B ist Standard. Je Stufe: Code, Mindestalter, Voraussetzungen, Höchstalter der
    /// Nachweise in Monaten, Hinweis. Angelegt nur, solange es kein Regelwerk mit dem Code gibt.
    /// </summary>
    public static IReadOnlyList<(Abzeichenregelwerk Regelwerk, (string Code, int? Mindestalter, string? Voraussetzungen, int? HoechstensMonate, string? Hinweis)[] Stufen)> Abzeichenregelwerke =>
    [
        (new() { Code = "JFL-A", Bezeichnung = "Jugendflamme Regelwerk A (Heilbronn 2017, Baden-Baden)", Kurzbezeichnung = "Regelwerk A", Geltungsbereich = "Landkreis Heilbronn, Stadtkreis Baden-Baden",
            Quelle = "https://www.jugendfeuerwehr-baden-baden.de/jugendflamme.php", Sortierung = 10 },
        [
            (Qualifikation.CodeJugendflamme1, 10, null, null, "Einzelabnahme durch den Jugendwart"),
            (Qualifikation.CodeJugendflamme2, 13, null, null, "Gruppe aus fünf, Abnahme auf Kreisebene"),
            (Qualifikation.CodeJugendflamme3, 15, "EH", null, "Abnahme auf Kreisebene"),
        ]),
        (new() { Code = "JFL-B", Bezeichnung = "Jugendflamme Regelwerk B (Neckar-Odenwald 2026)", Kurzbezeichnung = "Regelwerk B", Geltungsbereich = "Neckar-Odenwald-Kreis, DJF-Grundsätze 2026",
            Quelle = "https://www.jugendfeuerwehr-nok.de/ueber-die-jugendfeuerwehr/wettbewerbe/jugendflamme.html", Standard = true, Sortierung = 20 },
        [
            (Qualifikation.CodeJugendflamme1, 10, null, null, "Einzelabnahme durch den Jugendwart, Stempel durch den Fachgebietsleiter"),
            (Qualifikation.CodeJugendflamme2, 13, null, null, "Gruppe aus fünf, Abnahme auf Kreisebene; danach soll die Leistungsspange folgen"),
            (Qualifikation.CodeJugendflamme3, 16, "EH", 24, "Einzeln oder in der Gruppe auf Kreisebene; die Leistungsspange soll vorher abgelegt sein"),
        ]),
    ];

    /// <summary>Vorbelegte Nachweisarten, z. B. Führerscheinkontrolle.</summary>
    public static IReadOnlyList<Nachweisart> Nachweisarten =>
    [
        new() { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", IntervallMonate = 6, Pflicht = NachweisPflicht.MitFahrerlaubnis, Sortierung = 10, Beschreibung = "Sichtkontrolle des Führerscheins durch die Wehrführung (Halterpflicht der Gemeinde)." },
        new() { Code = "UVV", Bezeichnung = "Jährliche Unterweisung (UVV)", IntervallMonate = 12, Pflicht = NachweisPflicht.AlleEinsatzkraefte, Sortierung = 20, Beschreibung = "Unterweisung nach DGUV Vorschrift 49." },
        new() { Code = NachweisartFuehrungsfortbildung, Bezeichnung = "Fortbildungsseminar für Führungskräfte", IntervallMonate = 72, Pflicht = NachweisPflicht.MitQualifikation, Sortierung = 30,
            Beschreibung = "Spätestens alle sechs Jahre für Gruppen- und Zugführer (VwV Feuerwehrausbildung, V54); das Intervall ist hier pflegbar." },
    ];

    /// <summary>Code der Nachweisart „Fortbildungsseminar für Führungskräfte“ (V54).</summary>
    public const string NachweisartFuehrungsfortbildung = "FKFB";

    /// <summary>Nachweisarten mit Pflicht für Inhaber einer Qualifikation: Code der Nachweisart und Code der Qualifikation.</summary>
    public static IReadOnlyList<(string Nachweisart, string Qualifikation)> NachweisartQualifikationen =>
        [(NachweisartFuehrungsfortbildung, Qualifikation.CodeGruppenfuehrer)];

    /// <summary>Feste Schlüssel der Foto-Arten; die Migration EinwilligungsregisterDatenschutz überträgt die frühere Foto-Einwilligung darauf.</summary>
    public static readonly Guid[] FotoArtIds =
    [
        Guid.Parse("01980000-0000-7000-8000-00000000f001"), Guid.Parse("01980000-0000-7000-8000-00000000f002"),
        Guid.Parse("01980000-0000-7000-8000-00000000f003"), Guid.Parse("01980000-0000-7000-8000-00000000f004"),
    ];

    /// <summary>
    /// Arten im Einwilligungsregister (V39, V45): Fotos je Kanal (Andi, 25.10.2026: alle Kanäle werden genutzt), WhatsApp für die
    /// Elternkommunikation und die Einwilligungen der Jugendarbeit. Der Heimweg steht in der Abholregelung, nicht hier.
    /// </summary>
    public static IReadOnlyList<Einwilligungsart> Einwilligungsarten =>
    [
        new() { Id = FotoArtIds[0], Code = Einwilligungsart.CodeFotoHomepage, Bezeichnung = "Fotos auf der Homepage", Sortierung = 10, Foto = true,
            Beschreibung = "Fotos, auf denen die Person zu erkennen ist, dürfen auf der Homepage der Feuerwehr veröffentlicht werden.",
            HinweisBeiWiderruf = "veröffentlichte Fotos von der Homepage entfernen" },
        new() { Id = FotoArtIds[1], Code = "FOTO-AB", Bezeichnung = "Fotos im Amtsblatt", Sortierung = 20, Foto = true,
            Beschreibung = "Fotos dürfen im Amtsblatt der Gemeinde veröffentlicht werden." },
        new() { Id = FotoArtIds[2], Code = Einwilligungsart.CodeFotoSozialeMedien, Bezeichnung = "Fotos in sozialen Medien", Sortierung = 30, Foto = true,
            Beschreibung = "Fotos dürfen auf den Social-Media-Kanälen der Feuerwehr (z. B. Instagram, Facebook) veröffentlicht werden.",
            HinweisBeiWiderruf = "veröffentlichte Fotos aus den sozialen Medien entfernen" },
        new() { Id = FotoArtIds[3], Code = "FOTO-PR", Bezeichnung = "Fotos für Presse und Kreisfeuerwehrverband", Sortierung = 40, Foto = true,
            Beschreibung = "Fotos dürfen an die Presse und den Kreisfeuerwehrverband Göppingen weitergegeben werden." },
        new() { Code = Einwilligungsart.CodeWhatsApp, Bezeichnung = "Kommunikation über WhatsApp", Sortierung = 50, NurJugend = true,
            Beschreibung = "Aufnahme der Mobilnummer in die WhatsApp-Gruppe (Anbieter Meta, Übermittlung in die USA möglich). Infos gibt es auch ohne WhatsApp.",
            HinweisBeiWiderruf = "Mobilnummer aus der WhatsApp-Gruppe entfernen" },
        new() { Code = "FAHRT", Bezeichnung = "Teilnahme an Fahrten und Lagern", Sortierung = 60, NurJugend = true,
            Beschreibung = "Das Kind darf an Ausflügen, Fahrten und Zeltlagern der Jugendfeuerwehr teilnehmen." },
        new() { Code = "PKW", Bezeichnung = "Mitnahme im privaten Pkw", Sortierung = 70, NurJugend = true,
            Beschreibung = "Das Kind darf von Betreuerinnen und Betreuern im privaten Pkw mitgenommen werden." },
        new() { Code = "BADEN", Bezeichnung = "Baden und Schwimmen", Sortierung = 80, NurJugend = true,
            Beschreibung = "Das Kind darf unter Aufsicht baden und schwimmen." },
        new() { Code = "MED", Bezeichnung = "Gabe von Medikamenten", Sortierung = 90, NurJugend = true, ImAntrag = false,
            Beschreibung = "Betreuer dürfen Medikamente nach schriftlicher Anweisung der Eltern geben. Welche Medikamente, steht nur auf dem Papier, nicht in FEVAD." },
    ];

    /// <summary>Einsatzkategorien der Jahresstatistik.</summary>
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

    /// <summary>
    /// Themen der Unterweisung (V53), vorbelegt nach Andi (F22): UVV allgemein, Sonderrechte, Atemschutz, Gefahrstoffe, Hygiene. Je Thema der
    /// Code der Nachweisart, die der Abschluss einträgt, und der Code der Qualifikation, auf die sich die Zielgruppe beschränkt.
    /// </summary>
    public static IReadOnlyList<(Unterweisungsthema Thema, string? NachweisartCode, string? QualifikationCode)> Unterweisungsthemen =>
    [
        (new() { Code = Unterweisungsthema.CodeUvv, Bezeichnung = "UVV allgemein", Sortierung = 10,
            Beschreibung = "Jährliche Unterweisung nach DGUV Vorschrift 1 § 4 und DGUV Vorschrift 49 § 5." }, "UVV", null),
        (new() { Code = Unterweisungsthema.CodeSonderrechte, Bezeichnung = "Sonderrechte und Wegerecht", Sortierung = 20,
            Beschreibung = "§§ 35, 38 StVO; für Maschinisten." }, null, Qualifikation.CodeMaschinist),
        (new() { Code = Unterweisungsthema.CodeAtemschutz, Bezeichnung = "Atemschutz", Sortierung = 30, Atemschutzunterweisung = true,
            Beschreibung = "Jährliche Unterweisung nach FwDV 7; zählt als Atemschutz-Unterweisung." }, null, Qualifikation.CodeAtemschutz),
        (new() { Code = Unterweisungsthema.CodeGefahrstoffe, Bezeichnung = "Gefahrstoffe", Sortierung = 40,
            Beschreibung = "Gefahrstoffverordnung § 14, FwDV 500, Einsatzstellenhygiene bei Gefahrstoffen." }, null, null),
        (new() { Code = Unterweisungsthema.CodeHygiene, Bezeichnung = "Hygiene", Sortierung = 50,
            Beschreibung = "DGUV Information 205-035 (Hygiene und Kontaminationsvermeidung)." }, null, null),
    ];

    /// <summary>Themen der Standortausbildung für den Jahresausbildungsplan (V53); Pflicht = jedes Jahr mindestens einmal.</summary>
    public static IReadOnlyList<Ausbildungsthema> Ausbildungsthemen =>
    [
        new() { Code = "FWDV3", Bezeichnung = "Einheiten im Lösch- und Hilfeleistungseinsatz", Grundlage = "FwDV 3", Pflicht = true, Sortierung = 10 },
        new() { Code = "FWDV7", Bezeichnung = "Atemschutz", Grundlage = "FwDV 7", Pflicht = true, Sortierung = 20 },
        new() { Code = "FWDV10", Bezeichnung = "Tragbare Leitern", Grundlage = "FwDV 10", Pflicht = true, Sortierung = 30 },
        new() { Code = "TH", Bezeichnung = "Technische Hilfeleistung", Grundlage = "FwDV 3, DGUV Information 205-010", Pflicht = true, Sortierung = 40 },
        new() { Code = "FWDV500", Bezeichnung = "Einheiten im ABC-Einsatz", Grundlage = "FwDV 500", Pflicht = true, Sortierung = 50 },
        new() { Code = "WASSER", Bezeichnung = "Wasserförderung", Grundlage = "FwDV 3", Sortierung = 60 },
        new() { Code = "FUNK", Bezeichnung = "Sprechfunk und Digitalfunk", Grundlage = "FwDV 810", Pflicht = true, Sortierung = 70 },
        new() { Code = "FUEHR", Bezeichnung = "Führung und Lagemeldung", Grundlage = "FwDV 100", Sortierung = 80 },
        new() { Code = "EH", Bezeichnung = "Erste Hilfe und Reanimation", Grundlage = "DGUV Vorschrift 49 § 13", Pflicht = true, Sortierung = 90 },
        new() { Code = "MASCH", Bezeichnung = "Maschinisten, Fahrzeug- und Gerätekunde", Grundlage = "FwDV 2", Sortierung = 100 },
        new() { Code = "ABST", Bezeichnung = "Absturzsicherung", Grundlage = "FwDV 1, DGUV 305-002", Sortierung = 110 },
        new() { Code = "VEG", Bezeichnung = "Vegetationsbrand", Grundlage = "Hinweise LFV BW", Sortierung = 120 },
        new() { Code = "SPORT", Bezeichnung = "Dienstsport und Fitness", Grundlage = "DGUV Information 205-010", Sortierung = 130 },
    ];

    /// <summary>Funktionen bei Einsatz und Übung (V14, FwDV 3) mit nötiger Qualifikation; ohne Qualifikation nur Warnung.</summary>
    public static IReadOnlyList<Einsatzfunktion> Einsatzfunktionen =>
    [
        new() { Code = "EL", Bezeichnung = "Einsatzleiter", Qualifikationen = "GF|ZF|VF", Sortierung = 10 },
        new() { Code = "ZF", Bezeichnung = "Zugführer", Qualifikationen = "ZF|VF", Sortierung = 20 },
        new() { Code = "GF", Bezeichnung = "Gruppen- oder Staffelführer", Qualifikationen = "GF|ZF|VF", Sortierung = 30 },
        new() { Code = "MA", Bezeichnung = "Maschinist", Qualifikationen = "MA", Sortierung = 40 },
        new() { Code = "ME", Bezeichnung = "Melder", Sortierung = 50 },
        new() { Code = "AT", Bezeichnung = "Angriffstrupp", Qualifikationen = "AGT", Atemschutz = true, Sortierung = 60 },
        new() { Code = "WT", Bezeichnung = "Wassertrupp", Sortierung = 70 },
        new() { Code = "ST", Bezeichnung = "Schlauchtrupp", Sortierung = 80 },
        new() { Code = "SIT", Bezeichnung = "Sicherheitstrupp", Qualifikationen = "AGT", Atemschutz = true, Sortierung = 90 },
        new() { Code = "ASU", Bezeichnung = "Atemschutzüberwachung", Qualifikationen = "AGT|GF", Sortierung = 100 },
        new() { Code = "FUNK", Bezeichnung = "Funker / Führungsassistent", Qualifikationen = "SPRF", Sortierung = 110 },
        new() { Code = "TRUPP", Bezeichnung = "Truppmann", Sortierung = 120 },
    ];

    /// <summary>
    /// Jahreskreis der Führungspflichten (V51). Stichtage sind Vorschläge und in der Oberfläche änderbar; Wahlen und Einladungsfristen
    /// ergeben sich aus Ämtern und Sitzungen und stehen deshalb nicht im Katalog.
    /// </summary>
    public static IReadOnlyList<Fevad.Domain.Gremien.Fuehrungspflicht> Fuehrungspflichten =>
    [
        new() { Code = "JST", Bezeichnung = "Jahresstatistik an den Kreisbrandmeister", Monat = 1, Tag = 31, VorlaufTage = 30, Verantwortlich = "KDT|SF", Vorlage = "/Auswertungen/Jahresstatistik", Rechtsgrundlage = "Feuerwehrstatistik des Landes", Sortierung = 10 },
        new() { Code = "ZFEU", Bezeichnung = "Z-Feu-Antrag beim Landratsamt", Monat = 2, Tag = 15, VorlaufTage = 60, Verantwortlich = "KDT", Vorlage = "/Beschaffung/Foerderung/Index", Rechtsgrundlage = "VwV Z-Feu", Hinweis = "Antrag über die Gemeinde; Vorhaben vorher mit dem Kreisbrandmeister abstimmen.", Sortierung = 20 },
        new() { Code = "KAP", Bezeichnung = "Jahresabschluss und Kassenprüfung", Monat = 2, Tag = 28, VorlaufTage = 30, Verantwortlich = "KAS|KP", Vorlage = "/Kasse/Index", Rechtsgrundlage = "FwG BW § 18; Feuerwehrsatzung", Hinweis = "Vor der Hauptversammlung, die über die Entlastung beschließt.", Sortierung = 30 },
        new() { Code = "BEF", Bezeichnung = "Beförderungen vorbereiten", Monat = 2, Tag = 28, VorlaufTage = 30, Verantwortlich = "KDT", Vorlage = "/Laufbahn/Index", Hinweis = "Stichtag an den Termin der Hauptversammlung anpassen.", Sortierung = 40 },
        new() { Code = "HV", Bezeichnung = "Hauptversammlung durchführen", Monat = 3, Tag = 31, VorlaufTage = 60, Verantwortlich = "KDT|SF", Vorlage = "/Gremien/Index", Rechtsgrundlage = "Feuerwehrsatzung § 14", Sortierung = 50 },
        new() { Code = "JBER", Bezeichnung = "Jahresbericht an den Bürgermeister", Monat = 4, Tag = 30, VorlaufTage = 30, Verantwortlich = "KDT", Vorlage = "/Gremien/Index", Rechtsgrundlage = "FwG BW § 9", Sortierung = 60 },
        new() { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", Turnus = Fevad.Domain.Gremien.PflichtTurnus.Halbjaehrlich, Monat = 1, Tag = 15, VorlaufTage = 21, Verantwortlich = "KDT", Vorlage = "/Personal/Sammelerfassung", Rechtsgrundlage = "StVG § 21 (Halterpflicht)", Hinweis = "Am Dienstabend für alle Fahrer, Eintrag als Sammelnachweis.", Sortierung = 70 },
        new() { Code = "HH", Bezeichnung = "Haushaltsanmeldung an die Gemeinde", Monat = 9, Tag = 30, VorlaufTage = 45, Verantwortlich = "KDT", Vorlage = "/Haushalt/Index", Hinweis = "Bedarf von Gerätewart, Kleiderwart und Jugendwart vorher einsammeln.", Sortierung = 80 },
        new() { Code = "LGB", Bezeichnung = "Lehrgangsbedarf an den Kreisbrandmeister (Herbstmeldung)", Monat = 10, Tag = 15, VorlaufTage = 30, Verantwortlich = "KDT", Vorlage = "/Personal/Lehrgangswuensche", Sortierung = 90 },
        new() { Code = "BPL", Bezeichnung = "Feuerwehrbedarfsplan fortschreiben", Turnus = Fevad.Domain.Gremien.PflichtTurnus.Mehrjaehrlich, IntervallJahre = 5, Bezugsjahr = 2031, Monat = 3, Tag = 31, VorlaufTage = 180, Verantwortlich = "KDT", Rechtsgrundlage = "FwG BW § 3", Hinweis = "Geltender Plan 2026 bis 2031; Fortschreibung mit Gemeinde und Kreisbrandmeister.", Sortierung = 100 },
    ];

    /// <summary>
    /// Briefvorlagen (V56) als Vorschlag; die Schriftführung passt Texte an. Neue Vorlagen kommen nur dazu, geänderte bleiben unberührt.
    /// </summary>
    public static IReadOnlyList<Fevad.Domain.Gremien.Briefvorlage> Briefvorlagen =>
    [
        new()
        {
            Code = "EINL", Bezeichnung = "Einladung", Betreff = "Einladung", Sortierung = 10,
            Text = "{Briefanrede}\n\nhiermit laden wir Sie herzlich ein zu …\n\nTermin: …\nOrt: Feuerwehrhaus\n\nWir freuen uns auf Ihr Kommen.\n\nMit kameradschaftlichen Grüßen",
        },
        new()
        {
            Code = "EHRU", Bezeichnung = "Ehrungsurkunde", Form = Fevad.Domain.Gremien.VorlageForm.Urkunde, Betreff = "Urkunde", Sortierung = 20,
            Text = "Die {Feuerwehr} dankt\n\n{Dienstgrad} {Vorname} {Nachname}\n\nfür … Jahre treue Pflichterfüllung im Dienst der Feuerwehr.",
        },
        new()
        {
            Code = "BEFU", Bezeichnung = "Beförderungsurkunde", Form = Fevad.Domain.Gremien.VorlageForm.Urkunde, Betreff = "Ernennungsurkunde", Sortierung = 30,
            Text = "{Vorname} {Nachname}\n\nwird mit Wirkung vom {Datum} zum\n\n…\n\nernannt.",
        },
        new()
        {
            Code = "BESCH", Bezeichnung = "Bescheinigung", Betreff = "Bescheinigung über die Mitgliedschaft", Sortierung = 40,
            Text = "Hiermit bescheinigen wir, dass {Vorname} {Nachname}, wohnhaft {Strasse}, {PlzOrt}, Angehöriger der {Feuerwehr} ist.\n\nDiese Bescheinigung wird auf Wunsch zur Vorlage bei … ausgestellt.",
        },
        new()
        {
            Code = "DANK", Bezeichnung = "Dankschreiben", Betreff = "Herzlichen Dank", Sortierung = 50,
            Text = "{Briefanrede}\n\nfür Ihren Einsatz im Jahr {Jahr} danken wir Ihnen herzlich. …\n\nMit kameradschaftlichen Grüßen",
        },
        new()
        {
            Code = "ELTERN", Bezeichnung = "Elternbrief Jugendfeuerwehr", Empfaenger = Fevad.Domain.Gremien.VorlageEmpfaenger.Erziehungsberechtigte, Betreff = "Information der Jugendfeuerwehr",
            Sortierung = 60,
            Text = "{Briefanrede}\n\nwir möchten Sie über … informieren, an dem {Kind} teilnehmen kann.\n\nBei Fragen erreichen Sie die Jugendfeuerwehrwartin oder den Jugendfeuerwehrwart.\n\nMit freundlichen Grüßen",
        },
    ];

    /// <summary>Verletzungsarten für Unfallanzeige und Verbandbuch; nur Art, keine Befunde.</summary>
    public static IReadOnlyList<Verletzungsart> Verletzungsarten =>
    [
        new() { Code = "SCHNITT", Bezeichnung = "Schnitt- oder Stichwunde", Sortierung = 10 },
        new() { Code = "SCHUERF", Bezeichnung = "Schürfwunde", Sortierung = 20 },
        new() { Code = "PRELL", Bezeichnung = "Prellung", Sortierung = 30 },
        new() { Code = "VERST", Bezeichnung = "Verstauchung oder Zerrung", Sortierung = 40 },
        new() { Code = "BRUCH", Bezeichnung = "Knochenbruch", Sortierung = 50 },
        new() { Code = "VERBR", Bezeichnung = "Verbrennung oder Verbrühung", Sortierung = 60 },
        new() { Code = "VERAETZ", Bezeichnung = "Verätzung", Sortierung = 70 },
        new() { Code = "AUGE", Bezeichnung = "Augenverletzung", Sortierung = 80 },
        new() { Code = "ATEM", Bezeichnung = "Reizung der Atemwege (Rauch, Gase)", Sortierung = 90 },
        new() { Code = "FREMD", Bezeichnung = "Fremdkörper, Splitter", Sortierung = 100 },
        new() { Code = "STICH", Bezeichnung = "Insektenstich oder Biss", Sortierung = 110 },
        new() { Code = "SONST", Bezeichnung = "Sonstige Verletzung", Sortierung = 900 },
    ];

    /// <summary>Vorbelegte Kategorien der Dokumentenablage.</summary>
    public static IReadOnlyList<Dokumentkategorie> Dokumentkategorien =>
    [
        new() { Code = "SAT", Bezeichnung = "Satzung und Ordnungen", Sortierung = 10 },
        new() { Code = "DA", Bezeichnung = "Dienstanweisung", Sortierung = 20 },
        new() { Code = "PROT", Bezeichnung = "Protokoll", Sortierung = 30 },
        new() { Code = "MB", Bezeichnung = "Merkblatt", Sortierung = 40 },
        new() { Code = "FORM", Bezeichnung = "Formular und Vorlage", Sortierung = 50 },
        new() { Code = "AAO", Bezeichnung = "Alarm- und Ausrückeordnung", Sortierung = 60 },
        new() { Code = "GBU", Bezeichnung = "Gefährdungsbeurteilung", Sortierung = 70 },
        new() { Code = "SON", Bezeichnung = "Sonstiges", Sortierung = 90 },
    ];

    /// <summary>
    /// Zuschussgeber der Jugend (V48, Andi 25.10.2026: Jugendpflege des Landkreises und Kreisjugendring, nicht die Gemeinde). Sätze und
    /// Fristen sind noch unbekannt und bleiben leer.
    /// </summary>
    public static IReadOnlyList<Zuschussgeber> Zuschussgeber =>
    [
        new() { Name = "Kreisjugendpflege Landkreis Göppingen", Bemerkung = "Sätze und Fristen bei der Kreisjugendpflege erfragen und eintragen." },
        new() { Name = "Kreisjugendring Göppingen", Bemerkung = "Sätze und Fristen beim Kreisjugendring erfragen und eintragen." },
    ];

    /// <summary>Vorbelegte Parameter mit Quelle; Entschädigungssätze als gekennzeichnete Beispielwerte.</summary>
    public static IReadOnlyList<Parameter> Parameter =>
    [
        new() { Schluessel = ParameterSchluessel.ProbezeitMonate, Wert = "12", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.Truppmann2Stunden, Wert = "80", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 2 Nr. 2.1.2 (Truppmann Teil 2: 80 Stunden)" },
        new() { Schluessel = ParameterSchluessel.Truppmann2Monate, Wert = "24", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 2 Nr. 2.1.2 (innerhalb von zwei Jahren)" },
        new() { Schluessel = ParameterSchluessel.MassnahmeWarnTage, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag (V17)" },
        new() { Schluessel = ParameterSchluessel.UnterweisungWarnenAbMonat, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag wie Atemschutz-Warnung ab 01.10. (F27)" },
        new() { Schluessel = ParameterSchluessel.BrandsicherheitswacheAnteilKasseProzent, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Entschädigungssatzung regelt keinen Anteil (V25, Vorschlag 0 %)" },
        new() { Schluessel = ParameterSchluessel.AltersgrenzeEinsatzdienst, Wert = "65", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.AltersabteilungAbAlter, Wert = "55", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.FristWarnungTage, Wert = "90", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.PruefungWarnungTage, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.EtikettBreiteMm, Wert = "62", GueltigAb = new(2000, 1, 1), Quelle = "Etikettendrucker 62 mm Endlosband (Vorschlag)" },
        new() { Schluessel = ParameterSchluessel.EtikettHoeheMm, Wert = "29", GueltigAb = new(2000, 1, 1), Quelle = "Etikettendrucker 62 mm Endlosband (Vorschlag)" },
        new() { Schluessel = ParameterSchluessel.EtikettBogenSpalten, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "A4-Etikettenbogen 70 × 37 mm (Vorschlag)" },
        new() { Schluessel = ParameterSchluessel.EtikettBogenZeilen, Wert = "8", GueltigAb = new(2000, 1, 1), Quelle = "A4-Etikettenbogen 70 × 37 mm (Vorschlag)" },
        new() { Schluessel = ParameterSchluessel.InventarisierungAbWertEuro, Wert = "150", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag, mit der Kämmerei abstimmen" },
        new() { Schluessel = ParameterSchluessel.DirektabschreibungBisWertEuro, Wert = "1000", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag nach GemHVO BW, mit der Kämmerei abstimmen" },
        new() { Schluessel = ParameterSchluessel.BauteildatenAktiv, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Andi 25.10.2026: Atemschutz wird in Göppingen verwaltet, Bauteildaten optional" },
        new() { Schluessel = ParameterSchluessel.FuellprotokollAktiv, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Andi 25.10.2026: Füllen in Göppingen, eigenes Füllprotokoll optional" },
        new() { Schluessel = ParameterSchluessel.WartungsvorschauJahre, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag für die Haushaltsplanung" },
        new() { Schluessel = ParameterSchluessel.AussonderungBeschlussAbWertEuro, Wert = "1000", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag, Zuständigkeit laut Hauptsatzung mit der Gemeinde abstimmen" },
        new() { Schluessel = ParameterSchluessel.PauschalfoerderungJeAngehoerigemEuro, Wert = "90", GueltigAb = new(2025, 1, 1), Quelle = "VwV Z-Feu 2025–2031, Pauschalförderung" },
        new() { Schluessel = ParameterSchluessel.HaushaltInvestitionAbWertEuroNetto, Wert = "1000", GueltigAb = new(2020, 1, 1), Quelle = "Wertgrenze der Gemeinde für Investitionen (GemHVO BW § 38 Abs. 4, Vorschlag Kämmerei 1.000 € netto)" },
        new() { Schluessel = ParameterSchluessel.HaushaltWarnungAbProzent, Wert = "90", GueltigAb = new(2020, 1, 1), Quelle = "Vorschlag FEVAD (Haushaltsüberwachung)" },
        new() { Schluessel = ParameterSchluessel.HaushaltUebertragungHinweisAbMonat, Wert = "10", GueltigAb = new(2020, 1, 1), Quelle = "Vorschlag FEVAD: Haushaltsreste rechtzeitig vor dem Jahresabschluss bei der Kämmerei anmelden (§ 21 GemHVO)" },
        new() { Schluessel = ParameterSchluessel.ZFeuAntragsfristMonat, Wert = "2", GueltigAb = new(2025, 1, 1), Quelle = "VwV Z-Feu vom 21.08.2024: Antrag bis 15. Februar über die Gemeinde beim Landratsamt" },
        new() { Schluessel = ParameterSchluessel.ZFeuAntragsfristTag, Wert = "15", GueltigAb = new(2025, 1, 1), Quelle = "VwV Z-Feu vom 21.08.2024" },
        new() { Schluessel = ParameterSchluessel.ZFeuVerwendungsnachweisMonate, Wert = "12", GueltigAb = new(2025, 1, 1), Quelle = "VwV Z-Feu Nr. 6.6: Verwendungsnachweis innerhalb eines Jahres; am Bescheid prüfen" },
        new() { Schluessel = ParameterSchluessel.ZFeuAuszahlungNachweisBisMonat, Wert = "12", GueltigAb = new(2025, 1, 1), Quelle = "Handreichung Antragstellung ZFeuVwV, Stand 01.01.2026: Nachweis bis 1. Dezember für Auszahlung im laufenden Jahr" },
        new() { Schluessel = ParameterSchluessel.ZFeuAuszahlungNachweisBisTag, Wert = "1", GueltigAb = new(2025, 1, 1), Quelle = "Handreichung Antragstellung ZFeuVwV, Stand 01.01.2026" },
        new() { Schluessel = ParameterSchluessel.KameradschaftskasseZuschussEuro, Wert = "1500", GueltigAb = new(2019, 1, 1), Quelle = "Entschädigungssatzung Adelberg 2018, § 5 Abs. 1" },
        new() { Schluessel = ParameterSchluessel.KameradschaftskasseZuschussJugendEuro, Wert = "600", GueltigAb = new(2019, 1, 1), Quelle = "Entschädigungssatzung Adelberg 2018, § 5 Abs. 2" },
        new() { Schluessel = ParameterSchluessel.AtemschutzUebungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.AlarmdatenAufbewahrungMonate, Wert = "6", GueltigAb = new(2021, 8, 1), Quelle = "IM BW, Hinweise Datenschutz Alarmierungssysteme 08/2021" },
        new() { Schluessel = ParameterSchluessel.DokumentMaxMegabyte, Wert = "20", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.HauptversammlungEinladungsfristTage, Wert = "14", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg § 14 Abs. 3" },
        new() { Schluessel = ParameterSchluessel.AusschussFunktionen, Wert = "KDT|STV|JW|SF|KAS|AUS", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg § 13 Abs. 1 und 2" },
        new() { Schluessel = ParameterSchluessel.SollStaerke, Wert = "48", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025), Nr. 7.1" },
        new() { Schluessel = ParameterSchluessel.SollAtemschutz, Wert = "33", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): 66 % Atemschutzgeräteträger" },
        new() { Schluessel = ParameterSchluessel.SollMaschinisten, Wert = "17", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): 33 % Maschinisten" },
        new() { Schluessel = ParameterSchluessel.SollGruppenfuehrer, Wert = "5", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): 10 % Gruppenführer" },
        new() { Schluessel = ParameterSchluessel.SollZugfuehrer, Wert = "3", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): 5 % Zugführer" },
        new() { Schluessel = ParameterSchluessel.SchutzzielMinuten, Wert = "10", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): erste 6 Funktionen in 10 Minuten" },
        new() { Schluessel = ParameterSchluessel.SchutzzielFunktionen, Wert = "6", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): Staffel 1/5" },
        new() { Schluessel = ParameterSchluessel.Schutzziel2Minuten, Wert = "15", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): weitere 9 Funktionen in 15 Minuten" },
        new() { Schluessel = ParameterSchluessel.Schutzziel2Funktionen, Wert = "15", GueltigAb = new(2026, 1, 1), Quelle = "Feuerwehrbedarfsplan Adelberg 2026–2031 (Entwurf 18.11.2025): 6 + 9 Funktionen" },
        new() { Schluessel = ParameterSchluessel.SchutzzielErreichungProzent, Wert = "90", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag (V52, übliche Zielerreichung); mit der Gemeinde festlegen" },
        new() { Schluessel = ParameterSchluessel.SchutzzielKategorien, Wert = "BR|TH", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag (V52): Brand und Technische Hilfe" },
        new() { Schluessel = ParameterSchluessel.BeteiligungSchwelleProzent, Wert = "50", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag (V52); Festlegung des Kommandanten" },
        new() { Schluessel = ParameterSchluessel.JubilaeumMitgliedsjahre, Wert = "25|40|50|60|70", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag (V56), Ehrungsstufen nach VwV Ehrenzeichen" },
        new() { Schluessel = ParameterSchluessel.JubilaeumGeburtstage, Wert = "50|60|65|70|75|80|85|90|95|100", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag (V56)" },
        new() { Schluessel = ParameterSchluessel.BeteiligungNamenJahre, Wert = "2", GueltigAb = new(2026, 1, 1), Quelle = "Datenschutz-Vorschlag (V52), Freigabe DSB offen" },
        new() { Schluessel = ParameterSchluessel.AbgangPrognoseJahre, Wert = "5", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag (V52)" },
        new() { Schluessel = ParameterSchluessel.WahlniederschriftTage, Wert = "7", GueltigAb = new(2000, 1, 1), Quelle = "Wahlniederschrift binnen einer Woche an den Bürgermeister (V55)" },
        new() { Schluessel = ParameterSchluessel.AusschussEinladungsfristTage, Wert = "3", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg § 13 Abs. 4" },
        new() { Schluessel = ParameterSchluessel.AmtszeitWarnungTage, Wert = "180", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.NachweisWarnungTage, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.AtemschutzWarnungTage, Wert = "60", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.AtemschutzJahrespflichtWarnungAbMonat, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung Andi 25.10.2026: Kalenderjahr, Warnung ab 01.10." },
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
        new() { Schluessel = ParameterSchluessel.BmaObjekteLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L47 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.GemeindevorgaengePersonenbezugLoeschenNachJahren, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L48 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.MitgliederbescheideAblehnungLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L49 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.MitgliederbescheideLoeschenNachJahren, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L49 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.BrandsicherheitswachenAuftraggeberLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L58 (Datenschutz-Thread 26.10.2026, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.PflichtvermerkeLoeschenNachJahren, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L59 (Datenschutz-Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.LehrgangswuenscheLoeschenNachJahren, Wert = "2", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L60 (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.DienstpflichtAnzeigenLoeschenNachJahren, Wert = "1", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L61: Datenschutz-Vorschlag analog Tilgungsfristen Disziplinarrecht, Freigabe DSB offen" },
        new() { Schluessel = ParameterSchluessel.DienstpflichtMassnahmenLoeschenNachJahren, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L61: Datenschutz-Vorschlag analog Tilgungsfristen Disziplinarrecht, Freigabe DSB offen" },
        new() { Schluessel = ParameterSchluessel.MangelfotosLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L62: Fotos können Personen zeigen; ein Jahr nach Behebung reicht für Garantie und Rückfragen (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.FahrtenbuchFahrerLoeschenNachJahren, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L63: Rückfragen zu Fahrten (Bußgeld, Schaden) sind nach drei Jahren erledigt (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.BenachrichtigungenGeleseneLoeschenNachTagen, Wert = "90", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L64: gelesene Benachrichtigungen sind erledigt; die Mitteilung selbst bleibt (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.BenachrichtigungenLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L64: nie gelesene Benachrichtigungen nach einem Jahr (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.MitteilungenLoeschenNachMonaten, Wert = "24", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L65: Mitteilungen und Lesebestätigungen zwei Jahre nachvollziehbar (Vorschlag, Freigabe DSB offen)" },
        new() { Schluessel = ParameterSchluessel.SammelmailAbStunde, Wert = "18", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FEVAD: Sammelmail am frühen Abend, nach Feierabend" },
        new() { Schluessel = ParameterSchluessel.PruefprotokolleNamenLoeschenNachJahren, Wert = "1", GueltigAb = new(2000, 1, 1), Quelle = "DGUV G 305-002 / Löschkonzept FEVAD L11: Prüfnachweise gelten für die Lebensdauer des Geräts; danach reicht ein Jahr für Rückfragen" },
        new() { Schluessel = ParameterSchluessel.DienstausweiseLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L50 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzRundungMinuten, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Muster Feuerwehrkostenersatzsatzung Göppingen § 6 Abs. 2, mit Adelberger Satzung abgleichen" },
        new() { Schluessel = ParameterSchluessel.KostenersatzVorhaltezuschlagProzent, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter: fiktive Feuerwehr-Kostenersatzsatzung Adelberg (Entwurf 25.10.2026) § 4 Abs. 6 Nr. 3" },
        new() { Schluessel = ParameterSchluessel.KostenersatzAnhoerungFristTage, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig, mit Hauptamt abstimmen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzZugangsfiktionTage, Wert = "4", GueltigAb = new(2025, 1, 1), Quelle = "§ 41 Abs. 2 LVwVfG (vierter Tag nach Aufgabe zur Post, Postrechtsmodernisierung; prüfen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzFaelligkeitMonate, Wert = "1", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter: fiktive Feuerwehr-Kostenersatzsatzung Adelberg (Entwurf 25.10.2026) § 5 Abs. 2" },
        new() { Schluessel = ParameterSchluessel.KostenersatzVerjaehrungJahre, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "§ 3 Abs. 1 Nr. 5 KAG BW i. V. m. § 228 AO (Zahlungsverjährung; mit Kämmerei prüfen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzTextAnhoerung, Wert = Bescheidtexte.Anhoerung, GueltigAb = new(2000, 1, 1), Quelle = "Mustertext FEVAD (vom Hauptamt anzupassen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzTextBegruendung, Wert = Bescheidtexte.Begruendung, GueltigAb = new(2000, 1, 1), Quelle = "Mustertext FEVAD (vom Hauptamt anzupassen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzTextZahlung, Wert = Bescheidtexte.Zahlung, GueltigAb = new(2000, 1, 1), Quelle = "Mustertext FEVAD (vom Hauptamt anzupassen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzTextRechtsbehelf, Wert = Bescheidtexte.Rechtsbehelf, GueltigAb = new(2000, 1, 1), Quelle = "Mustertext FEVAD (vom Hauptamt anzupassen)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzKalkulationStunden, Wert = "80", GueltigAb = new(2000, 1, 1), Quelle = "§ 34 Abs. 5 FwG (80 Stunden je Angehörigem der Einsatzabteilung)" },
        new() { Schluessel = ParameterSchluessel.KostenersatzKalkulationWiedervorlageJahre, Wert = "4", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vier bis fünf Jahre, vorläufig)" },
        new() { Schluessel = ParameterSchluessel.GeldbusseHoechstbetrag, Wert = "1000", GueltigAb = new(2000, 1, 1), Quelle = "Feuerwehrsatzung Adelberg § 5 Abs. 9" },
        new() { Schluessel = ParameterSchluessel.MitgliederbescheidAnhoerungFristTage, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig, mit Hauptamt abstimmen)" },
        new() { Schluessel = ParameterSchluessel.MitgliederbescheidTextRechtsbehelf, Wert = Mitgliederbescheidtexte.Rechtsbehelf, GueltigAb = new(2000, 1, 1), Quelle = "Mustertext FEVAD (vom Hauptamt anzupassen)" },
        new() { Schluessel = ParameterSchluessel.DienstausweisGueltigkeitJahre, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.DienstRueckmeldungSchlussStunden, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.DienstEntschuldigungNachfristTage, Wert = "1", GueltigAb = new(2000, 1, 1), Quelle = "Feuerwehrsatzung Adelberg § 5 Abs. 6 (Entschuldigung bis zum Folgetag)" },
        new() { Schluessel = ParameterSchluessel.AbwesenheitAnzeigeAbTagen, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Feuerwehrsatzung Adelberg § 5 Abs. 6 (Abwesenheit ab zwei Wochen anzeigen)" },
        new() { Schluessel = ParameterSchluessel.AbwesenheitLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L39 (Vorschlag, DSB-Freigabe offen)" },
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
        new() { Schluessel = ParameterSchluessel.ImportdateienLoeschenNachTagen, Wert = "14", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L41 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.EinwilligungMitentscheidungAbAlter, Wert = "16", GueltigAb = new(2000, 1, 1), Quelle = "Empfehlung Rückfrage B P4 (Einsichtsfähigkeit, Art. 8 DSGVO als Richtwert)" },
        new() { Schluessel = ParameterSchluessel.FuehrungszeugnisWiedervorlageJahre, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "Vereinbarung nach § 72a SGB VIII, Landkreis Göppingen (V47)" },
        new() { Schluessel = ParameterSchluessel.FuehrungszeugnisWiedervorlageJahreKinder, Wert = "2", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag V47 P4 (Kinderfeuerwehr)" },
        new() { Schluessel = ParameterSchluessel.FuehrungszeugnisHoechstalterMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Mustervereinbarungen § 72a SGB VIII (LRA Konstanz, Heilbronn)" },
        new() { Schluessel = ParameterSchluessel.FuehrungszeugnisVorlaufTage, Wert = "60", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag V47 P4" },
        new() { Schluessel = ParameterSchluessel.BetreuerLoeschenNachMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "§ 72a Abs. 5 Satz 3 SGB VIII; Löschkonzept FEVAD L53" },
        new() { Schluessel = ParameterSchluessel.JugendveranstaltungenLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L55 (Vorschlag V48/V50 P4, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.WartelisteLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag V50 P4: Löschung nach zwölf Monaten; Löschkonzept FEVAD L56 (DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.NotfallmappeErneuernMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag V46 P4: jährlich vor dem Zeltlager erneuern" },
        new() { Schluessel = ParameterSchluessel.SchutzkonzeptPruefIntervallMonate, Wert = "24", GueltigAb = new(2000, 1, 1), Quelle = "Muster-Schutzkonzept Abschnitt 9 (F26): alle zwei Jahre prüfen" },
        new() { Schluessel = ParameterSchluessel.SchutzkonzeptVorlaufTage, Wert = "28", GueltigAb = new(2000, 1, 1), Quelle = "Muster-Schutzkonzept Abschnitt 9 (F26): vier Wochen vorher erinnern" },
        new() { Schluessel = ParameterSchluessel.EinwilligungenAufbewahrungMonate, Wert = "36", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L51 (Vorschlag, Verjährung § 195 BGB, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.LoeschanfragenAufbewahrungMonate, Wert = "36", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L52 (Vorschlag, Verjährung § 195 BGB, DSB-Freigabe offen)" },
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
        new() { Schluessel = ParameterSchluessel.ArchivStelle, Wert = "Gemeindearchiv Adelberg", GueltigAb = new(2000, 1, 1), Quelle = "Andi 25.10.2026 (F5); § 12 Abs. 2 LArchG BW" },
        new() { Schluessel = ParameterSchluessel.ArchivEntscheidungFristMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlag V65: höchstens ein Jahr Sperre bis zur Entscheidung des Archivs (§ 4 Abs. 3 LArchG BW)" },
        new() { Schluessel = ParameterSchluessel.ArchivBewertungPraefix + "Mitgliederakte", Wert = "archivwürdig", GueltigAb = new(2000, 1, 1), Quelle = "Bewertungskatalog V65 (Vorschlag, Bestätigung durch das Gemeindearchiv offen)" },
        new() { Schluessel = ParameterSchluessel.ArchivBewertungPraefix + "Kassenjahr", Wert = "archivwürdig", GueltigAb = new(2000, 1, 1), Quelle = "Bewertungskatalog V65 (Vorschlag, Bestätigung durch das Gemeindearchiv offen)" },
        new() { Schluessel = ParameterSchluessel.ArchivBewertungPraefix + "Mitgliederbescheid", Wert = "archivwürdig", GueltigAb = new(2000, 1, 1), Quelle = "Bewertungskatalog V65 (Vorschlag, Bestätigung durch das Gemeindearchiv offen)" },
        new() { Schluessel = ParameterSchluessel.ArchivBewertungPraefix + "Einsatzbericht", Wert = "archivwürdig", GueltigAb = new(2000, 1, 1), Quelle = "Bewertungskatalog V65 (Vorschlag, Bestätigung durch das Gemeindearchiv offen)" },
        new() { Schluessel = ParameterSchluessel.EinsatzberichteAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L2 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, Wert = "40", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L8: KoAtEx (DGUV, Januar 2025), § 10a GefStoffV; Festlegung Andi 25.10.2026" },
        new() { Schluessel = ParameterSchluessel.PruefhinweiseLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L31" },
        new() { Schluessel = ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, Wert = "18", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L15, Volljährigkeit nach § 2 BGB" },
        new() { Schluessel = ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, Wert = "36", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L19 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AenderungsprotokollVerdichtenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L19, Verdichtung nach der Zehn-Jahres-Simulation (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.StartseiteVorschauTage, Wert = "28", GueltigAb = new(2000, 1, 1), Quelle = "FEVAD, Zehn-Jahres-Simulation (Startseite)" },
        new() { Schluessel = ParameterSchluessel.UnfallAnzeigefristTage, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "§ 193 Abs. 4 SGB VII" },
        new() { Schluessel = ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate, Wert = "24", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L38 (Festlegung Andi 25.10.2026)" },
        new() { Schluessel = ParameterSchluessel.AbrechnungenAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L13" },
        new() { Schluessel = ParameterSchluessel.EntschaedigungsantraegeBelegeLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L44" },
        new() { Schluessel = ParameterSchluessel.EntschaedigungsantraegeAbgelehntLoeschenNachMonaten, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L44" },
        new() { Schluessel = ParameterSchluessel.ArbeitgeberbescheinigungenAufbewahrungJahre, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L45" },
        new() { Schluessel = ParameterSchluessel.ArbeitgebererstattungenAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L46" },
        new() { Schluessel = ParameterSchluessel.BankverbindungLoeschenNachMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L37 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.VerbandbuchAufbewahrungJahre, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "DGUV Vorschrift 1 § 24 Abs. 6 (fünf Jahre), Löschkonzept FEVAD L36" },
        new() { Schluessel = ParameterSchluessel.SachschadenLoeschenNachJahren, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L40 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.MeldungFotosHoechstAnzahl, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.MeldungFotoHoechstMegabyte, Wert = "8", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.UnfaelleLoeschenNachJahren, Wert = "5", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L36, angelehnt an DGUV Vorschrift 1 § 24 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, Wert = "6", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L5 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.AusstattungLoeschenNachRueckgabeMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L14 (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L10a (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.KleiderkammerAussonderungVorlaufMonate, Wert = "6", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.EinsatznummerFormat, Wert = Einsatznummer.StandardFormat, GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.LeistungsabzeichenBelastungsuebungMonate, Wert = "12", GueltigAb = new(2026, 3, 12), Quelle = "Richtlinie Leistungsabzeichen BW, Neufassung 2026" },
        new() { Schluessel = ParameterSchluessel.LeistungsspangeWarnungTage, Wert = "365", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.LeistungsspangeGruppenstaerke, Wert = "9", GueltigAb = new(2026, 1, 1), Quelle = "DJF-Richtlinie Leistungsspange" },
        new() { Schluessel = ParameterSchluessel.StandortfortbildungStunden, Wert = "40", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 2 / VwV Feuerwehrausbildung BW" },
        new() { Schluessel = ParameterSchluessel.UmsatzsteuerRegelsatzProzent, Wert = "19", GueltigAb = new(2021, 1, 1), Quelle = "§ 12 Abs. 1 UStG" },
        new() { Schluessel = ParameterSchluessel.UmsatzsteuerErmaessigterSatzProzent, Wert = "7", GueltigAb = new(2021, 1, 1), Quelle = "§ 12 Abs. 2 UStG" },
        new() { Schluessel = ParameterSchluessel.KleinunternehmerVorjahrEuro, Wert = "25000", GueltigAb = new(2025, 1, 1), Quelle = "§ 19 Abs. 1 UStG i. d. F. Jahressteuergesetz 2024" },
        new() { Schluessel = ParameterSchluessel.KleinunternehmerLaufendesJahrEuro, Wert = "100000", GueltigAb = new(2025, 1, 1), Quelle = "§ 19 Abs. 1 UStG i. d. F. Jahressteuergesetz 2024" },
        new() { Schluessel = ParameterSchluessel.KameradschaftskasseUmsatzgrenzeEuro, Wert = "45000", GueltigAb = new(2000, 1, 1), Quelle = "Angabe Kämmerei Adelberg (Andi 25.10.2026); R 4.1 Abs. 5 KStR, LFV-Ratgeber Kameradschaftskassen 04/2026" },
        new() { Schluessel = ParameterSchluessel.KoerperschaftsteuerFreibetragEuro, Wert = "5000", GueltigAb = new(2009, 1, 1), Quelle = "§ 24 KStG" },
        new() { Schluessel = ParameterSchluessel.KasseAusschussbeschlussUeberEuro, Wert = "100", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung Andi 25.10.2026: Ausgaben über 100 € beschließt der Ausschuss (nur Hinweis, keine Sperre)" },
        new() { Schluessel = ParameterSchluessel.KasseBelegMaxMegabyte, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KasseSteuerWarnungProzent, Wert = "80", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlagspaket V03 (Warnung bei 80 % der Umsatzgrenze)" },
        new() { Schluessel = ParameterSchluessel.EhrenamtspauschaleEuro, Wert = "840", GueltigAb = new(2021, 1, 1), Quelle = "§ 3 Nr. 26a EStG (Jahressteuergesetz 2020)" },
        new() { Schluessel = ParameterSchluessel.EhrenamtspauschaleEuro, Wert = "960", GueltigAb = new(2026, 1, 1), Quelle = "§ 3 Nr. 26a EStG (Steueränderungsgesetz 2025)" },
        new() { Schluessel = ParameterSchluessel.KassendatenAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "§ 147 Abs. 1 und 3 AO; Löschkonzept FEVAD L42" },
        new() { Schluessel = ParameterSchluessel.KasseBestandsverzeichnisAbWertEuro, Wert = "500", GueltigAb = new(2000, 1, 1), Quelle = "Satzung FF Adelberg § 12 Abs. 3; LFV-Ratgeber Kameradschaftskassen (Grenze mit der Gemeinde festlegen)" },
        new() { Schluessel = ParameterSchluessel.KasseZuwendungAnnahmeAbEuro, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "§ 78 Abs. 4 GemO; Wertgrenze ggf. aus der Hauptsatzung der Gemeinde" },
        new() { Schluessel = ParameterSchluessel.KasseZuwendungAufbewahrungJahre, Wert = "10", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD L43 (Spendenregister, Geberdaten)" },
        new() { Schluessel = ParameterSchluessel.KassePrueferHinweisMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Vorschlagspaket V09; frei wählbar" },
        new() { Schluessel = ParameterSchluessel.KassenanordnungEntschaedigungBuchungsstelle, Wert = "Produkt 12.60 Brandschutz, Sachkonto [von der Kämmerei eintragen]", GueltigAb = new(2000, 1, 1), Quelle = "Platzhalter bis zur Angabe der Kämmerei (SAP)" },
        new() { Schluessel = ParameterSchluessel.SteuerAufwandsentschaedigungMonatEuro, Wert = "250", GueltigAb = new(2021, 1, 1), Quelle = "R 3.12 Abs. 3 LStR" },
        new() { Schluessel = ParameterSchluessel.SteuerAufwandsentschaedigungMonatEuro, Wert = "275", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlagspaket V34 (2026: 275 € je Monat; vor Echtbetrieb prüfen)" },
        new() { Schluessel = ParameterSchluessel.SteuerUebungsleiterEuro, Wert = "3000", GueltigAb = new(2021, 1, 1), Quelle = "§ 3 Nr. 26 EStG (Jahressteuergesetz 2020)" },
        new() { Schluessel = ParameterSchluessel.SteuerUebungsleiterEuro, Wert = "3300", GueltigAb = new(2026, 1, 1), Quelle = "§ 3 Nr. 26 EStG (Steueränderungsgesetz 2025)" },
        new() { Schluessel = ParameterSchluessel.SteuerSonstigeLeistungenFreigrenzeEuro, Wert = "256", GueltigAb = new(2000, 1, 1), Quelle = "§ 22 Nr. 3 Satz 2 EStG" },
        new() { Schluessel = ParameterSchluessel.EntschaedigungssatzungBezeichnung, Wert = "Feuerwehr-Entschädigungssatzung Adelberg vom 22.11.2018", GueltigAb = new(2019, 1, 1), Quelle = "Satzung vom 22.11.2018, in Kraft seit 01.01.2019" },
        .. EntschaedigungssaetzeAdelberg,
    ];

    /// <summary>
    /// Quelle der früheren Beispielwerte (FwES Bad Überkingen 2022). Beim Start werden Parameter mit dieser Quelle durch die Sätze
    /// der Entschädigungssatzung Adelberg ersetzt (<see cref="DatenbankInitialisierung"/>).
    /// </summary>
    public const string FruehereBeispielquelle = Entschaedigung.Abrechnungen.PlatzhalterQuelle + ": Beispielwert aus der FwES Bad Überkingen 2022, durch die Entschädigungssatzung Adelberg ersetzen";

    private const string Satzung = "Feuerwehr-Entschädigungssatzung Adelberg vom 22.11.2018";

    /// <summary>
    /// Sätze der Feuerwehr-Entschädigungssatzung Adelberg vom 22.11.2018, in Kraft seit 01.01.2019, seitdem unverändert (Andi, 25.10.2026).
    /// Einsatz 6 € je angefangene halbe Stunde (§ 1 Abs. 1), besondere Aufgaben wie die Brandsicherheitswache 9 € je angefangene Stunde
    /// (§ 1 Abs. 5, F24), Lehrgang 10 € für die ersten drei und 5 € je weitere drei Stunden (§ 2 Abs. 1), Übungen unentgeltlich,
    /// Übungsleiter 5 € je Übung (§ 2 Abs. 3), Pauschalen nach § 3. Über zwei Tage in tatsächlicher Höhe (§ 1 Abs. 4, § 2 Abs. 5).
    /// </summary>
    private static IEnumerable<Parameter> EntschaedigungssaetzeAdelberg =>
    [
        Satz(Entschaedigungssaetze.Schluessel(Entschaedigungssaetze.VorgangEinsatz, "SatzStunde"), "12", "§ 1 Abs. 1 (6 € je angefangene halbe Stunde)"),
        Satz(Entschaedigungssaetze.Schluessel(Entschaedigungssaetze.VorgangEinsatz, "RundungMinuten"), "30", "§ 1 Abs. 1 (je angefangene halbe Stunde)"),
        Satz(Entschaedigungssaetze.EinsatzBisEinsatzbereitschaft, "0", "§ 1 Abs. 2 (von der Alarmierung bis zum Einsatzende)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Sonderdienst), "SatzStunde"), "9", "§ 1 Abs. 5 (besondere Aufgaben, z. B. Brandsicherheitswache)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Sonderdienst), "RundungMinuten"), "60", "§ 1 Abs. 5 (je angefangene Stunde)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Brandsicherheitswache), "SatzStunde"), "9", "§ 1 Abs. 5 (besondere Aufgaben, Brandsicherheitswache, F24)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Brandsicherheitswache), "RundungMinuten"), "60", "§ 1 Abs. 5 (je angefangene Stunde)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "StaffelErsteStunden"), "3", "§ 2 Abs. 1"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "StaffelErsterBetrag"), "10", "§ 2 Abs. 1 (10 € für die ersten drei Stunden)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "StaffelWeitereStunden"), "3", "§ 2 Abs. 1"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "StaffelWeitererBetrag"), "5", "§ 2 Abs. 1 (5 € je weitere drei Stunden)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "RundungMinuten"), "60", "§ 2 Abs. 2 (angefangene Stunden auf volle aufgerundet)"),
        Satz(Antragsberechnung.ZuschlagSchluessel, "7", "§ 2 Abs. 1 (zusätzlich 7 € je Stunde bei tatsächlichem Verdienstausfall)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), Entschaedigung.Abrechnungen.NurAufAntragFeld), "1", "§ 2 Abs. 1 (auf Antrag; Vorschlagspaket V32)"),
        Satz(Antragsberechnung.HaushaltSatzSchluessel, "7.5", "§ 4 (haushaltsführende Personen je Stunde)"),
        Satz(Antragsberechnung.HaushaltMaxStundenSchluessel, "8", "§ 4 (höchstens acht Stunden je Werktag)"),
        Satz(Entschaedigungssaetze.Schluessel(nameof(DienstArt.Uebung), "SatzLeitung"), "5", "§ 2 Abs. 3 (Übungsleiter 5 € je Übung)"),
        Satz(Entschaedigungssaetze.TatsaechlicheHoeheUeberTage, "2", "§ 1 Abs. 4, § 2 Abs. 5; § 16 Abs. 4 FwG (mehr als zwei aufeinanderfolgende Tage)"),
        Satz(Entschaedigungssaetze.PauschalePraefix + "KDT", "500", "§ 3"),
        Satz(Entschaedigungssaetze.PauschalePraefix + "STV", "250", "§ 3"),
        Satz(Entschaedigungssaetze.PauschalePraefix + "GW", "150", "§ 3"),
        Satz(Entschaedigungssaetze.PauschalePraefix + "JW", "150", "§ 3"),
        new() { Schluessel = Entschaedigungssaetze.PauschaleZwoelftel, Wert = "1", GueltigAb = new(2019, 1, 1), Quelle = "Vorschlagspaket V35 (Zwölftelung je angefangenen Monat; Satzung regelt es nicht)" },
        new() { Schluessel = Antragsberechnung.KilometerSchluessel, Wert = "0.35", GueltigAb = new(2019, 1, 1), Quelle = "§ 6 LRKG über § 2 Abs. 4 der Satzung (vor Echtbetrieb prüfen)" },
        new() { Schluessel = Entschaedigungssaetze.AntragsfristMonate, Wert = "6", GueltigAb = new(2019, 1, 1), Quelle = "Vorschlagspaket V32 (nur Hinweis, keine Ausschlussfrist)" },
        new() { Schluessel = Entschaedigungssaetze.AbrechnungMonate, Wert = "6", GueltigAb = new(2019, 1, 1), Quelle = "Vorschlag halbjährlich, mit der Gemeinde abstimmen" },
    ];

    private static Parameter Satz(string schluessel, string wert, string fundstelle) =>
        new() { Schluessel = schluessel, Wert = wert, GueltigAb = new(2019, 1, 1), Quelle = $"{Satzung}, {fundstelle}" };

    /// <summary>Quelle der Kostensätze aus dem fiktiven Entwurf; beginnt mit „Platzhalter“, damit FEVAD sie als vorläufig kennzeichnet (F3).</summary>
    public const string KostensatzPlatzhalterQuelle = Kostensatz.PlatzhalterQuelle + ": fiktive Feuerwehr-Kostenersatzsatzung Adelberg (Entwurf 25.10.2026), Anlage 1";

    /// <summary>
    /// Kostensätze ohne Fahrzeugbezug aus dem fiktiven Entwurf der Kostenersatzsatzung Adelberg (Personal nach § 34 Abs. 5 FwG kalkuliert,
    /// Geräte und Verbrauch nach Singen 2024). Eine echte Satzung gibt es noch nicht (Andi, 25.10.2026); die Fahrzeugsätze nach VOKeFw
    /// hängen am Fahrzeug und werden dort gepflegt. Angelegt wird nur, was es mit gleicher Art und Bezeichnung noch nicht gibt.
    /// </summary>
    public static IReadOnlyList<Kostensatz> Kostensaetze =>
    [
        Kosten(KostensatzArt.Personal, "Einsatzkraft", Kosteneinheit.Stunde, 19.50m, "Nr. 1.1"),
        Kosten(KostensatzArt.Personal, "Brandsicherheitswache", Kosteneinheit.Stunde, 16.50m, "Nr. 1.2"),
        Kosten(KostensatzArt.Geraet, "Tragkraftspritze", Kosteneinheit.Pauschale, 64.70m, "Nr. 3.1"),
        Kosten(KostensatzArt.Geraet, "Tauchpumpe", Kosteneinheit.Pauschale, 2.70m, "Nr. 3.2"),
        Kosten(KostensatzArt.Geraet, "Stromerzeuger", Kosteneinheit.Pauschale, 22.50m, "Nr. 3.3"),
        Kosten(KostensatzArt.Geraet, "Motorsäge", Kosteneinheit.Pauschale, 15.50m, "Nr. 3.4"),
        Kosten(KostensatzArt.Geraet, "Überdrucklüfter", Kosteneinheit.Pauschale, 20.00m, "Nr. 3.5"),
        Kosten(KostensatzArt.Geraet, "Wärmebildkamera", Kosteneinheit.Pauschale, 10.00m, "Nr. 3.6"),
        Kosten(KostensatzArt.Verbrauch, "Ölbindemittel Straße (kg)", Kosteneinheit.Stueck, 1.72m, "Nr. 4.1"),
        Kosten(KostensatzArt.Verbrauch, "Schaummittel (l)", Kosteneinheit.Stueck, 6.65m, "Nr. 4.2"),
        Kosten(KostensatzArt.Gebuehr, "Verwaltungsgebühr Kostenersatzbescheid", Kosteneinheit.Pauschale, 50.00m, "Nr. 5.1"),
        Klasse("MTW", 34, "§ 1 Nr. 4"),
        Klasse("MLF", 128, ""),
        Klasse("TSF-W", 99, ""),
        Klasse("LF 10", 172, ""),
        Klasse("HLF 10", 198, "§ 1 Nr. 10"),
        Klasse("TLF 2000", 155, "§ 1 Nr. 14"),
        Klasse("TLF 3000", 172, ""),
        Klasse("TLF 4000", 169, ""),
    ];

    /// <summary>Quelle der Stundensätze je Fahrzeugklasse; amtliche Sätze, keine Platzhalter.</summary>
    public const string VokefwQuelle = "VOKeFw i. d. F. vom 11.03.2024 (GBl. 2024 Nr. 21)";

    private static Kostensatz Klasse(string klasse, decimal betrag, string fundstelle) =>
        new() { Art = KostensatzArt.Fahrzeugklasse, Bezeichnung = klasse, Einheit = Kosteneinheit.Stunde, Betrag = betrag, GueltigAb = new(2024, 3, 11),
            Quelle = fundstelle.Length == 0 ? VokefwQuelle : $"{VokefwQuelle} {fundstelle}" };

    private static Kostensatz Kosten(KostensatzArt art, string bezeichnung, Kosteneinheit einheit, decimal betrag, string nummer) =>
        new() { Art = art, Bezeichnung = bezeichnung, Einheit = einheit, Betrag = betrag, GueltigAb = new(2000, 1, 1), Quelle = $"{KostensatzPlatzhalterQuelle} {nummer}" };

    /// <summary>
    /// Haushaltsstellen der Feuerwehr im Gemeindehaushalt (V29). Kostenstelle, Innenaufträge und Sachkonten sind fiktiv nach dem
    /// Kontenrahmen BW gewählt; die echten Werte liefert die Kämmerei (SAP) und werden hier geändert.
    /// </summary>
    public static IReadOnlyList<Fevad.Domain.Haushalt.Haushaltsstelle> Haushaltsstellen =>
    [
        Stelle("EH-4211", "Unterhaltung Feuerwehrhaus", "4211", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 10),
        Stelle("EH-4221", "Unterhaltung Fahrzeuge und Geräte", "4221", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 20),
        Stelle("EH-4221-AS", "Unterhaltung Atemschutz", "4221", "IA 126010 Atemschutz", Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 30),
        Stelle("EH-4251", "Haltung von Fahrzeugen (Treibstoff, Versicherung)", "4251", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 40),
        Stelle("EH-4271", "Dienst- und Schutzkleidung", "4271", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 50),
        Stelle("EH-4291", "Verbrauchsmaterial und Sonstiges", "4291", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 60),
        Stelle("EH-4291-JF", "Jugendfeuerwehr", "4291", "IA 126020 Jugendfeuerwehr", Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 70),
        Stelle("EH-4318", "Zuschüsse an die Kameradschaftskasse", "4318", null, Fevad.Domain.Haushalt.Haushaltsteil.Ergebnishaushalt, 80),
        Stelle("FH-7831", "Erwerb beweglicher Sachen über der Wertgrenze", "7831", null, Fevad.Domain.Haushalt.Haushaltsteil.Finanzhaushalt, 90),
        Stelle("FH-7832", "Erwerb von Feuerwehrfahrzeugen", "7832", null, Fevad.Domain.Haushalt.Haushaltsteil.Finanzhaushalt, 100),
        Stelle("FH-6811", "Investitionszuwendungen vom Land (Z-Feu)", "6811", null, Fevad.Domain.Haushalt.Haushaltsteil.Finanzhaushalt, 110, einzahlung: true),
    ];

    /// <summary>Haushaltsstelle im Produkt Brandschutz mit der fiktiven Kostenstelle der Feuerwehr.</summary>
    private static Fevad.Domain.Haushalt.Haushaltsstelle Stelle(string code, string bezeichnung, string sachkonto, string? innenauftrag,
        Fevad.Domain.Haushalt.Haushaltsteil teil, int sortierung, bool einzahlung = false) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Produkt = "12.60.01 Brandschutz", Kostenstelle = "12600000", Innenauftrag = innenauftrag,
        Sachkonto = sachkonto, Teil = teil, Einzahlung = einzahlung, Sortierung = sortierung,
    };

    /// <summary>
    /// Kostenansätze für Vorschlagszeilen (V29). Die Sätze der Werkstätten in Göppingen sind nicht bekannt und stehen auf 0 („Satz fehlt“),
    /// bis Andi oder die Kämmerei sie einträgt; Fremdprüfungen sind grobe Richtwerte.
    /// </summary>
    public static IReadOnlyList<Fevad.Domain.Haushalt.Kostenansatz> Kostenansaetze =>
    [
        Ansatz("KA-SCHLAUCH", "Schlauchpflege Schlauchwerkstatt Göppingen je Schlauch", Fevad.Domain.Haushalt.Kostenbezug.Schlauchwerkstatt, 0m, "EH-4221", "Satz fehlt – bei der Schlauchwerkstatt des Landkreises erfragen", 10),
        Ansatz("KA-AS-WERK", "Atemschutzwerkstatt FW Göppingen je Gerät", Fevad.Domain.Haushalt.Kostenbezug.Atemschutzwerkstatt, 0m, "EH-4221-AS", "Satz fehlt – bei der Feuerwehr Göppingen erfragen", 20),
        Ansatz("KA-FUELLUNG", "Flaschenfüllung FW Göppingen je Flasche", Fevad.Domain.Haushalt.Kostenbezug.Flaschenfuellung, 0m, "EH-4221-AS", "Satz fehlt – bei der Feuerwehr Göppingen erfragen", 30),
        Ansatz("KA-FAHRZEUG", "Versicherung je Fahrzeug im Dienst", Fevad.Domain.Haushalt.Kostenbezug.JeFahrzeug, 0m, "EH-4251", "Satz fehlt – Beitrag laut Versicherungsschein der Gemeinde (Kfz-Steuer entfällt für Feuerwehrfahrzeuge)", 40),
        Ansatz("KA-WAESCHE", "Wäsche Einsatzkleidung je Teil", Fevad.Domain.Haushalt.Kostenbezug.Waesche, 0m, "EH-4271", "Satz fehlt – Preis der Wäscherei eintragen", 50),
        Ansatz("KA-PR-LEITER", "Fremdprüfung tragbare Leitern je Leiter", Fevad.Domain.Haushalt.Kostenbezug.Fremdpruefung, 35m, "EH-4221", "Richtwert, Angebot einholen", 60, "LEITER"),
        Ansatz("KA-PR-HYDR", "Fremdprüfung hydraulisches Rettungsgerät je Gerät", Fevad.Domain.Haushalt.Kostenbezug.Fremdpruefung, 180m, "EH-4221", "Richtwert, Angebot einholen", 70, "HYDRAULIK"),
        Ansatz("KA-PR-HEBE", "Fremdprüfung Hebekissen je Kissen", Fevad.Domain.Haushalt.Kostenbezug.Fremdpruefung, 60m, "EH-4221", "Richtwert, Angebot einholen", 80, "HEBEKISSEN"),
        Ansatz("KA-PR-TOR", "Prüfung kraftbetätigtes Tor je Tor", Fevad.Domain.Haushalt.Kostenbezug.Fremdpruefung, 150m, "EH-4211", "Richtwert, Wartungsvertrag prüfen", 90, "GH-TOR"),
    ];

    /// <summary>Kostenansatz mit Bezugsgröße und Ziel-Haushaltsstelle.</summary>
    private static Fevad.Domain.Haushalt.Kostenansatz Ansatz(string code, string bezeichnung, Fevad.Domain.Haushalt.Kostenbezug bezug, decimal satz,
        string stelle, string quelle, int sortierung, string? geraeteart = null) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Bezug = bezug, Satz = satz, HaushaltsstelleCode = stelle, Quelle = quelle, Sortierung = sortierung, GeraeteartCode = geraeteart,
    };

    /// <summary>
    /// Fördergegenstände der VwV Z-Feu 2025–2031 (V30). Die Festbeträge sind Vorbelegung: Die Anlage der VwV lag dem Projekt nicht
    /// im Wortlaut vor; der HLF-10-Betrag stammt aus dem Bescheid Adelberg. Vor einem Antrag am Original prüfen und hier ändern.
    /// </summary>
    public static IReadOnlyList<Foerdergegenstand> Foerdergegenstaende =>
    [
        Gegenstand("ZF-MTW", "Mannschaftstransportwagen MTW", 25000m, 10, 10),
        Gegenstand("ZF-TSF", "Tragkraftspritzenfahrzeug TSF", 50000m, 10, 20),
        Gegenstand("ZF-TSF-W", "Tragkraftspritzenfahrzeug-Wasser TSF-W", 75000m, 20, 30),
        Gegenstand("ZF-MLF", "Mittleres Löschfahrzeug MLF", 95000m, 20, 40),
        Gegenstand("ZF-LF10", "Löschgruppenfahrzeug LF 10", 135000m, 20, 50),
        Gegenstand("ZF-HLF10", "Hilfeleistungslöschgruppenfahrzeug HLF 10", 150000m, 20, 60),
        Gegenstand("ZF-HLF20", "Hilfeleistungslöschgruppenfahrzeug HLF 20", 175000m, 20, 70),
        Gegenstand("ZF-TLF3000", "Tanklöschfahrzeug TLF 3000", 140000m, 20, 80),
        Gegenstand("ZF-GWL1", "Gerätewagen Logistik GW-L1", 60000m, 20, 90),
        Gegenstand("ZF-GH", "Feuerwehrhaus je Stellplatz", 90000m, 20, 100),
    ];

    /// <summary>Fördergegenstand mit Laufzeit der VwV bis Ende 2031.</summary>
    private static Foerdergegenstand Gegenstand(string code, string bezeichnung, decimal festbetrag, int zweckbindung, int sortierung) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Festbetrag = festbetrag, ZweckbindungJahre = zweckbindung, GueltigBis = new(2031, 12, 31), Sortierung = sortierung,
        Fundstelle = "VwV Z-Feu vom 21.08.2024, Anlage Festbeträge – Vorbelegung, vor dem Antrag am Original prüfen",
    };

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
        Schlauchwerkstatt(Art("DRUCKSCHLAUCH", "Druckschlauch", "DGUV 305-002 Tab. 1, DIN 14811", null, 50,
            Position(Pruefart.NachGebrauch, 0, null, "Druckprüfung nach Wäsche bestanden", "Prüfdruck [bar]"))),
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
        Art("GH-TOR", "Kraftbetätigtes Tor (Gerätehaus)", "ASR A1.7, DGUV Information 208-022 (Vorschlag)", null, 300,
            Position(Pruefart.Funktionspruefung, 12, "Fachfirma (Sachkundiger)", "Sicherheitseinrichtungen (Schließkantensicherung, Lichtschranke) wirksam", "Notentriegelung funktioniert", "Prüfbuch des Tors abgezeichnet")),
        Art("GH-ABGAS", "Abgasabsauganlage (Gerätehaus)", "DGUV Information 205-008, Herstellerangaben (Vorschlag)", null, 310,
            Position(Pruefart.Funktionspruefung, 12, "Fachfirma", "Absaugleistung in Ordnung", "Schläuche und Düsen ohne Schaden", "Abreißkupplung löst")),
        Art("GH-ELEKTRO", "Ortsfeste elektrische Anlage (Gerätehaus)", "DGUV Vorschrift 3, VDE 0105-100 (Vorschlag)", null, 320,
            Position(Pruefart.Elektro, 48, "Elektrofachkraft (Fachfirma)", "Prüfprotokoll der Fachfirma liegt vor", "Fehlerstrom-Schutzschalter ausgelöst")),
        Art("GH-NOTSTROM", "Netzersatz-Einspeisung (Gerätehaus)", "DIN VDE 0100-551, Herstellerangaben (Vorschlag)", null, 330,
            Position(Pruefart.Funktionspruefung, 12, "Fachfirma", "Umschaltung auf Netzersatz geprüft", "Einspeisestecker ohne Schaden")),
        Art("GH-ABSTURZ", "Anschlagpunkt Absturzsicherung (Gerätehaus)", "DGUV Regel 112-198, DIN EN 795 (Vorschlag)", null, 340,
            Position(Pruefart.Sichtpruefung, 12, "Sachkundiger (Fachfirma)", "Anschlagpunkt fest und ohne Korrosion", "Kennzeichnung lesbar")),
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
    /// Vorbelegung der Lebensdauer in Jahren und der Höchstzahl der Wäschen für PSA (V63). Übliche Herstellerangaben als Vorschlag;
    /// die Angaben des tatsächlichen Herstellers gehen vor und werden an der Geräteart gepflegt.
    /// </summary>
    public static IReadOnlyDictionary<string, (int Jahre, int? Waeschen)> PsaLebensdauer { get; } = new Dictionary<string, (int, int?)>
    {
        ["PSA-HELM"] = (15, null),
        ["PSA-EJ"] = (10, 25),
        ["PSA-EH"] = (10, 25),
    };

    /// <summary>
    /// Gerätearten für persönliche Schutzausrüstung, die die Kleiderkammer als Einzelstück führt. Prüfung und
    /// Nutzungsdauer richten sich nach Herstellerangabe; die Werte sind Vorschläge und vor Gebrauch zu prüfen.
    /// </summary>
    public static IReadOnlyList<Geraeteart> PsaGeraetearten =>
    [
        Art("PSA-HELM", "Feuerwehrhelm", "DIN EN 443, Herstellerangaben (Vorschlag)", PsaLebensdauer["PSA-HELM"].Jahre, 200,
            Position(Pruefart.Sichtpruefung, 12, null, "Helmschale ohne Risse, Verformung oder Hitzeschäden", "Innenausstattung und Kinnriemen intakt", "Visier und Nackenschutz in Ordnung", "Herstelldatum im Helm lesbar")),
        Waeschen(Art("PSA-EJ", "Einsatzjacke", "DIN EN 469, HuPF Teil 2, Herstellerangaben (Vorschlag)", PsaLebensdauer["PSA-EJ"].Jahre, 210,
            Position(Pruefart.Sichtpruefung, 12, null, "Obermaterial ohne Risse, Löcher oder Verfärbung", "Nähte und Reflexstreifen intakt", "Verschlüsse funktionsfähig", "Membran ohne sichtbaren Schaden")), PsaLebensdauer["PSA-EJ"].Waeschen),
        Waeschen(Art("PSA-EH", "Einsatzhose", "DIN EN 469, HuPF Teil 2, Herstellerangaben (Vorschlag)", PsaLebensdauer["PSA-EH"].Jahre, 220,
            Position(Pruefart.Sichtpruefung, 12, null, "Obermaterial ohne Risse, Löcher oder Verfärbung", "Nähte und Reflexstreifen intakt", "Verschlüsse und Hosenträger funktionsfähig")), PsaLebensdauer["PSA-EH"].Waeschen),
    ];

    /// <summary>
    /// Gerätearten für Atemschutz und Füllanlage (V61) nach FwDV 7, vfdb-Richtlinie 0804 und Herstellerangaben. Intervalle sind
    /// Vorschläge; maßgeblich sind die Angaben von Hersteller und Atemschutzwerkstatt (in Adelberg: Feuerwehr Göppingen).
    /// CSA gibt es in Adelberg nicht; die Art ist angelegt, aber inaktiv.
    /// </summary>
    public static IReadOnlyList<Geraeteart> AtemschutzGeraetearten =>
    [
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA", "Pressluftatmer", "FwDV 7, vfdb 0804, Herstellerangaben (Vorschlag)", null, 150,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Gereinigt und desinfiziert", "Hochdruck-Dichtprüfung bestanden", "Warnsignal in Ordnung"),
            Position(Pruefart.Funktionspruefung, 6, "Atemschutzgerätewart", "Sichtprüfung ohne Befund", "Hochdruck-Dichtprüfung bestanden", "Warnsignal in Ordnung"),
            Position(Pruefart.Hersteller, 72, "Hersteller oder autorisierte Werkstatt", "Grundüberholung durchgeführt"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-LA", "Lungenautomat", "FwDV 7, vfdb 0804, Herstellerangaben (Vorschlag)", null, 152,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Gereinigt und desinfiziert", "Dichtprüfung bestanden"),
            Position(Pruefart.Funktionspruefung, 24, "Atemschutzgerätewart", "Sichtprüfung ohne Befund", "Dichtprüfung bestanden", "Öffnungsdruck in Ordnung"),
            Position(Pruefart.Hersteller, 72, "Hersteller oder autorisierte Werkstatt", "Grundüberholung durchgeführt"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-MASKE", "Atemschutz-Vollmaske", "DIN EN 136, vfdb 0804, Herstellerangaben (Vorschlag)", null, 154,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Gereinigt und desinfiziert", "Dichtprüfung bestanden"),
            Position(Pruefart.Funktionspruefung, 24, "Atemschutzgerätewart", "Sichtprüfung ohne Befund", "Dichtprüfung bestanden", "Ausatemventil in Ordnung"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-FLASCHE", "Atemluftflasche Stahl", "BetrSichV, DIN EN ISO 18119, Herstellerangaben (Vorschlag)", null, 160,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Sichtprüfung ohne Befund", "Gefüllt", "Fülldruck [bar]"),
            Position(Pruefart.Hersteller, 120, "Zugelassene Überwachungsstelle", "Wiederkehrende Prüfung (Festigkeit) bestanden", "Flaschenventil grundüberholt"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-FLASCHE-CFK", "Atemluftflasche Composite", "BetrSichV, DIN EN ISO 11623, Herstellerangaben (Vorschlag)", null, 162,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Sichtprüfung ohne Befund", "Gefüllt", "Fülldruck [bar]"),
            Position(Pruefart.Hersteller, 60, "Zugelassene Überwachungsstelle", "Wiederkehrende Prüfung (Festigkeit) bestanden", "Flaschenventil grundüberholt", "Nutzungsende laut Prägung eingetragen"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-TAFEL", "Atemschutzüberwachungstafel", "FwDV 7, Herstellerangaben (Vorschlag)", null, 170,
            Position(Pruefart.Funktionspruefung, 12, null, "Uhr und Anzeige in Ordnung", "Beschriftung lesbar", "Halterung vollständig"))),
        Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("PA-BWM", "Bewegungsmelder", "DIN EN 1127-1 / Herstellerangaben (Vorschlag)", null, 172,
            Position(Pruefart.Funktionspruefung, 12, null, "Voralarm und Hauptalarm auslösbar", "Batterie in Ordnung"))),
        Inaktiv(Ruecklaufweg(Ruecklauf.Atemschutzwerkstatt, Art("CSA", "Chemikalienschutzanzug", "DIN EN 943, vfdb 0801, Herstellerangaben (Vorschlag)", null, 180,
            Position(Pruefart.NachGebrauch, 0, "Atemschutzwerkstatt", "Gereinigt", "Dichtprüfung bestanden"),
            Position(Pruefart.Funktionspruefung, 12, "Atemschutzwerkstatt", "Sichtprüfung ohne Befund", "Dichtprüfung bestanden")))),
        Art("PA-KOMPRESSOR", "Atemluftkompressor", "DIN EN 12021, BetrSichV, Herstellerangaben (Vorschlag)", null, 190,
            Position(Pruefart.Funktionspruefung, 6, null, "Luftqualität nach DIN EN 12021 geprüft", "Kohlendioxid [ppm]", "Wasser [mg/m³]", "Öl [mg/m³]"),
            Position(Pruefart.Hersteller, 12, "Hersteller oder autorisierte Werkstatt", "Wartung durchgeführt", "Betriebsstunden [h]")),
    ];

    /// <summary>
    /// Bauteile mit eigener Lebensdauer (V61): sechs Jahre eingebaut, acht Jahre ab Herstellung (Vorschlag nach Herstellerangaben).
    /// Paare aus Bauteilart und Code der Geräteart.
    /// </summary>
    public static IReadOnlyList<(Bauteilart Art, string Geraeteart)> Bauteilarten =>
    [
        (new() { Code = "LA-MEMBRAN", Bezeichnung = "Membran Lungenautomat", EingebautMonate = 72, GesamtMonate = 96, Grundlage = "Herstellerangaben (Vorschlag)", Sortierung = 10 }, "PA-LA"),
        (new() { Code = "MASKE-AUSATEM", Bezeichnung = "Ausatemventilscheibe Vollmaske", EingebautMonate = 72, GesamtMonate = 96, Grundlage = "Herstellerangaben (Vorschlag)", Sortierung = 20 }, "PA-MASKE"),
        (new() { Code = "MASKE-SPRECH", Bezeichnung = "Sprechmembran Vollmaske", EingebautMonate = 72, GesamtMonate = 96, Grundlage = "Herstellerangaben (Vorschlag)", Sortierung = 30 }, "PA-MASKE"),
    ];

    /// <summary>Gemeinsamer Anfang der Codes für Atemluftflaschen (Stahl und Composite).</summary>
    public const string FlaschenCodeAnfang = "PA-FLASCHE";

    /// <summary>Code der Geräteart Atemluftkompressor (Füllanlage).</summary>
    public const string KompressorCode = "PA-KOMPRESSOR";

    /// <summary>Name der Atemschutzwerkstatt, an die Adelberg abgibt (Vorbelegung im Werkstattauftrag).</summary>
    public const string AtemschutzwerkstattName = "Feuerwehr Göppingen, Atemschutzwerkstatt";

    /// <summary>Name der Schlauchwerkstatt, an die Adelberg abgibt (Vorbelegung im Werkstattauftrag).</summary>
    public const string SchlauchwerkstattName = "Feuerwehr Göppingen, Schlauchwerkstatt";

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

    /// <summary>Fördergeber für Zuwendungen nach der VwV Z-Feu.</summary>
    public const string FoerdergeberZfeu = "Land BW (Z-Feu)";

    /// <summary>Fahrzeuge der FF Adelberg (öffentliche Angaben, Stand 2026; Baujahre nach Andi, 25.10.2026).</summary>
    public static IReadOnlyList<Fahrzeug> Fahrzeuge =>
    [
        // HLF 10: Pilotfall Zweckbindung, 20 Jahre bis 2043, Festbetrag 150.000 € (Andi, 25.10.2026).
        new() { Funkrufname = "Florian Adelberg 43", Typ = "HLF 10", Kostenklasse = "HLF 10", Hersteller = "Mercedes-Benz Atego", Aufbau = "Ziegler", Baujahr = 2023, IndienstAm = new(2023, 11, 30),
            Foerdermittel = 150000m, Foerdergeber = FoerdergeberZfeu, ZweckbindungBis = new(2043, 11, 30) },
        new() { Funkrufname = "Florian Adelberg 21", Typ = "TLF 8/18", Kostenklasse = "TLF 2000", Hersteller = "Mercedes-Benz Unimog 1300 L", Aufbau = "Ziegler", Baujahr = 1986 },
        new() { Funkrufname = "Florian Adelberg 19", Typ = "MTW", Kostenklasse = "MTW", Hersteller = "Mercedes-Benz Sprinter", Baujahr = 1996, Sitzplaetze = 9 },
    ];

    /// <summary>Schläuche gehen nach dem Einsatz zur Schlauchwerkstatt (in Adelberg: Göppingen).</summary>
    private static Geraeteart Schlauchwerkstatt(Geraeteart art)
    {
        art.Ruecklauf = Ruecklauf.Schlauchwerkstatt;
        return art;
    }

    private static Geraeteart Waeschen(Geraeteart art, int? hoechstWaeschen)
    {
        art.HoechstWaeschen = hoechstWaeschen;
        return art;
    }

    private static Geraeteart Ruecklaufweg(Ruecklauf ruecklauf, Geraeteart art)
    {
        art.Ruecklauf = ruecklauf;
        return art;
    }

    private static Geraeteart Inaktiv(Geraeteart art)
    {
        art.Aktiv = false;
        return art;
    }

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
