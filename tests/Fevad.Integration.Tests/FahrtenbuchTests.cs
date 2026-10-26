using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Fahrtenbuch und Betankung (V63): Erfassung am Handy, Vorbelegung aus dem Einsatz, Fahrer nur für Berechtigte, Löschregel L63.</summary>
[Collection(AnwendungCollection.Name)]
public partial class FahrtenbuchTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static DateOnly Heute => Anzeige.Heute(TimeProvider.System);

    [Fact]
    public async Task Mitglied_traegt_sich_selbst_ein_und_Hauptamt_sieht_keine_Namen()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid fahrzeugId, mitgliedId, fremdId, kontoId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            Mitglied Neu(string name)
            {
                var m = new Mitglied { Personalnummer = "F" + Guid.NewGuid().ToString("N")[..8], Vorname = "Fritz", Nachname = name, Geburtsdatum = Heute.AddYears(-30) };
                m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
                return m;
            }

            var ich = Neu("Fahrer" + kennung);
            var fremd = Neu("Fremd" + kennung);
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Fahrt " + kennung, Typ = "MTW", Kilometerstand = 100_000 };
            db.AddRange(ich, fremd, fahrzeug);
            await db.SaveChangesAsync();
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var konto = new Benutzer { UserName = $"f-{kennung}@ff-adelberg.test", Email = $"f-{kennung}@ff-adelberg.test", MitgliedId = ich.Id };
            Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
            (fahrzeugId, mitgliedId, fremdId, kontoId) = (fahrzeug.Id, ich.Id, fremd.Id, konto.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var mitglied = Angemeldet(factory, Rollen.Mitglied);
        mitglied.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, kontoId.ToString());
        var seite = await mitglied.GetStringAsync($"/Fahrtenbuch?fahrzeugId={fahrzeugId}");
        Assert.Contains("value=\"100000\"", seite);
        Assert.Contains("keine ausreichende Fahrberechtigung", seite);
        Assert.DoesNotContain("Fremd" + kennung, seite);

        // Ein eingeschmuggelter fremder Fahrer wird ignoriert.
        var antwort = await mitglied.PostAsync("/Fahrtenbuch?handler=Fahrt", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["FahrzeugId"] = fahrzeugId.ToString(),
            ["Fahrt.Datum"] = Heute.ToString("dd.MM.yyyy"),
            ["Fahrt.Abfahrt"] = "18:30",
            ["Fahrt.Rueckkehr"] = "19:45",
            ["Fahrt.Zweck"] = nameof(Fahrtzweck.Dienstfahrt),
            ["Fahrt.Ziel"] = "Ziel" + kennung,
            ["Fahrt.KmBeginn"] = "100000",
            ["Fahrt.KmEnde"] = "100042",
            ["Fahrt.FahrerId"] = fremdId.ToString(),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var fahrt = await MitDbAsync(db => db.Fahrten.SingleAsync(f => f.FahrzeugId == fahrzeugId));
        Assert.Equal((mitgliedId, 42), (fahrt.FahrerId!.Value, fahrt.Strecke!.Value));
        Assert.Equal("18:30", Anzeige.Lokal(fahrt.Beginn).ToString("HH:mm"));
        Assert.Equal(100_042, await MitDbAsync(db => db.Fahrzeuge.Where(f => f.Id == fahrzeugId).Select(f => f.Kilometerstand!.Value).SingleAsync()));

        seite = await mitglied.GetStringAsync($"/Fahrtenbuch?fahrzeugId={fahrzeugId}");
        antwort = await mitglied.PostAsync("/Fahrtenbuch?handler=Tanken", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["FahrzeugId"] = fahrzeugId.ToString(),
            ["Tanken.Datum"] = Heute.ToString("dd.MM.yyyy"),
            ["Tanken.Kraftstoff"] = nameof(Kraftstoff.Diesel),
            ["Tanken.Liter"] = "12,6",
            ["Tanken.Betrag"] = "21,17",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var tankung = await MitDbAsync(db => db.Betankungen.SingleAsync(b => b.FahrzeugId == fahrzeugId));
        Assert.Equal((12.6m, mitgliedId), (tankung.Liter, tankung.GetanktVonId!.Value));

        // Mitglieder sehen das Fahrtenbuch in der Technik nicht; Hauptamt sieht es ohne Namen, der Gerätewart mit.
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Technik/Fahrzeuge/Fahrtenbuch/{fahrzeugId}")).StatusCode);
        var hauptamt = await Angemeldet(factory, Rollen.Hauptamt).GetStringAsync($"/Technik/Fahrzeuge/Fahrtenbuch/{fahrzeugId}");
        Assert.Contains("Ziel" + kennung, hauptamt);
        Assert.DoesNotContain("Fahrer" + kennung, hauptamt);
        var geraetewart = await Angemeldet(factory, Rollen.Geraetewart).GetStringAsync($"/Technik/Fahrzeuge/Fahrtenbuch/{fahrzeugId}");
        Assert.Contains("Fahrer" + kennung, geraetewart);
    }

    [Fact]
    public async Task Einsatzfahrt_ist_vorbelegt_und_verschwindet_nach_dem_Eintragen()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid fahrzeugId, einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Einsatzfahrt " + kennung, Typ = "HLF 10" };
            var ausgerueckt = DateTimeOffset.UtcNow.AddDays(-2);
            var einsatz = new Einsatz { Einsatznummer = "T-FB-" + kennung, Stichwort = "B2", Alarmiert = ausgerueckt.AddMinutes(-5), Status = EinsatzStatus.InBearbeitung };
            einsatz.Fahrzeuge.Add(new EinsatzFahrzeug { FahrzeugId = fahrzeug.Id, Ausgerueckt = ausgerueckt, Zurueck = ausgerueckt.AddHours(2), Kilometer = 14 });
            db.AddRange(fahrzeug, einsatz);
            await db.SaveChangesAsync();
            (fahrzeugId, einsatzId) = (fahrzeug.Id, einsatz.Id);
        }

        var gw = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Geraetewart);
        var seite = await gw.GetStringAsync($"/Fahrtenbuch?fahrzeugId={fahrzeugId}");
        Assert.Contains("T-FB-" + kennung, seite);
        seite = await gw.GetStringAsync($"/Fahrtenbuch?fahrzeugId={fahrzeugId}&einsatzId={einsatzId}");
        Assert.Contains("value=\"14\"", seite);
        Assert.Contains($"value=\"{einsatzId}\"", seite);

        var antwort = await gw.PostAsync("/Fahrtenbuch?handler=Fahrt", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["FahrzeugId"] = fahrzeugId.ToString(),
            ["Fahrt.Datum"] = Anzeige.LokalesDatum(DateTimeOffset.UtcNow.AddDays(-2)).ToString("dd.MM.yyyy"),
            ["Fahrt.Abfahrt"] = "23:10",
            ["Fahrt.Rueckkehr"] = "01:05",
            ["Fahrt.Zweck"] = nameof(Fahrtzweck.Einsatz),
            ["Fahrt.Kilometer"] = "14",
            ["Fahrt.EinsatzId"] = einsatzId.ToString(),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var fahrt = await MitDbAsync(db => db.Fahrten.SingleAsync(f => f.FahrzeugId == fahrzeugId));
        Assert.Equal(einsatzId, fahrt.EinsatzId);
        Assert.True(fahrt.Ende > fahrt.Beginn);
        seite = await gw.GetStringAsync($"/Fahrtenbuch?fahrzeugId={fahrzeugId}");
        Assert.DoesNotContain("T-FB-" + kennung, seite);
    }

    [Fact]
    public async Task L63_leert_Fahrer_alter_Fahrten_und_laesst_die_Kilometer()
    {
        Guid altId, neuId, tankId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitgliedId = await db.Mitglieder.Select(m => m.Id).FirstAsync();
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian L63 " + Guid.NewGuid().ToString("N")[..8], Typ = "MTW" };
            var alt = new Fahrt { FahrzeugId = fahrzeug.Id, Beginn = DateTimeOffset.UtcNow.AddYears(-5), FahrerId = mitgliedId, Kilometer = 20 };
            var neu = new Fahrt { FahrzeugId = fahrzeug.Id, Beginn = DateTimeOffset.UtcNow.AddDays(-10), FahrerId = mitgliedId, Kilometer = 20 };
            var tank = new Betankung { FahrzeugId = fahrzeug.Id, Datum = Heute.AddYears(-5), Liter = 50, GetanktVonId = mitgliedId };
            db.AddRange(fahrzeug, alt, neu, tank);
            await db.SaveChangesAsync();
            (altId, neuId, tankId) = (alt.Id, neu.Id, tank.Id);
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        }

        var fahrten = await MitDbAsync(db => db.Fahrten.Where(f => f.Id == altId || f.Id == neuId).ToDictionaryAsync(f => f.Id));
        Assert.Null(fahrten[altId].FahrerId);
        Assert.Equal(20, fahrten[altId].Kilometer);
        Assert.NotNull(fahrten[neuId].FahrerId);
        Assert.Null(await MitDbAsync(db => db.Betankungen.Where(b => b.Id == tankId).Select(b => b.GetanktVonId).SingleAsync()));
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
}
