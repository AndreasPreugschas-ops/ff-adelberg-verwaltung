using System.Globalization;
using System.Text;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Führungskennzahlen (V52): Einsatzbereitschaft Ist/Soll nach Funktionen, Ausrücke- und Eintreffzeiten gegen das Schutzziel des
/// Feuerwehrbedarfsplans, Ausrückestärke, Beteiligung je Person und Gruppe sowie Abgangsprognose. Sollwerte und Schwellen sind Parameter.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="atemschutz">Einsatzstatus der Geräteträger.</param>
/// <param name="parameter">Parameter (Sollwerte, Schutzziel, Schwellen).</param>
public sealed class Fuehrungskennzahlen(AppDbContext db, Atemschutzabfrage atemschutz, ParameterLeser parameter)
{
    private static readonly DienstArt[] Uebungsdienste = [DienstArt.Uebung, DienstArt.Unterricht, DienstArt.Unterweisung];

    /// <summary>Eine Funktion mit Ist und Soll.</summary>
    /// <param name="Funktion">Bezeichnung, z. B. „Atemschutzgeräteträger“.</param>
    /// <param name="Gesamt">Angehörige mit der Qualifikation.</param>
    /// <param name="Einsatzfaehig">Davon heute einsatzfähig.</param>
    /// <param name="Soll">Soll aus dem Bedarfsplan oder <c>null</c>.</param>
    public sealed record Staerke(string Funktion, int Gesamt, int Einsatzfaehig, int? Soll)
    {
        /// <summary>Das Soll ist mit einsatzfähigen Kräften erreicht.</summary>
        public bool? Erreicht => Soll is { } s ? Einsatzfaehig >= s : null;
    }

    /// <summary>Freiwillige Tagesverfügbarkeit einer Funktion (V43), nur als Summe.</summary>
    /// <param name="Funktion">Bezeichnung wie bei den Stärken.</param>
    /// <param name="Aktive">Aktive Angehörige mit der Funktion (ohne Probezeit und Befreite).</param>
    /// <param name="MitAngabe">Davon mit freiwilliger Angabe.</param>
    /// <param name="JeTag">Davon tagsüber im Ort, Montag bis Freitag.</param>
    public sealed record Verfuegbarkeit(string Funktion, int Aktive, int MitAngabe, IReadOnlyList<int> JeTag);

    /// <summary>Zeitkennzahlen einer Tageszeit.</summary>
    /// <param name="Tageszeit">Tageszeit der Alarmierung.</param>
    /// <param name="Einsaetze">Einsätze mit erfassten Zeiten.</param>
    /// <param name="AusrueckenMedian">Median Alarmierung bis Ausrücken in Minuten.</param>
    /// <param name="AusrueckenP90">90. Perzentil Alarmierung bis Ausrücken.</param>
    /// <param name="EintreffenMedian">Median Alarmierung bis Eintreffen.</param>
    /// <param name="EintreffenP90">90. Perzentil Alarmierung bis Eintreffen.</param>
    /// <param name="Gemessen">Einsätze, bei denen das Schutzziel gemessen werden konnte.</param>
    /// <param name="Schutzziel1">Davon mit erster Eintreffzeit erreicht.</param>
    /// <param name="Schutzziel2">Davon mit zweiter Eintreffzeit erreicht.</param>
    public sealed record Zeiten(Tageszeit? Tageszeit, int Einsaetze, double? AusrueckenMedian, double? AusrueckenP90, double? EintreffenMedian,
        double? EintreffenP90, int Gemessen, int Schutzziel1, int Schutzziel2)
    {
        /// <summary>Anteil der gemessenen Einsätze mit erreichter erster Eintreffzeit in Prozent.</summary>
        public double? Erreichung1 => Gemessen == 0 ? null : 100.0 * Schutzziel1 / Gemessen;
        /// <summary>Anteil der gemessenen Einsätze mit erreichter zweiter Eintreffzeit in Prozent.</summary>
        public double? Erreichung2 => Gemessen == 0 ? null : 100.0 * Schutzziel2 / Gemessen;
    }

