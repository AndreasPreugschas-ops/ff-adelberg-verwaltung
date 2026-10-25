using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class BlaetternTests(AnwendungFixture fixture)
{
    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(params string[] rollen)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    [Fact]
    public void Seite_begrenzt_die_Nummer_und_zaehlt_ab_eins()
    {
        var zahlen = Enumerable.Range(1, 120).ToList();

        var zweite = zahlen.Seite(2);
        Assert.Equal(51, zweite.Eintraege[0]);
        Assert.Equal((51, 100, 3), (zweite.Erster, zweite.Letzter, zweite.Seiten));

        var zuWeit = zahlen.Seite(99);
        Assert.Equal((3, 101, 120), (zuWeit.Nummer, zuWeit.Erster, zuWeit.Letzter));
        Assert.Equal(1, zahlen.Seite(0).Nummer);

        var leer = new List<int>().Seite(1);
        Assert.Equal((1, 0, 0), (leer.Seiten, leer.Gesamt, leer.Erster));
    }

    [Fact]
    public async Task Geraeteliste_blaettert_und_behaelt_die_Filter()
    {
        var kennung = $"BLT{Guid.NewGuid():N}"[..10];
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.Where(g => !g.FuerFahrzeuge).Select(g => g.Id).FirstAsync();
            var material = new Material { Bezeichnung = $"Blättertest {kennung}", GeraeteartId = art };
            db.Materialien.Add(material);
            for (var i = 1; i <= Seitenwahl.Standardgroesse + 5; i++)
            {
                db.Geraete.Add(new Geraet { Inventarnummer = $"{kennung}-{i:000}", Bezeichnung = "Blättergerät", MaterialId = material.Id, GeraeteartId = art });
            }

            await db.SaveChangesAsync();
        }

        var client = Client(Rollen.Geraetewart);
        var erste = WebUtility.HtmlDecode(await client.GetStringAsync($"/Technik/Geraete?Suche={kennung}"));
        Assert.Contains($"{kennung}-001", erste);
        Assert.DoesNotContain($"{kennung}-055", erste);
        Assert.Contains("1–50 von 55", erste);
        Assert.Contains($"Suche={kennung}&Seite=2", erste);

        // Die Live-Suche liefert nur die Tabelle; die Seitenleiste zeigt trotzdem auf die volle Seite.
        var tabelle = await client.GetStringAsync($"/Technik/Geraete?handler=Tabelle&Suche={kennung}");
        Assert.DoesNotContain("handler=Tabelle", tabelle);

        var zweite = WebUtility.HtmlDecode(await client.GetStringAsync($"/Technik/Geraete?Suche={kennung}&Seite=2"));
        Assert.Contains($"{kennung}-055", zweite);
        Assert.DoesNotContain($"{kennung}-001<", zweite);
        Assert.Contains("51–55 von 55", zweite);
    }

    [Fact]
    public async Task Fristenlisten_laden_mit_Suche_und_Seite()
    {
        var client = Client(Rollen.Kommandant);
        foreach (var pfad in new[]
        {
            "/Personal?Suche=niemand-heisst-so&Alle=true&Seite=3",
            "/Kleiderkammer/Fristen?Suche=niemand-heisst-so&NurFaellige=true&Seite=2",
        })
        {
            var antwort = await client.GetAsync(pfad);
            Assert.True(antwort.IsSuccessStatusCode, $"{pfad}: {antwort.StatusCode}");
        }

        Assert.Contains("Keine passenden Fristen.", WebUtility.HtmlDecode(await client.GetStringAsync("/Personal?Suche=niemand-heisst-so&Alle=true")));
        Assert.Contains("Keine Einzelstücke gefunden.", WebUtility.HtmlDecode(await client.GetStringAsync("/Kleiderkammer/Fristen?Suche=niemand-heisst-so")));
    }
}
