using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class RechteTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.UtcNow);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, IEnumerable<KeyValuePair<string, string>> felder)
    {
        var seite = await client.GetStringAsync(pfad);
        var token = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        return await client.PostAsync(pfad, new FormUrlEncodedContent(felder.Append(new("__RequestVerificationToken", token))));
    }

    private static async Task<string> TextAsync(HttpResponseMessage antwort) => WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync());

    [Fact]
    public void Katalog_und_Vorbelegung_passen_zusammen()
    {
        Assert.Equal(Berechtigungen.Katalog.Count, Berechtigungen.Katalog.Select(r => r.Name).Distinct().Count());
        Assert.All(Berechtigungen.Vorbelegung.Keys, recht => Assert.Contains(Berechtigungen.Katalog, r => r.Name == recht));
        Assert.All(Berechtigungen.VorbelegungAbteilungen, v => Assert.True(Berechtigungen.Katalog.Single(r => r.Name == v.Recht).Abteilungsbezug));
        Assert.All(Berechtigungen.Abgeleitet.Values, a => Assert.Contains(Berechtigungen.Katalog, r => r.Name == a.Recht));
        Assert.All(Berechtigungen.Vorbelegung.Values.SelectMany(r => r), rolle => Assert.Contains(rolle, Rollen.Alle));
    }

    [Fact]
    public async Task Vorbelegung_steht_in_der_Datenbank()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eingefuehrt = await db.RechteEingefuehrt.Select(r => r.Recht).ToListAsync();
        Assert.All(Berechtigungen.Katalog, r => Assert.Contains(r.Name, eingefuehrt));
        var jugend = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Jugend);
        var jugendwart = await db.RollenRechte.SingleAsync(r => r.Rolle == Rollen.Jugendwart && r.Recht == Berechtigungen.MitgliederLesen);
        Assert.Equal(jugend.Id, Assert.Single(jugendwart.Abteilungen));
        Assert.Equal(Rollen.Alle.Length, await db.Rollenprofile.CountAsync(p => Rollen.Alle.Contains(p.Name)));
    }

    [Fact]
    public async Task Eigene_Rolle_sieht_nur_ihre_Abteilung()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var admin = Angemeldet(factory, Rollen.Administrator);
        var name = "Al" + Guid.NewGuid().ToString("N")[..6];
        Guid alters, einsatz;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            alters = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Alters)).Id;
            einsatz = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id;
            foreach (var (abteilung, nachname) in new[] { (alters, name + "Alt"), (einsatz, name + "Ein") })
            {
                var m = new Mitglied { Personalnummer = "R" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = nachname, Geburtsdatum = new(1950, 1, 1) };
                m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
                db.Mitglieder.Add(m);
            }

            await db.SaveChangesAsync();
        }

        var angelegt = await AbschickenAsync(admin, "/Verwaltung/Rechte", [new("Bezeichnung", "Leitung " + name), new("Beschreibung", "Test"), new("Vorlage", "")]);
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var rolle = "Leitung" + name;
        Assert.EndsWith($"name={rolle}", angelegt.Headers.Location!.ToString());

        var pfad = $"/Verwaltung/Rechte/Bearbeiten?name={rolle}";
        var gespeichert = await AbschickenAsync(admin, pfad,
        [
            new("Bezeichnung", "Leitung " + name), new("recht", Berechtigungen.MitgliederLesen),
            new($"umfang:{Berechtigungen.MitgliederLesen}", "auswahl"), new($"abt:{Berechtigungen.MitgliederLesen}", alters.ToString()),
        ]);
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);

        var leitung = Angemeldet(factory, rolle);
        var liste = WebUtility.HtmlDecode(await leitung.GetStringAsync($"/Mitglieder?Suche={name}"));
        Assert.Contains(name + "Alt", liste);
        Assert.DoesNotContain(name + "Ein", liste);
        Assert.Contains(name + "Alt", WebUtility.HtmlDecode(await leitung.GetStringAsync("/Abteilungen?code=EA")));
        Assert.Equal(HttpStatusCode.Forbidden, (await leitung.GetAsync("/Personal")).StatusCode);

        // Atemschutz ist Gesundheitsdaten: ohne Bestätigung wird nichts gespeichert.
        var ohne = await AbschickenAsync(admin, pfad, [new("Bezeichnung", "Leitung " + name), new("recht", Berechtigungen.AtemschutzLesen)]);
        Assert.Equal(HttpStatusCode.OK, ohne.StatusCode);
        Assert.Contains("bitte die Bestätigung anhaken", await TextAsync(ohne));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(Berechtigungen.MitgliederLesen, (await db.RollenRechte.SingleAsync(r => r.Rolle == rolle)).Recht);
        }

        var mit = await AbschickenAsync(admin, pfad,
            [new("Bezeichnung", "Leitung " + name), new("recht", Berechtigungen.AtemschutzLesen), new("GesundheitBestaetigt", "true")]);
        Assert.Equal(HttpStatusCode.Redirect, mit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leitung.GetAsync("/Mitglieder")).StatusCode);

        // Ohne Konten lässt sich die eigene Rolle löschen.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(admin, pfad.Replace("?", "?handler=Loeschen&"), [])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Roles.AnyAsync(r => r.Name == rolle));
            Assert.False(await db.RollenRechte.AnyAsync(r => r.Rolle == rolle));
        }
    }

    [Fact]
    public async Task Administrator_behaelt_die_Benutzerverwaltung()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var admin = Angemeldet(factory, Rollen.Administrator);
        List<RolleRecht> vorher;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            vorher = await db.RollenRechte.AsNoTracking().Where(r => r.Rolle == Rollen.Administrator).ToListAsync();
        }

        try
        {
            var antwort = await AbschickenAsync(admin, $"/Verwaltung/Rechte/Bearbeiten?name={Rollen.Administrator}", [new("Bezeichnung", "Administrator")]);
            Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Verwaltung/Rechte")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/Stammdaten/Organisation")).StatusCode);

            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nachher = await db.RollenRechte.Where(r => r.Rolle == Rollen.Administrator).Select(r => r.Recht).ToListAsync();
            Assert.Equal(Berechtigungen.Unentziehbar.Order(), nachher.Order());
        }
        finally
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jetzt = await db.RollenRechte.Where(r => r.Rolle == Rollen.Administrator).Select(r => r.Recht).ToListAsync();
            foreach (var r in vorher.Where(r => !jetzt.Contains(r.Recht)))
            {
                db.RollenRechte.Add(new RolleRecht { Rolle = r.Rolle, Recht = r.Recht, Abteilungen = r.Abteilungen });
            }

            await db.SaveChangesAsync();
        }
    }
}