    /// <summary>Beteiligung einer Person im Jahr.</summary>
    /// <param name="MitgliedId">Kennung.</param>
    /// <param name="Name">Name „Nachname, Vorname“.</param>
    /// <param name="Gruppe">Gruppe oder Zug, falls zugeordnet.</param>
    /// <param name="Dienste">Übungsdienste seit Eintritt.</param>
    /// <param name="Anwesend">Davon anwesend.</param>
    /// <param name="Einsaetze">Einsätze seit Eintritt.</param>
    /// <param name="Teilgenommen">Davon teilgenommen.</param>
    public sealed record Beteiligung(Guid MitgliedId, string Name, string? Gruppe, int Dienste, int Anwesend, int Einsaetze, int Teilgenommen)
    {
        /// <summary>Anteil der Übungsdienste in Prozent.</summary>
        public double? DienstQuote => Dienste == 0 ? null : 100.0 * Anwesend / Dienste;
        /// <summary>Anteil der Einsätze in Prozent.</summary>
        public double? EinsatzQuote => Einsaetze == 0 ? null : 100.0 * Teilgenommen / Einsaetze;
    }

    /// <summary>Abgänge und mögliche Zugänge in einem künftigen Jahr.</summary>
    /// <param name="Jahr">Das Jahr.</param>
    /// <param name="Altersgrenze">Angehörige, die in diesem Jahr die Altersgrenze erreichen.</param>
    /// <param name="AusDerJugend">Jugendliche, die in diesem Jahr volljährig werden und übertreten könnten.</param>
    public sealed record Prognose(int Jahr, IReadOnlyList<string> Altersgrenze, int AusDerJugend);

    /// <summary>Alle Kennzahlen eines Jahres.</summary>
    /// <param name="Jahr">Auswertungsjahr der Zeiten und Beteiligung.</param>
    /// <param name="Stichtag">Stichtag der Einsatzbereitschaft.</param>
    /// <param name="Staerken">Funktionsstärken.</param>
    /// <param name="Zeiten">Zeitkennzahlen gesamt (Tageszeit leer) und je Tageszeit.</param>
    /// <param name="MittlereStaerke">Durchschnittliche Ausrückestärke.</param>
    /// <param name="MittlereAtemschutz">Durchschnittlich eingesetzte Atemschutzgeräteträger.</param>
    /// <param name="Einsatzleiter">Einsätze je Einsatzleiter.</param>
    /// <param name="Beteiligungen">Beteiligung je Person.</param>
    /// <param name="Prognosen">Abgangsprognose je Jahr.</param>
    /// <param name="Schwelle">Schwelle der Beteiligung in Prozent.</param>
    /// <param name="Schutzziel">Schutzziel aus den Parametern.</param>
    /// <param name="BeteiligungVorhanden">
    /// Die einzelnen Anwesenheiten des Jahres sind noch da; nach der Verdichtung (Löschkonzept L5) ergäbe die Rechnung für jeden 0 % und unterbleibt.
    /// </param>
    /// <param name="BeteiligungMitNamen">Das Jahr ist jung genug, um die Beteiligung je Person zu zeigen; sonst nur Summen.</param>
    /// <param name="Tagesverfuegbarkeit">Freiwillige Tagesverfügbarkeit je Funktion, ohne Namen.</param>
    public sealed record Bericht(int Jahr, DateOnly Stichtag, IReadOnlyList<Staerke> Staerken, IReadOnlyList<Zeiten> Zeiten, double? MittlereStaerke,
        double? MittlereAtemschutz, IReadOnlyList<(string Name, int Anzahl)> Einsatzleiter, IReadOnlyList<Beteiligung> Beteiligungen,
        IReadOnlyList<Prognose> Prognosen, int Schwelle, Schutzzielwerte Schutzziel, bool BeteiligungVorhanden, bool BeteiligungMitNamen,
        IReadOnlyList<Verfuegbarkeit> Tagesverfuegbarkeit)
    {
        /// <summary>Beteiligungen mit Quote an Übungsdiensten.</summary>
        public IReadOnlyList<Beteiligung> MitQuote => Beteiligungen.Where(x => x.DienstQuote is not null).ToList();

        /// <summary>Mittlere Beteiligung an Übungsdiensten in Prozent.</summary>
        public double? MittlereBeteiligung => MitQuote.Count == 0 ? null : MitQuote.Average(x => x.DienstQuote!.Value);

        /// <summary>Anzahl der Angehörigen unter der Schwelle.</summary>
        public int UnterSchwelle => MitQuote.Count(x => x.DienstQuote < Schwelle);
    }

