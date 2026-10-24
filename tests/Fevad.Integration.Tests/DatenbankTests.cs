using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

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
        Assert.Equal(8, await db.Abteilungen.CountAsync());
        // Genau ein Datensatz für Feuerwehr und Gemeinde, auch nach wiederholter Initialisierung.
        Assert.Equal(1, await db.Organisationen.IgnoreQueryFilters().CountAsync());
        Assert.Equal(["EA", "EA2", "EA3", "EA4", "EA5"],
            await db.Abteilungen.Where(a => a.Art == AbteilungArt.Einsatz && a.Aktiv).OrderBy(a => a.Sortierung).Select(a => a.Code).ToListAsync());
        var kinder = await db.Abteilungen.Include(a => a.Uebergeordnete).SingleAsync(a => a.Art == AbteilungArt.Kinder);
        Assert.Equal((false, AbteilungArt.Jugend), (kinder.Aktiv, kinder.Uebergeordnete!.Art));
        Assert.Equal(11, await db.Dienstgrade.CountAsync());
        Assert.Equal(5, await db.StatistikKategorien.CountAsync());
        Assert.Contains(await db.Fahrzeuge.Select(f => f.Funkrufname).ToListAsync(), n => n == "Florian Adelberg 43");
    }

    [Fact]
    public async Task Vorlaeufige_Ausschussfrist_wird_auf_den_Satzungswert_angehoben()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var frist = await db.Parameter.SingleAsync(p => p.Schluessel == ParameterSchluessel.AusschussEinladungsfristTage);
            frist.Wert = "7";
            frist.Quelle = "Mustersatzung (vorläufig, mit Satzung Adelberg abgleichen)";
            await db.SaveChangesAsync();
        }

        await DatenbankInitialisierung.InitialisierenAsync(fixture.Factory.Services);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var frist = await db.Parameter.SingleAsync(p => p.Schluessel == ParameterSchluessel.AusschussEinladungsfristTage);
            Assert.Equal("3", frist.Wert);
            Assert.Contains("§ 13", frist.Quelle);
        }
    }

    [Fact]
    public async Task Alte_Einsatzabteilung_bekommt_ihre_Nummer_nur_ohne_eigenen_Namen()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ea = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
            ea.Bezeichnung = "Einsatzabteilung";
            ea.Kurzbezeichnung = null;
            var ea2 = await db.Abteilungen.SingleAsync(a => a.Code == "EA2");
            ea2.Bezeichnung = "Einsatzabteilung";
            await db.SaveChangesAsync();
        }

        await DatenbankInitialisierung.InitialisierenAsync(fixture.Factory.Services);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ea = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
            Assert.Equal(("Einsatzabteilung 1", "EA 1"), (ea.Bezeichnung, ea.Kurzbezeichnung));

            // Nur die erste hieß früher so; jede andere bleibt, wie sie benannt wurde.
            var ea2 = await db.Abteilungen.SingleAsync(a => a.Code == "EA2");
            Assert.Equal("Einsatzabteilung", ea2.Bezeichnung);
            ea2.Bezeichnung = "Einsatzabteilung 2";
            await db.SaveChangesAsync();
        }
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

    [Fact]
    public async Task Rohdaten_von_Alarmimporten_landen_nicht_im_Protokoll()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = AlarmImport.Empfangen(AlarmQuelle.Manuell, "R-" + Guid.NewGuid().ToString("N")[..8], """{"stichwort":"B2","adresse":"Geheimweg 1"}""", DateTimeOffset.UtcNow, 6);
        db.AlarmImporte.Add(import);
        await db.SaveChangesAsync();

        var protokoll = await db.AuditEintraege.SingleAsync(a => a.DatensatzId == import.Id);
        Assert.DoesNotContain("Geheimweg", protokoll.Aenderungen);
        Assert.Contains("rohdaten", protokoll.Aenderungen, StringComparison.OrdinalIgnoreCase);
    }
}
