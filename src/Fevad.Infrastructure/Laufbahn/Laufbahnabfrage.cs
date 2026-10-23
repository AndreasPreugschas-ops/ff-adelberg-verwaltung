using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>Stand eines Mitglieds: Übertritte, Dienstzeiten, fällige Ehrungen und mögliche Beförderung.</summary>
public sealed record Laufbahnstand(
    Guid MitgliedId,
    string Name,
    DateOnly Geburtsdatum,
    int Alter,
    IReadOnlyList<Abschnitt> Abschnitte,
    DienstgradStufe? Dienstgrad,
    DateOnly? DienstgradSeit,
    IReadOnlyList<Laufbahnhinweis> Hinweise,
    Dienstzeit Einsatzdienst,
    Dienstzeit Zugehoerigkeit,
    IReadOnlyList<Ehrungsvorschlag> Ehrungsvorschlaege,
    Befoerderungsvorschlag? Befoerderung,
    IReadOnlyList<string> NichtAngerechnet)
{
    /// <summary>Die am Stichtag offenen Abschnitte (Vordienstzeiten ausgenommen).</summary>
    public IEnumerable<Abschnitt> OffeneAbschnitte(DateOnly stichtag) => Abschnitte.Where(a => !a.Vordienstzeit && a.IstOffenAm(stichtag));
}