    /// <summary>Schutzziel aus dem Bedarfsplan.</summary>
    /// <param name="Minuten1">Erste Eintreffzeit.</param>
    /// <param name="Funktionen1">Funktionen bis zur ersten Eintreffzeit.</param>
    /// <param name="Minuten2">Zweite Eintreffzeit.</param>
    /// <param name="Funktionen2">Funktionen insgesamt bis zur zweiten Eintreffzeit.</param>
    /// <param name="ZielProzent">Angestrebter Erreichungsgrad.</param>
    public sealed record Schutzzielwerte(int Minuten1, int Funktionen1, int Minuten2, int Funktionen2, int ZielProzent);

    /// <summary>Berechnet alle Kennzahlen.</summary>
    /// <param name="jahr">Auswertungsjahr.</param>
    /// <param name="heute">Heutiges Datum (Stichtag der Einsatzbereitschaft und Beginn der Prognose).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Bericht.</returns>
    public async Task<Bericht> BerichtAsync(int jahr, DateOnly heute, CancellationToken ct = default)
    {
        var ziel = new Schutzzielwerte(
            await parameter.ZahlAsync(ParameterSchluessel.SchutzzielMinuten, heute, 10, ct),
            await parameter.ZahlAsync(ParameterSchluessel.SchutzzielFunktionen, heute, 6, ct),
            await parameter.ZahlAsync(ParameterSchluessel.Schutzziel2Minuten, heute, 15, ct),
            await parameter.ZahlAsync(ParameterSchluessel.Schutzziel2Funktionen, heute, 15, ct),
            await parameter.ZahlAsync(ParameterSchluessel.SchutzzielErreichungProzent, heute, 90, ct));
        var (zeiten, staerke, agt, leiter) = await EinsaetzeAsync(jahr, ziel, heute, ct);
        // Löschkonzept L5: die einzelnen Anwesenheiten werden einige Jahre nach Jahresende verdichtet; Namen zeigt die Seite nur für junge Jahre.
        var vorhanden = jahr >= heute.Year - await parameter.ZahlAsync(ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, heute, 6, ct);
        var mitNamen = vorhanden && jahr > heute.Year - await parameter.ZahlAsync(ParameterSchluessel.BeteiligungNamenJahre, heute, 2, ct);
        var (staerken, verfuegbarkeit) = await StaerkenAsync(heute, ct);
        return new Bericht(jahr, heute, staerken, zeiten, staerke, agt, leiter, vorhanden ? await BeteiligungenAsync(jahr, ct) : [],
            await PrognoseAsync(heute, ct), await parameter.ZahlAsync(ParameterSchluessel.BeteiligungSchwelleProzent, heute, 50, ct), ziel, vorhanden, mitNamen,
            verfuegbarkeit);
    }

