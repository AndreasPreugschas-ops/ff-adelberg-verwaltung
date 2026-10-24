using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Persistenz.Seed;

/// <summary>
/// Fiktive Testdaten zum Ausprobieren: fünf ungleich große Einsatzabteilungen (EA, EA2 bis EA5), rund 200 Personen, 25 Fahrzeuge mit
/// Beladung, Magazin, Prüfungen, Personalfristen, Kleiderkammer sowie Dienste mit Anwesenheit und Einsätze der letzten zwei Jahre.
/// Namen, Adressen und Kennzeichen sind erfunden. Läuft nur in der Entwicklung mit <c>Testdaten:Anlegen</c>. Sind schon Testdaten
/// eines älteren <see cref="Stand"/> da (Personalnummern beginnen mit "T-"), werden sie vollständig gelöscht und neu erzeugt.
/// Die Stammfahrzeuge aus <see cref="Stammdaten.Fahrzeuge"/> werden dabei gelöscht.
/// </summary>
public static class Testdaten
{
    public const string PersonalnummerPraefix = "T-";

    /// <summary>Erhöhen, wenn sich der Erzeuger ändert: beim nächsten Start werden die Testdaten ersetzt.</summary>
    public const int Stand = 3;

    private const string Kennzeichnung = "Testdaten (fiktiv";
    private static readonly string Kennung = $"{Kennzeichnung}, Stand {Stand})";
    private const string FahrzeugKennung = "(Testdaten)";
    private const string Kleiderlager = "Kleiderkammer Gerätehaus Innenstadt";

