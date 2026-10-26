using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Dienstpflicht, Fachberater und Dienstgrade nach VwV (P5: V57, F21).</summary>
[Collection(AnwendungCollection.Name)]
public partial class DienstpflichtTests(AnwendungFixture fixture)
{
    private readonly WebApplicationFactory<Program> factory = TestAnmeldung.Factory(fixture);

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private HttpClient Client(string rolle)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Vorgang_und_Fachberater_nur_fuer_das_Kommando()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            var m = new Mitglied { Personalnummer = "DP-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Udo", Nachname = "Pflicht", Geburtsdatum = new(1980, 1, 1) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            id = m.Id;
        }

        var kdt = Client(Rollen.Kommandant);
        var seite = await kdt.GetStringAsync($"/Mitglieder/Dienstpflicht/{id}");
        var token = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        var antwort = await kdt.PostAsync($"/Mitglieder/Dienstpflicht/{id}?handler=Vorgang", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Formular.Art"] = nameof(DienstpflichtArt.Beurlaubung), ["Formular.Datum"] = heute.ToString("dd.MM.yyyy"),
            ["Formular.Von"] = heute.ToString("dd.MM.yyyy"), ["Formular.Bis"] = heute.AddMonths(3).ToString("dd.MM.yyyy"), ["Formular.Sachverhalt"] = "beruflich im Ausland",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        antwort = await kdt.PostAsync($"/Mitglieder/Dienstpflicht/{id}?handler=Fachberater", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Fachberater"] = "true", ["Fachgebiet"] = "Chemie",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        seite = WebUtility.HtmlDecode(await kdt.GetStringAsync($"/Mitglieder/Dienstpflicht/{id}"));
        Assert.Contains("beruflich im Ausland", seite);
        Assert.Contains("läuft", seite);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Mitglieder.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.True(m.Fachberater);
            Assert.Equal("Chemie", m.Fachgebiet);
            Assert.Equal("Oberfeuerwehrfrau/-mann", await db.Dienstgrade.Where(d => d.Code == "OFM").Select(d => d.Bezeichnung).SingleAsync());
            Assert.True(await db.Dokumentkategorien.AnyAsync(k => k.Code == "AAO"));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Schriftfuehrer).GetAsync($"/Mitglieder/Dienstpflicht/{id}")).StatusCode);
    }
}
