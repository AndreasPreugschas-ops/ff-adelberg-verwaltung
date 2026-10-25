using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Persistenz.Seed;

/// <summary>
/// Anmeldungen zum Ausprobieren der Rechte, je mit einer passenden Testperson verknüpft (Kommandant, Gerätewart, Jugendwart,
/// einfaches Mitglied, Mitglied der Jugendfeuerwehr, Elternkonto eines JF-Mitglieds). Generalberechtigung hat weiterhin der Administrator aus <c>Seed:Admin</c>.
/// Nur in der Entwicklung mit den Testdaten; Passwort wie beim Administrator. Nach neuen Testdaten werden die Verknüpfungen nachgezogen.
/// </summary>
public static class Testbenutzer
{
    /// <summary>Domäne der Test-Mailadressen.</summary>
    public const string Domaene = "@ff-adelberg.test";

    private sealed record Vorlage(string Name, string[] Rollen, Func<AppDbContext, DateOnly, Task<Guid?>> Person);

    private static readonly Vorlage[] Vorlagen =
    [
        new("kommandant", [Rollen.Kommandant, Rollen.Mitglied], (db, heute) => InhaberAsync(db, "KDT", heute)),
        new("geraetewart", [Rollen.Geraetewart, Rollen.Mitglied], (db, heute) => InhaberAsync(db, "GW", heute)),
        new("jugendwart", [Rollen.Jugendwart, Rollen.Mitglied], (db, heute) => InhaberAsync(db, "JW", heute)),
        new("mitglied", [Rollen.Mitglied], (db, heute) => MitgliedAsync(db, AbteilungArt.Einsatz, heute)),
        new("jugend", [Rollen.Mitglied], (db, heute) => MitgliedAsync(db, AbteilungArt.Jugend, heute)),
        new("eltern", [Rollen.Erziehungsberechtigt], (_, _) => Task.FromResult<Guid?>(null)),
    ];

    /// <summary>Legt je Rolle ein Testkonto an (nur Entwicklung).</summary>
    /// <param name="services">Dienste der Anwendung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public static async Task AnlegenAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Testbenutzer));
        var passwort = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Seed:Admin:Passwort"];
        if (string.IsNullOrWhiteSpace(passwort))
        {
            return;
        }

        var heute = Ortszeit.Datum(DateTimeOffset.UtcNow);
        foreach (var vorlage in Vorlagen)
        {
            var email = vorlage.Name + Domaene;
            var mitgliedId = await vorlage.Person(db, heute);
            var konto = await benutzer.FindByEmailAsync(email);
            if (konto is null)
            {
                konto = new Benutzer { UserName = email, Email = email, EmailConfirmed = true };
                var neu = await benutzer.CreateAsync(konto, passwort);
                if (!neu.Succeeded)
                {
                    log.LogError("Testbenutzer {Email} nicht angelegt: {Fehler}", email, string.Join(", ", neu.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            if (konto.MitgliedId != mitgliedId && (mitgliedId is null || !await db.Users.AnyAsync(u => u.MitgliedId == mitgliedId && u.Id != konto.Id, ct)))
            {
                konto.MitgliedId = mitgliedId;
                await benutzer.UpdateAsync(konto);
            }

            var fehlend = vorlage.Rollen.Except(await benutzer.GetRolesAsync(konto)).ToList();
            if (fehlend.Count > 0)
            {
                await benutzer.AddToRolesAsync(konto, fehlend);
            }
        }

        await ElternkontoZuordnenAsync(db, benutzer, heute, ct);
        log.LogWarning("Testbenutzer {Namen} (Domäne {Domaene}, Passwort wie Administrator). Nur für die Entwicklung!",
            string.Join(", ", Vorlagen.Select(v => v.Name)), Domaene);
    }

    /// <summary>Das Elternkonto bekommt ein Kind aus der Jugendfeuerwehr: ein Erziehungsberechtigter mit Konto, falls noch keiner besteht.</summary>
    private static async Task ElternkontoZuordnenAsync(AppDbContext db, UserManager<Benutzer> benutzer, DateOnly heute, CancellationToken ct)
    {
        if (await benutzer.FindByEmailAsync("eltern" + Domaene) is not { } eltern || await db.Erziehungsberechtigte.AnyAsync(e => e.KontoId == eltern.Id, ct))
        {
            return;
        }

        var geborenNach = heute.AddYears(-16);
        var kind = await db.Mitglieder
            .Where(m => m.Personalnummer.StartsWith(Testdaten.PersonalnummerPraefix) && m.Geburtsdatum > geborenNach && !m.Erziehungsberechtigte.Any()
                && m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Jugend && s.Status == MitgliedStatus.Aktiv
                    && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .OrderBy(m => m.Personalnummer)
            .FirstOrDefaultAsync(ct);
        if (kind is null)
        {
            return;
        }

        db.Erziehungsberechtigte.Add(new Erziehungsberechtigter
        {
            MitgliedId = kind.Id, Nummer = 1, Verhaeltnis = Sorgeverhaeltnis.Mutter, Vorname = "Petra", Nachname = kind.Nachname, Email = eltern.Email,
            KontoId = eltern.Id, KontoZugeordnetAm = DateTimeOffset.UtcNow, DatenschutzinformationAm = heute,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Testperson mit der laufenden Funktion, z. B. KDT.</summary>
    private static Task<Guid?> InhaberAsync(AppDbContext db, string funktion, DateOnly heute) =>
        db.FunktionZuordnungen.AsNoTracking()
            .Where(z => z.Funktion!.Code == funktion && z.GueltigAb <= heute && (z.GueltigBis == null || z.GueltigBis >= heute)
                && z.Mitglied!.Personalnummer.StartsWith(Testdaten.PersonalnummerPraefix))
            .OrderBy(z => z.Mitglied!.Personalnummer)
            .Select(z => (Guid?)z.MitgliedId)
            .FirstOrDefaultAsync();

    /// <summary>Testperson ohne Funktion, derzeit aktiv in einer Abteilung dieser Art.</summary>
    private static Task<Guid?> MitgliedAsync(AppDbContext db, AbteilungArt art, DateOnly heute) =>
        db.Mitglieder.AsNoTracking()
            .Where(m => m.Personalnummer.StartsWith(Testdaten.PersonalnummerPraefix)
                && !m.Funktionen.Any(f => f.GueltigBis == null || f.GueltigBis >= heute)
                && m.Mitgliedschaften.Any(s => s.Abteilung!.Art == art && s.Status == MitgliedStatus.Aktiv
                    && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .OrderBy(m => m.Personalnummer)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync();
}
