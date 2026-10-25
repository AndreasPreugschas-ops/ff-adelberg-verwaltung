using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Änderungsantrag für die eigenen Daten (V36): Mitglied beantragt, Personalverwaltung übernimmt oder lehnt ab.</summary>
[Collection(AnwendungCollection.Name)]
public partial class AenderungsantragTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(params string[] rollen)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, Dictionary<string, string> felder, string? ziel = null)
    {
        var seite = await client.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        return await client.PostAsync(ziel ?? pfad, new FormUrlEncodedContent(felder));
    }

    private async Task<(Guid MitgliedId, HttpClient Client)> MitgliedMitKontoAsync(string nachname)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied
        {
            Personalnummer = "A" + Guid.NewGuid().ToString("N")[..8], Vorname = "Ali", Nachname = nachname, Geburtsdatum = Heute.AddYears(-30),
            Strasse = "Alte Gasse 1", Plz = "73099", Ort = "Adelberg", TelefonMobil = "+491701111111", Arbeitgeber = "Bäckerei Alt", Arbeitsort = "Schorndorf",
        };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();

        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"a-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        var client = Client(Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, konto.Id.ToString());
        return (m.Id, client);
    }

    private async Task<Aenderungsantrag> AntragAsync(Guid mitgliedId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Aenderungsantraege.AsNoTracking().Include(a => a.Felder)
            .SingleAsync(a => a.MitgliedId == mitgliedId);
    }

    [Fact]
    public async Task Mitglied_beantragt_Umzug_Kommandant_uebernimmt_mit_Anschriftenverlauf()
    {
        var name = "Umzug" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);

        var seite = await mitglied.GetStringAsync("/Konto/Aenderung");
        Assert.Contains("value=\"Alte Gasse 1\"", seite);

        // Unverändert abgeschickt: kein Antrag.
        var nichts = await AbschickenAsync(mitglied, "/Konto/Aenderung", new() { ["Werte[Strasse]"] = "Alte Gasse 1 ", ["Werte[Ort]"] = "Adelberg" });
        Assert.Equal(HttpStatusCode.OK, nichts.StatusCode);
        Assert.Contains("nichts geändert", WebUtility.HtmlDecode(await nichts.Content.ReadAsStringAsync()));

        var antwort = await AbschickenAsync(mitglied, "/Konto/Aenderung", new()
        {
            ["Werte[Strasse]"] = "Neue Straße 5", ["Werte[Plz]"] = "73099", ["Werte[Ort]"] = "Adelberg", ["Werte[TelefonMobil]"] = "",
            ["Werte[NotfallkontaktName]"] = "Anna Muster (Ehefrau)", ["Werte[NotfallkontaktTelefon]"] = "+491702222222",
            ["Werte[Arbeitgeber]"] = "Bäckerei Alt", ["Bemerkung"] = "ab sofort",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var antrag = await AntragAsync(mitgliedId);
        Assert.Equal(AenderungsantragStatus.Offen, antrag.Status);
        Assert.Equal(
            [AenderungFeld.Strasse, AenderungFeld.TelefonMobil, AenderungFeld.NotfallkontaktName, AenderungFeld.NotfallkontaktTelefon],
            antrag.Felder.OrderBy(f => f.Feld).Select(f => f.Feld));
        Assert.Contains("Dein offener Antrag", await mitglied.GetStringAsync("/Konto/Aenderung"));

        // Die Stammdaten bleiben bis zur Übernahme unverändert.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Alte Gasse 1", (await db.Mitglieder.SingleAsync(m => m.Id == mitgliedId)).Strasse);
        }

        var kommandant = Client(Rollen.Kommandant);
        Assert.Contains(name, await kommandant.GetStringAsync("/Mitglieder/Aenderungen"));
        Assert.Contains("Änderungsanträge von Mitgliedern", WebUtility.HtmlDecode(await kommandant.GetStringAsync("/?handler=Anliegen")));
        var uebernommen = await AbschickenAsync(kommandant, "/Mitglieder/Aenderungen", [], $"/Mitglieder/Aenderungen?handler=Uebernehmen&antragId={antrag.Id}");
        Assert.Equal(HttpStatusCode.Redirect, uebernommen.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Mitglieder.SingleAsync(x => x.Id == mitgliedId);
            Assert.Equal("Neue Straße 5", m.Strasse);
            Assert.Null(m.TelefonMobil);
            Assert.Equal("Anna Muster (Ehefrau)", m.NotfallkontaktName);
            Assert.Equal("Bäckerei Alt", m.Arbeitgeber);
            var verlauf = await db.FruehereAnschriften.SingleAsync(f => f.MitgliedId == mitgliedId);
            Assert.Equal((AnschriftArt.Privat, "Alte Gasse 1", Heute), (verlauf.Art, verlauf.Strasse, verlauf.BisDatum));
        }

        Assert.Equal(AenderungsantragStatus.Uebernommen, (await AntragAsync(mitgliedId)).Status);
        Assert.Contains("übernommen", WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Aenderung")));
    }

    [Fact]
    public async Task Ablehnung_braucht_Begruendung_und_Jugendwart_sieht_Einsatzabteilung_nicht()
    {
        var name = "Ablehn" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Konto/Aenderung", new() { ["Werte[Email]"] = "neu@beispiel.de" })).StatusCode);
        var antrag = await AntragAsync(mitgliedId);

        // Ohne Mitglieder.Bearbeiten für die Einsatzabteilung bleibt die Seite zu.
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync("/Mitglieder/Aenderungen")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Mitglieder/Aenderungen")).StatusCode);

        var kommandant = Client(Rollen.Kommandant);
        var ohneGrund = await AbschickenAsync(kommandant, "/Mitglieder/Aenderungen", new() { ["grund"] = " " },
            $"/Mitglieder/Aenderungen?handler=Ablehnen&antragId={antrag.Id}");
        Assert.Equal(HttpStatusCode.OK, ohneGrund.StatusCode);
        Assert.Contains("Bitte kurz begründen", WebUtility.HtmlDecode(await ohneGrund.Content.ReadAsStringAsync()));

        var abgelehnt = await AbschickenAsync(kommandant, "/Mitglieder/Aenderungen", new() { ["grund"] = "Bitte dienstliche Adresse nehmen" },
            $"/Mitglieder/Aenderungen?handler=Ablehnen&antragId={antrag.Id}");
        Assert.Equal(HttpStatusCode.Redirect, abgelehnt.StatusCode);
        Assert.Contains("Bitte dienstliche Adresse nehmen", await mitglied.GetStringAsync("/Konto/Aenderung"));
    }

    [Fact]
    public async Task Mitglied_zieht_zurueck_und_Loeschlauf_entfernt_alte_Antraege()
    {
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync("Zurueck" + Guid.NewGuid().ToString("N")[..6]);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Konto/Aenderung", new() { ["Werte[Ort]"] = "Schorndorf" })).StatusCode);
        var zurueck = await AbschickenAsync(mitglied, "/Konto/Aenderung", [], "/Konto/Aenderung?handler=Zurueckziehen");
        Assert.Equal(HttpStatusCode.Redirect, zurueck.StatusCode);
        var antrag = await AntragAsync(mitgliedId);
        Assert.Equal(AenderungsantragStatus.Zurueckgezogen, antrag.Status);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Aenderungsantraege.Where(a => a.Id == antrag.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.EntschiedenAm, DateTimeOffset.UtcNow.AddYears(-3)));
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelAenderungsantraege).Anzahl >= 1);
            Assert.False(await db.Aenderungsantraege.IgnoreQueryFilters().AnyAsync(a => a.Id == antrag.Id));
            Assert.False(await db.Aenderungsfelder.IgnoreQueryFilters().AnyAsync(f => f.AenderungsantragId == antrag.Id));
        }
    }
}