    /// <summary>Kennzahlen ohne Namen als CSV für Bedarfsplan und Gemeinderat.</summary>
    /// <param name="b">Der Bericht.</param>
    /// <returns>UTF-8 mit BOM, Semikolon getrennt.</returns>
    public static byte[] Csv(Bericht b)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        string Z(double? w) => w is { } x ? x.ToString("0.0", de) : "";
        var sb = new StringBuilder();
        sb.AppendLine($"Führungskennzahlen {b.Jahr};Stichtag {b.Stichtag:dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine("Funktion;Gesamt;Einsatzfähig;Soll");
        foreach (var s in b.Staerken)
        {
            sb.AppendLine($"{s.Funktion};{s.Gesamt};{s.Einsatzfaehig};{s.Soll}");
        }

        sb.AppendLine();
        sb.AppendLine($"Tageszeit;Einsätze;Ausrücken Median (min);Ausrücken P90 (min);Eintreffen Median (min);Eintreffen P90 (min);Schutzziel gemessen;"
            + $"{b.Schutzziel.Funktionen1} Funktionen in {b.Schutzziel.Minuten1} min (%);{b.Schutzziel.Funktionen2} Funktionen in {b.Schutzziel.Minuten2} min (%)");
        foreach (var z in b.Zeiten)
        {
            var name = z.Tageszeit switch { Tageszeit.WerktagsTag => "werktags 06:00–18:00 Uhr", Tageszeit.Uebrige => "abends, nachts, Wochenende", _ => "gesamt" };
            sb.AppendLine($"{name};{z.Einsaetze};{Z(z.AusrueckenMedian)};{Z(z.AusrueckenP90)};{Z(z.EintreffenMedian)};{Z(z.EintreffenP90)};{z.Gemessen};"
                + $"{Z(z.Erreichung1)};{Z(z.Erreichung2)}");
        }

        sb.AppendLine();
        sb.AppendLine("Tagesverfügbarkeit werktags 06:00–18:00 Uhr (freiwillige Angaben);Aktive;Mit Angabe;Mo;Di;Mi;Do;Fr");
        foreach (var v in b.Tagesverfuegbarkeit)
        {
            sb.AppendLine($"{v.Funktion};{v.Aktive};{v.MitAngabe};{string.Join(';', v.JeTag)}");
        }

        sb.AppendLine();
        sb.AppendLine($"Mittlere Ausrückestärke;{Z(b.MittlereStaerke)}");
        sb.AppendLine($"Mittlere Atemschutzgeräteträger je Einsatz;{Z(b.MittlereAtemschutz)}");
        if (b.BeteiligungVorhanden)
        {
            sb.AppendLine($"Mittlere Beteiligung an Übungsdiensten (%);{Z(b.MittlereBeteiligung)}");
            sb.AppendLine($"Unter {b.Schwelle} % Beteiligung;{b.UnterSchwelle}");
        }
        sb.AppendLine();
        sb.AppendLine("Jahr;Erreichen die Altersgrenze;Werden in der Jugendfeuerwehr volljährig");
        foreach (var p in b.Prognosen)
        {
            sb.AppendLine($"{p.Jahr};{p.Altersgrenze.Count};{p.AusDerJugend}");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private async Task<(IReadOnlyList<Staerke>, IReadOnlyList<Verfuegbarkeit>)> StaerkenAsync(DateOnly heute, CancellationToken ct)
    {
        var angehoerige = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => s.Abteilung!.Art == AbteilungArt.Einsatz && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit || s.Status == MitgliedStatus.Befreit)
                && !s.Mitglied!.Fachberater)
            .Select(s => new { s.MitgliedId, s.Status })
            .ToListAsync(ct);
        var ids = angehoerige.Select(a => a.MitgliedId).Distinct().ToList();
        var abwesend = (await db.Abwesenheiten.AsNoTracking().Where(a => ids.Contains(a.MitgliedId) && a.Von <= heute && a.Bis >= heute)
            .Select(a => a.MitgliedId).ToListAsync(ct)).ToHashSet();
        // Beurlaubte und angezeigte Abwesenheiten aus der Akte (V57) zählen ebenfalls nicht.
        abwesend.UnionWith(await db.Dienstpflichtvorgaenge.AsNoTracking()
            .Where(v => ids.Contains(v.MitgliedId) && (v.Art == DienstpflichtArt.Beurlaubung || v.Art == DienstpflichtArt.Abwesenheitsanzeige)
                && v.Von != null && v.Von <= heute && (v.Bis == null || v.Bis >= heute))
            .Select(v => v.MitgliedId).ToListAsync(ct));

        // Einsatzfähig: aktiv (Probezeit beendet, nicht befreit) und heute nicht abwesend.
        var faehig = angehoerige.GroupBy(a => a.MitgliedId)
            .Where(g => g.Any(a => a.Status == MitgliedStatus.Aktiv) && g.All(a => a.Status != MitgliedStatus.Befreit) && !abwesend.Contains(g.Key))
            .Select(g => g.Key).ToHashSet();
        var qualifikationen = (await db.QualifikationNachweise.AsNoTracking()
            .Where(q => ids.Contains(q.MitgliedId) && q.ErworbenAm <= heute && (q.GueltigBis == null || q.GueltigBis >= heute))
            .Select(q => new { q.MitgliedId, q.Qualifikation!.Code }).ToListAsync(ct))
            .ToLookup(q => q.Code, q => q.MitgliedId);
        var agtBereit = (await atemschutz.TraegerAsync(heute, ct)).Where(t => t.Status.Einsatzbereit).Select(t => t.MitgliedId).ToHashSet();

        // Tagesverfügbarkeit (V43): Wochenmuster der Aktiven ohne Befreiung; abwesend heute spielt für das Muster keine Rolle.
        var aktiv = angehoerige.GroupBy(a => a.MitgliedId)
            .Where(g => g.Any(a => a.Status == MitgliedStatus.Aktiv) && g.All(a => a.Status != MitgliedStatus.Befreit)).Select(g => g.Key).ToList();
        var muster = (await db.Mitglieder.AsNoTracking().Where(m => aktiv.Contains(m.Id) && m.Tagesverfuegbarkeit != null)
            .Select(m => new { m.Id, m.Tagesverfuegbarkeit }).ToListAsync(ct)).ToDictionary(m => m.Id, m => m.Tagesverfuegbarkeit!.Value);
        var staerken = new List<Staerke>();
        var verfuegbarkeit = new List<Verfuegbarkeit>();

        void Zeile(string name, IEnumerable<Guid> personen, int? soll, Func<Guid, bool>? zusaetzlich = null)
        {
            var menge = personen.Where(ids.Contains).Distinct().ToList();
            staerken.Add(new Staerke(name, menge.Count, menge.Count(p => faehig.Contains(p) && (zusaetzlich?.Invoke(p) ?? true)), soll));
            var aktive = menge.Where(aktiv.Contains).ToList();
            var angaben = aktive.Where(muster.ContainsKey).Select(p => muster[p]).ToList();
            verfuegbarkeit.Add(new Verfuegbarkeit(name, aktive.Count, angaben.Count, Tagesverfuegbarkeit.Summen(angaben)));
        }

        async Task<int?> Soll(string schluessel) => int.TryParse(await parameter.WertAsync(schluessel, heute, ct), out var s) ? s : null;
        IEnumerable<Guid> Mit(params string[] codes) => codes.SelectMany(c => qualifikationen[c]);

        Zeile("Einsatzabteilung", ids, await Soll(ParameterSchluessel.SollStaerke));
        Zeile("Atemschutzgeräteträger", Mit(Domain.Ausbildung.Qualifikation.CodeAtemschutz), await Soll(ParameterSchluessel.SollAtemschutz), agtBereit.Contains);
        Zeile("Maschinisten", Mit("MA"), await Soll(ParameterSchluessel.SollMaschinisten));
        Zeile("Gruppenführer (mit Zug- und Verbandsführern)", Mit("GF", "ZF", "VF"), await Soll(ParameterSchluessel.SollGruppenfuehrer));
        Zeile("Zugführer (mit Verbandsführern)", Mit("ZF", "VF"), await Soll(ParameterSchluessel.SollZugfuehrer));
        Zeile("Sprechfunker", Mit("SPRF"), null);
        return (staerken, verfuegbarkeit);
    }