    public static async Task AnlegenAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Testdaten));

        if (await db.Mitglieder.IgnoreQueryFilters().AnyAsync(m => m.Personalnummer.StartsWith(PersonalnummerPraefix) && m.Notizen == Kennung, ct))
        {
            return;
        }

        if (await db.Mitglieder.IgnoreQueryFilters().AnyAsync(m => m.Personalnummer.StartsWith(PersonalnummerPraefix), ct))
        {
            await EntfernenAsync(db, ct);
            log.LogWarning("Testdaten eines älteren Stands gelöscht.");
        }

        var erzeuger = new Erzeuger(db, Ortszeit.Datum(DateTimeOffset.UtcNow));
        await erzeuger.KatalogeLadenAsync(ct);
        await erzeuger.StammfahrzeugeEntfernenAsync(ct);
        erzeuger.Erzeugen();
        await erzeuger.DienstbetriebErzeugenAsync(ct);
        await db.SaveChangesAsync(ct);

        log.LogWarning(
            "Testdaten Stand {Stand} angelegt: {Personen} Personen, {Fahrzeuge} Fahrzeuge, {Geraete} Geräte, {Dienste} Dienste, {Einsaetze} Einsätze. Nur für die Entwicklung!",
            Stand, erzeuger.Personen.Count, erzeuger.Fahrzeuge.Count, erzeuger.Geraete.Count, erzeuger.Dienste, erzeuger.Einsaetze);
    }

    /// <summary>
    /// Löscht alle Testdaten endgültig (an Soft-Delete und Protokoll vorbei), in der Reihenfolge der Fremdschlüssel. Was jemand
    /// selbst zu Testpersonen erfasst hat (Anwesenheit, Einsatzteilnahme, Anträge, Ausgaben), geht mit.
    /// </summary>
    private static async Task EntfernenAsync(AppDbContext db, CancellationToken ct)
    {
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);

        var m = await db.Mitglieder.IgnoreQueryFilters().Where(x => x.Personalnummer.StartsWith(PersonalnummerPraefix)).Select(x => x.Id).ToListAsync(ct);
        var f = await db.Fahrzeuge.IgnoreQueryFilters().Where(x => x.Bemerkung != null && x.Bemerkung.EndsWith(FahrzeugKennung)).Select(x => x.Id).ToListAsync(ct);
        var g = await db.Geraete.IgnoreQueryFilters()
            .Where(x => (x.Inventarnummer.StartsWith("T") && x.Inventarnummer.Length == 6) || (x.FahrzeugId != null && f.Contains(x.FahrzeugId.Value)))
            .Select(x => x.Id).ToListAsync(ct);

        await db.EinsatzTeilnahmen.IgnoreQueryFilters()
            .Where(x => m.Contains(x.MitgliedId) || x.Einsatz!.LeitstellenNummer!.StartsWith(PersonalnummerPraefix)
                || (x.EinsatzFahrzeug != null && f.Contains(x.EinsatzFahrzeug.FahrzeugId)))
            .ExecuteDeleteAsync(ct);
        await db.EinsatzFahrzeuge.IgnoreQueryFilters().Where(x => f.Contains(x.FahrzeugId)).ExecuteDeleteAsync(ct);
        await db.Einsaetze.IgnoreQueryFilters().Where(x => x.LeitstellenNummer!.StartsWith(PersonalnummerPraefix)).ExecuteDeleteAsync(ct);
        await db.Einsaetze.IgnoreQueryFilters().Where(x => x.EinsatzleiterId != null && m.Contains(x.EinsatzleiterId.Value))
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.EinsatzleiterId, (Guid?)null), ct);

        await db.Anwesenheiten.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.Rueckmeldungen.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.Dienste.IgnoreQueryFilters().Where(x => x.Bericht!.StartsWith(Kennzeichnung)).ExecuteDeleteAsync(ct);

        await db.AbrechnungsPosten.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.SitzungTeilnahmen.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.WahlBewerber.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.Antraege.IgnoreQueryFilters().Where(x => x.MitgliedId != null && m.Contains(x.MitgliedId.Value)).ExecuteDeleteAsync(ct);

        await db.Ausstattungen.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId) || (x.GeraetId != null && g.Contains(x.GeraetId.Value))).ExecuteDeleteAsync(ct);
        await db.Kleiderkammervorgaenge.IgnoreQueryFilters().Where(x => m.Contains(x.MitgliedId)).ExecuteDeleteAsync(ct);
        await db.Lagerbestaende.IgnoreQueryFilters().Where(x => x.Lagerort == Kleiderlager).ExecuteDeleteAsync(ct);

        await db.InventurPositionen.IgnoreQueryFilters().Where(x => g.Contains(x.GeraetId)).ExecuteDeleteAsync(ct);
        await db.Inventuren.IgnoreQueryFilters().Where(x => x.FahrzeugId != null && f.Contains(x.FahrzeugId.Value)).ExecuteDeleteAsync(ct);
        await db.Beschaffungen.IgnoreQueryFilters()
            .Where(x => x.Bemerkung!.StartsWith(Kennzeichnung) || AlteBeschaffungen.Contains(x.Bezeichnung)).ExecuteDeleteAsync(ct);
        await db.Maengel.IgnoreQueryFilters()
            .Where(x => (x.GeraetId != null && g.Contains(x.GeraetId.Value)) || (x.FahrzeugId != null && f.Contains(x.FahrzeugId.Value))).ExecuteDeleteAsync(ct);
        await db.Pruefungen.IgnoreQueryFilters()
            .Where(x => (x.GeraetId != null && g.Contains(x.GeraetId.Value)) || (x.FahrzeugId != null && f.Contains(x.FahrzeugId.Value))).ExecuteDeleteAsync(ct);
        await db.Geraete.IgnoreQueryFilters().Where(x => g.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.Fahrzeuge.IgnoreQueryFilters().Where(x => f.Contains(x.Id)).ExecuteDeleteAsync(ct);

        // Der erste Stand führte die Ortsabteilungen als Gruppen ABT1 bis ABT5.
        var alteGruppen = await db.Gruppen.IgnoreQueryFilters()
            .Where(x => x.Code.StartsWith("ABT") && x.Code.Length == 4 && x.Bezeichnung.StartsWith("Abteilung ")).Select(x => x.Id).ToListAsync(ct);
        await db.Dienste.IgnoreQueryFilters().Where(x => x.GruppeId != null && alteGruppen.Contains(x.GruppeId.Value))
            .ExecuteUpdateAsync(x => x.SetProperty(d => d.GruppeId, (Guid?)null), ct);
        await db.Gruppen.IgnoreQueryFilters().Where(x => alteGruppen.Contains(x.Id)).ExecuteDeleteAsync(ct);

        await db.Mitglieder.IgnoreQueryFilters().Where(x => m.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await transaktion.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Bezeichnungen der Beschaffungen aus Stand 1, der sie noch nicht gekennzeichnet hat.</summary>
    private static readonly string[] AlteBeschaffungen =
    [
        "Ersatz Motorsäge RW", "Löschgruppenfahrzeug LF 10 für Abteilung Buchenbach (Ersatz TSF)", "Einsatzjacken Größe L", "Hebekissensatz",
        "Wärmebildkamera für HLF 10",
    ];

    private sealed record Ortsabteilung(int Nummer, string Name, int Personen, string[] Strassen);

    private static readonly Ortsabteilung[] Orte =
    [
        new(1, "Innenstadt", 72, ["Hauptstraße", "Marktplatz", "Kirchgasse", "Schulstraße", "Bahnhofstraße", "Lindenweg", "Rathausgasse", "Klosterstraße"]),
        new(2, "Oberdorf", 44, ["Obere Gasse", "Brunnenstraße", "Am Hang", "Kelterweg", "Wiesenstraße"]),
        new(3, "Talhausen", 36, ["Talstraße", "Mühlweg", "Bachgasse", "Am Steg"]),
        new(4, "Buchenbach", 27, ["Buchenweg", "Waldstraße", "Hofäckerweg"]),
        new(5, "Steinach", 21, ["Steinacher Straße", "Grabenweg", "Im Winkel"]),
    ];

    private static readonly string[] VornamenM =
    [
        "Thomas", "Michael", "Andreas", "Stefan", "Markus", "Christian", "Martin", "Frank", "Jürgen", "Uwe", "Ralf", "Klaus",
        "Wolfgang", "Dieter", "Gerhard", "Manfred", "Helmut", "Rainer", "Peter", "Daniel", "Tobias", "Florian", "Sebastian",
        "Alexander", "Jonas", "Lukas", "Simon", "Fabian", "Patrick", "Dominik", "Philipp", "Kevin", "Marcel", "Jan", "Tim",
        "Niklas", "Felix", "Leon", "Paul", "Ben", "Noah", "Elias", "Moritz", "Julian", "David", "Benedikt", "Matthias",
        "Joachim", "Bernd", "Holger", "Karl", "Hans", "Werner", "Erwin", "Siegfried", "Heinz", "Oliver", "Sven", "Timo", "Marco",
    ];

    private static readonly string[] VornamenW =
    [
        "Julia", "Sarah", "Laura", "Lisa", "Anna", "Katharina", "Sabrina", "Melanie", "Nadine", "Stefanie", "Christina",
        "Sandra", "Nicole", "Claudia", "Andrea", "Petra", "Monika", "Lena", "Hannah", "Lea", "Emma", "Mia", "Sophie", "Marie",
        "Johanna", "Carina", "Jana", "Vanessa", "Franziska", "Theresa", "Ronja", "Paula", "Greta", "Ida",
    ];

    private static readonly string[] Nachnamen =
    [
        "Müller", "Schmid", "Maier", "Weber", "Wagner", "Fischer", "Bauer", "Schneider", "Hofmann", "Koch", "Richter", "Wolf",
        "Schäfer", "Kübler", "Hägele", "Bühler", "Kienzle", "Haug", "Scheufele", "Rapp", "Dangel", "Ziegler", "Eisele", "Pfeifer",
        "Rieger", "Mayer", "Bosch", "Kuhn", "Häußler", "Wörner", "Gaiser", "Schurr", "Fritz", "Kurz", "Lang", "Stohrer", "Bader",
        "Allmendinger", "Rommel", "Staiger", "Seiz", "Wahl", "Holl", "Kächele", "Bareiß", "Banzhaf", "Mössner", "Frank", "Graf",
        "Riedel", "Hahn", "Keller", "Köhler", "Sauter", "Beck", "Ott", "Uhl", "Single", "Nägele", "Schwarz", "Leibfritz",
        "Böhringer", "Brenner", "Dürr", "Eberle", "Föll", "Gölz", "Heinzelmann", "Knödler", "Laible", "Maurer", "Nuding",
        "Oesterle", "Pflüger", "Reichle", "Schaal", "Traub", "Unger", "Vogt", "Walz", "Zeller", "Ilg", "Jäger", "Kälberer",
    ];

    private static readonly string[] Arbeitgeber =
    [
        "Gemeinde (Bauhof)", "Autohaus Rieger (fiktiv)", "Schreinerei Haug (fiktiv)", "Maschinenbau Wörner GmbH (fiktiv)",
        "Kreisklinik (fiktiv)", "selbstständig", "Elektro Bühler (fiktiv)", "Landwirtschaft", "Logistikzentrum Süd (fiktiv)",
        "Stadtwerke (fiktiv)", "Schule", "Student/in", "Ruhestand",
    ];

    private static readonly string[] Berufe =
    [
        "Elektroniker", "Industriemechaniker", "Kfz-Mechatroniker", "Bankkaufmann/-frau", "Erzieher/in", "Landwirt/in",
        "Pflegefachkraft", "Ingenieur/in", "Schreiner/in", "Verwaltungsfachangestellte/r", "Anlagenmechaniker/in",
    ];

    private static readonly string[] Arbeitsorte = ["im Ort", "im Ort", "Göppingen", "Schorndorf", "Esslingen", "Stuttgart", "Kirchheim"];

    /// <summary>Fahrzeug je Ortsabteilung: Kennzahl nach dem Funkrufnamen-Schema BW, Typ, Fahrgestell, Aufbau, Baujahr, Sitze, Beladung.</summary>
    private sealed record Fahrzeugvorlage(int Abteilung, string Kennzahl, string Typ, string Fahrgestell, string? Aufbau, int Baujahr, int Sitze, string Beladung, bool Schwer);

    private static readonly Fahrzeugvorlage[] Fuhrpark =
    [
        new(1, "10", "KdoW", "VW Tiguan", null, 2021, 5, "KDOW", false),
        new(1, "11", "ELW 1", "Mercedes-Benz Sprinter 519", "GSF", 2019, 4, "ELW", true),
        new(1, "19", "MTW", "VW Crafter", null, 2018, 9, "MTW", false),
        new(1, "23", "TLF 3000", "MAN TGM 13.290", "Rosenbauer", 2015, 3, "TLF", true),
        new(1, "33", "DLK 23/12", "Mercedes-Benz Atego 1530", "Magirus", 2012, 3, "DLK", true),
        new(1, "44", "LF 20", "MAN TGM 15.290", "Ziegler", 2008, 9, "LF20", true),
        new(1, "46", "HLF 20", "Mercedes-Benz Atego 1530", "Rosenbauer", 2022, 9, "HLF20", true),
        new(1, "52", "RW", "MAN TGM 18.320", "Lentner", 2010, 3, "RW", true),
        new(1, "64", "GW-L2", "Mercedes-Benz Atego 1224", "Ziegler", 2016, 3, "GWL", true),
        new(1, "69", "GW-T", "Mercedes-Benz Atego 818", "Furtner & Ammer", 2005, 3, "GWT", true),
        new(2, "19", "MTW", "Ford Transit", null, 2014, 9, "MTW", false),
        new(2, "23", "TLF 16/25", "Mercedes-Benz 1124 AF", "Ziegler", 1996, 6, "TLF", true),
        new(2, "42", "LF 10", "Iveco Eurocargo 120E25", "Magirus", 2011, 9, "LF10", true),
        new(2, "43", "HLF 10", "Mercedes-Benz Atego 1327 AF", "Ziegler", 2020, 9, "HLF10", true),
        new(2, "59", "GW-L1", "Mercedes-Benz Sprinter 516", "Freytag", 2017, 3, "GWL", true),
        new(3, "19", "MTW", "VW T6", null, 2016, 9, "MTW", false),
        new(3, "42", "LF 10", "MAN TGL 10.220", "Schlingmann", 2013, 9, "LF10", true),
        new(3, "48", "TSF-W", "Iveco Daily 70C18", "Magirus", 2009, 6, "TSF", true),
        new(3, "49", "MLF", "Mercedes-Benz Atego 818", "Ziegler", 2019, 6, "MLF", true),
        new(4, "19", "MTW", "Mercedes-Benz Vito", null, 2012, 9, "MTW", false),
        new(4, "47", "TSF", "Ford Transit", "Albert Ziegler", 1994, 6, "TSF", false),
        new(4, "48", "TSF-W", "Mercedes-Benz Sprinter 519", "Rosenbauer", 2018, 6, "TSF", true),
        new(5, "19", "MTW", "Opel Movano", null, 2010, 9, "MTW", false),
        new(5, "47", "TSF", "VW Crafter", "Schlingmann", 2007, 6, "TSF", false),
        new(5, "49", "MLF", "Iveco Eurocargo 75E19", "Ziegler", 2021, 6, "MLF", true),
    ];

    /// <summary>Gerät auf einem Fahrzeug oder im Magazin: Geräteart-Code, Bezeichnung, Anzahl, Lagerort, Hersteller, Typ.</summary>
    private sealed record Ladung(string Art, string Bezeichnung, int Anzahl, string Lagerort, string? Hersteller = null, string? Typ = null);

    private static IEnumerable<Ladung> Beladung(string vorlage)
    {
        Ladung Schlauch(string groesse, int anzahl, string ort) => new("DRUCKSCHLAUCH", $"Druckschlauch {groesse}", anzahl, ort, "Parsch", groesse == "B" ? "B 75-20" : "C 42-15");
        Ladung Loescher(string ort) => new("FEUERLOESCHER", "Pulverlöscher PG 6", 1, ort, "Gloria", "PD6GA");

        switch (vorlage)
        {
            case "HLF20":
            case "HLF10":
                var gross = vorlage == "HLF20";
                return
                [
                    new("LEITER", "Steckleiter 4-teilig", 1, "Dach", "Günzburger", "4-tlg."),
                    new("LEITER", gross ? "Schiebleiter 3-teilig" : "Multifunktionsleiter", 1, "Dach", "Günzburger"),
                    new("HALTEGURT", "Feuerwehr-Haltegurt", gross ? 6 : 4, "Mannschaftsraum", "Spanset", "Typ A"),
                    new("LEINE", "Feuerwehrleine 30 m", 4, "G1", "Mewa", "FL 30"),
                    new("SAUGSCHLAUCH", "Saugschlauch A 1,6 m", 4, "Dach", "Parsch"),
                    new("PUMPE", "Feuerlöschkreiselpumpe FPN 10-2000", 1, "Heck", vorlage == "HLF20" ? "Rosenbauer" : "Ziegler", "FPN 10-2000"),
                    new("ARMATUR", "Verteiler B-CBC", 2, "G2"),
                    new("ARMATUR", "Hohlstrahlrohr C", 4, "G2", "AWG", "Turbo-Spritze 2"),
                    new("ARMATUR", "Sammelstück A-2B", 1, "G4"),
                    new("ARMATUR", "Standrohr 2B", 1, "G4"),
                    Schlauch("B", gross ? 8 : 6, "G4"),
                    Schlauch("C", gross ? 12 : 8, "G2"),
                    new("HYDRAULIK", "Rettungsschere", 1, "G3", "Weber Rescue", "RSX 200-107"),
                    new("HYDRAULIK", "Rettungsspreizer", 1, "G3", "Weber Rescue", "SP 49"),
                    new("HYDRAULIK", "Rettungszylinder", 2, "G3", "Weber Rescue", "RZ 1-1250"),
                    new("HYDRAULIK", "Hydraulikaggregat", 1, "G3", "Weber Rescue", "E-Force 2"),
                    new("HEBEKISSEN", "Hebekissen 12 t", 2, "G5", "Vetter", "V 12"),
                    new("STROMERZEUGER", "Stromerzeuger 8 kVA", 1, "G1", "Eisemann", "BSKA 8"),
                    new("ELEKTRO", "Flutlichtstrahler LED", 2, "G1", "Setolite"),
                    new("ELEKTRO", "Kabeltrommel 50 m", 2, "G1"),
                    new("ELEKTRO", "Tauchpumpe TP 4/1", 1, "G6", "Mast", "TP 4-1"),
                    new("MOTORSAEGE", "Motorsäge", 1, "G6", "Stihl", "MS 462"),
                    new("SONSTIGES", "Wärmebildkamera", 1, "Mannschaftsraum", "Dräger", "UCF 9000"),
                    new("SONSTIGES", "Überdrucklüfter", 1, "Heck", "Leader", "MT 236"),
                    new("PA", "Pressluftatmer", 4, "Mannschaftsraum", "Dräger", "PSS 7000"),
                    new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 4, "Mannschaftsraum", "Dräger"),
                    .. (gross ? [new Ladung("SPRUNGPOLSTER", "Sprungpolster SP 16", 1, "G5", "Vetter", "SP 16")] : Array.Empty<Ladung>()),
                    Loescher("G1"),
                    new("FEUERLOESCHER", "CO2-Löscher 5 kg", 1, "G1", "Gloria", "KS5SB"),
                ];
            case "LF20":
            case "LF10":
                var lf20 = vorlage == "LF20";
                return
                [
                    new("LEITER", "Steckleiter 4-teilig", 1, "Dach", "Günzburger", "4-tlg."),
                    new("LEITER", lf20 ? "Schiebleiter 3-teilig" : "Multifunktionsleiter", 1, "Dach", "Günzburger"),
                    new("HALTEGURT", "Feuerwehr-Haltegurt", lf20 ? 6 : 4, "Mannschaftsraum", "Spanset", "Typ A"),
                    new("LEINE", "Feuerwehrleine 30 m", 4, "G1", "Mewa", "FL 30"),
                    new("SAUGSCHLAUCH", "Saugschlauch A 1,6 m", 4, "Dach", "Parsch"),
                    new("PUMPE", "Feuerlöschkreiselpumpe FPN 10-2000", 1, "Heck", "Ziegler", "FPN 10-2000"),
                    new("ARMATUR", "Verteiler B-CBC", 2, "G2"),
                    new("ARMATUR", "Hohlstrahlrohr C", 4, "G2", "AWG", "Turbo-Spritze 2"),
                    new("ARMATUR", "Standrohr 2B", 1, "G4"),
                    Schlauch("B", lf20 ? 10 : 6, "G4"),
                    Schlauch("C", lf20 ? 12 : 8, "G2"),
                    new("STROMERZEUGER", "Stromerzeuger 5 kVA", 1, "G1", "Eisemann", "T 5.4"),
                    new("ELEKTRO", "Flutlichtstrahler LED", 2, "G1", "Setolite"),
                    new("ELEKTRO", "Tauchpumpe TP 4/1", 1, "G6", "Mast", "TP 4-1"),
                    new("MOTORSAEGE", "Motorsäge", 1, "G6", "Stihl", "MS 362"),
                    new("PA", "Pressluftatmer", 4, "Mannschaftsraum", "Dräger", "PSS 7000"),
                    new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 4, "Mannschaftsraum", "Dräger"),
                    Loescher("G1"),
                ];
            case "TLF":
                return
                [
                    new("LEITER", "Steckleiter 2-teilig", 1, "Dach", "Günzburger"),
                    new("HALTEGURT", "Feuerwehr-Haltegurt", 2, "Mannschaftsraum", "Spanset", "Typ A"),
                    new("LEINE", "Feuerwehrleine 30 m", 2, "G1", "Mewa", "FL 30"),
                    new("PUMPE", "Feuerlöschkreiselpumpe FPN 10-2000", 1, "Heck", "Rosenbauer", "N 25"),
                    new("ARMATUR", "Verteiler B-CBC", 1, "G2"),
                    new("ARMATUR", "Hohlstrahlrohr C", 3, "G2", "AWG", "Turbo-Spritze 2"),
                    Schlauch("B", 4, "G4"),
                    Schlauch("C", 6, "G2"),
                    new("SONSTIGES", "Wärmebildkamera", 1, "Mannschaftsraum", "MSA", "Evolution 6000"),
                    new("PA", "Pressluftatmer", 2, "Mannschaftsraum", "Dräger", "PSS 7000"),
                    new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 2, "Mannschaftsraum", "Dräger"),
                    Loescher("G1"),
                ];
            case "TSF":
            case "MLF":
                var mlf = vorlage == "MLF";
                return
                [
                    new("LEITER", "Steckleiter 4-teilig", 1, "Dach", "Günzburger", "4-tlg."),
                    new("HALTEGURT", "Feuerwehr-Haltegurt", 2, "Mannschaftsraum", "Spanset", "Typ A"),
                    new("LEINE", "Feuerwehrleine 30 m", 2, "G1", "Mewa", "FL 30"),
                    new("SAUGSCHLAUCH", "Saugschlauch A 1,6 m", 4, "Dach", "Parsch"),
                    mlf
                        ? new("PUMPE", "Feuerlöschkreiselpumpe FPN 10-1000", 1, "Heck", "Ziegler", "FPN 10-1000")
                        : new("PUMPE", "Tragkraftspritze PFPN 10-1000", 1, "Heck", "Rosenbauer", "Fox 4"),
                    new("ARMATUR", "Verteiler B-CBC", 1, "G2"),
                    new("ARMATUR", "Hohlstrahlrohr C", 2, "G2", "AWG", "Turbo-Spritze 2"),
                    new("ARMATUR", "Standrohr 2B", 1, "G2"),
                    Schlauch("B", 4, "G1"),
                    Schlauch("C", 6, "G2"),
                    new("ELEKTRO", "Flutlichtstrahler LED", 1, "G1", "Setolite"),
                    new("ELEKTRO", "Tauchpumpe TP 4/1", 1, "G1", "Mast", "TP 4-1"),
                    .. (mlf ? [new Ladung("STROMERZEUGER", "Stromerzeuger 5 kVA", 1, "G1", "Eisemann", "T 5.4")] : Array.Empty<Ladung>()),
                    new("PA", "Pressluftatmer", 2, "Mannschaftsraum", "Dräger", "PSS 4000"),
                    new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 2, "Mannschaftsraum", "Dräger"),
                    Loescher("G1"),
                ];
            case "DLK":
                return
                [
                    new("HALTEGURT", "Feuerwehr-Haltegurt", 2, "Korb", "Spanset", "Typ A"),
                    new("LEINE", "Feuerwehrleine 30 m", 2, "G1", "Mewa", "FL 30"),
                    new("SONSTIGES", "Krankentragehalterung", 1, "G2", "Magirus"),
                    new("ELEKTRO", "Flutlichtstrahler LED", 2, "Korb", "Setolite"),
                    Loescher("G1"),
                ];
            case "RW":
                return
                [
                    new("LEITER", "Multifunktionsleiter", 1, "Dach", "Günzburger"),
                    new("HALTEGURT", "Feuerwehr-Haltegurt", 2, "Mannschaftsraum", "Spanset", "Typ A"),
                    new("HYDRAULIK", "Rettungsschere", 1, "G3", "Lukas", "S 788"),
                    new("HYDRAULIK", "Rettungsspreizer", 1, "G3", "Lukas", "SP 777"),
                    new("HYDRAULIK", "Rettungszylinder", 2, "G3", "Lukas", "R 422"),
                    new("HYDRAULIK", "Hydraulikaggregat", 1, "G3", "Lukas", "P 650"),
                    new("HEBEKISSEN", "Hebekissen", 3, "G5", "Vetter", "V 24"),
                    new("STROMERZEUGER", "Stromerzeuger 13 kVA", 1, "G1", "Eisemann", "BSKA 14"),
                    new("ELEKTRO", "Flutlichtstrahler LED", 4, "G1", "Setolite"),
                    new("ELEKTRO", "Säbelsäge", 1, "G4", "Milwaukee"),
                    new("ELEKTRO", "Trennschleifer", 1, "G4", "Stihl", "TSA 230"),
                    new("MOTORSAEGE", "Motorsäge", 2, "G6", "Stihl", "MS 462"),
                    new("SONSTIGES", "Mehrzweckzug 1,6 t", 1, "G2", "Habegger"),
                    new("SONSTIGES", "Verkehrsabsicherung (Leitkegel, Warnleuchten)", 1, "G6"),
                    Loescher("G1"),
                ];
            case "GWL":
                return
                [
                    Schlauch("B", 20, "Rollcontainer Schlauch"),
                    new("STROMERZEUGER", "Stromerzeuger 8 kVA", 1, "Rollcontainer Strom", "Eisemann", "BSKA 8"),
                    new("ELEKTRO", "Tauchpumpe TP 8/1", 2, "Rollcontainer Wasser", "Mast", "TP 8-1"),
                    new("ELEKTRO", "Kabeltrommel 50 m", 4, "Rollcontainer Strom"),
                    new("SONSTIGES", "Ölbindemittel (Sackware)", 1, "Rollcontainer Öl"),
                    new("SONSTIGES", "Verkehrsabsicherung (Leitkegel, Warnleuchten)", 1, "Ladefläche"),
                    Loescher("Fahrerhaus"),
                ];
            case "GWT":
                return
                [
                    new("SONSTIGES", "Ladebordwand-Rollcontainer", 2, "Ladefläche"),
                    new("ELEKTRO", "Wassersauger", 2, "Ladefläche", "Kärcher", "NT 70"),
                    new("SONSTIGES", "Sandsäcke (Palette)", 1, "Ladefläche"),
                    Loescher("Fahrerhaus"),
                ];
            case "ELW":
                return
                [
                    new("ELEKTRO", "Führungsunterstützung (Laptop, Drucker)", 1, "Funkraum"),
                    new("SONSTIGES", "Lagekartentisch mit Magnettafel", 1, "Funkraum"),
                    Loescher("Heck"),
                ];
            default:
                return [Loescher("Heck")];
        }
    }

    /// <summary>Reserve und Lager im Gerätehaus je Ortsabteilung.</summary>
    private static IEnumerable<Ladung> Magazin(int abteilung) => abteilung switch
    {
        1 =>
        [
            new("DRUCKSCHLAUCH", "Druckschlauch B", 20, "Schlauchturm", "Parsch", "B 75-20"),
            new("DRUCKSCHLAUCH", "Druckschlauch C", 30, "Schlauchturm", "Parsch", "C 42-15"),
            new("PA", "Pressluftatmer", 6, "Atemschutzwerkstatt", "Dräger", "PSS 7000"),
            new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 24, "Atemschutzwerkstatt", "Dräger"),
            new("LEITER", "Steckleiter 4-teilig (Reserve)", 1, "Fahrzeughalle", "Günzburger"),
            new("HALTEGURT", "Feuerwehr-Haltegurt (Reserve)", 4, "Magazin", "Spanset", "Typ A"),
            new("LEINE", "Feuerwehrleine 30 m (Reserve)", 4, "Magazin", "Mewa", "FL 30"),
            new("ELEKTRO", "Waschmaschine Schlauchpflege", 1, "Schlauchwerkstatt", "Miele"),
            new("ELEKTRO", "Kompressor Atemluft", 1, "Atemschutzwerkstatt", "Bauer", "Mariner 320"),
            new("FEUERLOESCHER", "Pulverlöscher PG 6 (Gerätehaus)", 4, "Fahrzeughalle", "Gloria", "PD6GA"),
            new("SONSTIGES", "Hochwasserpumpe", 1, "Magazin", "Mast", "TP 15-1"),
        ],
        2 =>
        [
            new("DRUCKSCHLAUCH", "Druckschlauch B", 8, "Magazin", "Parsch", "B 75-20"),
            new("DRUCKSCHLAUCH", "Druckschlauch C", 10, "Magazin", "Parsch", "C 42-15"),
            new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 6, "Magazin", "Dräger"),
            new("FEUERLOESCHER", "Pulverlöscher PG 6 (Gerätehaus)", 2, "Fahrzeughalle", "Gloria", "PD6GA"),
        ],
        _ =>
        [
            new("DRUCKSCHLAUCH", "Druckschlauch B", 4, "Magazin", "Parsch", "B 75-20"),
            new("DRUCKSCHLAUCH", "Druckschlauch C", 6, "Magazin", "Parsch", "C 42-15"),
            new("PA-FLASCHE", "Atemluftflasche 6,8 l 300 bar", 2, "Magazin", "Dräger"),
            new("FEUERLOESCHER", "Pulverlöscher PG 6 (Gerätehaus)", 1, "Fahrzeughalle", "Gloria", "PD6GA"),
        ],
    };

    /// <summary>Gerätearten für Atemschutz, die die Stammdaten (noch) nicht kennen. Intervalle sind Vorschläge.</summary>
    private static IReadOnlyList<Geraeteart> AtemschutzGeraetearten =>
    [
        new()
        {
            Code = "PA", Bezeichnung = "Pressluftatmer", Pruefgrundlage = "DGUV 305-002, Herstellerangaben (Vorschlag)", Sortierung = 150,
            Pruefplan =
            [
                new() { Pruefart = Pruefart.Funktionspruefung, IntervallMonate = 6, Pruefer = "Atemschutzgerätewart", Checkliste = Pruefpunkt.AlsJson(Pruefpunkt.AusZeilen("Sichtprüfung ohne Befund\nHochdruck-Dichtprüfung bestanden\nWarnsignal in Ordnung")) },
                new() { Pruefart = Pruefart.Hersteller, IntervallMonate = 72, Pruefer = "Hersteller oder autorisierte Werkstatt", Checkliste = Pruefpunkt.AlsJson(Pruefpunkt.AusZeilen("Grundüberholung durchgeführt")) },
            ],
        },
        new()
        {
            Code = "PA-FLASCHE", Bezeichnung = "Atemluftflasche", Pruefgrundlage = "Betriebssicherheitsverordnung, Herstellerangaben (Vorschlag)", Sortierung = 160,
            Pruefplan =
            [
                new() { Pruefart = Pruefart.Hersteller, IntervallMonate = 60, Pruefer = "Zugelassene Überwachungsstelle", Checkliste = Pruefpunkt.AlsJson(Pruefpunkt.AusZeilen("Wiederkehrende Prüfung bestanden")) },
            ],
        },
    ];

    private sealed class Person
    {
        public required Mitglied Mitglied { get; init; }
        public required int Ort { get; init; }
        public required string Abteilung { get; init; }
        public bool Weiblich { get; init; }
        public DateOnly? JugendAb { get; set; }
        public DateOnly? EinsatzAb { get; set; }
        public DateOnly? EinsatzBis { get; set; }
        public DateOnly? BefreitAb { get; set; }
        public DateOnly? AltersAb { get; set; }

        /// <summary>Wie oft die Person zum Dienst kommt (0 bis 1).</summary>
        public double Fleiss { get; set; }
        public Dictionary<string, DateOnly> Qualifikationen { get; } = [];
        public int Alter { get; init; }
    }

    private sealed class Erzeuger(AppDbContext db, DateOnly heute)
    {
        private readonly Random zufall = new(1852);
        private readonly HashSet<string> namen = [];
        private int naechsteInventarnummer = 1;
        private readonly Dictionary<int, int> vorgangsnummern = [];

        // Vorhandene Datensätze einer schon genutzten Entwicklungsdatenbank, damit eindeutige Schlüssel nicht kollidieren.
        private HashSet<string> vorhandeneInventarnummern = [];
        private HashSet<(Guid ArtikelId, string Groesse)> vorhandeneBestaende = [];
        private readonly Dictionary<int, Abteilung> einsatzabteilungen = [];
        private (int Intervall, int IntervallAbAlter, int AbAlter) vorsorge = (36, 12, 50);

        private Dictionary<string, Abteilung> abteilungen = [];
        private Dictionary<string, Dienstgrad> dienstgrade = [];
        private Dictionary<string, Funktion> funktionen = [];
        private Dictionary<string, Qualifikation> qualifikationen = [];
        private Dictionary<string, Nachweisart> nachweisarten = [];
        private Dictionary<string, Geraeteart> geraetearten = [];
        private Dictionary<string, Artikel> artikel = [];
        private Dictionary<string, Ehrungsart> ehrungsarten = [];

        public List<Person> Personen { get; } = [];
        public List<Fahrzeug> Fahrzeuge { get; } = [];
        public List<Geraet> Geraete { get; } = [];
        public int Dienste { get; private set; }
        public int Einsaetze { get; private set; }
        private Mitglied? geraetewart;
        private Mitglied? atemschutzgeraetewart;

        public async Task KatalogeLadenAsync(CancellationToken ct)
        {
            abteilungen = await db.Abteilungen.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code, ct);
            dienstgrade = await db.Dienstgrade.ToDictionaryAsync(d => d.Code, ct);
            funktionen = await db.Funktionen.ToDictionaryAsync(f => f.Code, ct);
            qualifikationen = await db.Qualifikationen.ToDictionaryAsync(q => q.Code, ct);
            nachweisarten = await db.Nachweisarten.ToDictionaryAsync(n => n.Code, ct);
            artikel = await db.Artikel.ToDictionaryAsync(a => a.Code, ct);
            ehrungsarten = await db.Ehrungsarten.ToDictionaryAsync(e => e.Code, ct);

            foreach (var art in AtemschutzGeraetearten)
            {
                if (!await db.Geraetearten.IgnoreQueryFilters().AnyAsync(g => g.Code == art.Code, ct))
                {
                    db.Geraetearten.Add(art);
                }
            }

            await db.SaveChangesAsync(ct);
            geraetearten = await db.Geraetearten.Include(g => g.Pruefplan).ToDictionaryAsync(g => g.Code, ct);

            vorhandeneInventarnummern = (await db.Geraete.IgnoreQueryFilters().Select(g => g.Inventarnummer).ToListAsync(ct)).ToHashSet();
            vorhandeneBestaende = (await db.Lagerbestaende.IgnoreQueryFilters().Select(l => new { l.ArtikelId, l.Groesse }).ToListAsync(ct))
                .Select(l => (l.ArtikelId, l.Groesse)).ToHashSet();
            foreach (var nummer in await db.Kleiderkammervorgaenge.IgnoreQueryFilters().Select(v => v.Nummer).ToListAsync(ct))
            {
                var teile = nummer.Split('-');
                if (teile.Length == 3 && int.TryParse(teile[1], out var jahr) && int.TryParse(teile[2], out var laufend))
                {
                    vorgangsnummern[jahr] = Math.Max(vorgangsnummern.GetValueOrDefault(jahr), laufend);
                }
            }

            // Wie in der Personalakte: Vorsorgeintervalle aus den Parametern, Stand heute.
            var parameter = new ParameterLeser(db);
            vorsorge = (
                await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallMonate, heute, 36, ct),
                await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallAbAlterMonate, heute, 12, ct),
                await parameter.ZahlAsync(ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, heute, 50, ct));
        }

        public async Task StammfahrzeugeEntfernenAsync(CancellationToken ct)
        {
            var namen = Stammdaten.Fahrzeuge.Select(f => f.Funkrufname).ToList();
            db.Fahrzeuge.RemoveRange(await db.Fahrzeuge.Where(f => namen.Contains(f.Funkrufname)).ToListAsync(ct));
        }

        public void Erzeugen()
        {
            // Jede Ortsabteilung ist eine der fünf Einsatzabteilungen aus den Stammdaten und bekommt den Namen ihres Orts.
            foreach (var ort in Orte)
            {
                var code = ort.Nummer == 1 ? AbteilungCode.Einsatz : $"{AbteilungCode.Einsatz}{ort.Nummer}";
                if (!abteilungen.TryGetValue(code, out var abteilung))
                {
                    abteilung = new Abteilung { Code = code, Bezeichnung = "", Art = AbteilungArt.Einsatz };
                    db.Abteilungen.Add(abteilung);
                    abteilungen[code] = abteilung;
                }

                abteilung.Bezeichnung = $"Einsatzabteilung {ort.Name}";
                abteilung.Kurzbezeichnung = $"Abt. {ort.Name}";
                abteilung.Sortierung = 10 + ort.Nummer - 1;
                abteilung.Aktiv = true;
                abteilung.GeloeschtAm = null;
                einsatzabteilungen[ort.Nummer] = abteilung;
            }

            var nummer = 1;
            foreach (var ort in Orte)
            {
                for (var i = 0; i < ort.Personen; i++)
                {
                    var r = zufall.NextDouble();
                    var art = r < 0.15 ? AbteilungCode.Jugend : r < 0.28 ? AbteilungCode.Alters : AbteilungCode.Einsatz;
                    var person = PersonAnlegen(nummer++, ort, art);
                    Personen.Add(person);
                }
            }

            for (var i = 0; i < 6; i++)
            {
                var ort = Orte[zufall.Next(Orte.Length)];
                Personen.Add(EhemaligeAnlegen(nummer++, ort));
            }

            FunktionenVergeben();
            Ehrungen();
            FuhrparkAnlegen();
            KleiderkammerAnlegen();
            Beschaffungen();
        }

        public async Task DienstbetriebErzeugenAsync(CancellationToken ct)
        {
            var format = await new ParameterLeser(db).WertAsync(ParameterSchluessel.EinsatznummerFormat, heute, ct) ?? Einsatznummer.StandardFormat;
            var vergeben = (await db.Einsaetze.IgnoreQueryFilters().Select(e => e.Einsatznummer).ToListAsync(ct)).ToHashSet();
            var kategorien = await db.StatistikKategorien.ToDictionaryAsync(k => k.Code, ct);
            DiensteAnlegen();
            EinsaetzeAnlegen(format, vergeben, kategorien);
        }

        private string NeueInventarnummer()
        {
            string nummer;
            do
            {
                nummer = $"T{naechsteInventarnummer++:00000}";
            }
            while (vorhandeneInventarnummern.Contains(nummer));

            return nummer;
        }

        private DateOnly Tage(int von, int bis) => heute.AddDays(-zufall.Next(von, bis + 1));

        private DateOnly Spaetestens(DateOnly datum) => datum > heute ? heute : datum;

        private T Wahl<T>(IReadOnlyList<T> liste) => liste[zufall.Next(liste.Count)];

        private bool Wurf(double wahrscheinlichkeit) => zufall.NextDouble() < wahrscheinlichkeit;

        private Person PersonAnlegen(int nummer, Ortsabteilung ort, string art)
        {
            var weiblich = Wurf(art == AbteilungCode.Jugend ? 0.3 : art == AbteilungCode.Alters ? 0.05 : 0.16);
            var alter = art switch
            {
                AbteilungCode.Jugend => zufall.Next(10, 18),
                AbteilungCode.Alters => zufall.Next(60, 86),
                _ => 17 + (int)((zufall.NextDouble() + zufall.NextDouble()) / 2 * 48),
            };
            var geburt = heute.AddYears(-alter).AddDays(-zufall.Next(1, 364));
            var person = new Person
            {
                Mitglied = MitgliedAnlegen(nummer, ort, weiblich, geburt, alter), Ort = ort.Nummer, Abteilung = art, Weiblich = weiblich, Alter = alter,
                Fleiss = 0.3 + zufall.NextDouble() * 0.65,
            };

            switch (art)
            {
                case AbteilungCode.Jugend:
                    person.JugendAb = Spaetestens(geburt.AddYears(zufall.Next(10, alter + 1)).AddDays(zufall.Next(0, 200)));
                    Zugehoerigkeit(person, AbteilungCode.Jugend, MitgliedStatus.Aktiv, person.JugendAb.Value, null);
                    JugendAbzeichen(person, null);
                    break;

                case AbteilungCode.Alters:
                    person.EinsatzAb = geburt.AddYears(zufall.Next(17, 29)).AddDays(zufall.Next(0, 300));
                    var altersAb = Spaetestens(geburt.AddYears(zufall.Next(58, 66)).AddDays(zufall.Next(0, 200)));
                    person.EinsatzBis = altersAb.AddDays(-1);
                    person.AltersAb = altersAb;
                    Zugehoerigkeit(person, AbteilungCode.Einsatz, MitgliedStatus.Aktiv, person.EinsatzAb.Value, person.EinsatzBis, Austrittsgrund.Uebertritt);
                    Zugehoerigkeit(person, AbteilungCode.Alters, MitgliedStatus.Aktiv, altersAb, null);
                    Ausbildung(person);
                    break;

                default:
                    if (alter >= 18 && Wurf(0.45))
                    {
                        person.JugendAb = geburt.AddYears(zufall.Next(10, 14)).AddDays(zufall.Next(0, 300));
                        person.EinsatzAb = Spaetestens(geburt.AddYears(17).AddDays(zufall.Next(0, 300)));
                        Zugehoerigkeit(person, AbteilungCode.Jugend, MitgliedStatus.Aktiv, person.JugendAb.Value, person.EinsatzAb.Value.AddDays(-1), Austrittsgrund.Uebertritt);
                        JugendAbzeichen(person, person.EinsatzAb);
                    }
                    else if (Wurf(0.08))
                    {
                        person.EinsatzAb = Tage(20, 330);
                    }
                    else
                    {
                        person.EinsatzAb = Spaetestens(geburt.AddYears(zufall.Next(17, Math.Max(18, Math.Min(alter, 40)))).AddDays(zufall.Next(0, 300)));
                    }

                    if (Wurf(0.05) && person.EinsatzAb.Value.AddYears(-4) > geburt.AddYears(17))
                    {
                        var vorher = person.EinsatzAb.Value.AddYears(-zufall.Next(3, 5));
                        db.Mitgliedschaften.Add(new Mitgliedschaft
                        {
                            MitgliedId = person.Mitglied.Id, AbteilungId = abteilungen[AbteilungCode.Einsatz].Id, Status = MitgliedStatus.Aktiv,
                            GueltigAb = vorher, GueltigBis = person.EinsatzAb.Value.AddDays(-30), Austrittsgrund = Austrittsgrund.Wohnortwechsel,
                            Vordienstzeit = true, Vordienstart = Vordienstart.AndereFeuerwehr, Bemerkung = "Freiwillige Feuerwehr Musterhausen (fiktiv)",
                        });
                    }

                    var status = person.EinsatzAb > heute.AddMonths(-12) ? MitgliedStatus.Probezeit : MitgliedStatus.Aktiv;
                    if (status == MitgliedStatus.Aktiv && person.EinsatzAb < heute.AddYears(-4) && Wurf(0.04))
                    {
                        var befreitAb = Tage(30, 400);
                        person.BefreitAb = befreitAb;
                        Zugehoerigkeit(person, AbteilungCode.Einsatz, MitgliedStatus.Aktiv, person.EinsatzAb.Value, befreitAb.AddDays(-1));
                        var befreiung = Zugehoerigkeit(person, AbteilungCode.Einsatz, MitgliedStatus.Befreit, befreitAb, null);
                        befreiung.Befreiungsgrund = Wurf(0.5) ? Befreiungsgrund.Kindererziehung : Befreiungsgrund.Krankheit;
                    }
                    else
                    {
                        Zugehoerigkeit(person, AbteilungCode.Einsatz, status, person.EinsatzAb.Value, null);
                    }

                    Ausbildung(person);
                    break;
            }

            return person;
        }

        private Person EhemaligeAnlegen(int nummer, Ortsabteilung ort)
        {
            var alter = zufall.Next(22, 50);
            var geburt = heute.AddYears(-alter).AddDays(-zufall.Next(1, 364));
            var person = new Person
            {
                Mitglied = MitgliedAnlegen(nummer, ort, false, geburt, alter), Ort = ort.Nummer, Abteilung = AbteilungCode.Einsatz, Alter = alter, Fleiss = 0.3,
            };
            person.EinsatzAb = geburt.AddYears(18);
            person.EinsatzBis = Tage(40, 900);
            Zugehoerigkeit(person, AbteilungCode.Einsatz, MitgliedStatus.Aktiv, person.EinsatzAb.Value, person.EinsatzBis,
                Wahl([Austrittsgrund.Wohnortwechsel, Austrittsgrund.Austritt, Austrittsgrund.Wohnortwechsel]));
            return person;
        }

        private Mitglied MitgliedAnlegen(int nummer, Ortsabteilung ort, bool weiblich, DateOnly geburt, int alter)
        {
            string vorname, nachname;
            do
            {
                vorname = Wahl(weiblich ? VornamenW : VornamenM);
                nachname = Wahl(Nachnamen);
            }
            while (!namen.Add($"{vorname} {nachname}"));

            var mitglied = new Mitglied
            {
                Personalnummer = $"{PersonalnummerPraefix}{nummer:0000}",
                Anrede = weiblich ? "Frau" : "Herr",
                Geschlecht = weiblich ? Geschlecht.Weiblich : Geschlecht.Maennlich,
                Vorname = vorname,
                Nachname = nachname,
                Geburtsdatum = geburt,
                Strasse = $"{Wahl(ort.Strassen)} {zufall.Next(1, 60)}",
                Plz = "73099",
                Ort = $"Adelberg-{ort.Name}",
                // Rufnummernblock der Bundesnetzagentur für fiktive Nummern (030 23125 xxx).
                TelefonFestnetz = $"+493023125{nummer % 1000:000}",
                Email = $"{Ascii(vorname)}.{Ascii(nachname)}@example.org",
                Beruf = alter < 18 ? "Schüler/in" : alter >= 66 ? "Rentner/in" : Wahl(Berufe),
                Arbeitgeber = alter < 18 ? "Schule" : alter >= 66 ? "Ruhestand" : Wahl(Arbeitgeber),
                Arbeitsort = alter < 18 || alter >= 66 ? null : Wahl(Arbeitsorte),
                ArbeitgeberTelefon = alter < 18 || alter >= 66 ? null : $"+493023125{(nummer + 500) % 1000:000}",
                Notizen = Kennung,
            };
            db.Mitglieder.Add(mitglied);
            return mitglied;
        }

        private static string Ascii(string text) => text.ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Replace(' ', '-');

        private Mitgliedschaft Zugehoerigkeit(Person person, string abteilung, MitgliedStatus status, DateOnly ab, DateOnly? bis, Austrittsgrund? grund = null)
        {
            var mitgliedschaft = new Mitgliedschaft
            {
                MitgliedId = person.Mitglied.Id,
                AbteilungId = abteilung == AbteilungCode.Einsatz ? einsatzabteilungen[person.Ort].Id : abteilungen[abteilung].Id,
                Status = status,
                GueltigAb = ab,
                GueltigBis = bis,
                Austrittsgrund = grund,
                ProbezeitBis = status == MitgliedStatus.Probezeit ? ab.AddMonths(12) : null,
            };
            db.Mitgliedschaften.Add(mitgliedschaft);
            return mitgliedschaft;
        }

        private void Erwerben(Person person, string code, DateOnly erworben, DateOnly? gueltigBis = null, string? ort = null)
        {
            if (erworben > heute || !qualifikationen.TryGetValue(code, out var qualifikation))
            {
                return;
            }

            var nachweis = new QualifikationNachweis
            {
                MitgliedId = person.Mitglied.Id, QualifikationId = qualifikation.Id, ErworbenAm = erworben, GueltigBis = gueltigBis, Lehrgangsort = ort,
            };
            nachweis.GueltigkeitVorbelegen(qualifikation);
            db.QualifikationNachweise.Add(nachweis);
            person.Qualifikationen[code] = erworben;
        }

        private void JugendAbzeichen(Person person, DateOnly? bis)
        {
            var geburt = person.Mitglied.Geburtsdatum;
            var ende = bis ?? heute;
            DateOnly Im(int jahr) => new DateOnly(geburt.Year + jahr, zufall.Next(5, 8), zufall.Next(1, 28));

            var stufen = new[] { (Qualifikation.CodeJugendflamme1, 10), (Qualifikation.CodeJugendflamme2, 13), (Qualifikation.CodeJugendflamme3, 15) };
            foreach (var (code, ab) in stufen)
            {
                var datum = Im(Math.Max(ab, person.JugendAb!.Value.Year - geburt.Year + 1));
                if (datum > ende || !Wurf(0.8))
                {
                    break;
                }

                if (code == Qualifikation.CodeJugendflamme3)
                {
                    Erwerben(person, "EH", datum.AddMonths(-2));
                }

                Erwerben(person, code, datum, ort: "Kreisjugendfeuerwehrtag (fiktiv)");
            }

            var spange = Im(Math.Max(16, person.JugendAb!.Value.Year - geburt.Year + 2));
            if (spange <= ende && Wurf(0.75))
            {
                Erwerben(person, Qualifikation.CodeLeistungsspange, spange, ort: "Abnahme Kreisjugendfeuerwehr (fiktiv)");
            }
        }

        private void Ausbildung(Person person)
        {
            var ab = person.EinsatzAb!.Value;
            var bis = person.EinsatzBis ?? heute;
            var geburt = person.Mitglied.Geburtsdatum;
            var jahre = (bis.DayNumber - ab.DayNumber) / 365.25;
            DateOnly Nach(int vonMonate, int bisMonate) => ab.AddMonths(zufall.Next(vonMonate, bisMonate + 1)).AddDays(zufall.Next(0, 28));

            if (jahre >= 0.4) Erwerben(person, "TM1", Nach(4, 8), ort: "Kreisausbildung Göppingen");
            if (jahre >= 2) Erwerben(person, "TM2", Nach(18, 26), ort: "Standort");
            if (jahre >= 1.5 && Wurf(0.75)) Erwerben(person, "SPRF", Nach(10, 30), ort: "Kreisausbildung Göppingen");
            var agtAm = Nach(14, 40);
            if (jahre >= 1.5 && Mitglied.Alter(geburt, agtAm) < 50 && Wurf(0.62)) Erwerben(person, "AGT", agtAm, ort: "Kreisausbildung Göppingen");
            if (jahre >= 4 && Wurf(0.42)) Erwerben(person, "TF", Nach(36, 70), ort: "Kreisausbildung Göppingen");
            if (person.Qualifikationen.TryGetValue("TF", out var tf) && jahre >= 8 && Wurf(0.32))
            {
                var gf = tf.AddYears(zufall.Next(3, 6));
                Erwerben(person, "GF", gf, ort: "Landesfeuerwehrschule Bruchsal");
                if (Wurf(0.3)) Erwerben(person, "ZF", gf.AddYears(zufall.Next(3, 6)), ort: "Landesfeuerwehrschule Bruchsal");
            }

            // Fahrerlaubnisse: C/CE/C1 sind befristet, die Verlängerung liegt mal kurz, mal lang zurück.
            DateOnly Befristet() => heute.AddDays(zufall.Next(-90, 1800));
            var alter = person.Alter;
            if (alter >= 18 && Wurf(0.93)) Erwerben(person, "FS-B", geburt.AddYears(18).AddDays(zufall.Next(0, 400)));
            if (alter >= 21 && Wurf(person.Weiblich ? 0.15 : 0.48))
            {
                Erwerben(person, "FS-C", Spaetestens(geburt.AddYears(zufall.Next(21, Math.Max(22, Math.Min(alter, 32)))).AddDays(zufall.Next(0, 300))), Befristet());
                if (Wurf(0.35)) Erwerben(person, "FS-CE", Spaetestens(person.Qualifikationen["FS-C"].AddYears(zufall.Next(1, 5))), Befristet());
                if (jahre >= 3 && Wurf(0.6)) Erwerben(person, "MA", Spaetestens(person.Qualifikationen["FS-C"].AddMonths(zufall.Next(3, 30))), ort: "Kreisausbildung Göppingen");
            }
            else if (alter >= 18 && Wurf(0.2))
            {
                var c1 = Wurf(0.5);
                Erwerben(person, c1 ? "FS-C1" : "FW-FS", Spaetestens(geburt.AddYears(zufall.Next(18, Math.Max(19, Math.Min(alter, 30))))), c1 ? Befristet() : null);
            }

            if (person.Qualifikationen.ContainsKey("MA") && Wurf(0.5)) Erwerben(person, "GP-MA", Spaetestens(person.Qualifikationen["MA"].AddMonths(zufall.Next(6, 30))));
            if (person.Qualifikationen.TryGetValue("TM1", out var tm1) && jahre >= 1.5 && Wurf(0.45))
            {
                var bronze = tm1.AddYears(zufall.Next(1, 3));
                Erwerben(person, "LA-B", bronze, ort: "Leistungsabzeichen Kreis Göppingen");
                if ((person.Qualifikationen.ContainsKey("AGT") || person.Qualifikationen.ContainsKey("SPRF")) && Wurf(0.55))
                {
                    var silber = bronze.AddYears(zufall.Next(1, 4));
                    Erwerben(person, "LA-S", silber, ort: "Leistungsabzeichen Kreis Göppingen");
                    if (person.Qualifikationen.ContainsKey("TF") && Wurf(0.4)) Erwerben(person, "LA-G", silber.AddYears(zufall.Next(1, 4)), ort: "Leistungsabzeichen Kreis Göppingen");
                }
            }

            if (person.Abteilung == AbteilungCode.Alters)
            {
                Dienstgrade(person);
                return;
            }

            if (Wurf(0.8)) Erwerben(person, "EH", Tage(30, 900), ort: "DRK-Kreisverband (fiktiv)");
            Dienstgrade(person);
            Nachweise(person);
            Atemschutz(person);
        }

        private void Dienstgrade(Person person)
        {
            var ab = person.EinsatzAb!.Value;
            var q = person.Qualifikationen;
            var stufen = new List<(string Code, DateOnly Ab)> { ("FMP", ab) };
            if (q.TryGetValue("TM1", out var tm1)) stufen.Add(("FM", Max(ab.AddMonths(12), tm1).AddDays(zufall.Next(0, 90))));
            if (q.TryGetValue("TM2", out var tm2))
            {
                var ofm = Max(ab.AddYears(5), tm2).AddDays(zufall.Next(0, 120));
                stufen.Add(("OFM", ofm));
                stufen.Add(("HFM", ofm.AddYears(5).AddDays(zufall.Next(0, 200))));
            }

            if (q.TryGetValue("TF", out var tf))
            {
                var lm = tf.AddDays(zufall.Next(30, 365));
                stufen.Add(("LM", lm));
                stufen.Add(("OLM", lm.AddYears(5).AddDays(zufall.Next(0, 200))));
                stufen.Add(("HLM", lm.AddYears(10).AddDays(zufall.Next(0, 200))));
            }

            if (q.TryGetValue("GF", out var gf))
            {
                var bm = gf.AddDays(zufall.Next(30, 365));
                stufen.Add(("BM", bm));
                stufen.Add(("OBM", bm.AddYears(5).AddDays(zufall.Next(0, 200))));
            }

            if (q.TryGetValue("ZF", out var zf)) stufen.Add(("HBM", zf.AddDays(zufall.Next(60, 400))));

            var ende = person.EinsatzBis ?? heute;
            var letzte = -1;
            foreach (var (code, datum) in stufen.Where(s => s.Ab <= ende).OrderBy(s => s.Ab))
            {
                var dienstgrad = dienstgrade[code];
                if (dienstgrad.Sortierung <= letzte)
                {
                    continue;
                }

                letzte = dienstgrad.Sortierung;
                db.DienstgradZuordnungen.Add(new DienstgradZuordnung { MitgliedId = person.Mitglied.Id, DienstgradId = dienstgrad.Id, Ab = datum });
            }
        }

        private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

        private void Nachweise(Person person)
        {
            if (person.EinsatzAb > heute.AddDays(-20))
            {
                return;
            }

            if (Wurf(0.96))
            {
                var uvv = Max(Tage(5, 420), person.EinsatzAb!.Value);
                db.Nachweise.Add(new Nachweis
                {
                    MitgliedId = person.Mitglied.Id, NachweisartId = nachweisarten["UVV"].Id, Datum = uvv,
                    GueltigBis = Domain.Ausbildung.Nachweis.GueltigkeitAus(uvv, nachweisarten["UVV"]), DurchgefuehrtVon = "Kommandant",
                });
            }

            if (person.Qualifikationen.Keys.Any(k => k.StartsWith("FS-") || k == "FW-FS"))
            {
                var fsk = Tage(5, 230);
                db.Nachweise.Add(new Nachweis
                {
                    MitgliedId = person.Mitglied.Id, NachweisartId = nachweisarten["FSK"].Id, Datum = fsk,
                    GueltigBis = Domain.Ausbildung.Nachweis.GueltigkeitAus(fsk, nachweisarten["FSK"]), DurchgefuehrtVon = "Gerätewart",
                });
            }
        }

        private void Atemschutz(Person person)
        {
            if (!person.Qualifikationen.TryGetValue("AGT", out var agt))
            {
                return;
            }

            var id = person.Mitglied.Id;
            if (Wurf(0.95))
            {
                var untersucht = Max(Tage(30, 1250), agt.AddMonths(-1));
                var befristet = Wurf(0.08);
                db.AtemschutzVorsorgen.Add(new AtemschutzVorsorge
                {
                    MitgliedId = id,
                    UntersuchtAm = untersucht,
                    Ergebnis = befristet ? VorsorgeErgebnis.GeeignetBefristet : VorsorgeErgebnis.Geeignet,
                    NaechsteFaellig = befristet
                        ? untersucht.AddMonths(12)
                        : AtemschutzVorsorge.NaechsteVorschlagen(untersucht, person.Mitglied.Geburtsdatum, vorsorge.Intervall, vorsorge.IntervallAbAlter, vorsorge.AbAlter),
                });
            }

            void Nachweis(AtemschutzNachweisArt art, DateOnly datum, string? ort, string? taetigkeit) => db.AtemschutzNachweise.Add(new AtemschutzNachweis
            {
                MitgliedId = id, Art = art, Datum = Max(datum, agt), Geraetetyp = "Dräger PSS 7000",
                Minuten = art == AtemschutzNachweisArt.Unterweisung ? null : zufall.Next(18, 36), Ort = ort, Taetigkeit = taetigkeit,
            });

            Nachweis(AtemschutzNachweisArt.Belastungsuebung, Tage(20, 440), "Atemschutzübungsanlage Landkreis (fiktiv)", "Belastungsübung Übungsstrecke");
            Nachweis(AtemschutzNachweisArt.Unterweisung, Tage(10, 400), "Gerätehaus", "Jährliche Unterweisung");
            Nachweis(AtemschutzNachweisArt.Einsatzuebung, Tage(10, 420), "Übungsgelände", "Innenangriff mit Menschenrettung");
            if (Wurf(0.5)) Nachweis(AtemschutzNachweisArt.Einsatz, Tage(5, 300), null, "Brandbekämpfung im Innenangriff");
        }

        private void FunktionenVergeben()
        {
            var aktive = Personen.Where(p => p.Abteilung == AbteilungCode.Einsatz && p.EinsatzBis is null && p.EinsatzAb < heute.AddYears(-2)).ToList();
            var vergeben = new HashSet<Person>();

            Person? Finde(Func<Person, bool> bedingung)
            {
                var kandidaten = aktive.Where(p => !vergeben.Contains(p) && bedingung(p)).ToList();
                if (kandidaten.Count == 0)
                {
                    kandidaten = aktive.Where(p => !vergeben.Contains(p)).ToList();
                }

                var gewaehlt = Wahl(kandidaten);
                vergeben.Add(gewaehlt);
                return gewaehlt;
            }

            void Zuordnen(Person? person, string code, FunktionArt art, DateOnly ab, Abteilung? abteilung = null)
            {
                if (person is null)
                {
                    return;
                }

                var funktion = funktionen[code];
                db.FunktionZuordnungen.Add(new FunktionZuordnung
                {
                    MitgliedId = person.Mitglied.Id, FunktionId = funktion.Id, AbteilungId = abteilung?.Id, Art = art, GueltigAb = ab,
                    GueltigBis = funktion.AmtszeitJahre is { } jahre ? ab.AddYears(jahre).AddDays(-1) : null,
                    Beschlussdatum = art == FunktionArt.Wahl ? ab : null,
                });
            }

            var hauptversammlung = new DateOnly(2024, 3, 15);
            Zuordnen(Finde(p => p.Ort == 1 && p.Qualifikationen.ContainsKey("ZF")), "KDT", FunktionArt.Wahl, hauptversammlung);
            // Amtszeit endet bald: zeigt die Warnung vor dem Ende der Amtszeit.
            Zuordnen(Finde(p => p.Ort == 2 && p.Qualifikationen.ContainsKey("GF")), "STV", FunktionArt.Wahl, new DateOnly(2021, 12, 3));

            // Abteilungskommandant und Stellvertreter je Einsatzabteilung; in Steinach fehlt dem Stellvertreter noch der Gruppenführer.
            foreach (var ort in Orte)
            {
                var abteilung = einsatzabteilungen[ort.Nummer];
                Zuordnen(Finde(p => p.Ort == ort.Nummer && p.Qualifikationen.ContainsKey("GF")), "ABTKDT", FunktionArt.Wahl, hauptversammlung, abteilung);
                Zuordnen(Finde(p => p.Ort == ort.Nummer && (ort.Nummer == 5 ? !p.Qualifikationen.ContainsKey("GF") : p.Qualifikationen.ContainsKey("GF"))),
                    "ABTSTV", FunktionArt.Wahl, hauptversammlung, abteilung);
            }

            foreach (var gf in aktive.Where(p => p.Qualifikationen.ContainsKey("GF")))
            {
                Zuordnen(gf, "GF", FunktionArt.Bestellung, Max(gf.Qualifikationen["GF"].AddMonths(6), new DateOnly(2015, 1, 1)), einsatzabteilungen[gf.Ort]);
            }

            var gw = Finde(p => p.Ort == 1 && p.Qualifikationen.ContainsKey("MA"))!;
            Erwerben(gw, "GW", heute.AddYears(-zufall.Next(3, 9)), ort: "Landesfeuerwehrschule Bruchsal");
            Zuordnen(gw, "GW", FunktionArt.Bestellung, gw.Qualifikationen["GW"].AddMonths(2));
            geraetewart = gw.Mitglied;

            var gw2 = Finde(p => p.Ort == 2 && p.Qualifikationen.ContainsKey("MA"))!;
            Erwerben(gw2, "GW", heute.AddYears(-zufall.Next(2, 6)), ort: "Landesfeuerwehrschule Bruchsal");
            Zuordnen(gw2, "GW", FunktionArt.Bestellung, gw2.Qualifikationen["GW"].AddMonths(2));

            var agw = Finde(p => p.Ort == 1 && p.Qualifikationen.ContainsKey("AGT"))!;
            Erwerben(agw, "AGW", heute.AddYears(-zufall.Next(3, 8)), ort: "Landesfeuerwehrschule Bruchsal");
            Zuordnen(agw, "AGW", FunktionArt.Bestellung, agw.Qualifikationen["AGW"].AddMonths(1));
            atemschutzgeraetewart = agw.Mitglied;

            var jw = Finde(p => p.Alter is >= 24 and <= 45 && p.JugendAb is not null)!;
            Erwerben(jw, "JGL", heute.AddYears(-zufall.Next(2, 8)), ort: "Kreisjugendfeuerwehr Göppingen");
            Zuordnen(jw, "JW", FunktionArt.Wahl, hauptversammlung, abteilungen[AbteilungCode.Jugend]);
            Zuordnen(Finde(p => p.Alter is >= 20 and <= 35), "JWSTV", FunktionArt.Wahl, hauptversammlung, abteilungen[AbteilungCode.Jugend]);

            // UVV: Sicherheitsbeauftragter mit Lehrgang, je Einsatzabteilung ein Ersthelfer; ein früherer Sicherheitsbeauftragter als Verlauf.
            var sib = Finde(p => p.Alter >= 30)!;
            Erwerben(sib, "SIB", heute.AddYears(-zufall.Next(1, 5)), ort: "UKBW");
            Zuordnen(sib, "SIB", FunktionArt.Bestellung, sib.Qualifikationen["SIB"].AddMonths(1));
            var frueher = Finde(p => p.Alter >= 40)!;
            db.FunktionZuordnungen.Add(new FunktionZuordnung
            {
                MitgliedId = frueher.Mitglied.Id, FunktionId = funktionen["SIB"].Id, Art = FunktionArt.Bestellung,
                GueltigAb = new DateOnly(2016, 1, 1), GueltigBis = sib.Qualifikationen["SIB"],
            });
            foreach (var ort in Orte)
            {
                var ersthelfer = Finde(p => p.Ort == ort.Nummer)!;
                Erwerben(ersthelfer, "EH", heute.AddMonths(-zufall.Next(1, 20)), ort: "DRK Göppingen");
                Zuordnen(ersthelfer, "EHF", FunktionArt.Bestellung, heute.AddMonths(-zufall.Next(1, 30)));
            }

            Zuordnen(Finde(p => p.Alter >= 25 && p.Qualifikationen.ContainsKey("GF")), "AUSB", FunktionArt.Bestellung, new DateOnly(2023, 1, 1));
            Zuordnen(Finde(p => p.Alter >= 30), "KAS", FunktionArt.Wahl, hauptversammlung);
            Zuordnen(Finde(p => p.Alter >= 25), "SF", FunktionArt.Wahl, hauptversammlung);
            Zuordnen(Finde(p => p.Ort == 1), "KW", FunktionArt.Bestellung, new DateOnly(2022, 1, 1));
            Zuordnen(Finde(p => p.Alter >= 30), "KP", FunktionArt.Wahl, hauptversammlung);
            Zuordnen(Finde(p => p.Alter >= 30), "KP", FunktionArt.Wahl, hauptversammlung);
            foreach (var ort in Orte)
            {
                Zuordnen(Finde(p => p.Ort == ort.Nummer), "AUS", FunktionArt.Wahl, hauptversammlung);
            }

            Zuordnen(Finde(p => p.Alter < 30), "AUS", FunktionArt.Wahl, hauptversammlung);
        }

        /// <summary>Bereits verliehene Ehrungen; was im letzten Jahr erreicht wurde, bleibt offen und erscheint als Vorschlag.</summary>
        private void Ehrungen()
        {
            foreach (var person in Personen.Where(p => p.EinsatzAb is not null && p.Abteilung != AbteilungCode.Jugend))
            {
                var ende = person.EinsatzBis ?? heute;
                foreach (var (code, jahre) in new[] { ("FEZ15", 15), ("FEZ25", 25), ("FEZ40", 40), ("FEZ50", 50) })
                {
                    Verleihen(person, code, person.EinsatzAb!.Value.AddYears(jahre), ende);
                }

                var erste = person.JugendAb ?? person.EinsatzAb!.Value;
                foreach (var jahre in new[] { 10, 20, 25, 30, 40, 50, 60 })
                {
                    Verleihen(person, $"JUB{jahre}", erste.AddYears(jahre), heute);
                }
            }
        }

        private void Verleihen(Person person, string code, DateOnly erreicht, DateOnly ende)
        {
            if (erreicht > ende || erreicht > heute.AddMonths(-10) || !ehrungsarten.TryGetValue(code, out var art))
            {
                return;
            }

            db.Ehrungen.Add(new Ehrung
            {
                MitgliedId = person.Mitglied.Id, EhrungsartId = art.Id, Status = EhrungStatus.Verliehen,
                VoraussetzungAm = erreicht, VerliehenAm = Spaetestens(erreicht.AddDays(zufall.Next(60, 300))),
            });
        }

        private bool ImEinsatzdienst(Person p, DateOnly tag) =>
            p.EinsatzAb <= tag && (p.EinsatzBis is null || p.EinsatzBis >= tag) && !(p.BefreitAb <= tag);

        private static bool InJugend(Person p, DateOnly tag) => p.JugendAb <= tag && (p.EinsatzAb is null || p.EinsatzAb > tag);

        private static bool InAltersabteilung(Person p, DateOnly tag) => p.AltersAb <= tag;

        private bool Hat(Person p, string qualifikation, DateOnly tag) => p.Qualifikationen.TryGetValue(qualifikation, out var am) && am <= tag;

        private Guid? Inhaber(string funktion, Abteilung? abteilung) => db.FunktionZuordnungen.Local
            .FirstOrDefault(z => z.FunktionId == funktionen[funktion].Id && (abteilung is null || z.AbteilungId == abteilung.Id))?.MitgliedId;

        private static readonly (string Titel, DienstArt Art)[] Uebungsthemen =
        [
            ("Löschangriff nach FwDV 3", DienstArt.Uebung), ("Technische Hilfe: Verkehrsunfall mit eingeklemmter Person", DienstArt.Uebung),
            ("Atemschutz: Innenangriff und Menschenrettung", DienstArt.Uebung), ("Wasserförderung über lange Wegstrecke", DienstArt.Uebung),
            ("Absturzsicherung und Leinenarbeit", DienstArt.Uebung), ("Gerätekunde Löschfahrzeug", DienstArt.Unterricht),
            ("Sprechfunk und Lagemeldung", DienstArt.Unterricht), ("Erste Hilfe und Reanimation", DienstArt.Uebung),
            ("Hydrantenkontrolle im Ausrückebereich", DienstArt.Arbeitsdienst), ("Brandschutz in Gebäuden, Rauchausbreitung", DienstArt.Unterricht),
            ("Vegetationsbrand", DienstArt.Uebung), ("Gefahrgut: Erkennen und Absperren", DienstArt.Unterricht), ("Leitern: Vornahme und Rettung", DienstArt.Uebung),
            ("Unwetter: Pumpen und Sandsäcke", DienstArt.Uebung), ("Fahrzeug- und Gerätepflege", DienstArt.Arbeitsdienst), ("UVV-Unterweisung", DienstArt.Unterricht),
        ];

        /// <summary>
        /// Dienste der letzten zwei Jahre mit Anwesenheit und einige geplante mit Rückmeldungen: Übungsabende je Einsatzabteilung, Gesamtübungen,
        /// Atemschutz- und Maschinistenausbildung, Hauptversammlung, Jugendfeuerwehr und Altersabteilung. Sommerpause im August, Weihnachtspause.
        /// </summary>
        private void DiensteAnlegen()
        {
            var von = heute.AddYears(-2);
            var bis = heute.AddDays(35);
            static bool Pause(DateOnly tag) => tag.Month == 8 || (tag.Month == 12 && tag.Day >= 20) || (tag.Month == 1 && tag.Day <= 6);
            static IEnumerable<DateOnly> Wochentage(DateOnly von, DateOnly bis, DayOfWeek wochentag, int schritt)
            {
                var tag = von.AddDays(((int)wochentag - (int)von.DayOfWeek + 7) % 7);
                for (; tag <= bis; tag = tag.AddDays(schritt))
                {
                    yield return tag;
                }
            }

            var einsatzkraefte = Personen.Where(p => p.EinsatzAb is not null).ToList();
            var orte = new Dictionary<int, string> { [1] = "Gerätehaus Innenstadt", [2] = "Gerätehaus Oberdorf", [3] = "Gerätehaus Talhausen", [4] = "Gerätehaus Buchenbach", [5] = "Gerätehaus Steinach" };

            foreach (var ort in Orte)
            {
                var abteilung = einsatzabteilungen[ort.Nummer];
                var wochentag = (DayOfWeek)((int)DayOfWeek.Monday + (ort.Nummer - 1) % 4);
                var thema = zufall.Next(Uebungsthemen.Length);
                foreach (var tag in Wochentage(von, bis, wochentag, ort.Nummer == 1 ? 7 : 14).Where(t => !Pause(t)))
                {
                    var (titel, art) = Uebungsthemen[thema++ % Uebungsthemen.Length];
                    DienstAnlegen(art, titel, tag, 19.5, 2, orte[ort.Nummer], abteilung, null, Inhaber("ABTKDT", abteilung),
                        einsatzkraefte.Where(p => p.Ort == ort.Nummer && ImEinsatzdienst(p, tag)), true);
                }
            }

            foreach (var tag in Wochentage(von, bis, DayOfWeek.Saturday, 7).Where(t => t.Month is 5 or 10 && t.Day <= 7))
            {
                DienstAnlegen(DienstArt.Uebung, "Gesamtübung aller Abteilungen", tag, 14, 3, "Gewerbegebiet (Übungsobjekt)", null, null, Inhaber("KDT", null),
                    einsatzkraefte.Where(p => ImEinsatzdienst(p, tag)), true);
            }

            foreach (var tag in Wochentage(von, bis, DayOfWeek.Saturday, 7).Where(t => t.Month is 2 or 4 or 9 or 11 && t.Day is > 7 and <= 14))
            {
                DienstAnlegen(DienstArt.Uebung, "Atemschutz: Belastungsübung und Unterweisung", tag, 9, 3, "Atemschutzübungsanlage Landkreis (fiktiv)", null,
                    qualifikationen["AGT"], Inhaber("AGW", null), einsatzkraefte.Where(p => ImEinsatzdienst(p, tag) && Hat(p, "AGT", tag)), true);
            }

            foreach (var tag in Wochentage(von, bis, DayOfWeek.Saturday, 7).Where(t => t.Month is 3 or 10 && t.Day is > 14 and <= 21))
            {
                DienstAnlegen(DienstArt.Uebung, "Maschinistenausbildung: Pumpe und Fahrsicherheit", tag, 9, 4, "Gerätehaus Innenstadt", null,
                    qualifikationen["MA"], Inhaber("GW", null), einsatzkraefte.Where(p => ImEinsatzdienst(p, tag) && Hat(p, "MA", tag)), true);
            }

            foreach (var tag in Wochentage(von, bis, DayOfWeek.Friday, 7).Where(t => t.Month == 3 && t.Day is > 7 and <= 14))
            {
                DienstAnlegen(DienstArt.Hauptversammlung, $"Hauptversammlung {tag.Year}", tag, 19.5, 3, "Gemeindehalle", null, null, Inhaber("KDT", null),
                    Personen.Where(p => ImEinsatzdienst(p, tag) || InAltersabteilung(p, tag)), false);
            }

            var jugend = abteilungen[AbteilungCode.Jugend];
            var jugendthemen = new[] { "Knotenkunde", "Löschangriff mit Kübelspritze", "Gerätekunde", "Erste Hilfe", "Vorbereitung Jugendflamme", "Spiel und Sport", "Wasserförderung", "Vorbereitung Leistungsspange", "Orientierungslauf" };
            var jt = 0;
            foreach (var tag in Wochentage(von, bis, DayOfWeek.Friday, 7).Where(t => !Pause(t)))
            {
                DienstAnlegen(DienstArt.Uebung, $"Jugendfeuerwehr: {jugendthemen[jt++ % jugendthemen.Length]}", tag, 18, 2, "Gerätehaus Innenstadt", jugend, null,
                    Inhaber("JW", jugend), Personen.Where(p => InJugend(p, tag)), false);
            }

            var alters = abteilungen[AbteilungCode.Alters];
            foreach (var tag in Wochentage(von, bis, DayOfWeek.Wednesday, 7).Where(t => t.Day is > 7 and <= 14 && !Pause(t)))
            {
                DienstAnlegen(DienstArt.Veranstaltung, "Treffen der Altersabteilung", tag, 14.5, 2.5, "Gerätehaus Innenstadt, Schulungsraum", alters, null,
                    null, Personen.Where(p => InAltersabteilung(p, tag)), false);
            }
        }

        private void DienstAnlegen(
            DienstArt art, string titel, DateOnly tag, double stunde, double dauer, string ort, Abteilung? abteilung, Qualifikation? qualifikation,
            Guid? verantwortlich, IEnumerable<Person> zielgruppe, bool fortbildung)
        {
            var beginn = Ortszeit.TagesbeginnUtc(tag).AddHours(stunde);
            var vergangen = tag < heute;
            var abgesagt = vergangen && Wurf(0.03);
            var dienst = new Dienst
            {
                Art = art, Titel = titel, Beginn = beginn, Ende = beginn.AddHours(dauer), Ort = ort, Abteilungen = abteilung is null ? [] : [abteilung], QualifikationId = qualifikation?.Id,
                VerantwortlicherId = verantwortlich, ZaehltFuerFortbildung = fortbildung && art is not (DienstArt.Hauptversammlung or DienstArt.Veranstaltung),
                RueckmeldungErbeten = art != DienstArt.Hauptversammlung,
                Status = abgesagt ? DienstStatus.Abgesagt : vergangen ? DienstStatus.Durchgefuehrt : DienstStatus.Geplant,
                Bericht = abgesagt ? $"{Kennung}: abgesagt wegen Unwetterwarnung" : Kennung,
            };
            db.Dienste.Add(dienst);
            Dienste++;
            if (abgesagt)
            {
                return;
            }

            foreach (var person in zielgruppe)
            {
                if (!vergangen)
                {
                    // Geplante Dienste: etwa die Hälfte hat schon zu- oder abgesagt.
                    if (Wurf(0.5))
                    {
                        var zusage = Wurf(person.Fleiss);
                        db.Rueckmeldungen.Add(new Rueckmeldung
                        {
                            DienstId = dienst.Id, MitgliedId = person.Mitglied.Id, Antwort = zusage ? RueckmeldungAntwort.Zusage : RueckmeldungAntwort.Absage,
                            GemeldetAm = beginn.AddDays(-zufall.Next(1, 20)),
                        });

                        // Wie im Betrieb: Eine Absage steht bei einem Dienst gleich als entschuldigt in der Anwesenheit.
                        if (!zusage)
                        {
                            db.Anwesenheiten.Add(new Anwesenheit
                            {
                                DienstId = dienst.Id, MitgliedId = person.Mitglied.Id, Status = AnwesenheitStatus.Entschuldigt, ErfasstUeber = ErfassungsWeg.Rueckmeldung,
                            });
                        }
                    }

                    continue;
                }

                var anwesend = Wurf(person.Fleiss);
                if (!anwesend && Wurf(0.35))
                {
                    continue;
                }

                var spaeter = anwesend && Wurf(0.08);
                db.Anwesenheiten.Add(new Anwesenheit
                {
                    DienstId = dienst.Id, MitgliedId = person.Mitglied.Id,
                    Status = anwesend ? AnwesenheitStatus.Anwesend : Wurf(0.8) ? AnwesenheitStatus.Entschuldigt : AnwesenheitStatus.Unentschuldigt,
                    Von = spaeter ? beginn.AddMinutes(30) : null,
                    ErfasstUeber = anwesend ? (Wurf(0.6) ? ErfassungsWeg.Qr : ErfassungsWeg.Web) : ErfassungsWeg.Rueckmeldung,
                    Bemerkung = spaeter ? "kam später (Arbeit)" : null,
                });
            }
        }

        private sealed record Einsatzvorlage(string Kategorie, string Stichwort, string Beschreibung, int Gewicht, int MinutenVon, int MinutenBis, int Fahrzeuge,
            bool Atemschutz, bool Kostenersatz, bool Ueberoertlich, string Lage, string Massnahmen);

        private static readonly Einsatzvorlage[] Einsatzarten =
        [
            new("BR", "B 1", "Kleinbrand, Mülltonne", 6, 25, 60, 1, false, false, false, "Mülltonne an Hauswand brennt, keine Ausbreitung.", "Ablöschen mit Kleinlöschgerät, Hauswand kontrolliert."),
            new("BR", "B 2", "Brand Gartenhütte", 4, 60, 150, 2, true, false, false, "Gartenhütte im Vollbrand, Übergriff auf Hecke droht.", "Ein C-Rohr unter PA, Riegelstellung zur Hecke, Nachlöscharbeiten."),
            new("BR", "B 2", "Flächenbrand", 3, 45, 180, 2, false, false, false, "Wiese und Böschung auf ca. 500 m² brennend.", "Zwei C-Rohre, Feuerpatschen, Wasserversorgung über Pendelverkehr."),
            new("BR", "B 3", "Wohnungsbrand, Person vermisst", 2, 90, 240, 3, true, false, false, "Starke Rauchentwicklung aus Obergeschoss, eine Person wird vermisst.", "Menschenrettung unter PA, Innenangriff, Belüftung, Übergabe an Rettungsdienst."),
            new("BR", "B 2", "Kaminbrand", 2, 60, 120, 1, false, false, false, "Rußbrand im Kamin, Schornsteinfeger verständigt.", "Kontrolle mit Wärmebildkamera, Brandwache bis Eintreffen Schornsteinfeger."),
            new("TH", "TH 1", "Ölspur", 8, 30, 120, 1, false, true, false, "Ölspur über ca. 300 m auf der Ortsdurchfahrt.", "Absichern, Ölbindemittel aufgebracht und aufgenommen, Straßenmeisterei informiert."),
            new("TH", "TH 1", "Baum auf Straße", 7, 30, 90, 1, false, false, false, "Umgestürzter Baum blockiert Kreisstraße nach Sturm.", "Absichern, Baum mit Motorsäge zerkleinert und beseitigt."),
            new("TH", "TH 2", "Verkehrsunfall, Person eingeklemmt", 3, 60, 150, 3, false, false, true, "Pkw gegen Baum, Fahrer eingeklemmt.", "Absichern, Brandschutz, patientengerechte Rettung mit hydraulischem Rettungsgerät."),
            new("TH", "TH 1", "Wasser im Keller", 5, 45, 180, 1, false, true, false, "Keller nach Starkregen ca. 20 cm unter Wasser.", "Tauchpumpe und Wassersauger eingesetzt, Strom freigeschaltet."),
            new("TH", "TH 1", "Türöffnung, Person in Notlage", 4, 20, 60, 1, false, true, false, "Nachbarn hören Hilferufe aus Wohnung.", "Tür mit Ziehfix geöffnet, Übergabe an Rettungsdienst."),
            new("TH", "TH 1", "Tierrettung", 2, 30, 90, 1, false, true, false, "Katze sitzt seit zwei Tagen auf Baum fest.", "Tier mit Steckleiter gerettet und an Besitzer übergeben."),
            new("RD", "RD", "Tragehilfe für den Rettungsdienst", 3, 30, 60, 1, false, false, false, "Rettungsdienst fordert Tragehilfe an.", "Patient mit Schleifkorbtrage aus Obergeschoss gebracht."),
            new("SO", "SO", "Wespennest", 4, 20, 45, 1, false, true, false, "Wespennest im Rollladenkasten eines Kindergartens.", "Nest unter Schutzkleidung entfernt."),
            new("SO", "SO", "Personensuche", 1, 120, 300, 3, false, false, true, "Ältere Person aus Pflegeheim vermisst.", "Flächensuche mit Polizei, Person nach zwei Stunden unverletzt gefunden."),
            new("FA", "BMA", "Brandmeldeanlage ausgelöst", 7, 20, 45, 2, false, false, false, "Brandmeldeanlage im Gewerbebetrieb ausgelöst.", "Erkundung, Ursache Wasserdampf, Anlage zurückgestellt."),
            new("FA", "B 1", "Rauchmelder piept", 3, 20, 40, 1, false, false, false, "Heimrauchmelder piept in verschlossener Wohnung.", "Kontrolle von außen, Batterie leer, kein Brand."),
        ];

        /// <summary>
        /// Einsätze der letzten zwei Jahre (rund 45 im Jahr) mit Fahrzeugen der Abteilung am Einsatzort, bei größeren Lagen mit der Innenstadt, und
        /// Teilnehmenden der beteiligten Abteilungen. Ältere Berichte sind freigegeben, die jüngsten noch in Bearbeitung.
        /// </summary>
        private void EinsaetzeAnlegen(string format, HashSet<string> vergeben, Dictionary<string, StatistikKategorie> kategorien)
        {
            var anzahl = 85 + zufall.Next(0, 15);
            var gewichte = Einsatzarten.Sum(a => a.Gewicht);
            var ortsgewicht = Orte.Sum(o => o.Personen);
            var alarme = Enumerable.Range(0, anzahl).Select(_ =>
            {
                var tag = heute.AddDays(-zufall.Next(1, 730));
                // Tagsüber häufiger als nachts.
                var stunde = Wurf(0.75) ? zufall.Next(7, 22) : Wahl([0, 1, 2, 3, 4, 5, 6, 22, 23]);
                return Ortszeit.TagesbeginnUtc(tag).AddHours(stunde).AddMinutes(zufall.Next(0, 60));
            }).Order().ToList();
            var laufend = new Dictionary<int, int>();

            for (var i = 0; i < alarme.Count; i++)
            {
                var alarmiert = alarme[i];
                var tag = Ortszeit.Datum(alarmiert);
                var r = zufall.Next(gewichte);
                var vorlage = Einsatzarten.First(a => (r -= a.Gewicht) < 0);
                var o = zufall.Next(ortsgewicht);
                var ort = Orte.First(x => (o -= x.Personen) < 0);

                string nummer;
                do
                {
                    laufend[tag.Year] = laufend.GetValueOrDefault(tag.Year) + 1;
                    nummer = Einsatznummer.Bilden(format, tag.Year, laufend[tag.Year]);
                }
                while (!vergeben.Add(nummer));

                var dauer = zufall.Next(vorlage.MinutenVon, vorlage.MinutenBis + 1);
                var ausgerueckt = alarmiert.AddMinutes(zufall.Next(3, 8));
                var eintreffen = ausgerueckt.AddMinutes(zufall.Next(3, 12));
                var ende = alarmiert.AddMinutes(dauer);
                var alter = heute.DayNumber - tag.DayNumber;
                var einsatz = new Einsatz
                {
                    Einsatznummer = nummer,
                    LeitstellenNummer = $"{PersonalnummerPraefix}{tag:yyyyMMdd}-{i + 1:000}",
                    Stichwort = vorlage.Stichwort,
                    StichwortBeschreibung = vorlage.Beschreibung,
                    StichwortKategorie = kategorien[vorlage.Kategorie].Bezeichnung,
                    Meldung = vorlage.Beschreibung,
                    Strasse = Wahl(ort.Strassen),
                    Hausnummer = $"{zufall.Next(1, 60)}",
                    Plz = "73099",
                    Ort = "Adelberg",
                    Ortsteil = ort.Name,
                    Latitude = 48.76 + (zufall.NextDouble() - 0.5) * 0.04,
                    Longitude = 9.60 + (zufall.NextDouble() - 0.5) * 0.06,
                    Alarmiert = alarmiert,
                    Ausgerueckt = ausgerueckt,
                    EintreffenEinsatzort = eintreffen,
                    Einsatzende = ende,
                    EinsatzbereitschaftHergestellt = ende.AddMinutes(zufall.Next(15, 60)),
                    StatistikKategorieId = kategorien[vorlage.Kategorie].Id,
                    Rechtsgrundlage = vorlage.Kostenersatz ? Rechtsgrundlage.Para2Abs2 : Rechtsgrundlage.Para2Abs1,
                    Lagebericht = vorlage.Lage,
                    Massnahmen = vorlage.Massnahmen,
                    Gerettete = vorlage.Stichwort is "TH 2" or "B 3" ? 1 : 0,
                    Verletzte = vorlage.Stichwort == "TH 2" ? zufall.Next(1, 3) : 0,
                    Ueberoertlich = vorlage.Ueberoertlich && Wurf(0.3),
                    // Der jüngste Bericht ist noch ein Entwurf, die zwei davor und alles aus den letzten drei Wochen in Bearbeitung.
                    Status = i == alarme.Count - 1 ? EinsatzStatus.Entwurf
                        : i >= alarme.Count - 3 || alter <= 21 ? EinsatzStatus.InBearbeitung : EinsatzStatus.Freigegeben,
                };
                if (einsatz.Status == EinsatzStatus.Freigegeben)
                {
                    einsatz.FreigegebenAm = Ortszeit.TagesbeginnUtc(tag.AddDays(zufall.Next(2, 14))).AddHours(20);
                }

                db.Einsaetze.Add(einsatz);
                Einsaetze++;

                // Fahrzeuge: zuerst die der Abteilung (Löschfahrzeug vor MTW), bei größeren Lagen dazu Fahrzeuge der Innenstadt.
                static int Rang(Fahrzeug f) => f.Typ.StartsWith("HLF") ? 0 : f.Typ.StartsWith("LF") || f.Typ.StartsWith("TSF") || f.Typ.StartsWith("MLF") ? 1
                    : f.Typ.StartsWith("TLF") ? 2 : f.Typ == "MTW" ? 3 : 4;
                var eigene = Fahrzeuge.Where(f => f.Funkrufname.StartsWith($"Florian Adelberg {ort.Nummer}/") && f.IndienstAm <= tag).OrderBy(Rang).ToList();
                var ausgewaehlt = eigene.Take(vorlage.Fahrzeuge).ToList();
                if (vorlage.Fahrzeuge >= 3 && ort.Nummer != 1)
                {
                    ausgewaehlt.AddRange(Fahrzeuge.Where(f => f.Funkrufname is "Florian Adelberg 1/46" or "Florian Adelberg 1/11"
                        || (vorlage.Stichwort == "TH 2" && f.Funkrufname == "Florian Adelberg 1/52") || (vorlage.Stichwort == "B 3" && f.Funkrufname == "Florian Adelberg 1/33")));
                }

                var abteilungenImEinsatz = ausgewaehlt.Select(f => int.Parse(f.Funkrufname["Florian Adelberg ".Length..].Split('/')[0])).ToHashSet();
                var verfuegbar = Personen.Where(p => abteilungenImEinsatz.Contains(p.Ort) && ImEinsatzdienst(p, tag) && Wurf(0.25 + p.Fleiss * 0.45))
                    .OrderBy(_ => zufall.Next()).ToList();
                Person? leiter = null;

                foreach (var fahrzeug in ausgewaehlt)
                {
                    var abteilungsnummer = int.Parse(fahrzeug.Funkrufname["Florian Adelberg ".Length..].Split('/')[0]);
                    var besatzung = verfuegbar.Where(p => p.Ort == abteilungsnummer).Take(fahrzeug.Sitzplaetze ?? 6).ToList();
                    if (besatzung.Count == 0)
                    {
                        continue;
                    }

                    verfuegbar.RemoveAll(besatzung.Contains);
                    var eingesetzt = new EinsatzFahrzeug
                    {
                        EinsatzId = einsatz.Id, FahrzeugId = fahrzeug.Id, Ausgerueckt = ausgerueckt.AddMinutes(zufall.Next(0, 4)),
                        Eingetroffen = eintreffen.AddMinutes(zufall.Next(0, 6)), Zurueck = ende.AddMinutes(zufall.Next(5, 20)),
                        Einsatzbereit = einsatz.EinsatzbereitschaftHergestellt, Kilometer = zufall.Next(2, 25),
                    };
                    db.EinsatzFahrzeuge.Add(eingesetzt);

                    var fuehrer = besatzung.OrderByDescending(p => Hat(p, "ZF", tag) ? 3 : Hat(p, "GF", tag) ? 2 : Hat(p, "TF", tag) ? 1 : 0).First();
                    var maschinist = besatzung.FirstOrDefault(p => p != fuehrer && Hat(p, "MA", tag));
                    if (leiter is null || (Hat(fuehrer, "ZF", tag) && !Hat(leiter, "ZF", tag)))
                    {
                        leiter = fuehrer;
                    }

                    var trupps = new[] { "Angriffstrupp", "Angriffstrupp", "Wassertrupp", "Wassertrupp", "Schlauchtrupp", "Schlauchtrupp", "Melder" };
                    var t = 0;
                    var atemschutz = vorlage.Atemschutz ? 2 + zufall.Next(0, 3) : 0;
                    foreach (var person in besatzung)
                    {
                        var funktion = person == fuehrer ? (fahrzeug.Sitzplaetze >= 9 ? "Gruppenführer" : "Staffelführer")
                            : person == maschinist ? "Maschinist" : trupps[t++ % trupps.Length];
                        var mitPa = atemschutz > 0 && funktion is "Angriffstrupp" or "Wassertrupp" && Hat(person, "AGT", tag);
                        if (mitPa)
                        {
                            atemschutz--;
                        }

                        db.EinsatzTeilnahmen.Add(new EinsatzTeilnahme
                        {
                            EinsatzId = einsatz.Id, MitgliedId = person.Mitglied.Id, EinsatzFahrzeugId = eingesetzt.Id, Funktion = funktion,
                            Atemschutz = mitPa, AtemschutzMinuten = mitPa ? zufall.Next(15, 36) : null, Quelle = TeilnahmeQuelle.Fe2Rueckmeldung,
                            Bestaetigt = einsatz.Status == EinsatzStatus.Freigegeben && Wurf(0.6),
                        });
                    }
                }

                einsatz.EinsatzleiterId = leiter?.Mitglied.Id;
                if (leiter is null && einsatz.Status == EinsatzStatus.Freigegeben)
                {
                    // Ohne Teilnehmende keine Freigabe.
                    einsatz.Status = EinsatzStatus.InBearbeitung;
                    einsatz.FreigegebenAm = null;
                }
            }
        }

        private void FuhrparkAnlegen()
        {
            foreach (var vorlage in Fuhrpark)
            {
                var ort = Orte.Single(o => o.Nummer == vorlage.Abteilung);
                var indienst = new DateOnly(vorlage.Baujahr, zufall.Next(3, 12), zufall.Next(1, 28));
                var fahrzeug = new Fahrzeug
                {
                    Funkrufname = $"Florian Adelberg {vorlage.Abteilung}/{vorlage.Kennzahl}",
                    Typ = vorlage.Typ,
                    Kennzeichen = $"GP-FA {vorlage.Abteilung}{vorlage.Kennzahl}",
                    Hersteller = vorlage.Fahrgestell,
                    Aufbau = vorlage.Aufbau,
                    Baujahr = vorlage.Baujahr,
                    IndienstAm = indienst,
                    Sitzplaetze = vorlage.Sitze,
                    PruefvorlageId = geraetearten[vorlage.Schwer ? "FZG-LKW" : "FZG-PKW"].Id,
                    Bemerkung = $"Abteilung {ort.Name} (Testdaten)",
                };
                db.Fahrzeuge.Add(fahrzeug);
                Fahrzeuge.Add(fahrzeug);
                Pruefen(null, fahrzeug, geraetearten[vorlage.Schwer ? "FZG-LKW" : "FZG-PKW"].Pruefplan, indienst, "TÜV SÜD");

                foreach (var ladung in Beladung(vorlage.Beladung))
                {
                    GeraeteAnlegen(ladung, fahrzeug, null, vorlage.Baujahr);
                }
            }

            foreach (var ort in Orte)
            {
                foreach (var ladung in Magazin(ort.Nummer))
                {
                    GeraeteAnlegen(ladung, null, $"Gerätehaus {ort.Name}, {ladung.Lagerort}", 2005);
                }
            }

            Maengel();
        }

        private void GeraeteAnlegen(Ladung ladung, Fahrzeug? fahrzeug, string? lagerort, int abBaujahr)
        {
            var art = geraetearten[ladung.Art];
            for (var i = 0; i < ladung.Anzahl; i++)
            {
                var baujahr = Math.Min(heute.Year, Math.Max(abBaujahr, heute.Year - 18) + zufall.Next(0, 6));
                var herstellung = Spaetestens(new DateOnly(baujahr, zufall.Next(1, 13), zufall.Next(1, 28)));
                // Haltegurte erreichen öfter die Lebensdauer; ein paar sollen bald ausgesondert werden.
                if (art.AussonderungNachJahren is { } lebensdauer && Wurf(0.25))
                {
                    herstellung = heute.AddYears(-lebensdauer).AddDays(zufall.Next(20, 200));
                }

                var geraet = new Geraet
                {
                    Inventarnummer = NeueInventarnummer(),
                    GeraeteartId = art.Id,
                    Bezeichnung = ladung.Bezeichnung,
                    Hersteller = ladung.Hersteller,
                    Typ = ladung.Typ,
                    Seriennummer = $"{zufall.Next(100000, 999999)}",
                    Herstelldatum = herstellung,
                    Baujahr = herstellung.Year,
                    IndienstAm = Spaetestens(herstellung.AddDays(zufall.Next(30, 200))),
                    FahrzeugId = fahrzeug?.Id,
                    Lagerort = lagerort ?? ladung.Lagerort,
                };
                db.Geraete.Add(geraet);
                Geraete.Add(geraet);

                // Ein paar neue Geräte sind noch nie geprüft (sofort rot).
                if (!Wurf(0.015))
                {
                    Pruefen(geraet, null, art.Pruefplan, geraet.IndienstAm.Value, null);
                }
            }
        }

        /// <summary>Letzte (und bei kurzen Intervallen vorletzte) Prüfung je Prüfplan-Position; ein Teil ist bald fällig oder überfällig.</summary>
        private void Pruefen(Geraet? geraet, Fahrzeug? fahrzeug, IEnumerable<PruefplanPosition> plan, DateOnly ab, string? extern_)
        {
            foreach (var position in plan)
            {
                DateOnly datum;
                if (position.IntervallMonate == 0)
                {
                    datum = Max(Tage(5, 300), ab);
                }
                else
                {
                    var intervallTage = position.IntervallMonate * 30;
                    var r = zufall.NextDouble();
                    datum = r < 0.07 ? heute.AddDays(-(intervallTage + zufall.Next(5, 120)))
                        : r < 0.17 ? heute.AddDays(-(intervallTage - zufall.Next(1, 28)))
                        : heute.AddDays(-zufall.Next(7, Math.Max(8, intervallTage - 30)));
                    datum = Max(datum, ab);
                }

                if (datum > heute)
                {
                    continue;
                }

                if (position.IntervallMonate is > 0 and <= 24 && datum.AddMonths(-position.IntervallMonate) >= ab)
                {
                    PruefungAnlegen(geraet, fahrzeug, position, datum.AddMonths(-position.IntervallMonate).AddDays(zufall.Next(-10, 10)), extern_, PruefErgebnis.Bestanden);
                }

                var ergebnis = Wurf(0.94) ? PruefErgebnis.Bestanden : PruefErgebnis.BestandenMitMaengeln;
                var pruefung = PruefungAnlegen(geraet, fahrzeug, position, datum, extern_, ergebnis);
                if (ergebnis == PruefErgebnis.BestandenMitMaengeln)
                {
                    db.Maengel.Add(new Mangel
                    {
                        GeraetId = geraet?.Id, FahrzeugId = fahrzeug?.Id, PruefungId = pruefung.Id, GemeldetVonId = geraetewart?.Id,
                        GemeldetAm = Ortszeit.TagesbeginnUtc(datum).AddHours(19), Schwere = MangelSchwere.Info,
                        Beschreibung = Wahl(["Kennzeichnung schwer lesbar", "Leichte Gebrauchsspuren, beobachten", "Reinigung erforderlich", "Aufkleber erneuern"]),
                        Status = Wurf(0.6) ? MangelStatus.Behoben : MangelStatus.Offen,
                    });
                }
            }
        }

        private Pruefung PruefungAnlegen(Geraet? geraet, Fahrzeug? fahrzeug, PruefplanPosition position, DateOnly datum, string? extern_, PruefErgebnis ergebnis)
        {
            var fachfirma = position.Pruefart is Pruefart.Hauptuntersuchung or Pruefart.Hersteller or Pruefart.Elektro;
            var pruefung = new Pruefung
            {
                GeraetId = geraet?.Id,
                FahrzeugId = fahrzeug?.Id,
                PruefplanPositionId = position.Id,
                Pruefart = position.Pruefart,
                Datum = datum,
                Ergebnis = ergebnis,
                PrueferId = fachfirma ? null : (geraet?.GeraeteartId == geraetearten["PA"].Id ? atemschutzgeraetewart : geraetewart)?.Id,
                PrueferExtern = fachfirma ? position.Pruefart switch
                {
                    Pruefart.Hauptuntersuchung => extern_ ?? "TÜV SÜD",
                    Pruefart.Elektro => "Elektro Bühler (fiktiv)",
                    _ => geraet?.Hersteller ?? "Fachfirma",
                } : null,
                Abgeschlossen = true,
            };
            pruefung.NaechsteFaelligkeitVorbelegen(position);
            if (position.IntervallMonate == 0)
            {
                pruefung.NaechsteFaellig = null;
            }

            db.Pruefungen.Add(pruefung);
            return pruefung;
        }

        private void Maengel()
        {
            Mitglied Melder() => Wahl(Personen.Where(p => p.Abteilung == AbteilungCode.Einsatz && p.EinsatzBis is null).ToList()).Mitglied;
            DateTimeOffset Vor(int tage) => Ortszeit.TagesbeginnUtc(heute.AddDays(-tage)).AddHours(20).AddMinutes(zufall.Next(0, 50));

            Fahrzeug Fz(string funkrufname) => Fahrzeuge.Single(f => f.Funkrufname == $"Florian Adelberg {funkrufname}");
            Geraet Gr(string bezeichnung, Fahrzeug fahrzeug) => Geraete.First(g => g.FahrzeugId == fahrzeug.Id && g.Bezeichnung == bezeichnung);

            db.Maengel.AddRange(
                new Mangel { FahrzeugId = Fz("2/23").Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(40), Beschreibung = "Rollladen G2 klemmt, Korrosion an der Führungsschiene", Schwere = MangelSchwere.Einschraenkung, Status = MangelStatus.InArbeit, Massnahme = "Angebot Aufbauhersteller angefragt" },
                new Mangel { FahrzeugId = Fz("4/47").Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(9), Beschreibung = "Batterie schwach, springt nur mit Erhaltungsladegerät an", Schwere = MangelSchwere.Einschraenkung, Status = MangelStatus.Offen },
                new Mangel { FahrzeugId = Fz("1/46").Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(3), Beschreibung = "Umfeldbeleuchtung rechts ohne Funktion", Schwere = MangelSchwere.Info, Status = MangelStatus.Offen },
                new Mangel { GeraetId = Gr("Motorsäge", Fz("1/52")).Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(14), Beschreibung = "Kettenbremse löst nicht zuverlässig aus", Schwere = MangelSchwere.AusserBetrieb, Status = MangelStatus.Offen },
                new Mangel { GeraetId = Gr("Hebekissen 12 t", Fz("2/43")).Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(21), Beschreibung = "Füllschlauch am Kupplungsstück undicht", Schwere = MangelSchwere.Einschraenkung, Status = MangelStatus.Offen },
                new Mangel { GeraetId = Gr("Steckleiter 4-teilig", Fz("3/48")).Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(60), Beschreibung = "Sprosse am Mittelteil verbogen", Schwere = MangelSchwere.AusserBetrieb, Status = MangelStatus.InArbeit, Massnahme = "Zur Reparatur beim Hersteller" },
                new Mangel { GeraetId = Gr("Wärmebildkamera", Fz("1/46")).Id, GemeldetVonId = Melder().Id, GemeldetAm = Vor(120), Beschreibung = "Akku hält nur noch 20 Minuten", Schwere = MangelSchwere.Einschraenkung, Status = MangelStatus.Behoben, BehobenAm = Vor(100), Massnahme = "Neuer Akku eingesetzt" });
        }

        private void KleiderkammerAnlegen()
        {
            string Groesse(Groessenart art) => art switch
            {
                Groessenart.Jacke or Groessenart.Hose or Groessenart.Oberteil => Wahl(["S", "M", "M", "L", "L", "L", "XL", "XL", "XXL", "3XL"]),
                Groessenart.Schuhe => $"{zufall.Next(38, 48)}",
                Groessenart.Handschuhe => $"{zufall.Next(7, 12)}",
                Groessenart.Kopf => Wahl(["M", "L", "L"]),
                _ => "",
            };

            var ausgestattet = Personen.Where(p => p.Abteilung == AbteilungCode.Einsatz && p.EinsatzBis is null).ToList();
            // Ein paar Neue in der Probezeit warten noch auf ihre Grundausstattung (Nachbestellbedarf).
            var ohneAusstattung = ausgestattet.Where(p => p.EinsatzAb > heute.AddMonths(-3)).Take(4).ToHashSet();

            foreach (var person in ausgestattet)
            {
                var groessen = new[] { Groessenart.Jacke, Groessenart.Hose, Groessenart.Oberteil, Groessenart.Schuhe, Groessenart.Handschuhe, Groessenart.Kopf }
                    .ToDictionary(a => a, Groesse);
                foreach (var (art, wert) in groessen)
                {
                    db.Groessenangaben.Add(new Groessenangabe { MitgliedId = person.Mitglied.Id, Groessenart = art, Wert = wert, ErfasstAm = Spaetestens(person.EinsatzAb!.Value.AddDays(7)) });
                }

                if (ohneAusstattung.Contains(person))
                {
                    continue;
                }

                var datum = Spaetestens(Max(person.EinsatzAb!.Value.AddDays(zufall.Next(10, 60)), Tage(30, 365 * 11)));
                var vorgang = Vorgang(person.Mitglied, datum);

                foreach (var code in new[] { "HELM", "EJ", "EH" })
                {
                    var a = artikel[code];
                    var geraet = PsaStueck(a, groessen[a.Groessenart], datum, null);
                    Ausgeben(person.Mitglied, a, geraet.Groesse!, 1, geraet, datum, vorgang);
                }

                var zeilen = new List<Ausstattung>();
                foreach (var (code, menge) in new[] { ("HSH", 1), ("STF", 1), ("FSH", 1), ("TSH", 2), ("PLS", 1) })
                {
                    var a = artikel[code];
                    zeilen.Add(Ausgeben(person.Mitglied, a, a.Groessenart == Groessenart.Einheitsgroesse ? "" : groessen[a.Groessenart], menge, null, datum, vorgang));
                }

                // Verschleiß: Handschuhe und Schuhe nach ein paar Jahren getauscht, die alten ausgesondert.
                if (datum < heute.AddYears(-3) && Wurf(0.5))
                {
                    var tausch = Tage(20, Math.Min(900, heute.DayNumber - datum.DayNumber - 400));
                    var ruecknahme = Vorgang(person.Mitglied, tausch, VorgangArt.Ruecknahme);
                    var neu = Vorgang(person.Mitglied, tausch);
                    foreach (var alt in zeilen.Where(z => z.ArtikelId == artikel["HSH"].Id || (z.ArtikelId == artikel["STF"].Id && Wurf(0.5))))
                    {
                        (alt.ZurueckAm, alt.RuecknahmeVorgangId, alt.Zustand) = (tausch, ruecknahme.Id, RuecknahmeZustand.Aussondern);
                        Ausgeben(person.Mitglied, artikel.Values.Single(a => a.Id == alt.ArtikelId), alt.Groesse, alt.Menge, null, tausch, neu);
                    }
                }
            }

            // Altersabteilung: Dienstkleidung für Veranstaltungen.
            foreach (var person in Personen.Where(p => p.Abteilung == AbteilungCode.Alters))
            {
                var datum = person.AltersAb!.Value.AddDays(zufall.Next(7, 60)) is var d && d < heute ? d : heute;
                var vorgang = Vorgang(person.Mitglied, datum);
                Ausgeben(person.Mitglied, artikel["PLS"], Wahl(["L", "XL", "XXL"]), 2, null, datum, vorgang);
            }

            // Ausgetretene haben alles zurückgegeben.
            foreach (var person in Personen.Where(p => p.Abteilung == AbteilungCode.Einsatz && p.EinsatzBis is not null))
            {
                var datum = person.EinsatzAb!.Value.AddDays(zufall.Next(10, 60));
                var vorgang = Vorgang(person.Mitglied, datum);
                var zurueck = person.EinsatzBis!.Value.AddDays(zufall.Next(0, 30)) is var z && z < heute ? z : heute;
                var ruecknahme = Vorgang(person.Mitglied, zurueck, VorgangArt.Ruecknahme);
                foreach (var (code, groesse, menge) in new[] { ("HSH", "10", 1), ("STF", "43", 1), ("FSH", "", 1), ("TSH", "L", 2) })
                {
                    var zeile = Ausgeben(person.Mitglied, artikel[code], groesse, menge, null, datum, vorgang);
                    (zeile.ZurueckAm, zeile.RuecknahmeVorgangId, zeile.Zustand) = (zurueck, ruecknahme.Id, code == "HSH" ? RuecknahmeZustand.Aussondern : RuecknahmeZustand.Einlagern);
                }
            }

            // Jugendfeuerwehr bekommt T-Shirts.
            foreach (var person in Personen.Where(p => p.Abteilung == AbteilungCode.Jugend))
            {
                var datum = Spaetestens(person.JugendAb!.Value.AddDays(zufall.Next(7, 40)));
                Ausgeben(person.Mitglied, artikel["TSH"], Wahl(["XS", "S", "M", "L"]), 2, null, datum, Vorgang(person.Mitglied, datum));
            }

            // Lager: Einzelstücke zum Ausgeben und Mengenartikel je Größe, teils unter Mindestbestand.
            const string Lager = Kleiderlager;
            foreach (var (code, groessen) in new[] { ("HELM", new[] { "M", "L", "L" }), ("EJ", new[] { "M", "L", "XL" }), ("EH", new[] { "M", "L", "XL" }) })
            {
                foreach (var g in groessen)
                {
                    PsaStueck(artikel[code], g, Tage(30, 400), Lager);
                }
            }

            foreach (var (code, groessen) in new[]
            {
                ("HSH", new[] { "8", "9", "10", "11" }), ("STF", new[] { "40", "42", "43", "44", "45", "46" }), ("FSH", new[] { "" }),
                ("TSH", new[] { "S", "M", "L", "XL", "XXL" }), ("PLS", new[] { "M", "L", "XL" }),
            })
            {
                foreach (var g in groessen.Where(g => !vorhandeneBestaende.Contains((artikel[code].Id, Domain.Kleiderkammer.Groesse.Normalisieren(g)))))
                {
                    var mindest = code is "TSH" or "PLS" ? 3 : 2;
                    db.Lagerbestaende.Add(new Lagerbestand
                    {
                        ArtikelId = artikel[code].Id, Groesse = Domain.Kleiderkammer.Groesse.Normalisieren(g),
                        Menge = Wurf(0.25) ? zufall.Next(0, mindest) : zufall.Next(mindest, mindest + 8), Mindestmenge = mindest, Lagerort = Lager,
                    });
                }
            }
        }

        private Geraet PsaStueck(Artikel artikel, string groesse, DateOnly ab, string? lagerort)
        {
            var (hersteller, typ) = artikel.Code switch
            {
                "HELM" => Wahl([("Rosenbauer", "HEROS-titan"), ("Dräger", "HPS 7000"), ("MSA", "F1 XF")]),
                "EJ" => Wahl([("S-GARD", "HuPF Teil 2 Jacke"), ("Texport", "Fire Max 3")]),
                _ => Wahl([("S-GARD", "HuPF Teil 2 Hose"), ("Texport", "Fire Max 3")]),
            };
            var herstellung = ab.AddDays(-zufall.Next(30, 300));
            var geraet = new Geraet
            {
                Inventarnummer = NeueInventarnummer(),
                GeraeteartId = artikel.GeraeteartId!.Value,
                Bezeichnung = artikel.Bezeichnung,
                Hersteller = hersteller,
                Typ = typ,
                Groesse = Domain.Kleiderkammer.Groesse.Normalisieren(groesse),
                Herstelldatum = herstellung,
                Baujahr = herstellung.Year,
                BeschafftAm = herstellung.AddDays(20),
                IndienstAm = ab,
                Lagerort = lagerort,
            };
            db.Geraete.Add(geraet);
            Geraete.Add(geraet);
            Pruefen(geraet, null, geraetearten.Values.Single(g => g.Id == artikel.GeraeteartId).Pruefplan, ab, null);
            return geraet;
        }

        private Kleiderkammervorgang Vorgang(Mitglied mitglied, DateOnly datum, VorgangArt art = VorgangArt.Ausgabe)
        {
            var nummer = vorgangsnummern.GetValueOrDefault(datum.Year) + 1;
            vorgangsnummern[datum.Year] = nummer;
            var zeitpunkt = Ortszeit.TagesbeginnUtc(datum).AddHours(19).AddMinutes(zufall.Next(0, 90));
            var vorgang = new Kleiderkammervorgang
            {
                Nummer = $"KK-{datum.Year}-{nummer:0000}", Art = art, MitgliedId = mitglied.Id, Zeitpunkt = zeitpunkt, QuittiertAm = zeitpunkt,
            };
            db.Kleiderkammervorgaenge.Add(vorgang);
            return vorgang;
        }

        private Ausstattung Ausgeben(Mitglied mitglied, Artikel artikel, string groesse, int menge, Geraet? geraet, DateOnly datum, Kleiderkammervorgang vorgang)
        {
            var zeile = new Ausstattung
            {
                MitgliedId = mitglied.Id, ArtikelId = artikel.Id, Groesse = Domain.Kleiderkammer.Groesse.Normalisieren(groesse), Menge = menge,
                GeraetId = geraet?.Id, AusgegebenAm = datum, AusgabeVorgangId = vorgang.Id,
            };
            db.Ausstattungen.Add(zeile);
            return zeile;
        }

        private void Beschaffungen()
        {
            var motorsaege = db.Maengel.Local.First(m => m.Beschreibung.StartsWith("Kettenbremse"));
            db.Beschaffungen.AddRange(
                new Beschaffung { Bezeichnung = "Ersatz Motorsäge RW", GeraeteartId = geraetearten["MOTORSAEGE"].Id, MangelId = motorsaege.Id, Prioritaet = Prioritaet.Einsatzrelevant, Status = BeschaffungStatus.Bedarf, Kostenschaetzung = 1450m, Haushaltsjahr = heute.Year, Bemerkung = Kennung },
                new Beschaffung { Bezeichnung = "Löschgruppenfahrzeug LF 10 für Abteilung Buchenbach (Ersatz TSF)", FahrzeugId = Fahrzeuge.Single(f => f.Funkrufname.EndsWith("4/47")).Id, Prioritaet = Prioritaet.Hoch, Status = BeschaffungStatus.Angemeldet, Kostenschaetzung = 480000m, Foerderfaehig = true, Haushaltsjahr = heute.Year + 1, Bemerkung = Kennung, Begruendung = "TSF Baujahr 1994, Ersatz laut Feuerwehrbedarfsplan" },
                new Beschaffung { Bezeichnung = "Einsatzjacken Größe L", ArtikelId = artikel["EJ"].Id, Groesse = "L", Menge = 5, Prioritaet = Prioritaet.Normal, Status = BeschaffungStatus.Bestellt, Kostenschaetzung = 3250m, Lieferant = "Feuerwehrbedarf Muster (fiktiv)", BestelltAm = heute.AddDays(-18), Haushaltsjahr = heute.Year, Bemerkung = Kennung },
                new Beschaffung { Bezeichnung = "Hebekissensatz", GeraeteartId = geraetearten["HEBEKISSEN"].Id, Prioritaet = Prioritaet.Normal, Status = BeschaffungStatus.Genehmigt, Kostenschaetzung = 6800m, GenehmigtAm = heute.AddDays(-30), Haushaltsjahr = heute.Year, Bemerkung = Kennung },
                new Beschaffung { Bezeichnung = "Wärmebildkamera für HLF 10", GeraeteartId = geraetearten["SONSTIGES"].Id, Prioritaet = Prioritaet.Normal, Status = BeschaffungStatus.Geliefert, Kostenschaetzung = 5200m, Istkosten = 4980m, GeliefertAm = heute.AddDays(-75), Haushaltsjahr = heute.Year, Bemerkung = Kennung });
        }
    }
}
