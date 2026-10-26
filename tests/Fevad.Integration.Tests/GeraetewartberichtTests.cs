using System.Net;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Jahresbericht Gerätewart (V64): Kosten je Fahrzeug, Mängel und PDF; nur mit Leserecht Technik.</summary>
[Collection(AnwendungCollection.Name)]
public class GeraetewartberichtTests(AnwendungFixture fixture)
{
    [Fact]
    public async Task Bericht_mit_Kosten_je_Fahrzeug_und_PDF()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var jahr = Random.Shared.Next(2040, 2100);
        var fahrzeug = new Fahrzeug { Funkrufname = "Florian Test " + kennung, Typ = "MTW", Baujahr = jahr - 20, IndienstAm = new(jahr - 20, 3, 1), NutzungsdauerJahre = 20 };
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Fahrzeuge.Add(fahrzeug);
            db.Betankungen.Add(new Betankung { FahrzeugId = fahrzeug.Id, Datum = new(jahr, 5, 4), Liter = 60m, Betrag = 105.40m });
            db.Maengel.Add(new Mangel
            {
                FahrzeugId = fahrzeug.Id, Beschreibung = "Bremse " + kennung, GemeldetAm = new DateTimeOffset(jahr, 6, 1, 8, 0, 0, TimeSpan.Zero),
                BehobenAm = new DateTimeOffset(jahr, 6, 10, 8, 0, 0, TimeSpan.Zero), Status = MangelStatus.Behoben, Kosten = 380m,
            });
            await db.SaveChangesAsync();

            var bericht = await scope.ServiceProvider.GetRequiredService<Geraetewartberichte>().ErstellenAsync(jahr, new DateOnly(jahr + 1, 1, 15), CancellationToken.None);
            var kosten = Assert.Single(bericht.Kosten, k => k.Funkrufname == fahrzeug.Funkrufname);
            Assert.Equal((60m, 105.40m, 380m, 1, 485.40m), (kosten.Liter, kosten.Treibstoff, kosten.Reparaturen, kosten.Maengel, kosten.Summe));
            Assert.Contains(bericht.Ausblick, a => a.Gegenstand.StartsWith(fahrzeug.Funkrufname, StringComparison.Ordinal) && a.Jahr == jahr + 1);
            Assert.Equal(new DateOnly(jahr, 12, 31), bericht.Stichtag);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var seite = WebUtility.HtmlDecode(await geraetewart.GetStringAsync($"/Technik/Jahresbericht?jahr={jahr}"));
        Assert.Contains(fahrzeug.Funkrufname, seite);
        var pdf = await geraetewart.GetAsync($"/Technik/Jahresbericht?handler=Pdf&jahr={jahr}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var jugendwart = Angemeldet(factory, Rollen.Jugendwart);
        Assert.Equal(HttpStatusCode.Forbidden, (await jugendwart.GetAsync($"/Technik/Jahresbericht?jahr={jahr}")).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Maengel.Where(m => m.FahrzeugId == fahrzeug.Id).ExecuteDeleteAsync();
            await db.Betankungen.Where(b => b.FahrzeugId == fahrzeug.Id).ExecuteDeleteAsync();
            await db.Fahrzeuge.Where(f => f.Id == fahrzeug.Id).ExecuteDeleteAsync();
        }
    }

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }
}
