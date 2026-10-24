using System.Security.Claims;
using Fevad.Domain.Dienstbetrieb;
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
/// am Stichtag dort Mitglied sind; Dienste über ihre Zielgruppe. Die Jugend- und Kinderfeuerwehr ist abgeschirmt: Wer keinen
/// Zugang zu ihr hat (<see cref="Berechtigungen.JugendZugang"/>), sieht ihre Mitglieder nirgends, auch nicht mit Rechten für
/// alle Abteilungen; Mitglieder, die nur dort waren, bleiben auch nach dem Austritt verborgen.
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
        return freigabe.Alle ? abfrage
            : freigabe.OhneJugend is { } jugend ? abfrage.Where(OhneJugend(jugend, stichtag))
            : abfrage.Where(InAbteilungen(freigabe.Abteilungen, stichtag));
    }

    /// <summary>Nur die Abschirmung der Jugendfeuerwehr, für Seiten, die nicht über Mitglieder.Lesen gehen (Ausbildung, Ehrungen).</summary>
    public async Task<IQueryable<Mitglied>> OhneJugendAsync(IQueryable<Mitglied> abfrage, ClaimsPrincipal benutzer, DateOnly stichtag)
    {
        var stand = await rechte.StandAsync();
        return await rechte.JugendzugangAsync(benutzer) || stand.Jugendabteilungen.Count == 0
            ? abfrage
            : abfrage.Where(OhneJugend(stand.Jugendabteilungen, stichtag));
    }

    public Task<bool> JugendzugangAsync(ClaimsPrincipal benutzer) => rechte.JugendzugangAsync(benutzer);

    /// <summary>Personal der Jugend- und Kinderfeuerwehr: am Stichtag dort Mitglied.</summary>
    public static IQueryable<Mitglied> NurJugend(IQueryable<Mitglied> abfrage, DateOnly stichtag) =>
        abfrage.Where(m => m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
            && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)));

    /// <summary>Übriges Personal: nicht in der Jugend- oder Kinderfeuerwehr und nicht nur dort gewesen.</summary>
    public static IQueryable<Mitglied> OhneJugendpersonal(IQueryable<Mitglied> abfrage, DateOnly stichtag) =>
        abfrage.Where(m => !m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag))
            && (!m.Mitgliedschaften.Any() || m.Mitgliedschaften.Any(s => s.Abteilung!.Art != AbteilungArt.Jugend && s.Abteilung.Art != AbteilungArt.Kinder)));

    /// <summary>Bearbeiten eines vorhandenen Mitglieds oder, mit <paramref name="mitgliedId"/> null, Anlegen.</summary>
    public async Task<bool> DarfBearbeitenAsync(
        ClaimsPrincipal benutzer, IQueryable<Mitglied> mitglieder, Guid? mitgliedId, DateOnly stichtag, CancellationToken ct)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, mitgliedId is null ? Berechtigungen.MitgliederAnlegen : Berechtigungen.MitgliederBearbeiten, ct);
        if (freigabe.Alle || mitgliedId is null)
        {
            return freigabe.Irgendwo;
        }

        return freigabe.Irgendwo && await mitglieder.Where(m => m.Id == mitgliedId)
            .AnyAsync(freigabe.OhneJugend is { } jugend ? OhneJugend(jugend, stichtag) : InAbteilungen(freigabe.Abteilungen, stichtag), ct);
    }

    /// <summary>Abteilungen, in denen jemand Mitglieder anlegen darf; null für alle.</summary>
    public async Task<IReadOnlySet<Guid>?> AnlegeabteilungenAsync(ClaimsPrincipal benutzer)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitgliederAnlegen);
        return freigabe.Alle ? null : freigabe.Abteilungen;
    }

    /// <summary>
    /// Planen, Ändern und Anwesenheit erfassen für einen Dienst mit diesen Zielabteilungen (leer: alle Abteilungen).
    /// Üben mehrere Abteilungen zusammen, braucht es die Freigabe für jede davon.
    /// </summary>
    public async Task<bool> DarfDienstBearbeitenAsync(ClaimsPrincipal benutzer, IReadOnlyCollection<Guid> abteilungen, CancellationToken ct) =>
        Umfasst(await rechte.FreigabeAsync(benutzer, Berechtigungen.DiensteBearbeiten, ct), abteilungen);

    /// <summary>Anwesenheit und Rückmeldungen eines Dienstes sehen; bei mehreren Zielabteilungen genügt eine davon.</summary>
    public async Task<bool> DarfDienstSehenAsync(ClaimsPrincipal benutzer, IReadOnlyCollection<Guid> abteilungen, CancellationToken ct)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.AnwesenheitLesen, ct);
        return abteilungen.Count == 0 ? Umfasst(freigabe, abteilungen) : abteilungen.Any(a => Umfasst(freigabe, [a]));
    }

    /// <summary>
    /// Rückmeldungen und Anwesenheit eines Dienstes ändern, auch nach dem Rückmeldeschluss: wer den Dienst bearbeiten darf
    /// und der eingetragene Übungsleiter.
    /// </summary>
    public async Task<bool> DarfMeldungenBearbeitenAsync(ClaimsPrincipal benutzer, Dienst dienst, CancellationToken ct) =>
        await DarfDienstBearbeitenAsync(benutzer, dienst.AbteilungIds, ct) || await IstUebungsleiterAsync(benutzer, dienst, ct);

    public async Task<bool> IstUebungsleiterAsync(ClaimsPrincipal benutzer, Dienst dienst, CancellationToken ct) =>
        dienst.VerantwortlicherId is { } leiter
        && Guid.TryParse(benutzer.FindFirstValue(ClaimTypes.NameIdentifier), out var benutzerId)
        && await db.Users.AnyAsync(b => b.Id == benutzerId && b.MitgliedId == leiter, ct);

    /// <summary>Ohne Zielabteilung (alle) genügt auch eine Freigabe ohne Jugendfeuerwehr; deren Mitglieder blendet <see cref="Filtern"/> aus.</summary>
    public static bool Umfasst(Freigabe freigabe, IReadOnlyCollection<Guid> abteilungen) =>
        freigabe.Alle || (abteilungen.Count > 0 ? abteilungen.All(freigabe.Abteilungen.Contains) : freigabe.OhneJugend is not null);

    /// <summary>Abteilungen, für die jemand Dienste planen darf; null für alle.</summary>
    public async Task<IReadOnlySet<Guid>?> DienstabteilungenAsync(ClaimsPrincipal benutzer)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.DiensteBearbeiten);
        return freigabe.Alle ? null : freigabe.Abteilungen;
    }

    /// <summary>Erste aktive Abteilung aus der Menge, als Vorbelegung (z. B. die Jugendfeuerwehr für den Jugendwart).</summary>
    public Task<Guid> ErsteAbteilungAsync(IReadOnlySet<Guid> abteilungen, CancellationToken ct) =>
        db.Abteilungen.Where(a => abteilungen.Contains(a.Id)).OrderByDescending(a => a.Aktiv).ThenBy(a => a.Sortierung).Select(a => a.Id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Nicht derzeit in der Jugend- oder Kinderfeuerwehr und schon einmal anderswo (z. B. nach dem Übertritt). Wer nur dort war,
    /// bleibt verborgen; Mitglieder ganz ohne Mitgliedschaft bleiben sichtbar.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> OhneJugend(IReadOnlySet<Guid> jugend, DateOnly stichtag)
    {
        var ids = jugend.ToList();
        return m => !m.Mitgliedschaften.Any(s => ids.Contains(s.AbteilungId) && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag))
            && (!m.Mitgliedschaften.Any() || m.Mitgliedschaften.Any(s => !ids.Contains(s.AbteilungId)));
    }

    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> InAbteilungen(IReadOnlySet<Guid> abteilungen, DateOnly stichtag)
    {
        var ids = abteilungen.ToList();
        return m => m.Mitgliedschaften.Any(s => ids.Contains(s.AbteilungId) && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag));
    }
}
