using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Kennzahlen eines Kalenderjahres für Jahresbericht (Hauptversammlung) und Jahresmeldung an den
/// Kreisbrandmeister. Einsätze zählen nur freigegeben, Dienste nur durchgeführt; Personalstand zum 31.12.
/// </summary>
public sealed class Jahresstatistik(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Einsätze einer Kategorie der Landesstatistik.</summary>
    /// <param name="Code">Code der Kategorie, z. B. „BR“.</param>
    /// <param name="Bezeichnung">Bezeichnung, z. B. „Brand“.</param>
    /// <param name="Anzahl">Freigegebene Einsätze dieser Kategorie.</param>
    /// <param name="Personenstunden">Summe der Einsatzstunden aller Teilnehmer, auf eine Stelle gerundet.</param>
    public sealed record Kategorie(string Code, string Bezeichnung, int Anzahl, decimal Personenstunden);

    /// <summary>Kennzahlen der Einsätze eines Jahres; gezählt werden nur freigegebene Einsatzberichte.</summary>
    /// <param name="Gesamt">Alle Einsätze des Jahres, auch noch nicht freigegebene.</param>
    /// <param name="Freigegeben">Freigegebene Einsätze; Grundlage aller weiteren Zahlen.</param>
    /// <param name="NachKategorie">Einsätze je Statistikkategorie, auch Kategorien ohne Einsatz.</param>
    /// <param name="Gerettete">Gerettete Personen.</param>
    /// <param name="Verletzte">Verletzte Personen.</param>
    /// <param name="Tote">Tote Personen.</param>
    /// <param name="VerletzteFeuerwehr">Verletzte Einsatzkräfte.</param>
    /// <param name="Ueberoertlich">Einsätze in überörtlicher Hilfe.</param>
    /// <param name="Personenstunden">Summe der Einsatzstunden aller Teilnehmer.</param>
    /// <param name="DurchschnittTeilnehmer">Durchschnittliche Zahl der Teilnehmer je Einsatz.</param>
    /// <param name="AusrueckzeitMedianMinuten">Median der Minuten von der Alarmierung bis zum Ausrücken; leer ohne Ausrückzeiten.</param>
    /// <param name="WerktagsTagsueber">Einsätze montags bis freitags zwischen 6:00 und 18:00 Uhr (Tagesverfügbarkeit).</param>
    /// <param name="NachMonat">Einsätze je Kalendermonat, Januar zuerst.</param>
    public sealed record EinsatzKennzahlen(
        int Gesamt,
        int Freigegeben,
        IReadOnlyList<Kategorie> NachKategorie,
        int Gerettete,
        int Verletzte,
        int Tote,
        int VerletzteFeuerwehr,
        int Ueberoertlich,
        decimal Personenstunden,
        decimal DurchschnittTeilnehmer,
        double? AusrueckzeitMedianMinuten,
        int WerktagsTagsueber,
        IReadOnlyList<int> NachMonat);

    /// <summary>Kennzahlen einer Dienstart.</summary>
    /// <param name="Art">Dienstart.</param>
    /// <param name="Anzahl">Durchgeführte Dienste.</param>
    /// <param name="Stunden">Summe der Dienstdauern.</param>
    /// <param name="Teilnahmen">Anwesenheiten.</param>
    /// <param name="Personenstunden">Summe der anwesenden Stunden aller Teilnehmer.</param>
    public sealed record DienstArtKennzahl(DienstArt Art, int Anzahl, decimal Stunden, int Teilnahmen, decimal Personenstunden);

    /// <summary>Kennzahlen der Dienste eines Jahres.</summary>
    /// <param name="NachArt">Kennzahlen je Dienstart.</param>
    /// <param name="FortbildungZielStunden">Jahresziel der Standortfortbildung in Stunden.</param>
    /// <param name="FortbildungZielErreicht">Aktive Mitglieder der Einsatzabteilung, die das Ziel erreicht haben.</param>
    /// <param name="FortbildungBasis">Aktive Mitglieder der Einsatzabteilung zum Stichtag.</param>
    public sealed record DienstKennzahlen(IReadOnlyList<DienstArtKennzahl> NachArt, decimal FortbildungZielStunden, int FortbildungZielErreicht, int FortbildungBasis);

    /// <summary>Personalstand einer Abteilung zum Stichtag.</summary>
    /// <param name="Code">Code der Abteilung.</param>
    /// <param name="Bezeichnung">Bezeichnung der Abteilung.</param>
    /// <param name="Gesamt">Mitglieder zum Stichtag.</param>
    /// <param name="Aktiv">Davon aktiv.</param>
    /// <param name="Probezeit">Davon in der Probezeit.</param>
    /// <param name="Befreit">Davon befreit.</param>
    /// <param name="Zugaenge">Zugänge im Jahr.</param>
    /// <param name="Abgaenge">Abgänge im Jahr.</param>
    /// <param name="Weiblich">Davon weiblich (für die Statistik des Landesfeuerwehrverbands); ohne Angabe zum Geschlecht nicht gezählt.</param>
    public sealed record AbteilungStand(string Code, string Bezeichnung, int Gesamt, int Aktiv, int Probezeit, int Befreit, int Zugaenge, int Abgaenge, int Weiblich);

    /// <summary>Eine Zeile mit Text und Anzahl, z. B. Übertritte, Abgangsgründe oder erworbene Qualifikationen.</summary>
    /// <param name="Text">Beschreibung.</param>
    /// <param name="Anzahl">Anzahl.</param>
    public sealed record Bewegung(string Text, int Anzahl);

    /// <summary>Mitglieder einer Altersgruppe.</summary>
    /// <param name="Bezeichnung">Altersgruppe, z. B. „31 bis 40“.</param>
    /// <param name="Anzahl">Anzahl der Mitglieder.</param>
    public sealed record Altersgruppe(string Bezeichnung, int Anzahl);

    /// <summary>Kennzahlen zum Personal.</summary>
    /// <param name="Abteilungen">Personalstand je Abteilung.</param>
    /// <param name="Uebertritte">Übertritte zwischen Abteilungen im Jahr.</param>
    /// <param name="Abgaenge">Abgänge nach Grund.</param>
    /// <param name="AltersstrukturEinsatzabteilung">Altersgruppen der Einsatzabteilung.</param>
    /// <param name="DurchschnittsalterEinsatzabteilung">Durchschnittsalter der Einsatzabteilung; leer ohne Mitglieder.</param>
    /// <param name="Altersgrenze">Altersgrenze des Einsatzdienstes.</param>
    /// <param name="ErreichenAltersgrenzeInFuenfJahren">Mitglieder, die in den nächsten fünf Jahren die Altersgrenze erreichen.</param>
    public sealed record PersonalKennzahlen(
        IReadOnlyList<AbteilungStand> Abteilungen,
        IReadOnlyList<Bewegung> Uebertritte,
        IReadOnlyList<Bewegung> Abgaenge,
        IReadOnlyList<Altersgruppe> AltersstrukturEinsatzabteilung,
        double? DurchschnittsalterEinsatzabteilung,
        int Altersgrenze,
        int ErreichenAltersgrenzeInFuenfJahren);

    /// <summary>Beförderung im Jahr.</summary>
    /// <param name="Datum">Tag der Beförderung.</param>
    /// <param name="Name">Name des Mitglieds.</param>
    /// <param name="Dienstgrad">Neuer Dienstgrad.</param>
    public sealed record Befoerderung(DateOnly Datum, string Name, string Dienstgrad);

    /// <summary>Einsätze eines Fahrzeugs im Jahr.</summary>
    /// <param name="Funkrufname">Funkrufname des Fahrzeugs.</param>
    /// <param name="Typ">Fahrzeugtyp, z. B. „HLF 10“.</param>
    /// <param name="Einsaetze">Freigegebene Einsätze mit diesem Fahrzeug.</param>
    /// <param name="Kilometer">Gefahrene Kilometer laut Einsatzbericht.</param>
    public sealed record FahrzeugKennzahl(string Funkrufname, string Typ, int Einsaetze, int Kilometer);

    /// <summary>Gesamter Jahresbericht.</summary>
    /// <param name="Jahr">Kalenderjahr.</param>
    /// <param name="Stichtag">Stichtag des Personalstands, der 31.12.</param>
    /// <param name="Einsaetze">Einsatzkennzahlen.</param>
    /// <param name="Dienste">Dienstkennzahlen.</param>
    /// <param name="Personal">Personalkennzahlen.</param>
    /// <param name="Befoerderungen">Beförderungen im Jahr.</param>
    /// <param name="Qualifikationen">Im Jahr erworbene Qualifikationen je Qualifikation.</param>
    /// <param name="Fahrzeuge">Kennzahlen je Fahrzeug.</param>
    /// <param name="Hinweise">Hinweise zur Vollständigkeit, z. B. noch nicht freigegebene Einsatzberichte.</param>
    public sealed record Bericht(
        int Jahr,
        DateOnly Stichtag,
        EinsatzKennzahlen Einsaetze,
        DienstKennzahlen Dienste,
        PersonalKennzahlen Personal,
        IReadOnlyList<Befoerderung> Befoerderungen,
        IReadOnlyList<Bewegung> Qualifikationen,
        IReadOnlyList<FahrzeugKennzahl> Fahrzeuge,
        IReadOnlyList<string> Hinweise);

    /// <summary>Berechnet den Jahresbericht für ein Kalenderjahr (Ortszeit).</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Bericht.</returns>
    public async Task<Bericht> ErstellenAsync(int jahr, CancellationToken ct = default)
    {
        var erster = new DateOnly(jahr, 1, 1);
        var stichtag = new DateOnly(jahr, 12, 31);
        var utcVon = Ortszeit.TagesbeginnUtc(erster);
        var utcBis = Ortszeit.TagesbeginnUtc(stichtag.AddDays(1));
        var hinweise = new List<string>();

        var einsaetze = await EinsaetzeAsync(utcVon, utcBis, hinweise, ct);
        var dienste = await DiensteAsync(utcVon, utcBis, stichtag, hinweise, ct);
        var personal = await PersonalAsync(erster, stichtag, ct);

        var befoerderungen = await db.DienstgradZuordnungen.AsNoTracking()
            .Where(d => d.Ab >= erster && d.Ab <= stichtag)
            .OrderBy(d => d.Ab)
            .Select(d => new Befoerderung(d.Ab, d.Mitglied!.Nachname + ", " + d.Mitglied.Vorname, d.Dienstgrad!.Bezeichnung))
            .ToListAsync(ct);

        var qualifikationen = (await db.QualifikationNachweise.AsNoTracking()
                .Where(q => q.ErworbenAm >= erster && q.ErworbenAm <= stichtag)
                .GroupBy(q => new { q.Qualifikation!.Bezeichnung, q.Qualifikation.Sortierung })
                .Select(g => new { g.Key.Bezeichnung, g.Key.Sortierung, Anzahl = g.Count() })
                .ToListAsync(ct))
            .OrderBy(q => q.Sortierung)
            .Select(q => new Bewegung(q.Bezeichnung, q.Anzahl))
            .ToList();

        var fahrzeuge = (await db.EinsatzFahrzeuge.AsNoTracking()
                .Where(f => f.Einsatz!.Status == EinsatzStatus.Freigegeben && f.Einsatz.Alarmiert >= utcVon && f.Einsatz.Alarmiert < utcBis)
                .GroupBy(f => new { f.Fahrzeug!.Funkrufname, f.Fahrzeug.Typ })
                .Select(g => new { g.Key.Funkrufname, g.Key.Typ, Anzahl = g.Count(), Kilometer = g.Sum(f => f.Kilometer ?? 0) })
                .ToListAsync(ct))
            .OrderBy(f => f.Funkrufname)
            .Select(f => new FahrzeugKennzahl(f.Funkrufname, f.Typ, f.Anzahl, f.Kilometer))
            .ToList();

        return new Bericht(jahr, stichtag, einsaetze, dienste, personal, befoerderungen, qualifikationen, fahrzeuge, hinweise);
    }

    private async Task<EinsatzKennzahlen> EinsaetzeAsync(DateTimeOffset utcVon, DateTimeOffset utcBis, List<string> hinweise, CancellationToken ct)
    {
        var alle = await db.Einsaetze.CountAsync(e => e.Alarmiert >= utcVon && e.Alarmiert < utcBis, ct);

        var einsaetze = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Status == EinsatzStatus.Freigegeben && e.Alarmiert >= utcVon && e.Alarmiert < utcBis)
            .Select(e => new
            {
                e.Alarmiert,
                e.Ausgerueckt,
                Ende = e.EinsatzbereitschaftHergestellt ?? e.Einsatzende,
                Code = e.StatistikKategorie!.Code,
                Bezeichnung = e.StatistikKategorie.Bezeichnung,
                Sortierung = e.StatistikKategorie.Sortierung,
                e.Gerettete,
                e.Verletzte,
                e.Tote,
                e.VerletzteFeuerwehr,
                e.Ueberoertlich,
                Teilnahmen = e.Teilnahmen.Select(t => new { t.Von, t.Bis }).ToList(),
            })
            .ToListAsync(ct);

        if (alle > einsaetze.Count)
        {
            hinweise.Add($"{alle - einsaetze.Count} Einsatzbericht(e) des Jahres sind noch nicht freigegeben und fehlen in den Zahlen.");
        }

        decimal Stunden(DateTimeOffset alarmiert, DateTimeOffset? ende, DateTimeOffset? von, DateTimeOffset? bis)
        {
            var b = von ?? alarmiert;
            var e = bis ?? ende;
            return e is { } x && x > b ? (decimal)(x - b).TotalHours : 0;
        }

        var mitStunden = einsaetze
            .Select(e => new { e, Stunden = e.Teilnahmen.Sum(t => Stunden(e.Alarmiert, e.Ende, t.Von, t.Bis)) })
            .ToList();

        // Alle Landeskategorien zeigen, auch mit 0, damit die Jahresmeldung vollständig ist.
        var katalog = await db.StatistikKategorien.AsNoTracking().OrderBy(k => k.Sortierung).ToListAsync(ct);
        var nachKategorie = katalog
            .Select(k => new Kategorie(k.Code, k.Bezeichnung,
                mitStunden.Count(x => x.e.Code == k.Code),
                Math.Round(mitStunden.Where(x => x.e.Code == k.Code).Sum(x => x.Stunden), 1)))
            .ToList();

        var ausrueckzeiten = einsaetze
            .Where(e => e.Ausgerueckt is { } a && a >= e.Alarmiert)
            .Select(e => (e.Ausgerueckt!.Value - e.Alarmiert).TotalMinutes)
            .Order()
            .ToList();

        var werktagsTagsueber = einsaetze.Count(e =>
        {
            var lokal = TimeZoneInfo.ConvertTime(e.Alarmiert, Ortszeit.Berlin);
            return lokal.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && lokal.Hour is >= 6 and < 18;
        });

        var nachMonat = Enumerable.Range(1, 12)
            .Select(m => einsaetze.Count(e => TimeZoneInfo.ConvertTime(e.Alarmiert, Ortszeit.Berlin).Month == m))
            .ToList();

        var teilnahmen = einsaetze.Sum(e => e.Teilnahmen.Count);
        return new EinsatzKennzahlen(
            alle,
            einsaetze.Count,
            nachKategorie,
            einsaetze.Sum(e => e.Gerettete),
            einsaetze.Sum(e => e.Verletzte),
            einsaetze.Sum(e => e.Tote),
            einsaetze.Sum(e => e.VerletzteFeuerwehr),
            einsaetze.Count(e => e.Ueberoertlich),
            Math.Round(mitStunden.Sum(x => x.Stunden), 1),
            einsaetze.Count == 0 ? 0 : Math.Round((decimal)teilnahmen / einsaetze.Count, 1),
            Median(ausrueckzeiten),
            werktagsTagsueber,
            nachMonat);
    }

    private async Task<DienstKennzahlen> DiensteAsync(DateTimeOffset utcVon, DateTimeOffset utcBis, DateOnly stichtag, List<string> hinweise, CancellationToken ct)
    {
        var dienste = await db.Dienste.AsNoTracking()
            .Where(d => d.Status == DienstStatus.Durchgefuehrt && d.Beginn >= utcVon && d.Beginn < utcBis)
            .Select(d => new
            {
                d.Art,
                d.Beginn,
                d.Ende,
                d.ZaehltFuerFortbildung,
                Anwesend = d.Anwesenheiten.Where(a => a.Status == AnwesenheitStatus.Anwesend).Select(a => new { a.MitgliedId, a.Von, a.Bis }).ToList(),
            })
            .ToListAsync(ct);

        var offen = await db.Dienste.CountAsync(d => d.Status == DienstStatus.Geplant && d.Beginn >= utcVon && d.Beginn < utcBis && d.Ende < utcBis, ct);
        if (offen > 0)
        {
            hinweise.Add($"{offen} Dienst(e) des Jahres sind noch als geplant markiert und fehlen in den Zahlen.");
        }

        static decimal Stunden(DateTimeOffset von, DateTimeOffset bis) => bis > von ? (decimal)(bis - von).TotalHours : 0;

        var nachArt = dienste
            .GroupBy(d => d.Art)
            .OrderBy(g => g.Key)
            .Select(g => new DienstArtKennzahl(
                g.Key,
                g.Count(),
                Math.Round(g.Sum(d => Stunden(d.Beginn, d.Ende)), 1),
                g.Sum(d => d.Anwesend.Count),
                Math.Round(g.Sum(d => d.Anwesend.Sum(a => Stunden(a.Von ?? d.Beginn, a.Bis ?? d.Ende))), 1)))
            .ToList();

        // Standortfortbildung: Stunden je Angehörigem der Einsatzabteilung, die zum 31.12. aktiv sind.
        var ziel = await parameter.ZahlAsync(ParameterSchluessel.StandortfortbildungStunden, stichtag, 40, ct);
        var basis = await AktiveEinsatzabteilungAsync(stichtag, ct);
        var stunden = dienste
            .Where(d => d.ZaehltFuerFortbildung)
            .SelectMany(d => d.Anwesend.Select(a => new { a.MitgliedId, Stunden = Stunden(a.Von ?? d.Beginn, a.Bis ?? d.Ende) }))
            .GroupBy(x => x.MitgliedId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Stunden));
        var erreicht = basis.Count(id => stunden.GetValueOrDefault(id) >= ziel);

        return new DienstKennzahlen(nachArt, ziel, erreicht, basis.Count);
    }

    private async Task<List<Guid>> AktiveEinsatzabteilungAsync(DateOnly stichtag, CancellationToken ct) =>
        await db.Mitgliedschaften.AsNoTracking()
            .Where(m => m.Abteilung!.Art == AbteilungArt.Einsatz
                && m.GueltigAb <= stichtag && (m.GueltigBis == null || m.GueltigBis >= stichtag)
                && m.Status != MitgliedStatus.Ausgeschieden)
            .Select(m => m.MitgliedId)
            .Distinct()
            .ToListAsync(ct);

    private async Task<PersonalKennzahlen> PersonalAsync(DateOnly erster, DateOnly stichtag, CancellationToken ct)
    {
        var abteilungen = await db.Abteilungen.AsNoTracking().OrderBy(a => a.Sortierung).ToListAsync(ct);
        var weiblich = (await db.Mitglieder.AsNoTracking().Where(m => m.Geschlecht == Geschlecht.Weiblich).Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        var mitgliedschaften = await db.Mitgliedschaften.AsNoTracking()
            .Select(m => new { m.MitgliedId, m.AbteilungId, m.Status, m.GueltigAb, m.GueltigBis, m.Austrittsgrund })
            .ToListAsync(ct);

        var artVon = abteilungen.ToDictionary(a => a.Id, a => a.Art);
        var nameVon = abteilungen.ToDictionary(a => a.Id, a => a.Bezeichnung);

        bool HatVorgaenger(Guid mitglied, DateOnly ab) =>
            mitgliedschaften.Any(m => m.MitgliedId == mitglied && m.GueltigBis is { } b && b >= ab.AddDays(-1) && b < ab);

        bool HatNachfolger(Guid mitglied, DateOnly bis) =>
            mitgliedschaften.Any(m => m.MitgliedId == mitglied && m.GueltigAb > bis && m.GueltigAb <= bis.AddDays(1));

        var aktivAmStichtag = mitgliedschaften
            .Where(m => m.GueltigAb <= stichtag && (m.GueltigBis is null || m.GueltigBis >= stichtag) && m.Status != MitgliedStatus.Ausgeschieden)
            .ToList();

        var staende = abteilungen
            .Select(a => new AbteilungStand(
                a.Code,
                a.Bezeichnung,
                aktivAmStichtag.Count(m => m.AbteilungId == a.Id),
                aktivAmStichtag.Count(m => m.AbteilungId == a.Id && m.Status == MitgliedStatus.Aktiv),
                aktivAmStichtag.Count(m => m.AbteilungId == a.Id && m.Status == MitgliedStatus.Probezeit),
                aktivAmStichtag.Count(m => m.AbteilungId == a.Id && m.Status == MitgliedStatus.Befreit),
                mitgliedschaften.Count(m => m.AbteilungId == a.Id && m.GueltigAb >= erster && m.GueltigAb <= stichtag && !HatVorgaenger(m.MitgliedId, m.GueltigAb)),
                mitgliedschaften.Count(m => m.AbteilungId == a.Id && m.GueltigBis is { } b && b >= erster && b <= stichtag && !HatNachfolger(m.MitgliedId, b)),
                aktivAmStichtag.Count(m => m.AbteilungId == a.Id && weiblich.Contains(m.MitgliedId))))
            .ToList();

        var uebertritte = mitgliedschaften
            .Where(m => m.GueltigBis is { } b && b >= erster && b <= stichtag)
            .SelectMany(alt => mitgliedschaften
                .Where(neu => neu.MitgliedId == alt.MitgliedId && neu.GueltigAb > alt.GueltigBis && neu.GueltigAb <= alt.GueltigBis!.Value.AddDays(1) && neu.AbteilungId != alt.AbteilungId)
                .Select(neu => $"{nameVon.GetValueOrDefault(alt.AbteilungId, "?")} → {nameVon.GetValueOrDefault(neu.AbteilungId, "?")}"))
            .GroupBy(t => t)
            .Select(g => new Bewegung(g.Key, g.Count()))
            .OrderBy(b => b.Text)
            .ToList();

        var abgaenge = mitgliedschaften
            .Where(m => m.GueltigBis is { } b && b >= erster && b <= stichtag && !HatNachfolger(m.MitgliedId, b))
            .GroupBy(m => m.Austrittsgrund)
            .Select(g => new Bewegung(GrundText(g.Key), g.Count()))
            .OrderByDescending(b => b.Anzahl)
            .ToList();

        var einsatzabteilung = aktivAmStichtag.Where(m => artVon.TryGetValue(m.AbteilungId, out var art) && art == AbteilungArt.Einsatz).Select(m => m.MitgliedId).Distinct().ToList();
        var geburtsdaten = await db.Mitglieder.AsNoTracking().Where(m => einsatzabteilung.Contains(m.Id)).Select(m => m.Geburtsdatum).ToListAsync(ct);
        var alter = geburtsdaten.Select(g => Mitglied.Alter(g, stichtag)).ToList();
        var altersgrenze = await parameter.ZahlAsync(ParameterSchluessel.AltersgrenzeEinsatzdienst, stichtag, 65, ct);

        List<Altersgruppe> gruppen =
        [
            new("unter 30", alter.Count(a => a < 30)),
            new("30–39", alter.Count(a => a is >= 30 and < 40)),
            new("40–49", alter.Count(a => a is >= 40 and < 50)),
            new("50–59", alter.Count(a => a is >= 50 and < 60)),
            new("ab 60", alter.Count(a => a >= 60)),
        ];

        var inFuenfJahren = stichtag.AddYears(5);
        var erreichen = geburtsdaten.Count(g => Mitglied.Alter(g, stichtag) < altersgrenze && Mitglied.Alter(g, inFuenfJahren) >= altersgrenze);

        return new PersonalKennzahlen(
            staende, uebertritte, abgaenge, gruppen,
            alter.Count == 0 ? null : Math.Round(alter.Average(), 1),
            altersgrenze, erreichen);
    }

    private static string GrundText(Austrittsgrund? grund) => grund switch
    {
        null => "ohne Angabe",
        Austrittsgrund.Altersgrenze => "Altersgrenze",
        Austrittsgrund.ProbezeitNichtBestanden => "Probezeit nicht bestanden",
        Austrittsgrund.PersoenlicheGruende => "persönliche Gründe",
        Austrittsgrund.Wohnortwechsel => "Wohnortwechsel",
        Austrittsgrund.Austritt => "Austritt",
        Austrittsgrund.Ausschluss => "Ausschluss",
        Austrittsgrund.Tod => "Tod",
        Austrittsgrund.Uebertritt => "Übertritt ohne Folgeabteilung",
        _ => grund.ToString()!,
    };

    private static double? Median(IReadOnlyList<double> werte) => werte.Count switch
    {
        0 => null,
        var n when n % 2 == 1 => Math.Round(werte[n / 2], 1),
        var n => Math.Round((werte[n / 2 - 1] + werte[n / 2]) / 2, 1),
    };
}
