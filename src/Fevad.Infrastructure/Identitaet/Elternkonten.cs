using System.Linq.Expressions;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>
/// Elternkonten: ein Erziehungsberechtigter mit <see cref="Erziehungsberechtigter.KontoId"/> sieht die Termine seines Kindes,
/// solange das Kind minderjährig und in einer Jugend- oder Kinderabteilung aktiv ist und keine Sperre gesetzt wurde.
/// Dieselben Bedingungen nutzen Terminanzeige, Kalender-Abo und der Löschlauf (L35), damit nichts auseinanderläuft.
/// </summary>
public static class Elternkonten
{
    /// <summary>Zuordnungen, die heute gelten: Kind unter <paramref name="volljaehrigAlter"/>, aktiv in Jugend- oder Kinderfeuerwehr, keine Sperre.</summary>
    public static Expression<Func<Erziehungsberechtigter, bool>> Berechtigt(DateOnly heute, int volljaehrigAlter)
    {
        var geborenNach = heute.AddYears(-volljaehrigAlter);
        return e => e.KontoId != null && !e.KeineKontozuordnung && e.Mitglied!.Geburtsdatum > geborenNach
            && e.Mitglied.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= heute
                && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder));
    }

    /// <summary>Zuordnungen, die der Löschlauf aufhebt: das Gegenteil von <see cref="Berechtigt"/> bei gesetztem Konto.</summary>
    public static Expression<Func<Erziehungsberechtigter, bool>> Faellig(DateOnly heute, int volljaehrigAlter)
    {
        var geborenNach = heute.AddYears(-volljaehrigAlter);
        return e => e.KontoId != null && (e.KeineKontozuordnung || e.Mitglied!.Geburtsdatum <= geborenNach
            || !e.Mitglied.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= heute
                && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)));
    }

    /// <summary>
    /// Deaktiviert reine Elternkonten (Rolle Erziehungsberechtigt, kein eigenes Mitglied), denen kein Kind mehr zugeordnet ist.
    /// Danach greift L18. Gibt die Anzahl zurück.
    /// </summary>
    public static async Task<int> VerwaisteDeaktivierenAsync(AppDbContext db, DateTimeOffset jetzt, CancellationToken ct)
    {
        var rolle = await db.Roles.Where(r => r.Name == Rollen.Erziehungsberechtigt).Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (rolle == Guid.Empty)
        {
            return 0;
        }

        return await db.Users
            .Where(u => u.MitgliedId == null && u.DeaktiviertAm == null && u.AnonymisiertAm == null
                && db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == rolle)
                && !db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId != rolle)
                && !db.Erziehungsberechtigte.Any(e => e.KontoId == u.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.DeaktiviertAm, jetzt)
                .SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString())
                .SetProperty(u => u.ConcurrencyStamp, Guid.NewGuid().ToString()), ct);
    }
}