    private async Task<(IReadOnlyList<Zeiten>, double?, double?, IReadOnlyList<(string, int)>)> EinsaetzeAsync(int jahr, Schutzzielwerte ziel, DateOnly heute,
        CancellationToken ct)
    {
        var von = Ortszeit.TagesbeginnUtc(new DateOnly(jahr, 1, 1));
        var bis = Ortszeit.TagesbeginnUtc(new DateOnly(jahr + 1, 1, 1));
        var kategorien = (await parameter.WertAsync(ParameterSchluessel.SchutzzielKategorien, heute, ct) ?? "BR|TH")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var einsaetze = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Status == EinsatzStatus.Freigegeben && e.Alarmiert >= von && e.Alarmiert < bis)
            .Select(e => new
            {
                e.Alarmiert, e.Ausgerueckt, e.EintreffenEinsatzort, e.Ueberoertlich, Kategorie = e.StatistikKategorie!.Code,
                Leiter = e.Einsatzleiter == null ? null : e.Einsatzleiter.Nachname + ", " + e.Einsatzleiter.Vorname,
                Fahrzeuge = e.Fahrzeuge.Select(f => new { f.Id, f.Ausgerueckt, f.Eingetroffen }).ToList(),
                Teilnahmen = e.Teilnahmen.Select(t => new { t.MitgliedId, t.EinsatzFahrzeugId, t.Atemschutz }).ToList(),
            })
            .ToListAsync(ct);

