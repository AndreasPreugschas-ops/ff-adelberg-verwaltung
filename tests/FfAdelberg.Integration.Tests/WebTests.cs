using System.Net;
using System.Net.Http.Json;
using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FfAdelberg.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class WebTests(AnwendungFixture fixture)
{
    private HttpClient Client() =>
        fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/")]
    [InlineData("/Mitglieder")]
    [InlineData("/Dienste")]
    [InlineData("/Einsaetze")]
    public async Task Ohne_Anmeldung_geht_es_zur_Anmeldeseite(string pfad)
    {
        var antwort = await Client().GetAsync(pfad);

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("/Konto/Anmelden", antwort.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Anmeldeseite_und_Gesundheitspruefung_sind_offen()
    {
        var client = Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Konto/Anmelden")).StatusCode);
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));
    }

    [Fact]
    public async Task Fe2_Webhook_ohne_Token_wird_abgelehnt()
    {
        var antwort = await Client().PostAsJsonAsync("/api/alarm/fe2", new { externalId = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, antwort.StatusCode);
    }

    [Fact]
    public async Task Fe2_Webhook_speichert_Alarm_genau_einmal()
    {
        var externeId = "ILS-" + Guid.NewGuid().ToString("N")[..8];
        var alarm = new
        {
            type = "ALARM",
            timestamp = 1792691100000,
            data = new { externalId = externeId, keyword = "TH 1", message = new[] { "Baum auf Straße" }, location = new { city = "Adelberg" } },
        };

        var client = Client();
        client.DefaultRequestHeaders.Add(Fe2Endpunkte.TokenHeader, AnwendungFixture.Fe2Token);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/alarm/fe2", alarm)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/alarm/fe2", alarm)).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = await db.AlarmImporte.SingleAsync(a => a.ExterneId == externeId);
        Assert.Equal(AlarmQuelle.Fe2Webhook, import.Quelle);
        Assert.Contains("Baum auf Straße", import.Rohdaten);
        Assert.Equal(DateOnly.FromDateTime(import.EmpfangenAm.UtcDateTime).AddMonths(6), import.LoeschenAb);
    }

    [Fact]
    public async Task Fe2_Webhook_ohne_externalId_ist_ungueltig()
    {
        var client = Client();
        client.DefaultRequestHeaders.Add(Fe2Endpunkte.TokenHeader, AnwendungFixture.Fe2Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/alarm/fe2", new { keyword = "B 1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/alarm/fe2", new StringContent("kein json"))).StatusCode);
    }
}
