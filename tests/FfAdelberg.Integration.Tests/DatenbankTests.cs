using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Domain.Mitglieder;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Infrastructure.Persistenz.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FfAdelberg.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class DatenbankTests(AnwendungFixture fixture)
{
    [Fact]
    public async Task Stammdaten_und_Rollen_sind_angelegt_und_Initialisierung_ist_wiederholbar()
    {
        await DatenbankInitialisierung.InitialisierenAsync(fixture.Factory.Services);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rollen = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.Equal(Rollen.Alle.Length, await rollen.Roles.CountAsync());
        Assert.Equal(3, await db.Abteilungen.CountAsync());
        Assert.Equal(11, await db.Dienstgrade.CountAsync());
        Assert.Equal(5, await db.StatistikKategorien.CountAsync());
        Assert.Contains(await db.Fahrzeuge.Select(f => f.Funkrufname).ToListAsync(), n => n == "Florian Adelberg 43");
    }

    [Fact]
    public async Task Loeschen_ist_Soft_Delete_und_wird_protokolliert()
    {
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = new Mitglied { Personalnummer = "T-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Erika", Nachname = "Test", Geburtsdatum = new(1995, 4, 1) };
            db.Mitglieder.Add(mitglied);
            await db.SaveChangesAsync();
            id = mitglied.Id;

            mitglied.Ort = "Adelberg";
            await db.SaveChangesAsync();

            db.Mitglieder.Remove(mitglied);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.False(await db.Mitglieder.AnyAsync(m => m.Id == id));
            var geloescht = await db.Mitglieder.IgnoreQueryFilters().SingleAsync(m => m.Id == id);
            Assert.NotNull(geloescht.GeloeschtAm);
            Assert.NotEqual(default, geloescht.ErstelltAm);

            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == id).OrderBy(a => a.Id).ToListAsync();
            Assert.Equal([AuditAktion.Angelegt, AuditAktion.Geaendert, AuditAktion.Geloescht], protokoll.Select(a => a.Aktion));
            Assert.Contains("Adelberg", protokoll[1].Aenderungen);
            Assert.All(protokoll, a => Assert.Equal("mitglieder", a.Tabelle));
        }
    }

    [Fact]
    public async Task Gleichzeitige_Aenderung_wird_erkannt()
    {
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = new Mitglied { Personalnummer = "K-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Karl", Nachname = "Konflikt", Geburtsdatum = new(1980, 1, 1) };
            db.Mitglieder.Add(mitglied);
            await db.SaveChangesAsync();
            id = mitglied.Id;
        }

        await using var scopeA = fixture.Factory.Services.CreateAsyncScope();
        await using var scopeB = fixture.Factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await dbA.Mitglieder.SingleAsync(m => m.Id == id);
        var b = await dbB.Mitglieder.SingleAsync(m => m.Id == id);

        a.Ort = "Adelberg";
        await dbA.SaveChangesAsync();

        b.Ort = "Göppingen";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
