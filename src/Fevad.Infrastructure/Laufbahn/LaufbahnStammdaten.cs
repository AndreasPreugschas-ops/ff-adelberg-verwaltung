using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>Standardwerte für Ehrungen, Beförderungswege und Laufbahn-Parameter.</summary>
public static class LaufbahnStammdaten
{
    private const string Satzung = "Feuerwehrsatzung Adelberg";
    private const string VwvEhrenzeichen = "VwV Feuerwehr-Ehrenzeichen BW i. d. F. vom 27.02.2025 (gültig bis 25.04.2032), Nr. 4";

    /// <summary>Quelle des früheren Standardwerts 0, an der ein unveränderter Eintrag erkannt wird.</summary>
    private const string AlterStandardQuelle = "VwV Feuerwehr-Ehrenzeichen BW, Nr. 4 (0 = Altersabteilung zählt nicht)";

    private const string WegLandratsamt = "Vorschlag Bürgermeisteramt im Einvernehmen mit dem Kommandanten an das Landratsamt Göppingen";
    private const string WegVerband = "Kommandant oder Bürgermeister → Kreisfeuerwehrverband → LFV BW";
    private const string WegDfv = WegVerband + " → DFV";
    private const string WegJugend = "Kreisjugendfeuerwehrwart → Landesjugendfeuerwehr";

    public static IReadOnlyList<Parameter> Parameter =>
    [
        new() { Schluessel = LaufbahnParameter.AufnahmeEinsatzAbAlter, Wert = "17", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.EinsatzdienstAbAlter, Wert = "18", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.JugendEndeAlter, Wert = "18", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.VorlaufMonate, Wert = "3", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = LaufbahnParameter.EhrungJugendAbAlter, Wert = "14", GueltigAb = new(2018, 3, 28), Quelle = VwvEhrenzeichen },

        // Nur Abschnitte mit bestätigten Aufgaben nach Konzeption 65plus zählen, dann bis 70 (Festlegung Andi 22.10.2026).
        new() { Schluessel = LaufbahnParameter.EhrungAltersabteilungBisAlter, Wert = "70", GueltigAb = new(2018, 3, 28), Quelle = VwvEhrenzeichen + " (nur mit Bestätigung Konzeption 65plus)" },
        new() { Schluessel = LaufbahnParameter.EhrungKindererziehungMonate, Wert = "12", GueltigAb = new(2018, 3, 28), Quelle = VwvEhrenzeichen + " (1 Jahr je Kind)" },
        new() { Schluessel = LaufbahnParameter.EhrungLeistungsspangeAbEintritt, Wert = "2018-01-01", GueltigAb = new(2018, 3, 28), Quelle = "VwV Feuerwehr-Ehrenzeichen BW Nr. 6; Leistungsspange als Pflicht (Festlegung Andi 22.10.2026)" },
        new() { Schluessel = LaufbahnParameter.EhrungAntragsschlussVerband, Wert = "30.11.", GueltigAb = new(2026, 1, 1), Quelle = "Vorschlag nach KFV-Ehrenordnung (für Göppingen erfragen), Anträge für das Folgejahr" },
    ];

