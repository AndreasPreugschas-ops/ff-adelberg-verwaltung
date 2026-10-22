using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Domain.Mitglieder;
using FfAdelberg.Infrastructure.Einsaetze;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FfAdelberg.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class EinsatzberichtTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex("id=\"Formular_Version\" name=\"Formular.Version\" value=\"(\\d+)\"")]
    private static partial Regex VersionFeld();

    private static object Alarm(string externeId, long zeitpunkt, params string[] fahrzeuge) => new
    {
        type = "ALARM",
        timestamp = zeitpunkt,
        data = new
        {
            externalId = externeId,
            keyword = "B 2",
            keyword_description = "Brand Wohngebäude",
            message = new[] { "Rauch aus Dachstuhl" },
            location = new { coordinate = new[] { 9.5961, 48.7647 }, street = "Hauptstraße", house = "12", postalCode = "73099", city = "Adelberg" },
            vehicles = fahrzeuge.Select(f => new { radioName = f }).ToArray(),
        },
    };

    private HttpClient Webhook()
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(Fe2Endpunkte.TokenHeader, AnwendungFixture.Fe2Token);
        return client;
    }

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private async Task<Einsatz> EinsatzZuAlarmAsync(string externeId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = await db.AlarmImporte.SingleAsync(a => a.ExterneId == externeId);
        Assert.True(import.Verarbeitet);
        return await db.Einsaetze.Include(e => e.Fahrzeuge).ThenInclude(f => f.Fahrzeug).SingleAsync(e => e.Id == import.EinsatzId);
    }

    [Fact]
    public async Task Fe2_Alarm_erzeugt_Entwurf_mit_Einsatznummer_und_Fahrzeug()
    {
        // 2031, damit andere Tests die laufende Nummer nicht beeinflussen.
        var ersterAlarm = new DateTimeOffset(2031, 3, 1, 17, 45, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var erste = "ILS-" + Guid.NewGuid().ToString("N")[..8];
        var zweite = "ILS-" + Guid.NewGuid().ToString("N")[..8];

        var client = Webhook();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/alarm/fe2", Alarm(erste, ersterAlarm, "Florian Adelberg 43", "Florian Unbekannt 1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/alarm/fe2", Alarm(zweite, ersterAlarm + 3_600_000))).StatusCode);

        var einsatz = await EinsatzZuAlarmAsync(erste);
        Assert.Equal("2031-001", einsatz.Einsatznummer);
        Assert.Equal(EinsatzStatus.Entwurf, einsatz.Status);
        Assert.Equal(erste, einsatz.LeitstellenNummer);
        Assert.Equal("B 2", einsatz.Stichwort);
        Assert.Equal("Brand Wohngebäude", einsatz.StichwortBeschreibung);
        Assert.Equal("Rauch aus Dachstuhl", einsatz.Meldung);
        Assert.Equal("Hauptstraße", einsatz.Strasse);
        Assert.Equal("Adelberg", einsatz.Ort);
        Assert.Equal(48.7647, einsatz.Latitude);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(ersterAlarm), einsatz.Alarmiert);
        Assert.Equal(["Florian Adelberg 43"], einsatz.Fahrzeuge.Select(f => f.Fahrzeug!.Funkrufname));

        Assert.Equal("2031-002", (await EinsatzZuAlarmAsync(zweite)).Einsatznummer);
    }

    [Fact]
    public async Task Statusmeldungen_werden_nicht_gespeichert()
    {
        var externeId = "ST-" + Guid.NewGuid().ToString("N")[..8];
        var antwort = await Webhook().PostAsJsonAsync("/api/alarm/fe2", new { type = "STATUS", data = new { externalId = externeId } });

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AlarmImporte.AnyAsync(a => a.ExterneId == externeId));
    }

    [Fact]
    public async Task Alarm_zu_vorhandenem_Bericht_wird_nur_verknuepft()
    {
        var externeId = "ILS-" + Guid.NewGuid().ToString("N")[..8];
        Guid vorhandenId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var berichte = scope.ServiceProvider.GetRequiredService<Einsatzberichte>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vorhanden = await berichte.NeuAnlegenAsync(new DateTimeOffset(2032, 5, 1, 10, 0, 0, TimeSpan.Zero));
            vorhanden.LeitstellenNummer = externeId;
            await db.SaveChangesAsync();
            vorhandenId = vorhanden.Id;
        }

        await Webhook().PostAsJsonAsync("/api/alarm/fe2", Alarm(externeId, new DateTimeOffset(2032, 5, 1, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()));

        Assert.Equal(vorhandenId, (await EinsatzZuAlarmAsync(externeId)).Id);
    }

    [Fact]
    public async Task Bericht_bearbeiten_freigeben_und_zuruecknehmen()
    {
        Guid einsatzId, mitgliedId, kategorieId, fahrzeugId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = new Mitglied { Personalnummer = "E-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Erika", Nachname = "Einsatz", Geburtsdatum = new(1990, 1, 1) };
            db.Mitglieder.Add(mitglied);
            await db.SaveChangesAsync();
            mitgliedId = mitglied.Id;
            kategorieId = await db.StatistikKategorien.Where(k => k.Code == "BR").Select(k => k.Id).SingleAsync();
            fahrzeugId = await db.Fahrzeuge.Where(f => f.Funkrufname == "Florian Adelberg 43").Select(f => f.Id).SingleAsync();
            einsatzId = (await scope.ServiceProvider.GetRequiredService<Einsatzberichte>()
                .NeuAnlegenAsync(new DateTimeOffset(2033, 7, 1, 21, 30, 0, TimeSpan.Zero))).Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var pfad = $"/Einsaetze/Bericht/{einsatzId}";

        // Lesen darf die Gemeinde, ändern nicht.
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        var nurLesen = await gemeinde.GetStringAsync(pfad);
        Assert.Contains("<fieldset disabled=\"disabled\"", nurLesen);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(gemeinde, pfad, null, Felder(Version(nurLesen), Token(nurLesen)))).StatusCode);

        // Führungskraft speichert, darf aber nicht freigeben.
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);
        var seite = await fuehrung.GetStringAsync(pfad);
        Assert.Contains("Statistik-Kategorie fehlt.", seite);
        Assert.DoesNotContain("Speichern und freigeben", seite);
        var felder = Felder(Version(seite), Token(seite));
        felder["Formular.StatistikKategorieId"] = kategorieId.ToString();
        felder["Formular.Einsatzende"] = "01:15";
        felder["Formular.Teilnehmer[0].MitgliedId"] = mitgliedId.ToString();
        felder["Formular.Teilnehmer[0].Dabei"] = "true";
        felder["Formular.Teilnehmer[0].FahrzeugId"] = fahrzeugId.ToString();
        felder["Formular.Teilnehmer[0].Atemschutz"] = "true";
        felder["Formular.Teilnehmer[0].AtemschutzMinuten"] = "25";
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(fuehrung, pfad, "Freigeben", felder)).StatusCode);
        var gespeichert = await PostAsync(fuehrung, pfad, null, felder);
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Einsaetze.Include(e => e.Teilnahmen).Include(e => e.Fahrzeuge).SingleAsync(e => e.Id == einsatzId);
            Assert.Equal(EinsatzStatus.InBearbeitung, einsatz.Status);
            Assert.Equal(new DateTimeOffset(2033, 7, 1, 23, 15, 0, TimeSpan.Zero), einsatz.Einsatzende);
            var teilnahme = Assert.Single(einsatz.Teilnahmen);
            Assert.Equal(25, teilnahme.AtemschutzMinuten);
            Assert.Equal(einsatz.Fahrzeuge.Single(f => f.FahrzeugId == fahrzeugId).Id, teilnahme.EinsatzFahrzeugId);
        }

        // Veraltete Version wird erkannt.
        var konflikt = await PostAsync(fuehrung, pfad, null, felder);
        Assert.Equal(HttpStatusCode.OK, konflikt.StatusCode);
        var konfliktText = WebUtility.HtmlDecode(await konflikt.Content.ReadAsStringAsync());
        Assert.Contains("inzwischen von jemand anderem geändert", konfliktText);

        // Kommandant gibt frei.
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        seite = await kommandant.GetStringAsync(pfad);
        felder["Formular.Version"] = Version(seite).ToString();
        felder["__RequestVerificationToken"] = Token(seite);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad, "Freigeben", felder)).StatusCode);

        // Nach der Freigabe kann die Führungskraft nichts mehr ändern.
        seite = await fuehrung.GetStringAsync(pfad);
        Assert.Contains("Nur noch lesbar.", seite);
        felder["Formular.Version"] = Version(seite).ToString();
        felder["__RequestVerificationToken"] = Token(seite);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(fuehrung, pfad, null, felder)).StatusCode);

        seite = await kommandant.GetStringAsync(pfad);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad, "Zuruecknehmen", new()
        {
            ["Formular.Version"] = Version(seite).ToString(),
            ["__RequestVerificationToken"] = Token(seite),
        })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatzId);
            Assert.Equal(EinsatzStatus.InBearbeitung, einsatz.Status);
            Assert.Null(einsatz.FreigegebenAm);
            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == einsatzId).Select(a => a.Aenderungen).ToListAsync();
            // Angelegt, gespeichert, freigegeben, zurückgenommen: jeder Schritt steht im Protokoll.
            Assert.Equal(4, protokoll.Count);
        }
    }

    [Fact]
    public async Task Neuer_Bericht_von_Hand()
    {
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Fuehrungskraft);
        var seite = await client.GetStringAsync("/Einsaetze/Neu");

        var antwort = await PostAsync(client, "/Einsaetze/Neu", null, new()
        {
            ["Alarmiert"] = "05.01.2034 06:40",
            ["__RequestVerificationToken"] = Token(seite),
        });

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var einsatz = await db.Einsaetze.SingleAsync(e => e.Alarmiert == new DateTimeOffset(2034, 1, 5, 5, 40, 0, TimeSpan.Zero));
        Assert.Equal("2034-001", einsatz.Einsatznummer);
        Assert.EndsWith(einsatz.Id.ToString(), antwort.Headers.Location!.ToString());
    }

    private static Dictionary<string, string> Felder(uint version, string token) => new()
    {
        ["__RequestVerificationToken"] = token,
        ["Formular.Version"] = version.ToString(),
        ["Formular.Alarmiert"] = "01.07.2033 23:30",
        ["Formular.Stichwort"] = "B 2",
        ["Formular.Rechtsgrundlage"] = nameof(Rechtsgrundlage.Para2Abs1),
        ["Formular.Lagebericht"] = "Rauch aus Dachstuhl",
    };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string pfad, string? handler, Dictionary<string, string> felder) =>
        client.PostAsync(handler is null ? pfad : $"{pfad}?handler={handler}", new FormUrlEncodedContent(felder));

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static uint Version(string html) => uint.Parse(VersionFeld().Match(html).Groups[1].Value);
}
