using System.Security.Claims;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Mitglieder jemand sehen und bearbeiten darf. Mit Mitglieder.Lesen alle; mit Jugend.Lesen
/// (Jugendwart) nur die, die am Stichtag in der Jugend- oder Kinderfeuerwehr sind. Für Dienste gilt dasselbe: der
/// Jugendwart plant und erfasst nur Dienste mit der Zielgruppe Jugend- oder Kinderfeuerwehr.
/// </summary>
public sealed class Datenbereich(IAuthorizationService autorisierung, AppDbContext db)
{
    public async Task<bool> NurJugendAsync(ClaimsPrincipal benutzer) =>
        !await DarfAsync(benutzer, Berechtigungen.MitgliederLesen) && await DarfAsync(benutzer, Berechtigungen.JugendLesen);

    public async Task<IQueryable<Mitglied>> Filtern(IQueryable<Mitglied> abfrage, ClaimsPrincipal benutzer, DateOnly stichtag)
    {
        if (await DarfAsync(benutzer, Berechtigungen.MitgliederLesen))
        {
            return abfrage;
        }

        return await DarfAsync(benutzer, Berechtigungen.JugendLesen)
            ? abfrage.Where(InJugendfeuerwehr(stichtag))
            : abfrage.Where(m => false);
    }

    /// <summary>Bearbeiten eines vorhandenen Mitglieds oder, mit <paramref name="mitgliedId"/> null, Anlegen.</summary>
    public async Task<bool> DarfBearbeitenAsync(
        ClaimsPrincipal benutzer, IQueryable<Mitglied> mitglieder, Guid? mitgliedId, DateOnly stichtag, CancellationToken ct)
    {
        if (await DarfAsync(benutzer, Berechtigungen.MitgliederBearbeiten))
        {
            return true;
        }

        return await DarfAsync(benutzer, Berechtigungen.JugendBearbeiten)
            && (mitgliedId is null || await mitglieder.Where(m => m.Id == mitgliedId).AnyAsync(InJugendfeuerwehr(stichtag), ct));
    }

    /// <summary>Ob jemand nur Mitglieder der Jugendfeuerwehr anlegen darf.</summary>
    public async Task<bool> NurJugendAnlegenAsync(ClaimsPrincipal benutzer) =>
        !await DarfAsync(benutzer, Berechtigungen.MitgliederBearbeiten) && await DarfAsync(benutzer, Berechtigungen.JugendBearbeiten);

    /// <summary>Planen, Ändern und Anwesenheit erfassen für einen Dienst mit dieser Zielgruppe (null: alle Abteilungen).</summary>
    public async Task<bool> DarfDienstBearbeitenAsync(ClaimsPrincipal benutzer, Guid? abteilungId, CancellationToken ct) =>
        await DarfAsync(benutzer, Berechtigungen.DiensteBearbeiten)
        || (await DarfAsync(benutzer, Berechtigungen.JugendBearbeiten) && await IstJugendAsync(abteilungId, ct));

    /// <summary>Anwesenheit und Rückmeldungen eines Dienstes mit dieser Zielgruppe sehen.</summary>
    public async Task<bool> DarfDienstSehenAsync(ClaimsPrincipal benutzer, Guid? abteilungId, CancellationToken ct) =>
        await DarfAsync(benutzer, Berechtigungen.AnwesenheitLesen)
        || (await DarfAsync(benutzer, Berechtigungen.JugendLesen) && await IstJugendAsync(abteilungId, ct));

    /// <summary>Ob jemand Dienste nur für die Jugendfeuerwehr planen darf.</summary>
    public async Task<bool> NurJugendDiensteAsync(ClaimsPrincipal benutzer) =>
        !await DarfAsync(benutzer, Berechtigungen.DiensteBearbeiten) && await DarfAsync(benutzer, Berechtigungen.JugendBearbeiten);

    public Task<Guid> JugendAbteilungIdAsync(CancellationToken ct) =>
        db.Abteilungen.Where(a => a.Art == AbteilungArt.Jugend).OrderByDescending(a => a.Aktiv).ThenBy(a => a.Sortierung).Select(a => a.Id).FirstAsync(ct);

    private async Task<bool> IstJugendAsync(Guid? abteilungId, CancellationToken ct) =>
        abteilungId is { } id && await db.Abteilungen.AnyAsync(a => a.Id == id && (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder), ct);

    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> InJugendfeuerwehr(DateOnly stichtag) =>
        m => m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
            && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag));

    private async Task<bool> DarfAsync(ClaimsPrincipal benutzer, string policy) =>
        (await autorisierung.AuthorizeAsync(benutzer, policy)).Succeeded;
}
