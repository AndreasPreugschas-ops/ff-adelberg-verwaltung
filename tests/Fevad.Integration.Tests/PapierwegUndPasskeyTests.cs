using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Altersabteilung, Terminzettel auf Papier und Passkeys (V41).</summary>
[Collection(AnwendungCollection.Name)]
public partial class PapierwegUndPasskeyTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle, Guid? konto = null)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, (konto ?? Guid.NewGuid()).ToString());
        return client;
    }

    private async Task<(Guid MitgliedId, Guid KontoId)> AltesMitgliedAsync(string name)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Alters);
        var m = new Mitglied { Personalnummer = "P" + Guid.NewGuid().ToString("N")[..8], Vorname = "Hans", Nachname = name, Geburtsdatum = Heute.AddYears(-72) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
        db.Mitglieder.Add(m);
        db.Dienste.Add(new Dienst
        {
            Art = DienstArt.Sonderdienst, Titel = "Stammtisch " + name, Ort = "Gerätehaus", Abteilungen = [abteilung],
            Beginn = DateTimeOffset.UtcNow.AddDays(10), Ende = DateTimeOffset.UtcNow.AddDays(10).AddHours(2),
        });
        await db.SaveChangesAsync();
        var email = $"p-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var k = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(k)).Succeeded);
        return (m.Id, k.Id);
    }

    [Fact]
    public async Task Altersabteilung_sieht_Hinweis_statt_Fristen()
    {
        var (_, konto) = await AltesMitgliedAsync("Alt" + Guid.NewGuid().ToString("N")[..6]);
        var start = WebUtility.HtmlDecode(await Client(Rollen.Mitglied, konto).GetStringAsync("/?handler=Anliegen"));
        Assert.Contains("Altersabteilung: keine Dienstpflicht und keine Fristen", start);
        Assert.DoesNotContain("Altersabteilung: keine", WebUtility.HtmlDecode(await Client(Rollen.Kommandant).GetStringAsync("/?handler=Anliegen")));
    }

    [Fact]
    public async Task Terminzettel_mit_den_Terminen_des_Mitglieds()
    {
        var name = "Zettel" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, _) = await AltesMitgliedAsync(name);
        var kommandant = Client(Rollen.Kommandant);
        var seite = WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Dienste/Terminzettel"));
        Assert.Contains($"{name}, Hans", seite);
        Assert.Contains("Terminzettel drucken", await kommandant.GetStringAsync("/Dienste/Index"));
        var pdf = await kommandant.GetAsync($"/Dienste/Terminzettel?handler=Pdf&mitgliedId={mitgliedId}&monate=3");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync("/Dienste/Terminzettel")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync($"/Dienste/Terminzettel?handler=Pdf&mitgliedId={mitgliedId}&monate=3")).StatusCode);
    }

    [Fact]
    public async Task Passkey_Optionen_fuer_Einrichtung_und_Anmeldung()
    {
        var (_, konto) = await AltesMitgliedAsync("Pk" + Guid.NewGuid().ToString("N")[..6]);
        var selbst = Client(Rollen.Mitglied, konto);
        var html = await selbst.GetStringAsync("/Konto/Passkeys");
        Assert.Contains("Noch kein Passkey eingerichtet", WebUtility.HtmlDecode(html));
        var anfrage = new HttpRequestMessage(HttpMethod.Post, "/Konto/Passkeys?handler=Optionen");
        anfrage.Headers.Add("RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value));
        var antwort = await selbst.SendAsync(anfrage);
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        using (var json = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync()))
        {
            Assert.Equal("required", json.RootElement.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString());
            Assert.True(json.RootElement.TryGetProperty("challenge", out _));
        }

        // Unvollständige Antwort des Browsers wird abgelehnt, nichts gespeichert.
        var formular = new FormUrlEncodedContent([new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)), new("credentialJson", "{}")]);
        Assert.Equal(HttpStatusCode.Redirect, (await selbst.PostAsync("/Konto/Passkeys", formular)).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        Assert.Empty(await benutzer.GetPasskeysAsync((await benutzer.FindByIdAsync(konto.ToString()))!));

        var anonym = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anmelden = await anonym.GetStringAsync("/Konto/Anmelden");
        Assert.Contains("Mit Passkey anmelden", anmelden);
        var optionen = new HttpRequestMessage(HttpMethod.Post, "/Konto/Anmelden?handler=PasskeyOptionen");
        optionen.Headers.Add("RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(anmelden).Groups[1].Value));
        foreach (var cookie in (await anonym.GetAsync("/Konto/Anmelden")).Headers.TryGetValues("Set-Cookie", out var c) ? c : [])
        {
            optionen.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        Assert.Equal(HttpStatusCode.OK, (await anonym.SendAsync(optionen)).StatusCode);
    }
}