        var messungen = einsaetze.Select(e =>
        {
            var ausgerueckt = e.Ausgerueckt ?? e.Fahrzeuge.Where(f => f.Ausgerueckt != null).Min(f => f.Ausgerueckt);
            var eingetroffen = e.EintreffenEinsatzort ?? e.Fahrzeuge.Where(f => f.Eingetroffen != null).Min(f => f.Eingetroffen);
            var besatzung = e.Fahrzeuge.Select(f => (f.Eingetroffen, e.Teilnahmen.Count(t => t.EinsatzFahrzeugId == f.Id))).ToList();
            var messbar = kategorien.Contains(e.Kategorie) && !e.Ueberoertlich && e.Fahrzeuge.Any(f => f.Eingetroffen != null);
            return new
            {
                Tageszeit = Kennzahlen.TageszeitVon(e.Alarmiert),
                Ausruecken = Kennzahlen.Minuten(e.Alarmiert, ausgerueckt),
                Eintreffen = Kennzahlen.Minuten(e.Alarmiert, eingetroffen),
                Messbar = messbar,
                Ziel1 = messbar && Kennzahlen.FunktionenBis(e.Alarmiert, besatzung, ziel.Minuten1) >= ziel.Funktionen1,
                Ziel2 = messbar && Kennzahlen.FunktionenBis(e.Alarmiert, besatzung, ziel.Minuten2) >= ziel.Funktionen2,
            };
        }).ToList();

        Zeiten Auswerten(Tageszeit? tageszeit)
        {
            var m = messungen.Where(x => tageszeit is null || x.Tageszeit == tageszeit).ToList();
            var aus = m.Where(x => x.Ausruecken is not null).Select(x => x.Ausruecken!.Value).ToList();
            var ein = m.Where(x => x.Eintreffen is not null).Select(x => x.Eintreffen!.Value).ToList();
            return new Zeiten(tageszeit, m.Count, Kennzahlen.Median(aus), Kennzahlen.Perzentil(aus, 0.9), Kennzahlen.Median(ein), Kennzahlen.Perzentil(ein, 0.9),
                m.Count(x => x.Messbar), m.Count(x => x.Ziel1), m.Count(x => x.Ziel2));
        }

