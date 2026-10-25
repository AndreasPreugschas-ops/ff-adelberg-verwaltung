using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Benutzer;

/// <summary>Liste aller Konten mit Rollen, Zwei-Faktor-Stand und letzter Anmeldung.</summary>
public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Die Konten nach E-Mail-Adresse.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Zustand eines Kontos.</summary>
    public enum KontoStatus
    {
        /// <summary>Eingerichtet und nutzbar.</summary>
        Aktiv,
        /// <summary>Das Passwort ist noch nicht über den Einrichtungslink gesetzt.</summary>
        EinrichtungOffen,
        /// <summary>Nach Fehlversuchen vorübergehend gesperrt.</summary>
        VoruebergehendGesperrt,
        /// <summary>Deaktiviert; Anmelden ist nicht möglich.</summary>
        Deaktiviert,
    }

    /// <summary>Ein Konto in der Liste.</summary>
    /// <param name="Id">Kennung des Kontos.</param>
    /// <param name="Email">E-Mail-Adresse.</param>
    /// <param name="Mitglied">Name des verknüpften Mitglieds.</param>
    /// <param name="Rollen">Bezeichnungen der Rollen.</param>
    /// <param name="ZweiFaktor">Zwei-Faktor-Anmeldung ist eingerichtet.</param>
    /// <param name="ZweiFaktorPflicht">Für die Rollen des Kontos ist die Zwei-Faktor-Anmeldung Pflicht.</param>
    /// <param name="Status">Zustand des Kontos.</param>
    /// <param name="LetzteAnmeldung">Zeitpunkt der letzten Anmeldung (UTC).</param>
    public sealed record Zeile(Guid Id, string Email, string? Mitglied, IReadOnlyList<string> Rollen, bool ZweiFaktor, bool ZweiFaktorPflicht, KontoStatus Status, DateTimeOffset? LetzteAnmeldung);

    /// <summary>Lädt die Konten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var rollen = await (from ur in db.UserRoles
                            join r in db.Roles on ur.RoleId equals r.Id
                            select new { ur.UserId, r.Name })
            .ToListAsync(ct);
        var rollenJeKonto = rollen.ToLookup(r => r.UserId, r => r.Name!);
        var bezeichnungen = await db.Rollenprofile.AsNoTracking().ToDictionaryAsync(p => p.Name, p => p.Bezeichnung, ct);

        var konten = await (from b in db.Users.AsNoTracking().Where(u => u.AnonymisiertAm == null)
                            join m in db.Mitglieder on b.MitgliedId equals m.Id into mg
                            from m in mg.DefaultIfEmpty()
                            orderby b.Email
                            select new
                            {
                                b.Id, b.Email, b.TwoFactorEnabled, b.LockoutEnd, b.DeaktiviertAm, b.LetzteAnmeldungAm,
                                PasswortGesetzt = b.PasswordHash != null,
                                Mitglied = m == null ? null : m.Nachname + ", " + m.Vorname,
                            })
            .ToListAsync(ct);

        Zeilen = konten
            .Select(k =>
            {
                var namen = rollenJeKonto[k.Id].OrderBy(x => Rollen.Alle.Contains(x) ? Array.IndexOf(Rollen.Alle, x) : int.MaxValue).ToList();
                var r = namen.Select(x => bezeichnungen.GetValueOrDefault(x, Rollen.Bezeichnung(x))).ToList();
                var status = k.DeaktiviertAm is not null ? KontoStatus.Deaktiviert
                    : k.LockoutEnd > jetzt ? KontoStatus.VoruebergehendGesperrt
                    : !k.PasswortGesetzt ? KontoStatus.EinrichtungOffen
                    : KontoStatus.Aktiv;
                return new Zeile(k.Id, k.Email ?? "", k.Mitglied, r, k.TwoFactorEnabled, namen.Any(Rollen.ZweiFaktorNoetig), status, k.LetzteAnmeldungAm);
            })
            .ToList();
    }
}
