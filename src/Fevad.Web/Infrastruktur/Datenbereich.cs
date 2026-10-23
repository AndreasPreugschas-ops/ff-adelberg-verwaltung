using System.Security.Claims;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Mitglieder und Dienste jemand sehen und bearbeiten darf. Rechte mit Abteilungsbezug (Mitglieder lesen, anlegen,
/// bearbeiten; Anwesenheit lesen; Dienste bearbeiten) gelten für alle Abteilungen oder nur für die freigegebenen samt
/// Unterabteilungen, z. B. beim Jugendwart für Jugend- und Kinderfeuerwehr. Mitglieder zählen zu einer Abteilung, wenn sie
/// am Stichtag dort Mitglied sind; Dienste über ihre Zielgruppe.
/// </summary>
public sealed class Datenbereich(Rechteverzeichnis rechte, AppDbContext db)
{
    public Task<Freigabe> FreigabeAsync(ClaimsPrincipal benutzer, string recht) => rechte.FreigabeAsync(benutzer, recht);

    /// <summary>Abteilungen, deren Mitglieder jemand lesen darf; null für alle.</summary>
    public async Task<IReadOnlySet<Guid>?> LeseabteilungenAsync(ClaimsPrincipal benutzer)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitgliederLesen);
        return freigabe.Alle ? null : freigabe.Abteilungen;
    }

    public async Task<IQueryable<Mitglied>> Filtern(IQueryable<Mitglied> abfrage, ClaimsPrincipal benutzer, DateOnly stichtag)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitgliederLesen);
        return freigabe.Alle ? abfrage : abfrage.Where(InAbteilungen(freigabe.Abteilungen, stichtag));
    }

    /// <summary>Bearbeiten eines vorhandenen Mitglieds oder, mit <paramref name="mitgliedId"/> null, Anlegen.</summary>
    public async Task<bool> DarfBearbeitenAsync(
        ClaimsPrincipal benutzer, IQueryable<Mitglied> mitglieder, Guid? mitgliedId, DateOnly stichtag, CancellationToken ct)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, mitgliedId is null ? Berechtigungen.MitgliederAnlegen : Berechtigungen.MitgliederBearbeiten, ct);
        if (freigabe.Alle || mitgliedId is null)
        {
            return freigabe.Irgendwo;
        }

        return freigabe.Irgendwo
            && await mitglieder.Where(m => m.Id == mitgliedId).AnyAsync(InAbteilungen(freigabe.Abteilungen, stichtag), ct);
    }

    /// <summary>Abteilungen, in denen jemand Mitglieder anlegen darf; null für alle.</summary>
    public async Task<IReadOnlySet<Guid>?> AnlegeabteilungenAsync(ClaimsPrincipal benutzer)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitgliederAnlegen);
        return freigabe.Alle ? null : freigabe.Abteilungen;
    }

    /// <summary>Planen, Ändern und Anwesenheit erfassen für einen Dienst mit dieser Zielgruppe (null: alle Abteilungen).</summary>
    public async Task<bool> DarfDienstBearbeitenAsync(ClaimsPrincipal benutzer, Guid? abteilungId, CancellationToken ct) =>
        Umfasst(await rechte.FreigabeAsync(benutzer, Berechtigungen.DiensteBearbeiten, ct), abteilungId);

    /// <summary>Anwesenheit und Rückmeldungen eines Dienstes mit dieser Zielgruppe sehen.</summary>
    public async Task<bool> DarfDienstSehenAsync(ClaimsPrincipal benutzer, Guid? abteilungId, CancellationToken ct) =>
        Umfasst(await rechte.FreigabeAsync(benutzer, Berechtigungen.AnwesenheitLesen, ct), abteilungId);

    /// <summary>Abteilungen, für die jemand Dienste planen darf; null für alle.</summary>
    public async Task<IReadOnlySet<Guid>?> DienstabteilungenAsync(ClaimsPrincipal benutzer)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.DiensteBearbeiten);
        return freigabe.Alle ? null : freigabe.Abteilungen;
    }

    /// <summary>Erste aktive Abteilung aus der Menge, als Vorbelegung (z. B. die Jugendfeuerwehr für den Jugendwart).</summary>
    public Task<Guid> ErsteAbteilungAsync(IReadOnlySet<Guid> abteilungen, CancellationToken ct) =>
        db.Abteilungen.Where(a => abteilungen.Contains(a.Id)).OrderByDescending(a => a.Aktiv).ThenBy(a => a.Sortierung).Select(a => a.Id).FirstOrDefaultAsync(ct);

    private static bool Umfasst(Freigabe freigabe, Guid? abteilungId) =>
        freigabe.Alle || (abteilungId is { } id && freigabe.Abteilungen.Contains(id));

    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> InAbteilungen(IReadOnlySet<Guid> abteilungen, DateOnly stichtag)
    {
        var ids = abteilungen.ToList();
        return m => m.Mitgliedschaften.Any(s => ids.Contains(s.AbteilungId) && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag));
    }
}
