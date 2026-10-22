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
    Befoerderungsvorschlag? Befoerderung)
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
            await parameter.ZahlAsync(LaufbahnParameter.VorlaufMonate, stichtag, s.VorlaufMonate, ct));
    }

    public async Task<Dienstzeitregeln> DienstzeitregelnAsync(DateOnly stichtag, CancellationToken ct) => new(
        await parameter.ZahlAsync(LaufbahnParameter.EhrungJugendAbAlter, stichtag, 14, ct),
        await parameter.ZahlAsync(LaufbahnParameter.EhrungAltersabteilungBisAlter, stichtag, 0, ct));

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
                .Select(s => new { s.MitgliedId, A = new Abschnitt(s.Id, s.Abteilung!.Code, s.Abteilung.Bezeichnung, s.Status, s.GueltigAb, s.GueltigBis, s.Vordienstzeit) })
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
                .Select(e => new { e.MitgliedId, e.EhrungsartId })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.EhrungsartId);

        var ehrungsarten = await db.Ehrungsarten.AsNoTracking().ToListAsync(ct);
        var stufen = await DienstgradeAsync(ct);
        var alleStufen = await db.Dienstgrade.AsNoTracking()
            .Select(d => new DienstgradStufe(d.Id, d.Code, d.Bezeichnung, d.Sortierung, d.MindestwartezeitMonate))
            .ToDictionaryAsync(d => d.Id, ct);
        var wege = await db.Befoerderungswege.AsNoTracking().ToListAsync(ct);

        return personen.Select(p =>
        {
            var eigene = abschnitte[p.Id].OrderBy(a => a.Von).ToList();
            var einsatzdienst = Dienstzeitrechnung.Einsatzdienst(p.Geburtsdatum, eigene, dienstzeitregeln);
            var zugehoerigkeit = Dienstzeitrechnung.Zugehoerigkeit(eigene);
            var letzter = dienstgrade[p.Id].MaxBy(d => d.Ab);
            var dienstgrad = letzter is null ? null : alleStufen.GetValueOrDefault(letzter.DienstgradId);

            Befoerderungsvorschlag? befoerderung = null;
            var einsatz = eigene.FirstOrDefault(a => !a.Vordienstzeit && a.AbteilungCode == AbteilungCode.Einsatz && a.IstOffenAm(stichtag)
                && a.Status is MitgliedStatus.Aktiv or MitgliedStatus.Probezeit);
            if (einsatz is not null)
            {
                var eintritt = eigene.Where(a => !a.Vordienstzeit && a.AbteilungCode == AbteilungCode.Einsatz).Min(a => a.Von);
                var stand = new Befoerderungsstand(
                    dienstgrad,
                    letzter?.Ab ?? eintritt,
                    einsatz.Status != MitgliedStatus.Probezeit,
                    qualifikationen[p.Id].ToHashSet(StringComparer.OrdinalIgnoreCase));
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
                Ehrungsvorschlaege.Ermitteln(ehrungsarten, ehrungen[p.Id].ToList(), einsatzdienst, zugehoerigkeit, stichtag),
                befoerderung);
        }).ToList();
    }

    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var hinweise = new List<Fristhinweis>();
        foreach (var stand in await LadenAsync(stichtag, ct: ct))
        {
            foreach (var h in stand.Hinweise.Where(h => h.Pflicht || h.Art != HinweisArt.EinsatzdienstAb))
            {
                var jugend = stand.Abschnitte.Any(a => a.Id == h.MitgliedschaftId && a.AbteilungCode == AbteilungCode.Jugend);
                hinweise.Add(new Fristhinweis(
                    "Mitgliedschaft", stand.Name, h.Text, h.FaelligAm, h.Pflicht ? h.Ampel(stichtag) : Fristampel.Gelb,
                    jugend ? Berechtigungen.ErinnerungJugend : Berechtigungen.ErinnerungLaufbahn));
            }

            foreach (var e in stand.Ehrungsvorschlaege)
            {
                hinweise.Add(new Fristhinweis(
                    "Ehrungen", stand.Name, $"{e.Ehrungsart}: {e.Jahre} Jahre erreicht am {e.ErreichtAm:dd.MM.yyyy}", e.ErreichtAm,
                    e.ErreichtAm < stichtag ? Fristampel.Rot : Fristampel.Gelb, Berechtigungen.ErinnerungLaufbahn));
            }

            if (stand.Befoerderung is { } b && b.MoeglichAb >= stichtag.AddDays(-BefoerderungInMailTage))
            {
                hinweise.Add(new Fristhinweis(
                    "Beförderungen", stand.Name, $"Beförderung zum {b.Dienstgrad.Bezeichnung} möglich ab {b.MoeglichAb:dd.MM.yyyy}", b.MoeglichAb,
                    Fristampel.Gelb, Berechtigungen.ErinnerungLaufbahn));
            }
        }

        return hinweise;
    }
}
