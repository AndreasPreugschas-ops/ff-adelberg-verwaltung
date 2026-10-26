using System.IO.Compression;
using System.Net;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

public partial class DatenschutzTests
{
    [Fact]
    public async Task Archivanbietung_haelt_die_Loeschung_bis_zur_Uebergabe_zurueck()
    {
        var name = "Ar" + Guid.NewGuid().ToString("N")[..6];
        var jetzt = DateTimeOffset.UtcNow;
        Guid alt, liegen;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            alt = (await MitgliedAnlegenAsync(db, name + "Alt", Heute.AddYears(-11))).Id;
            liegen = (await MitgliedAnlegenAsync(db, name + "Liegen", Heute.AddYears(-11))).Id;
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null((await db.Mitglieder.SingleAsync(m => m.Id == alt)).AnonymisiertAm);
            var angebot = await db.Archivangebote.SingleAsync(a => a.DatensatzId == alt && a.Art == Archivgut.Mitgliederakte);
            Assert.Null(angebot.AngebotenAm);
            Assert.Contains(name + "Alt", angebot.Bezeichnung);

            // Ein Angebot, auf das das Archiv seit 13 Monaten nicht geantwortet hat, darf gelöscht werden.
            var ohneAntwort = await db.Archivangebote.SingleAsync(a => a.DatensatzId == liegen && a.Art == Archivgut.Mitgliederakte);
            ohneAntwort.AngebotenAm = Heute.AddMonths(-13);
            await db.SaveChangesAsync();
        }

        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, null, Rollen.Kommandant);
        const string pfad = "/Verwaltung/Datenschutz/Archiv";
        var verzeichnis = await AbschickenAsync(kommandant, pfad, "Verzeichnis", []);
        Assert.Equal("text/csv", verzeichnis.Content.Headers.ContentType?.MediaType);
        Assert.Contains(name + "Alt", await verzeichnis.Content.ReadAsStringAsync());

        Guid angebotId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var angebot = await db.Archivangebote.SingleAsync(a => a.DatensatzId == alt && a.Art == Archivgut.Mitgliederakte);
            Assert.Equal(Heute, angebot.AngebotenAm);
            angebotId = angebot.Id;
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "Entscheiden",
            new() { ["ids"] = angebotId.ToString(), ["entscheidung"] = nameof(Archiventscheidung.Uebernehmen) })).StatusCode);

        // Übernommen, aber noch nicht übergeben: der Löschlauf hält die Akte zurück.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null((await db.Mitglieder.SingleAsync(m => m.Id == alt)).AnonymisiertAm);
            Assert.NotNull((await db.Mitglieder.SingleAsync(m => m.Id == liegen)).AnonymisiertAm);
        }

        // Übergabe vor dem Paket geht nicht.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "Uebergeben", new() { ["ids"] = angebotId.ToString() })).StatusCode);

        var paket = await AbschickenAsync(kommandant, pfad, "Paket", new() { ["ids"] = angebotId.ToString() });
        Assert.Equal("application/zip", paket.Content.Headers.ContentType?.MediaType);
        using (var zip = new ZipArchive(await paket.Content.ReadAsStreamAsync()))
        {
            var dateien = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("metadaten.xml", dateien);
            Assert.Contains("verzeichnis.csv", dateien);
            Assert.Contains(dateien, d => d.EndsWith("/unterlage.pdf", StringComparison.Ordinal));
            using var leser = new StreamReader(zip.GetEntry("verzeichnis.csv")!.Open());
            Assert.Contains(name + "Alt", await leser.ReadToEndAsync());
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "Uebergeben", new() { ["ids"] = angebotId.ToString() })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await db.Mitglieder.SingleAsync(m => m.Id == alt)).AnonymisiertAm);
            var erledigt = await db.Archivangebote.SingleAsync(a => a.Id == angebotId);
            Assert.NotNull(erledigt.ErledigtAm);
            Assert.NotNull(erledigt.Pruefsumme);
            Assert.DoesNotContain(name, erledigt.Bezeichnung);
        }

        // Ohne Recht kein Zugriff.
        var schriftfuehrer = Angemeldet(factory, null, Rollen.Schriftfuehrer);
        Assert.Equal(HttpStatusCode.Forbidden, (await schriftfuehrer.GetAsync(pfad)).StatusCode);
    }
}
