using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Fristen;

/// <summary>Lädt die Daten für <see cref="Personalfristen"/> und berechnet die Fristen.</summary>
public sealed class Personalfristenabfrage(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    /// <summary>Fristen gelten nur für die Einsatzabteilung; Jugend- und Altersabteilung brauchen keine Nachweise.</summary>
    public const string AbteilungEinsatz = "EA";

    public async Task<Fristeinstellungen> EinstellungenAsync(DateOnly stichtag, CancellationToken ct)
    {
        // Fehlt ein eigenes Intervall je Übungsart, gilt das allgemeine aus FwDV 7.
        var allgemein = await parameter.ZahlAsync(ParameterSchluessel.AtemschutzUebungIntervallMonate, stichtag, 12, ct);
        var intervalle = new Dictionary<AtemschutzNachweisArt, int>();
        foreach (var art in AtemschutzStatus.JaehrlichePflichten)
        {
            intervalle[art] = await parameter.ZahlAsync(AtemschutzStatus.IntervallSchluessel(art), stichtag, allgemein, ct);
        }

        return new Fristeinstellungen(
            await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct),
            await parameter.ZahlAsync(ParameterSchluessel.NachweisWarnungTage, stichtag, 30, ct),
            await parameter.ZahlAsync(ParameterSchluessel.AtemschutzWarnungTage, stichtag, 60, ct),
            intervalle);
    }

    /// <summary>Fristen aller aktiven Mitglieder der Einsatzabteilung oder, mit <paramref name="mitgliedId"/>, eines einzelnen Mitglieds.</summary>
    public async Task<IReadOnlyList<Fristeintrag>> LadenAsync(DateOnly stichtag, Guid? mitgliedId = null, CancellationToken ct = default)
    {
        var mitglieder = db.Mitglieder.AsNoTracking();
        mitglieder = mitgliedId is { } id
            ? mitglieder.Where(m => m.Id == id)
            : mitglieder.Where(m => m.Mitgliedschaften.Any(s =>
                s.Abteilung!.Code == AbteilungEinsatz
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)));

        var personen = await mitglieder
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();

        var qualifikationen = await db.QualifikationNachweise.AsNoTracking().Include(q => q.Qualifikation)
            .Where(q => ids.Contains(q.MitgliedId)).ToListAsync(ct);
        var nachweise = await db.Nachweise.AsNoTracking().Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct);
        var vorsorgen = await db.AtemschutzVorsorgen.AsNoTracking().Where(v => ids.Contains(v.MitgliedId)).ToListAsync(ct);
        var atemschutz = await db.AtemschutzNachweise.AsNoTracking().Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct);
        var arten = await db.Nachweisarten.AsNoTracking().OrderBy(a => a.Sortierung).ToListAsync(ct);
        var einstellungen = await EinstellungenAsync(stichtag, ct);

        var q = qualifikationen.ToLookup(x => x.MitgliedId);
        var n = nachweise.ToLookup(x => x.MitgliedId);
        var v = vorsorgen.ToLookup(x => x.MitgliedId);
        var a = atemschutz.ToLookup(x => x.MitgliedId);

        return personen
            .SelectMany(p => Personalfristen.Berechnen(
                new Fristdaten(p.Id, $"{p.Nachname}, {p.Vorname}", q[p.Id].ToList(), n[p.Id].ToList(), v[p.Id].ToList(), a[p.Id].ToList()),
                arten, stichtag, einstellungen))
            .ToList();
    }

    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct) =>
        (await LadenAsync(stichtag, ct: ct))
            .Where(f => f.Ampel != Fristampel.Gruen)
            .Select(f => new Fristhinweis(
                "Personal",
                f.Mitglied,
                f.Bezeichnung,
                f.FaelligAm,
                f.Ampel,
                f.Bereich == Fristbereich.Atemschutz || f.Gesundheitsbezogen ? Berechtigungen.ErinnerungAtemschutz : Berechtigungen.ErinnerungPersonal))
            .ToList();
}
