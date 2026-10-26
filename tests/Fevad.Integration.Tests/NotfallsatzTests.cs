using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Notbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Papier-Notfallsatz für den Ausfall von Internet oder Server (R18).</summary>
[Collection(AnwendungCollection.Name)]
public partial class NotfallsatzTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _factory = TestAnmeldung.Factory(fixture);

    private HttpClient Angemeldet(string rolle)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Druck_liefert_PDF_und_setzt_die_Erinnerung_zurueck()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            var m = new Mitglied
            {
                Personalnummer = "NS" + Guid.NewGuid().ToString("N")[..8], Vorname = "Nora", Nachname = "Notfall", Geburtsdatum = Heute.AddYears(-30),
                TelefonMobil = "0171 1234567", NotfallkontaktName = "Ben", NotfallkontaktTelefon = "0171 7654321",
            };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-3) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Mitglied).GetAsync("/Verwaltung/Notfallsatz")).StatusCode);

        var kommandant = Angemeldet(Rollen.Kommandant);
        var html = await kommandant.GetStringAsync("/Verwaltung/Notfallsatz");
        Assert.Contains("Papier-Notfallsatz", WebUtility.HtmlDecode(html));
        var token = WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
        var antwort = await kommandant.PostAsync("/Verwaltung/Notfallsatz?handler=Pdf",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("application/pdf", antwort.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await antwort.Content.ReadAsByteArrayAsync())[..4]));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var frist = scope.ServiceProvider.GetRequiredService<Notfallsatzfrist>();
            Assert.Empty(await frist.FaelligeAsync(Heute, CancellationToken.None));
            // Nach Ablauf der Frist (Parameter, 31 Tage) erinnert „Was liegt an“ wieder.
            var spaeter = Assert.Single(await frist.FaelligeAsync(Heute.AddDays(40), CancellationToken.None));
            Assert.Equal(Fristampel.Rot, spaeter.Ampel);
            Assert.Equal(Berechtigungen.Notfallsatz, spaeter.Empfaenger);
        }

        Assert.Contains("Zuletzt gedruckt am", WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Verwaltung/Notfallsatz")));
    }
}