        var zeiten = new List<Zeiten> { Auswerten(null), Auswerten(Tageszeit.WerktagsTag), Auswerten(Tageszeit.Uebrige) };
        double? staerke = einsaetze.Count == 0 ? null : einsaetze.Average(e => e.Teilnahmen.Select(t => t.MitgliedId).Distinct().Count());
        double? agt = einsaetze.Count == 0 ? null : einsaetze.Average(e => e.Teilnahmen.Where(t => t.Atemschutz).Select(t => t.MitgliedId).Distinct().Count());
        var leiter = einsaetze.GroupBy(e => e.Leiter ?? "nicht eingetragen").OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => (g.Key, g.Count())).ToList();
        return (zeiten, staerke, agt, leiter);
    }

    private async Task<IReadOnlyList<Beteiligung>> BeteiligungenAsync(int jahr, CancellationToken ct)
    {
        var anfang = new DateOnly(jahr, 1, 1);
        var ende = new DateOnly(jahr, 12, 31);
        var von = Ortszeit.TagesbeginnUtc(anfang);
        var bis = Ortszeit.TagesbeginnUtc(ende.AddDays(1));
        var mitgliedschaften = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => s.Abteilung!.Art == AbteilungArt.Einsatz && s.GueltigAb <= ende && (s.GueltigBis == null || s.GueltigBis >= anfang)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit) && !s.Mitglied!.Fachberater)
            .Select(s => new { s.MitgliedId, s.AbteilungId, s.GueltigAb, s.GueltigBis, Name = s.Mitglied!.Nachname + ", " + s.Mitglied.Vorname })
            .ToListAsync(ct);
        var ids = mitgliedschaften.Select(m => m.MitgliedId).Distinct().ToList();
        var dienste = await db.Dienste.AsNoTracking()
            .Where(d => d.Status == DienstStatus.Durchgefuehrt && d.AlsDienst && Uebungsdienste.Contains(d.Art) && d.Beginn >= von && d.Beginn < bis)
            .Select(d => new { d.Id, d.Beginn, Abteilungen = d.Abteilungen.Select(a => a.Id).ToList() })
            .ToListAsync(ct);
        var anwesend = (await db.Anwesenheiten.AsNoTracking()
            .Where(a => ids.Contains(a.MitgliedId) && a.Status == AnwesenheitStatus.Anwesend && a.Dienst!.Beginn >= von && a.Dienst.Beginn < bis)
            .Select(a => new { a.MitgliedId, a.DienstId }).ToListAsync(ct)).Select(a => (a.MitgliedId, a.DienstId)).ToHashSet();
        var einsaetze = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Status == EinsatzStatus.Freigegeben && e.Alarmiert >= von && e.Alarmiert < bis)
            .Select(e => new { e.Alarmiert, Teilnehmer = e.Teilnahmen.Select(t => t.MitgliedId).ToList() })
            .ToListAsync(ct);
        var gruppen = (await db.GruppenMitgliedschaften.AsNoTracking().Where(g => ids.Contains(g.MitgliedId))
            .Select(g => new { g.MitgliedId, g.Gruppe!.Bezeichnung }).ToListAsync(ct))
            .GroupBy(g => g.MitgliedId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Bezeichnung).Distinct()));

        return mitgliedschaften.GroupBy(m => m.MitgliedId).Select(g =>
        {
            bool Zugehoerig(DateOnly tag, IReadOnlyCollection<Guid>? abteilungen) =>
                g.Any(m => m.GueltigAb <= tag && (m.GueltigBis == null || m.GueltigBis >= tag) && (abteilungen is null || abteilungen.Count == 0 || abteilungen.Contains(m.AbteilungId)));
            var eigeneDienste = dienste.Where(d => Zugehoerig(Ortszeit.Datum(d.Beginn), d.Abteilungen)).ToList();
            var eigeneEinsaetze = einsaetze.Where(e => Zugehoerig(Ortszeit.Datum(e.Alarmiert), null)).ToList();
            return new Beteiligung(g.Key, g.First().Name, gruppen.GetValueOrDefault(g.Key), eigeneDienste.Count,
                eigeneDienste.Count(d => anwesend.Contains((g.Key, d.Id))), eigeneEinsaetze.Count, eigeneEinsaetze.Count(e => e.Teilnehmer.Contains(g.Key)));
        }).OrderBy(b => b.DienstQuote ?? double.MaxValue).ThenBy(b => b.Name).ToList();
    }

    private async Task<IReadOnlyList<Prognose>> PrognoseAsync(DateOnly heute, CancellationToken ct)
    {
        var jahre = await parameter.ZahlAsync(ParameterSchluessel.AbgangPrognoseJahre, heute, 5, ct);
        var grenze = await parameter.ZahlAsync(ParameterSchluessel.AltersgrenzeEinsatzdienst, heute, 65, ct);
        var volljaehrig = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var personen = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => (s.Abteilung!.Art == AbteilungArt.Einsatz || s.Abteilung.Art == AbteilungArt.Jugend)
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit || s.Status == MitgliedStatus.Befreit))
            .Select(s => new { s.MitgliedId, s.Abteilung!.Art, s.Mitglied!.Geburtsdatum, Name = s.Mitglied.Nachname + ", " + s.Mitglied.Vorname })
            .ToListAsync(ct);
        var einsatz = personen.Where(p => p.Art == AbteilungArt.Einsatz).DistinctBy(p => p.MitgliedId).ToList();
        var jugend = personen.Where(p => p.Art == AbteilungArt.Jugend).DistinctBy(p => p.MitgliedId).ToList();
        return Enumerable.Range(heute.Year, Math.Max(1, jahre)).Select(j => new Prognose(j,
            einsatz.Where(p => p.Geburtsdatum.Year + grenze == j).OrderBy(p => p.Name).Select(p => p.Name).ToList(),
            jugend.Count(p => p.Geburtsdatum.Year + volljaehrig == j))).ToList();
    }
}
