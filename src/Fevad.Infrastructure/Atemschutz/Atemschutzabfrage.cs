using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Atemschutz;

/// <summary>Geräteträger mit Einsatzstatus, offene Bestätigungen und Abmeldungen für die Atemschutz-Übersicht und die Startseite.</summary>
public sealed class Atemschutzabfrage(AppDbContext db, Personalfristenabfrage fristen, ParameterLeser parameter)
{
    /// <summary>Ein überwachter Geräteträger mit Status und gegebenenfalls wirksamer Abmeldung.</summary>
    public sealed record Traeger(Guid MitgliedId, string Name, AtemschutzStatus Status, AtemschutzSperre? Sperre);

    /// <summary>Alle als aktiv gekennzeichneten Geräteträger mit Status am Stichtag; nur Status und Frist, keine Befunde.</summary>
    public async Task<IReadOnlyList<Traeger>> TraegerAsync(DateOnly stichtag, CancellationToken ct)
    {
        // Ausgetretene oder in die Altersabteilung Gewechselte behalten das Kennzeichen, werden aber nicht mehr überwacht.
        var personen = await db.Mitglieder.AsNoTracking()
            .Where(m => m.AtemschutzAktiv && m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, Name = m.Nachname + ", " + m.Vorname })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();
        var lehrgang = (await db.QualifikationNachweise.AsNoTracking()
            .Where(q => ids.Contains(q.MitgliedId) && q.Qualifikation!.Code == Qualifikation.CodeAtemschutz && q.ErworbenAm <= stichtag)
            .Select(q => q.MitgliedId).ToListAsync(ct)).ToHashSet();
        var vorsorgen = (await db.AtemschutzVorsorgen.AsNoTracking().Where(v => ids.Contains(v.MitgliedId)).ToListAsync(ct)).ToLookup(v => v.MitgliedId);
        var nachweise = (await db.AtemschutzNachweise.AsNoTracking().Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct)).ToLookup(n => n.MitgliedId);
        var sperren = (await WirksameSperrenAsync(stichtag, ct)).ToLookup(s => s.MitgliedId);
        var einstellungen = await fristen.EinstellungenAsync(stichtag, ct);

        return personen.Select(p =>
        {
            var sperre = sperren[p.Id].OrderByDescending(s => s.Bis).FirstOrDefault();
            return new Traeger(p.Id, p.Name, AtemschutzStatus.Berechnen(lehrgang.Contains(p.Id), vorsorgen[p.Id], nachweise[p.Id], stichtag,
                einstellungen.Intervall, einstellungen.AtemschutzWarnungTage, sperre is not null), sperre);
        }).ToList();
    }

    /// <summary>Am Stichtag wirksame Abmeldungen „vorübergehend nicht einsetzbar“.</summary>
    public async Task<IReadOnlyList<AtemschutzSperre>> WirksameSperrenAsync(DateOnly stichtag, CancellationToken ct, Guid? mitgliedId = null) =>
        await db.AtemschutzSperren.AsNoTracking()
            .Where(s => s.AufgehobenAm == null && s.Von <= stichtag && s.Bis >= stichtag && (mitgliedId == null || s.MitgliedId == mitgliedId))
            .ToListAsync(ct);

    /// <summary>Höchstdauer einer Selbstmeldung ohne Genehmigung am Stichtag (Parameter).</summary>
    public Task<int> HoechstTageAsync(DateOnly stichtag, CancellationToken ct) =>
        parameter.ZahlAsync(ParameterSchluessel.AtemschutzSelbstmeldungHoechstTage, stichtag, 14, ct);

    /// <summary>Selbstmeldungen über die Höchstdauer, die noch nicht genehmigt sind und nicht abgelaufen.</summary>
    public async Task<int> GenehmigungenOffenAsync(DateOnly stichtag, CancellationToken ct)
    {
        var hoechst = await HoechstTageAsync(stichtag, ct);
        return (await db.AtemschutzSperren.AsNoTracking()
                .Where(s => s.Selbstmeldung && s.AufgehobenAm == null && s.GenehmigtAm == null && s.Bis >= stichtag)
                .ToListAsync(ct))
            .Count(s => s.GenehmigungOffen(hoechst));
    }

    /// <summary>Nachweise, denen die Bestätigung von Kommando (<paramref name="kommando"/>) bzw. Atemschutzwart noch fehlt.</summary>
    public IQueryable<AtemschutzNachweis> Unbestaetigt(bool kommando, bool wart) =>
        db.AtemschutzNachweise.Where(n => (kommando && n.BestaetigtKommandoAm == null) || (wart && n.BestaetigtWartAm == null));
}
