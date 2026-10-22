using System.Security.Claims;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Mitglieder jemand sehen und bearbeiten darf. Mit Mitglieder.Lesen alle; mit Jugend.Lesen
/// (Jugendwart) nur die, die am Stichtag in der Jugendfeuerwehr sind.
/// </summary>
public sealed class Datenbereich(IAuthorizationService autorisierung)
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

    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> InJugendfeuerwehr(DateOnly stichtag) =>
        m => m.Mitgliedschaften.Any(s => s.Abteilung!.Code == AbteilungCode.Jugend
            && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag));

    private async Task<bool> DarfAsync(ClaimsPrincipal benutzer, string policy) =>
        (await autorisierung.AuthorizeAsync(benutzer, policy)).Succeeded;
}
