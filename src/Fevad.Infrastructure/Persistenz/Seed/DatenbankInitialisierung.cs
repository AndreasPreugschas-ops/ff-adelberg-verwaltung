using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Persistenz.Seed;

public static class DatenbankInitialisierung
{
    /// <summary>Migrationen anwenden, Rollen und Stammdaten anlegen, optional einen ersten Administrator.</summary>
    public static async Task InitialisierenAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var konfiguration = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatenbankInitialisierung));

        if (konfiguration.GetValue("Datenbank:AutoMigrate", true))
        {
            await db.Database.MigrateAsync(ct);
        }

        await RollenAnlegenAsync(sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>());

        await KatalogAnlegenAsync(db, Stammdaten.Abteilungen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Dienstgrade, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Funktionen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Qualifikationen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.StatistikKategorien, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Dokumentkategorien, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Nachweisarten, ct);

        foreach (var parameter in Stammdaten.Parameter)
        {
            if (!await db.Parameter.IgnoreQueryFilters().AnyAsync(p => p.Schluessel == parameter.Schluessel, ct))
            {
                db.Parameter.Add(parameter);
            }
        }

        // Wege brauchen die Ids der Dienstgrade.
        await db.SaveChangesAsync(ct);
        await LaufbahnStammdaten.AnlegenAsync(db, ct);

        foreach (var fahrzeug in Stammdaten.Fahrzeuge)
        {
            if (!await db.Fahrzeuge.IgnoreQueryFilters().AnyAsync(f => f.Funkrufname == fahrzeug.Funkrufname, ct))
            {
                db.Fahrzeuge.Add(fahrzeug);
            }
        }

        await db.SaveChangesAsync(ct);

        await AdministratorAnlegenAsync(sp.GetRequiredService<UserManager<Benutzer>>(), konfiguration, log);
    }

    private static async Task RollenAnlegenAsync(RoleManager<IdentityRole<Guid>> rollen)
    {
        foreach (var name in Rollen.Alle)
        {
            if (!await rollen.RoleExistsAsync(name))
            {
                await rollen.CreateAsync(new IdentityRole<Guid>(name) { Id = Guid.CreateVersion7() });
            }
        }
    }

    private static async Task KatalogAnlegenAsync<T>(AppDbContext db, IEnumerable<T> eintraege, CancellationToken ct)
        where T : Katalogeintrag
    {
        var vorhanden = await db.Set<T>().IgnoreQueryFilters().Select(k => k.Code).ToListAsync(ct);
        db.Set<T>().AddRange(eintraege.Where(e => !vorhanden.Contains(e.Code)));
    }

    /// <summary>Legt den ersten Administrator an, wenn Seed:Admin:Email und Seed:Admin:Passwort gesetzt sind und es noch keinen gibt.</summary>
    private static async Task AdministratorAnlegenAsync(UserManager<Benutzer> benutzer, IConfiguration konfiguration, ILogger log)
    {
        var email = konfiguration["Seed:Admin:Email"];
        var passwort = konfiguration["Seed:Admin:Passwort"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwort))
        {
            return;
        }

        if ((await benutzer.GetUsersInRoleAsync(Rollen.Administrator)).Count > 0)
        {
            return;
        }

        var admin = new Benutzer { UserName = email, Email = email, EmailConfirmed = true };
        var ergebnis = await benutzer.CreateAsync(admin, passwort);
        if (!ergebnis.Succeeded)
        {
            log.LogError("Administrator konnte nicht angelegt werden: {Fehler}", string.Join(", ", ergebnis.Errors.Select(e => e.Description)));
            return;
        }

        await benutzer.AddToRolesAsync(admin, [Rollen.Administrator, Rollen.Kommandant]);
        log.LogInformation("Erster Administrator {Email} angelegt.", email);
    }
}