    /// <summary>Ehrungsarten mit dem Code ihrer Vorstufe.</summary>
    public static IReadOnlyList<(Ehrungsart Art, string? Vorstufe)> Ehrungsarten =>
    [
        (Ehrenzeichen("FEZ15", "Feuerwehr-Ehrenzeichen in Bronze", "FEZ Bronze", 15, 10), null),
        (Ehrenzeichen("FEZ25", "Feuerwehr-Ehrenzeichen in Silber", "FEZ Silber", 25, 20), null),
        (Ehrenzeichen("FEZ40", "Feuerwehr-Ehrenzeichen in Gold", "FEZ Gold", 40, 30), null),
        (Ehrenzeichen("FEZ50", "Feuerwehr-Ehrenzeichen in Gold, Sonderstufe", "FEZ Gold Sonderstufe", 50, 40), null),
        (new() { Code = "FEZSO", Bezeichnung = "Feuerwehr-Ehrenzeichen für besondere Verdienste", Kurzbezeichnung = "FEZ Verdienst", Grundlage = EhrungGrundlage.Ohne, Verleiher = "Innenministerium", Antragsweg = "Bürgermeister → Landratsamt → Regierungspräsidium → Innenministerium, mit Begründung und Würdigung", Sortierung = 50 }, null),
        (Jubilaeum(10, 110), null), (Jubilaeum(20, 120), null), (Jubilaeum(25, 125), null), (Jubilaeum(30, 130), null),
        (Jubilaeum(40, 140), null), (Jubilaeum(50, 150), null), (Jubilaeum(60, 160), null),

        (Verband("LFV-EMS", "Ehrenmedaille des LFV BW in Silber", "LFV Silber", "LFV BW", "1 je 1.000 beitragszahlende Angehörige im Jahr", 210), null),
        (Verband("LFV-EMG", "Ehrenmedaille des LFV BW in Gold", "LFV Gold", "LFV BW", "1 je 3.000 beitragszahlende Angehörige im Jahr", 220), "LFV-EMS"),
        (Verband("DFV-EKB", "Deutsches Feuerwehr-Ehrenkreuz in Bronze", "DFV-EK Bronze", "DFV", "1 je 800 aktive Angehörige im Jahr", 230, WegDfv), null),
        (Verband("DFV-EKS", "Deutsches Feuerwehr-Ehrenkreuz in Silber", "DFV-EK Silber", "DFV", "1 je 1.000 aktive Angehörige im Jahr; auch ohne Bronze", 240, WegDfv), null),
        (Verband("DFV-EKG", "Deutsches Feuerwehr-Ehrenkreuz in Gold", "DFV-EK Gold", "DFV", "1 je 3.000 aktive Angehörige im Jahr", 250, WegDfv), "DFV-EKS"),
        (Verband("DFV-EM", "Deutsche Feuerwehr-Ehrenmedaille", "DFV-Ehrenmedaille", "DFV", "für Nicht-Feuerwehrangehörige; wie Ehrenkreuz Gold", 260, WegDfv), null),
        (new() { Code = "BEVS", Bezeichnung = "Ehrenzeichen für Verdienste um den Bevölkerungsschutz", Kurzbezeichnung = "Bevölkerungsschutz", Grundlage = EhrungGrundlage.Ohne, Verleiher = "Land BW", VorlaufWochen = 12, Sortierung = 270 }, null),

        (new()
        {
            Code = "JF-TN", Bezeichnung = "Traditionsnadel der Jugendfeuerwehr BW", Kurzbezeichnung = "JF-Traditionsnadel", Grundlage = EhrungGrundlage.Jugendfeuerwehr,
            Jahre = 2, Verleiher = "Jugendfeuerwehr BW", Antragsweg = WegJugend + "; Übergabe beim Übertritt in die Einsatzabteilung", VorlaufWochen = 6,
            NichtMitQualifikationen = [JugendabzeichenCode.Jugendflamme1, JugendabzeichenCode.Jugendflamme2, JugendabzeichenCode.Jugendflamme3, JugendabzeichenCode.Leistungsspange],
            Sortierung = 310,
        }, null),
        (Jugendnadel("JF-ENS", "Ehrennadel der Jugendfeuerwehr BW in Silber", "JF-Nadel Silber", "Jugendfeuerwehr BW", null, 6, 320), null),
        (Jugendnadel("JF-ENG", "Ehrennadel der Jugendfeuerwehr BW in Gold", "JF-Nadel Gold", "Jugendfeuerwehr BW", 2, 6, 330), "JF-ENS"),
        (Djf("DJF-ENS", "Ehrennadel der Deutschen Jugendfeuerwehr in Silber", "DJF-Nadel Silber", null, 340), null),
        (Djf("DJF-ENG", "Ehrennadel der Deutschen Jugendfeuerwehr in Gold", "DJF-Nadel Gold", 5, 350), "DJF-ENS"),
    ];

