using System.Security.Claims;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Ein Mitglied mit seiner An- oder Abmeldung und seinem Anwesenheitseintrag zu einem Dienst.</summary>
public sealed record Meldung(
    Guid MitgliedId, string Name, string? Abteilung, RueckmeldungAntwort? Antwort, string? Begruendung, DateTimeOffset? GemeldetAm,
    AnwesenheitStatus? Anwesenheit);

public static class Meldungsliste
{
    /// <summary>
    /// Die Zielgruppe des Dienstes plus alle mit einer Rückmeldung oder einem Eintrag, nach Namen sortiert. Mitglieder der
    /// Jugend- und Kinderfeuerwehr nur für Benutzer mit Zugang zu ihr.
    /// </summary>
    public static async Task<List<Meldung>> LadenAsync(
        AppDbContext db, Dienstplanung planung, Datenbereich bereich, ClaimsPrincipal benutzer, Dienst dienst, CancellationToken ct)
    {
        var rueckmeldungen = await db.Rueckmeldungen.AsNoTracking().Where(r => r.DienstId == dienst.Id).ToDictionaryAsync(r => r.MitgliedId, ct);
        var anwesenheit = await db.Anwesenheiten.AsNoTracking().Where(a => a.DienstId == dienst.Id).ToDictionaryAsync(a => a.MitgliedId, a => a.Status, ct);

        var kreis = await planung.TeilnehmerkreisAsync(dienst, ct);
        var imKreis = kreis.Select(k => k.MitgliedId).ToHashSet();
        var weitere = rueckmeldungen.Keys.Concat(anwesenheit.Keys).Where(id => !imKreis.Contains(id)).Distinct().ToList();
        if (weitere.Count > 0)
        {
            kreis.AddRange(await db.Mitglieder.AsNoTracking().Where(m => weitere.Contains(m.Id))
                .Select(m => new Dienstplanung.Teilnehmer(m.Id, m.Nachname, m.Vorname, null)).ToListAsync(ct));
        }

        var ids = kreis.Select(k => k.MitgliedId).ToList();
        var sichtbar = (await (await bereich.OhneJugendAsync(db.Mitglieder.AsNoTracking(), benutzer, Anzeige.LokalesDatum(dienst.Beginn)))
            .Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct)).ToHashSet();

        return kreis.Where(k => sichtbar.Contains(k.MitgliedId))
            .OrderBy(k => k.Nachname).ThenBy(k => k.Vorname)
            .Select(k =>
            {
                rueckmeldungen.TryGetValue(k.MitgliedId, out var r);
                return new Meldung(k.MitgliedId, $"{k.Nachname}, {k.Vorname}", k.Abteilung, r?.Antwort, r?.Begruendung, r?.GemeldetAm,
                    anwesenheit.TryGetValue(k.MitgliedId, out var a) ? a : null);
            })
            .ToList();
    }
}
