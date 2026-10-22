using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>Standardwerte für Ehrungen, Beförderungswege und Laufbahn-Parameter.</summary>
public static class LaufbahnStammdaten
{
    private const string Satzung = "Feuerwehrsatzung Adelberg";
    private const string VwvEhrenzeichen = "VwV Feuerwehr-Ehrenzeichen BW, Nr. 4";

    public static IReadOnlyList<Parameter> Parameter =>
    [
        new() { Schluessel = LaufbahnParameter.AufnahmeEinsatzAbAlter, Wert = "17", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.EinsatzdienstAbAlter, Wert = "18", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.JugendEndeAlter, Wert = "18", GueltigAb = new(2015, 2, 26), Quelle = Satzung },
        new() { Schluessel = LaufbahnParameter.VorlaufMonate, Wert = "3", GueltigAb = new(2026, 1, 1), Quelle = "Festlegung FF Adelberg (vorläufig)" },
        new() { Schluessel = LaufbahnParameter.EhrungJugendAbAlter, Wert = "14", GueltigAb = new(2018, 3, 28), Quelle = VwvEhrenzeichen },

        // Altersabteilung zählt nach VwV nur mit Aufgaben nach Konzeption 65plus und Bestätigung des Kommandanten (dann 70).
        new() { Schluessel = LaufbahnParameter.EhrungAltersabteilungBisAlter, Wert = "0", GueltigAb = new(2018, 3, 28), Quelle = VwvEhrenzeichen + " (0 = Altersabteilung zählt nicht)" },
    ];

    public static IReadOnlyList<Ehrungsart> Ehrungsarten =>
    [
        Ehrenzeichen("FEZ15", "Feuerwehr-Ehrenzeichen in Bronze", "FEZ Bronze", 15, 10),
        Ehrenzeichen("FEZ25", "Feuerwehr-Ehrenzeichen in Silber", "FEZ Silber", 25, 20),
        Ehrenzeichen("FEZ40", "Feuerwehr-Ehrenzeichen in Gold", "FEZ Gold", 40, 30),
        Ehrenzeichen("FEZ50", "Feuerwehr-Ehrenzeichen in Gold, besondere Ausführung", "FEZ Gold 50", 50, 40),
        new() { Code = "FEZSO", Bezeichnung = "Feuerwehr-Ehrenzeichen, Sonderstufe", Kurzbezeichnung = "FEZ Sonder", Grundlage = EhrungGrundlage.Ohne, Verleiher = "Innenministerium", Sortierung = 50 },
        Jubilaeum(10, 110), Jubilaeum(20, 120), Jubilaeum(25, 125), Jubilaeum(30, 130), Jubilaeum(40, 140), Jubilaeum(50, 150), Jubilaeum(60, 160),
    ];

    private static Ehrungsart Ehrenzeichen(string code, string bezeichnung, string kurz, int jahre, int sortierung) => new()
    {
        Code = code, Bezeichnung = bezeichnung, Kurzbezeichnung = kurz, Grundlage = EhrungGrundlage.Einsatzdienst,
        Jahre = jahre, Reihe = "FEZ", Verleiher = "Landratsamt", VorlaufMonate = 6, Sortierung = sortierung,
    };

    private static Ehrungsart Jubilaeum(int jahre, int sortierung) => new()
    {
        Code = $"JUB{jahre}", Bezeichnung = $"Jubiläum {jahre} Jahre Feuerwehr", Kurzbezeichnung = $"{jahre} Jahre",
        Grundlage = EhrungGrundlage.Zugehoerigkeit, Jahre = jahre, Reihe = "JUB", Verleiher = "Feuerwehr Adelberg",
        VorlaufMonate = 3, Sortierung = sortierung,
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
        foreach (var parameter in Parameter)
        {
            if (!await db.Parameter.IgnoreQueryFilters().AnyAsync(p => p.Schluessel == parameter.Schluessel, ct))
            {
                db.Parameter.Add(parameter);
            }
        }

        var arten = await db.Ehrungsarten.IgnoreQueryFilters().Select(e => e.Code).ToListAsync(ct);
        db.Ehrungsarten.AddRange(Ehrungsarten.Where(e => !arten.Contains(e.Code)));

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