    private static Ehrungsart Ehrenzeichen(string code, string bezeichnung, string kurz, int jahre, int sortierung) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Kurzbezeichnung = kurz, Grundlage = EhrungGrundlage.Einsatzdienst,
        Jahre = jahre, Reihe = "FEZ", Verleiher = "Landratsamt", Antragsweg = WegLandratsamt, VorlaufWochen = 26, Sortierung = sortierung,
    };

    private static Ehrungsart Jubilaeum(int jahre, int sortierung) => new()
    {
        Code = $"JUB{jahre}", Bezeichnung = $"Jubiläum {jahre} Jahre Feuerwehr", Kurzbezeichnung = $"{jahre} Jahre",
        Grundlage = EhrungGrundlage.Zugehoerigkeit, Jahre = jahre, Reihe = "JUB", Verleiher = "Feuerwehr Adelberg",
        VorlaufWochen = 13, Sortierung = sortierung,
    };

    /// <summary>Verbandsehrung: nur von Hand, mit Kontingent und gemeinsamem Antragsschluss.</summary>
    private static Ehrungsart Verband(string code, string bezeichnung, string kurz, string verleiher, string kontingent, int sortierung, string antragsweg = WegVerband) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Kurzbezeichnung = kurz, Grundlage = EhrungGrundlage.Ohne, Verleiher = verleiher,
        Kontingent = kontingent, Antragsweg = antragsweg, MitAntragsschluss = true, VorlaufWochen = 8, Sortierung = sortierung,
    };

    /// <summary>Ehrennadel der Jugendfeuerwehr BW für 5 Jahre als Jugendfeuerwehrwart; Gold nach Silber mit Mindestabstand.</summary>
    private static Ehrungsart Jugendnadel(string code, string bezeichnung, string kurz, string verleiher, int? abstand, int vorlaufWochen, int sortierung) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Kurzbezeichnung = kurz, Grundlage = EhrungGrundlage.Funktion, FunktionCode = "JW", Jahre = 5,
        MindestabstandJahre = abstand, Verleiher = verleiher, Antragsweg = WegJugend, VorlaufWochen = vorlaufWochen, Sortierung = sortierung,
    };

    /// <summary>Ehrennadel der DJF: Voraussetzungen nicht als Jahre festgelegt, daher von Hand; Gold nach Silber mit Abstand.</summary>
    private static Ehrungsart Djf(string code, string bezeichnung, string kurz, int? abstand, int sortierung) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Kurzbezeichnung = kurz, Grundlage = EhrungGrundlage.Ohne, MindestabstandJahre = abstand,
        Verleiher = "Deutsche Jugendfeuerwehr", Antragsweg = WegJugend + " → DJF", VorlaufWochen = 12, Sortierung = sortierung,
    };

    /// <summary>
    /// Beförderungswege je Dienstgrad-Code. Quelle: Landratsamt Schwarzwald-Baar-Kreis, „Dienstgrade und Beförderungen
    /// der Feuerwehrangehörigen“ (Stand 11/2025) nach VwV Feuerwehrbekleidung BW.
    /// </summary>
    public static IReadOnlyList<(string Dienstgrad, Befoerderungsweg Weg)> Befoerderungswege =>
    [
        ("FM", new() { Bezeichnung = "Probezeit und Grundausbildung", ProbezeitBeendet = true, AlleQualifikationen = ["TM1"] }),
        ("OFM", new() { Bezeichnung = "Weg A", MindestjahreImDienstgrad = 10 }),
        ("OFM", new() { Bezeichnung = "Weg B", MindestjahreImDienstgrad = 3, AlleQualifikationen = ["TM2"], EineDerQualifikationen = ["MA", "AGT", "SPRF"] }),
        ("HFM", new() { Bezeichnung = "Weg A", MindestjahreImDienstgrad = 10 }),
        ("HFM", new() { Bezeichnung = "Weg B", MindestjahreImDienstgrad = 5, AlleQualifikationen = ["TF"] }),
        ("LM", new() { Bezeichnung = "Gruppenführer", AlleQualifikationen = ["GF"], UeberspringenErlaubt = true }),
        ("OLM", new() { MindestjahreImDienstgrad = 5 }),
        ("HLM", new() { MindestjahreImDienstgrad = 5 }),
        ("BM", new() { Bezeichnung = "Zugführer", AlleQualifikationen = ["ZF"], UeberspringenErlaubt = true }),
        ("OBM", new() { MindestjahreImDienstgrad = 5, Hinweis = "Höchstmöglichen Dienstgrad nach Funktion (Tabelle 2) beachten." }),
        ("HBM", new() { MindestjahreImDienstgrad = 5, Hinweis = "Höchstmöglichen Dienstgrad nach Funktion (Tabelle 2) beachten." }),
        ("LHBM", new() { NurVonHand = true, Hinweis = "Nur für Kommandanten in Gemeinden mit mehr als 15.000 Einwohnern." }),
    ];

    /// <summary>Legt fehlende Einträge an; Änderungen in der Oberfläche bleiben erhalten.</summary>
    public static async Task AnlegenAsync(AppDbContext db, CancellationToken ct)
    {
        // #10 hat die Altersabteilung mit 0 vorbelegt; den unveränderten Standard auf 70 heben, eigene Werte bleiben.
        var alterStandard = await db.Parameter.SingleOrDefaultAsync(
            p => p.Schluessel == LaufbahnParameter.EhrungAltersabteilungBisAlter && p.Wert == "0" && p.Quelle == AlterStandardQuelle, ct);
        if (alterStandard is not null)
        {
            var standard = Parameter.Single(p => p.Schluessel == LaufbahnParameter.EhrungAltersabteilungBisAlter);
            alterStandard.Wert = standard.Wert;
            alterStandard.Quelle = standard.Quelle;
        }

        foreach (var parameter in Parameter)
        {
            if (!await db.Parameter.IgnoreQueryFilters().AnyAsync(p => p.Schluessel == parameter.Schluessel, ct))
            {
                db.Parameter.Add(parameter);
            }
        }

        // Fehlende Ehrungsarten anlegen; die Vorstufe wird über den Code verknüpft (vorhandene oder neue).
        var arten = await db.Ehrungsarten.IgnoreQueryFilters().ToDictionaryAsync(e => e.Code, e => e.Id, ct);
        var neu = Ehrungsarten.Where(e => !arten.ContainsKey(e.Art.Code)).ToList();
        foreach (var (art, _) in neu)
        {
            arten[art.Code] = art.Id;
        }

        foreach (var (art, vorstufe) in neu)
        {
            art.VorstufeId = vorstufe is null ? null : arten.GetValueOrDefault(vorstufe);
            db.Ehrungsarten.Add(art);
        }

        // Leistungsspange als Qualifikation, falls die Personalverwaltung sie noch nicht angelegt hat.
        if (!await db.Qualifikationen.IgnoreQueryFilters().AnyAsync(q => q.Code == JugendabzeichenCode.Leistungsspange, ct))
        {
            db.Qualifikationen.Add(new Qualifikation
            {
                Code = JugendabzeichenCode.Leistungsspange, Bezeichnung = "Leistungsspange der Deutschen Jugendfeuerwehr",
                Art = QualifikationArt.Sonstiges, Sortierung = 200,
            });
        }

        // Wege nur beim ersten Start; danach pflegt sie der Kommandant, auch gelöschte bleiben gelöscht.
        if (!await db.Befoerderungswege.IgnoreQueryFilters().AnyAsync(ct))
        {
            var dienstgrade = await db.Dienstgrade.ToDictionaryAsync(d => d.Code, d => d.Id, ct);
            foreach (var (code, weg) in Befoerderungswege)
            {
                if (dienstgrade.TryGetValue(code, out var id))
                {
                    weg.DienstgradId = id;
                    db.Befoerderungswege.Add(weg);
                }
            }
        }
    }
}