/// <summary>Lädt die Daten für die Laufbahnregeln und liefert die Erinnerungen dazu.</summary>
public sealed class Laufbahnabfrage(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    /// <summary>Beförderungen erscheinen nur so lange in der Erinnerungsmail, danach nur noch in der Übersicht.</summary>
    private const int BefoerderungInMailTage = 30;

    public async Task<Laufbahnregeln> RegelnAsync(DateOnly stichtag, CancellationToken ct)
    {
        var s = Laufbahnregeln.Satzung;
        return new Laufbahnregeln(
            await parameter.ZahlAsync(ParameterSchluessel.ProbezeitMonate, stichtag, s.ProbezeitMonate, ct),
            await parameter.ZahlAsync(LaufbahnParameter.AufnahmeEinsatzAbAlter, stichtag, s.AufnahmeEinsatzAbAlter, ct),
            await parameter.ZahlAsync(LaufbahnParameter.EinsatzdienstAbAlter, stichtag, s.EinsatzdienstAbAlter, ct),
            await parameter.ZahlAsync(LaufbahnParameter.JugendEndeAlter, stichtag, s.JugendEndeAlter, ct),
            await parameter.ZahlAsync(ParameterSchluessel.AltersabteilungAbAlter, stichtag, s.AltersabteilungAbAlter, ct),
            await parameter.ZahlAsync(ParameterSchluessel.AltersgrenzeEinsatzdienst, stichtag, s.AltersgrenzeEinsatzdienst, ct),
            await parameter.ZahlAsync(LaufbahnParameter.VorlaufMonate, stichtag, s.VorlaufMonate, ct),
            await parameter.ZahlAsync(LaufbahnParameter.JugendAufnahmeBisAlter, stichtag, s.JugendAufnahmeBisAlter, ct),
            await parameter.ZahlAsync(LaufbahnParameter.JugendAufnahmeAbAlter, stichtag, s.JugendAufnahmeAbAlter, ct),
            await parameter.ZahlAsync(LaufbahnParameter.KinderAufnahmeAbAlter, stichtag, s.KinderAufnahmeAbAlter, ct),
            await parameter.ZahlAsync(LaufbahnParameter.KinderEndeAlter, stichtag, s.KinderEndeAlter, ct));
    }

    public async Task<Dienstzeitregeln> DienstzeitregelnAsync(DateOnly stichtag, CancellationToken ct) => new(
        await parameter.ZahlAsync(LaufbahnParameter.EhrungJugendAbAlter, stichtag, 14, ct),
        await parameter.ZahlAsync(LaufbahnParameter.EhrungAltersabteilungBisAlter, stichtag, 70, ct),
        await parameter.ZahlAsync(LaufbahnParameter.EhrungKindererziehungMonate, stichtag, 12, ct),
        DateOnly.TryParse(await parameter.WertAsync(ParameterSchluessel.LeistungsspangePflichtAbEintritt, stichtag, ct),
            System.Globalization.CultureInfo.InvariantCulture, out var ab) ? ab : null);

    /// <summary>Nächster Antragsschluss für Verbandsehrungen am oder nach dem Stichtag; null, wenn kein Parameter gesetzt ist.</summary>
    public async Task<DateOnly?> AntragsschlussAsync(DateOnly stichtag, CancellationToken ct) =>
        Antragsschluss.Naechster(await parameter.WertAsync(LaufbahnParameter.EhrungAntragsschlussVerband, stichtag, ct), stichtag);

    public async Task<IReadOnlyList<DienstgradStufe>> DienstgradeAsync(CancellationToken ct) =>
        await db.Dienstgrade.AsNoTracking()
            .Where(d => d.Aktiv)
            .OrderBy(d => d.Sortierung)
            .Select(d => new DienstgradStufe(d.Id, d.Code, d.Bezeichnung, d.Sortierung, d.MindestwartezeitMonate))
            .ToListAsync(ct);

    /// <summary>Alle Mitglieder mit offener Mitgliedschaft am Stichtag oder, mit <paramref name="mitgliedIds"/>, genau diese.</summary>
    public async Task<IReadOnlyList<Laufbahnstand>> LadenAsync(DateOnly stichtag, IReadOnlyCollection<Guid>? mitgliedIds = null, CancellationToken ct = default)
    {
        var regeln = await RegelnAsync(stichtag, ct);
        var dienstzeitregeln = await DienstzeitregelnAsync(stichtag, ct);
        var grenze = stichtag.AddMonths(regeln.VorlaufMonate);

        var mitglieder = db.Mitglieder.AsNoTracking();
        mitglieder = mitgliedIds is not null
            ? mitglieder.Where(m => mitgliedIds.Contains(m.Id))
            : mitglieder.Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)));

        var personen = await mitglieder
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Geburtsdatum })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();

        var abschnitte = (await db.Mitgliedschaften.AsNoTracking()
                .Where(s => ids.Contains(s.MitgliedId))
                .Select(s => new
                {
                    s.MitgliedId,
                    A = new Abschnitt(s.Id, s.Abteilung!.Art, s.Abteilung.Bezeichnung, s.Status, s.GueltigAb, s.GueltigBis, s.Vordienstzeit,
                        s.Vordienstart, s.Befreiungsgrund, s.EinsatzdienstBestaetigt, s.ProbezeitBis, s.AbteilungId),
                })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.A);
        var dienstgrade = (await db.DienstgradZuordnungen.AsNoTracking()
                .Where(d => ids.Contains(d.MitgliedId) && d.Ab <= stichtag)
                .Select(d => new { d.MitgliedId, d.DienstgradId, d.Ab })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId);
        var qualifikationen = (await db.QualifikationNachweise.AsNoTracking()
                .Where(q => ids.Contains(q.MitgliedId) && q.ErworbenAm <= grenze)
                .Select(q => new { q.MitgliedId, q.Qualifikation!.Code })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.Code);
        var ehrungen = (await db.Ehrungen.AsNoTracking()
                .Where(e => ids.Contains(e.MitgliedId))
                .Select(e => new { e.MitgliedId, E = new ErfassteEhrung(e.EhrungsartId, e.Status, e.VerliehenAm) })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.E);
        var funktionen = (await db.FunktionZuordnungen.AsNoTracking()
                .Where(f => ids.Contains(f.MitgliedId))
                .Select(f => new { f.MitgliedId, f.Funktion!.Code, f.GueltigAb, f.GueltigBis })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId);

        var ehrungsarten = await db.Ehrungsarten.AsNoTracking().ToListAsync(ct);
        var stufen = await DienstgradeAsync(ct);
        var alleStufen = await db.Dienstgrade.AsNoTracking()
            .Select(d => new DienstgradStufe(d.Id, d.Code, d.Bezeichnung, d.Sortierung, d.MindestwartezeitMonate))
            .ToDictionaryAsync(d => d.Id, ct);
        var wege = await db.Befoerderungswege.AsNoTracking().ToListAsync(ct);

        return personen.Select(p =>
        {
            var eigene = abschnitte[p.Id].OrderBy(a => a.Von).ToList();
            var eigeneQualifikationen = qualifikationen[p.Id].ToHashSet(StringComparer.OrdinalIgnoreCase);
            var anrechnung = Dienstzeitrechnung.Anrechnen(
                p.Geburtsdatum, eigene, dienstzeitregeln, eigeneQualifikationen.Contains(Qualifikation.CodeLeistungsspange));
            var einsatzdienst = anrechnung.Zeit;
            var zugehoerigkeit = Dienstzeitrechnung.Zugehoerigkeit(eigene);
            var grundlagen = new Ehrungsgrundlagen(
                einsatzdienst,
                zugehoerigkeit,
                Dienstzeitrechnung.Jugendfeuerwehr(eigene),
                funktionen[p.Id].GroupBy(f => f.Code).ToDictionary(g => g.Key, g => new Dienstzeit(g.Select(f => (f.GueltigAb, f.GueltigBis)))),
                eigeneQualifikationen);
            var letzter = dienstgrade[p.Id].MaxBy(d => d.Ab);
            var dienstgrad = letzter is null ? null : alleStufen.GetValueOrDefault(letzter.DienstgradId);

            Befoerderungsvorschlag? befoerderung = null;
            var einsatz = eigene.FirstOrDefault(a => !a.Vordienstzeit && a.Art == AbteilungArt.Einsatz && a.IstOffenAm(stichtag)
                && a.Status is MitgliedStatus.Aktiv or MitgliedStatus.Probezeit);
            if (einsatz is not null)
            {
                var eintritt = eigene.Where(a => !a.Vordienstzeit && a.Art == AbteilungArt.Einsatz).Min(a => a.Von);
                var stand = new Befoerderungsstand(
                    dienstgrad,
                    letzter?.Ab ?? eintritt,
                    einsatz.Status != MitgliedStatus.Probezeit,
                    eigeneQualifikationen);
                befoerderung = Befoerderungspruefung.Pruefen(stand, stufen, wege, grenze);
            }

            return new Laufbahnstand(
                p.Id,
                $"{p.Nachname}, {p.Vorname}",
                p.Geburtsdatum,
                Mitglied.Alter(p.Geburtsdatum, stichtag),
                eigene,
                dienstgrad,
                letzter?.Ab,
                Laufbahnhinweise.Berechnen(p.Geburtsdatum, eigene, regeln, stichtag),
                einsatzdienst,
                zugehoerigkeit,
                Ehrungsvorschlaege.Ermitteln(ehrungsarten, ehrungen[p.Id].ToList(), grundlagen, stichtag),
                befoerderung,
                anrechnung.NichtAngerechnet);
        }).ToList();
    }

    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var hinweise = new List<Fristhinweis>();
        foreach (var stand in await LadenAsync(stichtag, ct: ct))
        {
            foreach (var h in stand.Hinweise.Where(h => h.Pflicht || h.Art != HinweisArt.EinsatzdienstAb))
            {
                var jugend = stand.Abschnitte.Any(a => a.Id == h.MitgliedschaftId && a.Art is AbteilungArt.Jugend or AbteilungArt.Kinder);
                hinweise.Add(new Fristhinweis(
                    "Mitgliedschaft", stand.Name, h.Text, h.FaelligAm, h.Pflicht ? h.Ampel(stichtag) : Fristampel.Gelb,
                    jugend ? Berechtigungen.ErinnerungJugend : Berechtigungen.ErinnerungLaufbahn));
            }

            foreach (var e in stand.Ehrungsvorschlaege)
            {
                hinweise.Add(new Fristhinweis(
                    "Ehrungen", stand.Name, $"{e.Ehrungsart}: Voraussetzung erfüllt am {e.ErreichtAm:dd.MM.yyyy}", e.ErreichtAm,
                    e.ErreichtAm < stichtag ? Fristampel.Rot : Fristampel.Gelb, Berechtigungen.ErinnerungLaufbahn));
            }

            if (stand.Befoerderung is { } b && b.MoeglichAb >= stichtag.AddDays(-BefoerderungInMailTage))
            {
                hinweise.Add(new Fristhinweis(
                    "Beförderungen", stand.Name, $"Beförderung zum {b.Dienstgrad.Bezeichnung} möglich ab {b.MoeglichAb:dd.MM.yyyy}", b.MoeglichAb,
                    Fristampel.Gelb, Berechtigungen.ErinnerungLaufbahn));
            }
        }

        // Vorgeschlagene Verbandsehrungen bis zum gemeinsamen Antragsschluss beantragen.
        if (await AntragsschlussAsync(stichtag, ct) is { } schluss)
        {
            var offen = await db.Ehrungen.AsNoTracking()
                .Where(e => e.Status == EhrungStatus.Vorgeschlagen && e.Ehrungsart!.MitAntragsschluss)
                .Select(e => new { e.Mitglied!.Nachname, e.Mitglied.Vorname, e.Ehrungsart!.Bezeichnung })
                .ToListAsync(ct);
            hinweise.AddRange(offen.Select(e => new Fristhinweis(
                "Ehrungen", $"{e.Nachname}, {e.Vorname}", $"{e.Bezeichnung}: Antrag bis {schluss:dd.MM.yyyy} stellen", schluss,
                schluss.AddDays(-28) <= stichtag ? Fristampel.Rot : Fristampel.Gelb, Berechtigungen.ErinnerungLaufbahn)));
        }

        return hinweise;
    }
}
