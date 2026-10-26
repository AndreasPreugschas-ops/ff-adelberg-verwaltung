using System.IO.Compression;
using System.Net;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Auswertungen;
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

    [Fact]
    public async Task L2_verdichtet_Einsatzberichte_nach_der_Anbietung_und_erhaelt_Statistik_und_Exposition()
    {
        var name = "Ez" + Guid.NewGuid().ToString("N")[..6];
        var alarm = new DateTimeOffset(Heute.Year - 12, 6, 15, 18, 0, 0, TimeSpan.Zero);
        var jahr = alarm.Year;
        Guid einsatz, nachweis;
        Jahresstatistik.Bericht vorher;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = await MitgliedAnlegenAsync(db, name, null);
            var e = new Einsatz
            {
                Einsatznummer = name, Stichwort = "B 2", Ort = "Adelberg", Strasse = "Hauptstraße", Hausnummer = "7", Meldung = "Rauch aus Fenster " + name,
                Lagebericht = "Familie " + name, Alarmiert = alarm, Ausgerueckt = alarm.AddMinutes(5), Einsatzende = alarm.AddHours(2),
                Status = EinsatzStatus.Freigegeben, EinsatzleiterId = mitglied.Id,
                StatistikKategorieId = await db.StatistikKategorien.Where(k => k.Code == "BR").Select(k => k.Id).SingleAsync(),
            };
            e.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = mitglied.Id, Exposition = ExpositionStatus.Exponiert, Gefahrstoffe = "Brandrauch" });
            e.Beteiligte.Add(new EinsatzBeteiligter { Name = "Geschädigter " + name, Telefon = "0700" });
            e.Tagebuch.Add(new Tagebucheintrag { Zeitpunkt = alarm.AddMinutes(10), Text = "Lage " + name });
            db.Einsaetze.Add(e);
            var n = new AtemschutzNachweis { MitgliedId = mitglied.Id, EinsatzId = e.Id, Datum = DateOnly.FromDateTime(alarm.Date), Art = AtemschutzNachweisArt.Exposition, Ort = "Adelberg", Exposition = "Brandrauch" };
            db.AtemschutzNachweise.Add(n);
            await db.SaveChangesAsync();
            (einsatz, nachweis) = (e.Id, n.Id);
            vorher = await scope.ServiceProvider.GetRequiredService<Jahresstatistik>().ZeitraumAsync(new(jahr, 1, 1), new(jahr, 12, 31));
        }

        var ergebnisse = await Archivhilfe.AnbietenUndLoeschenAsync(fixture.Factory.Services, DateTimeOffset.UtcNow, Archivgut.Einsatzbericht, einsatz);
        Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelEinsatzberichte).Anzahl >= 1);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.Einsaetze.Include(x => x.Teilnahmen).Include(x => x.Beteiligte).Include(x => x.Tagebuch).SingleAsync(x => x.Id == einsatz);
            Assert.NotNull(e.VerdichtetAm);
            Assert.Equal((1, 2m), (e.VerdichtetTeilnehmer, e.VerdichtetPersonenstunden));
            Assert.Empty(e.Teilnahmen);
            Assert.Empty(e.Beteiligte);
            Assert.Empty(e.Tagebuch);
            Assert.Equal((null, null, null, null, null), (e.Strasse, e.Hausnummer, e.Meldung, e.Lagebericht, e.EinsatzleiterId));
            Assert.Equal(("B 2", "Adelberg"), (e.Stichwort, e.Ort));

            // Exposition (L8, 40 Jahre) bleibt mit Verweis auf den Einsatz.
            Assert.Equal(einsatz, (await db.AtemschutzNachweise.SingleAsync(x => x.Id == nachweis)).EinsatzId);

            // Die Jahresstatistik zählt gleich wie vorher.
            var nachher = await scope.ServiceProvider.GetRequiredService<Jahresstatistik>().ZeitraumAsync(new(jahr, 1, 1), new(jahr, 12, 31));
            Assert.Equal(vorher.Einsaetze.Personenstunden, nachher.Einsaetze.Personenstunden);
            Assert.Equal(vorher.Einsaetze.DurchschnittTeilnehmer, nachher.Einsaetze.DurchschnittTeilnehmer);
            Assert.Equal(vorher.Einsaetze.Freigegeben, nachher.Einsaetze.Freigegeben);

            var angebot = await db.Archivangebote.SingleAsync(a => a.DatensatzId == einsatz && a.Art == Archivgut.Einsatzbericht);
            Assert.NotNull(angebot.ErledigtAm);
            Assert.DoesNotContain(name, angebot.Bezeichnung);
        }

        // Ein zweiter Lauf ändert nichts mehr.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2m, (await db.Einsaetze.SingleAsync(x => x.Id == einsatz)).VerdichtetPersonenstunden);
        }
    }

    [Fact]
    public async Task Archiveinstellungen_sind_in_der_Oberflaeche_einstellbar()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, null, Rollen.Kommandant);
        const string pfad = "/Verwaltung/Datenschutz/Archiv";
        var felder = new Dictionary<string, string> { ["stelle"] = "Kreisarchiv Göppingen", ["fristMonate"] = "0" };
        foreach (var b in Archivierung.Katalog)
        {
            felder[$"aufbewahrung[{b.Art}]"] = "10";
            felder[$"bewertung[{b.Art}]"] = "archivwürdig";
        }

        // Ungültige Frist: Seite mit Fehler, nichts gespeichert.
        var fehler = await AbschickenAsync(kommandant, pfad, "Einstellungen", new(felder));
        Assert.Equal(HttpStatusCode.OK, fehler.StatusCode);
        Assert.Contains("zwischen 1 und 60 Monaten", WebUtility.HtmlDecode(await fehler.Content.ReadAsStringAsync()));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var parameter = scope.ServiceProvider.GetRequiredService<ParameterLeser>();
        var bisher = new Dictionary<string, string?>();
        foreach (var s in new[] { ParameterSchluessel.ArchivStelle, ParameterSchluessel.ArchivEntscheidungFristMonate, ParameterSchluessel.ArchivBewertungPraefix + "Kassenjahr", ParameterSchluessel.EinsatzberichteAufbewahrungJahre })
        {
            bisher[s] = await parameter.WertAsync(s, Heute);
        }

        try
        {
            felder["fristMonate"] = "6";
            felder["bewertung[Kassenjahr]"] = Archivierung.Kassabel;
            felder["aufbewahrung[Einsatzbericht]"] = "12";
            Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "Einstellungen", new(felder))).StatusCode);
            Assert.Equal("Kreisarchiv Göppingen", await parameter.WertAsync(ParameterSchluessel.ArchivStelle, Heute));
            Assert.Equal("6", await parameter.WertAsync(ParameterSchluessel.ArchivEntscheidungFristMonate, Heute));
            Assert.Equal(Archivierung.Kassabel, await parameter.WertAsync(ParameterSchluessel.ArchivBewertungPraefix + "Kassenjahr", Heute));
            Assert.Equal("12", await parameter.WertAsync(ParameterSchluessel.EinsatzberichteAufbewahrungJahre, Heute));
            Assert.Contains("Kreisarchiv Göppingen", WebUtility.HtmlDecode(await kommandant.GetStringAsync(pfad)));
        }
        finally
        {
            // Andere Tests verlassen sich auf die Vorgaben: die Werte von heute wieder entfernen.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Parameter.Where(p => p.GueltigAb == Heute && p.Quelle == "Einstellung in FEVAD (Archivanbietung)").ExecuteDeleteAsync();
        }
    }
}
