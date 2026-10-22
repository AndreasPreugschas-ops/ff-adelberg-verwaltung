using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
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
    public static IReadOnlyList<Abteilung> Abteilungen =>
    [
        new() { Code = "EA", Bezeichnung = "Einsatzabteilung", Sortierung = 10 },
        new() { Code = "JF", Bezeichnung = "Jugendfeuerwehr", Sortierung = 20 },
        new() { Code = "AA", Bezeichnung = "Altersabteilung", Sortierung = 30 },
    ];

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

    public static IReadOnlyList<Funktion> Funktionen =>
    [
        new() { Code = "KDT", Bezeichnung = "Kommandant/in", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Kommandant, Sortierung = 10 },
        new() { Code = "STV", Bezeichnung = "Stellvertretende/r Kommandant/in", AmtszeitJahre = 5, VorgeschlageneRolle = Rollen.Stellvertreter, Sortierung = 20 },
        new() { Code = "GF", Bezeichnung = "Gruppenführer/in", VorgeschlageneRolle = Rollen.Fuehrungskraft, Sortierung = 30 },
        new() { Code = "GW", Bezeichnung = "Gerätewart/in", VorgeschlageneRolle = Rollen.Geraetewart, Sortierung = 40 },
        new() { Code = "AGW", Bezeichnung = "Atemschutzgerätewart/in", VorgeschlageneRolle = Rollen.Atemschutzgeraetewart, Sortierung = 50 },
        new() { Code = "JW", Bezeichnung = "Jugendfeuerwehrwart/in", VorgeschlageneRolle = Rollen.Jugendwart, Sortierung = 60 },
        new() { Code = "KAS", Bezeichnung = "Kassier/in", VorgeschlageneRolle = Rollen.Kassier, Sortierung = 70 },
        new() { Code = "SF", Bezeichnung = "Schriftführer/in", VorgeschlageneRolle = Rollen.Schriftfuehrer, Sortierung = 80 },
        new() { Code = "KW", Bezeichnung = "Kleiderwart/in", Sortierung = 90 },
        new() { Code = "KP", Bezeichnung = "Kassenprüfer/in", Sortierung = 100 },
        new() { Code = "AUS", Bezeichnung = "Mitglied Feuerwehrausschuss", AmtszeitJahre = 5, Sortierung = 110 },
    ];

    public static IReadOnlyList<Qualifikation> Qualifikationen =>
    [
        new() { Code = "TM1", Bezeichnung = "Truppmann Teil 1", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Sortierung = 10 },
        new() { Code = "TM2", Bezeichnung = "Truppmann Teil 2", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Standort, Sortierung = 20 },
        new() { Code = "SPRF", Bezeichnung = "Sprechfunker", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Sortierung = 30 },
        new() { Code = "AGT", Bezeichnung = "Atemschutzgeräteträger", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "AGT", Sortierung = 40 },
        new() { Code = "MA", Bezeichnung = "Maschinist", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "MA", Sortierung = 50 },
        new() { Code = "TF", Bezeichnung = "Truppführer", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Kreis, Fe2Funktion = "TF", Sortierung = 60 },
        new() { Code = "GF", Bezeichnung = "Gruppenführer", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Fe2Funktion = "GF", Sortierung = 70 },
        new() { Code = "ZF", Bezeichnung = "Zugführer", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Fe2Funktion = "ZF", Sortierung = 80 },
        new() { Code = "GW", Bezeichnung = "Gerätewart", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Sortierung = 90 },
        new() { Code = "AGW", Bezeichnung = "Atemschutzgerätewart", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Land, Sortierung = 100 },
        new() { Code = "FS-C", Bezeichnung = "Fahrerlaubnis Klasse C", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Fe2Funktion = "C", Sortierung = 110 },
        new() { Code = "FS-CE", Bezeichnung = "Fahrerlaubnis Klasse CE", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Sortierung = 120 },
        new() { Code = "FS-B", Bezeichnung = "Fahrerlaubnis Klasse B", Art = QualifikationArt.Fahrerlaubnis, Sortierung = 105 },
        new() { Code = "FS-C1", Bezeichnung = "Fahrerlaubnis Klasse C1", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60, Sortierung = 108 },
        new() { Code = "FW-FS", Bezeichnung = "Feuerwehr-Fahrberechtigung (bis 4,75 t / 7,5 t)", Art = QualifikationArt.Fahrerlaubnis, Sortierung = 125 },
        new() { Code = "EH", Bezeichnung = "Erste-Hilfe-Ausbildung", Art = QualifikationArt.Lehrgang, Ebene = AusbildungsEbene.Standort, GueltigkeitMonate = 24, Sortierung = 130 },
    ];

    public static IReadOnlyList<Nachweisart> Nachweisarten =>
    [
        new() { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", IntervallMonate = 6, Pflicht = NachweisPflicht.MitFahrerlaubnis, Sortierung = 10, Beschreibung = "Sichtkontrolle des Führerscheins durch die Wehrführung (Halterpflicht der Gemeinde)." },
        new() { Code = "UVV", Bezeichnung = "Jährliche Unterweisung (UVV)", IntervallMonate = 12, Pflicht = NachweisPflicht.AlleEinsatzkraefte, Sortierung = 20, Beschreibung = "Unterweisung nach DGUV Vorschrift 49." },
    ];

    public static IReadOnlyList<StatistikKategorie> StatistikKategorien =>
    [
        new() { Code = "BR", Bezeichnung = "Brand", Sortierung = 10 },
        new() { Code = "TH", Bezeichnung = "Technische Hilfe", Sortierung = 20 },
        new() { Code = "RD", Bezeichnung = "Rettungsdienstlicher Notfalleinsatz", Sortierung = 30 },
        new() { Code = "SO", Bezeichnung = "Sonstige", Sortierung = 40 },
        new() { Code = "FA", Bezeichnung = "Fehlalarm", Sortierung = 50 },
    ];

    public static IReadOnlyList<Parameter> Parameter =>
    [
        new() { Schluessel = ParameterSchluessel.ProbezeitMonate, Wert = "12", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.AltersgrenzeEinsatzdienst, Wert = "65", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.AltersabteilungAbAlter, Wert = "55", GueltigAb = new(2015, 2, 26), Quelle = "Feuerwehrsatzung Adelberg" },
        new() { Schluessel = ParameterSchluessel.FristWarnungTage, Wert = "90", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.PruefungWarnungTage, Wert = "30", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg" },
        new() { Schluessel = ParameterSchluessel.AtemschutzUebungIntervallMonate, Wert = "12", GueltigAb = new(2026, 1, 1), Quelle = "FwDV 7" },
        new() { Schluessel = ParameterSchluessel.AlarmdatenAufbewahrungMonate, Wert = "6", GueltigAb = new(2021, 8, 1), Quelle = "IM BW, Hinweise Datenschutz Alarmierungssysteme 08/2021" },
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
        new() { Schluessel = ParameterSchluessel.DienstSerieMaxMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.DienstRueckmeldungSchlussStunden, Wert = "0", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KalenderVorschauMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.KalenderRueckblickMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, Wert = "3", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.RueckmeldungLoeschenMonate, Wert = "12", GueltigAb = new(2000, 1, 1), Quelle = "Löschkonzept FEVAD (Vorschlag, DSB-Freigabe offen)" },
        new() { Schluessel = ParameterSchluessel.EinsatznummerFormat, Wert = Einsatznummer.StandardFormat, GueltigAb = new(2000, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = ParameterSchluessel.StandortfortbildungStunden, Wert = "40", GueltigAb = new(2000, 1, 1), Quelle = "FwDV 2 / VwV Feuerwehrausbildung BW" },
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

    /// <summary>Fahrzeuge der FF Adelberg (öffentliche Angaben, Stand 2026).</summary>
    public static IReadOnlyList<Fahrzeug> Fahrzeuge =>
    [
        new() { Funkrufname = "Florian Adelberg 43", Typ = "HLF 10", Hersteller = "Mercedes-Benz Atego", Aufbau = "Ziegler", Baujahr = 2023, IndienstAm = new(2023, 11, 30) },
        new() { Funkrufname = "Florian Adelberg 21", Typ = "TLF 8/18", Hersteller = "Mercedes-Benz Unimog 1300 L", Aufbau = "Ziegler", Baujahr = 1986 },
        new() { Funkrufname = "Florian Adelberg 19", Typ = "MTW", Hersteller = "Mercedes-Benz Sprinter", Baujahr = 1999, Sitzplaetze = 9 },
    ];
}
