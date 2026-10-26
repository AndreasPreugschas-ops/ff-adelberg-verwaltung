using System.Net;
using System.Text;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Führungskennzahlen (P5: V52): nur Kommandant und Stellvertreter, CSV ohne Namen.</summary>
[Collection(AnwendungCollection.Name)]
public class FuehrungskennzahlenTests(AnwendungFixture fixture)
{
    private HttpClient Client(string rolle)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Kommandant_sieht_Kennzahlen_mit_Namen_die_CSV_kommt_ohne()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var nachname = "Kennzahl" + Guid.NewGuid().ToString("N")[..6];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            var m = new Mitglied { Personalnummer = "KZ-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Klara", Nachname = nachname, Geburtsdatum = new(1990, 3, 3) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(heute.Year, 1, 1) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
        }

        var kommandant = Client(Rollen.Kommandant);
        var seite = await kommandant.GetAsync("/Auswertungen/Fuehrung");
        Assert.Equal(HttpStatusCode.OK, seite.StatusCode);
        var html = await seite.Content.ReadAsStringAsync();
        Assert.Contains("Einsatzbereitschaft am", html);
        Assert.Contains("Atemschutzgeräteträger", html);
        Assert.Contains($"{nachname}, Klara", html);

        var csv = await kommandant.GetAsync($"/Auswertungen/Fuehrung?handler=Csv&jahr={heute.Year}");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Funktion;Gesamt;Einsatzfähig;Soll", text);
        Assert.DoesNotContain(nachname, text);

        // Ältere Jahre nur als Summen; nach der Verdichtung der Anwesenheiten (L5) gar keine Beteiligung je Person.
        Assert.Contains("nur die Summen, keine Namen", await kommandant.GetStringAsync($"/Auswertungen/Fuehrung?jahr={heute.Year - 3}"));
        Assert.Contains("zu Jahressummen verdichtet", await kommandant.GetStringAsync($"/Auswertungen/Fuehrung?jahr={heute.Year - 8}"));

        Assert.Contains("Führungskennzahlen", await Client(Rollen.Stellvertreter).GetStringAsync("/Auswertungen"));
        Assert.Equal(HttpStatusCode.BadRequest, (await kommandant.GetAsync($"/Auswertungen/Fuehrung?jahr={heute.Year + 1}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Kassier).GetAsync("/Auswertungen/Fuehrung")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Fuehrungskraft).GetAsync("/Auswertungen/Fuehrung?handler=Csv")).StatusCode);
    }
}
