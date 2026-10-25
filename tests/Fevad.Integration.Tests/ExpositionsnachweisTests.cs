using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class ExpositionsnachweisTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? factory;

    private HttpClient Client(string rolle)
    {
        var client = (factory ??= TestAnmeldung.Factory(fixture)).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Atemschutznachweis_mit_Geraet_Maske_und_Gefahrstoff_als_Expositionsnachweis()
    {
        var mitglied = new Mitglied { Personalnummer = "X-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Paula", Nachname = "Pressluft", Geburtsdatum = new(1990, 3, 3) };
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Mitglieder.Add(mitglied);
            db.AtemschutzNachweise.Add(new AtemschutzNachweis { MitgliedId = mitglied.Id, Datum = new(2026, 2, 1), Art = AtemschutzNachweisArt.Belastungsuebung, Minuten = 20 });
            await db.SaveChangesAsync();
        }

        var pfad = $"/Personal/Akte/{mitglied.Id}";
        var wart = Client(Rollen.Atemschutzgeraetewart);
        var token = TokenFeld().Match(await wart.GetStringAsync(pfad)).Groups[1].Value;
        var antwort = await wart.PostAsync(pfad + "?handler=Atemschutz", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Art"] = nameof(AtemschutzNachweisArt.Einsatz),
            ["Datum"] = "2026-03-14",
            ["Minuten"] = "25",
            ["PaGeraet"] = "PA-07",
            ["Maske"] = "M-12",
            ["Exposition"] = "Brandrauch",
            ["Ort"] = "Hauptstraße",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("PA PA-07 · Maske M-12", WebUtility.HtmlDecode(await wart.GetStringAsync(pfad)));

        // CSV: nur der Einsatz, nicht die Belastungsübung; Dateiname ohne Namen.
        var csv = await wart.GetAsync(pfad + "?handler=Exposition&format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal($"Expositionsnachweis-{mitglied.Personalnummer}.csv", csv.Content.Headers.ContentDisposition?.FileName);
        var zeilen = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()).TrimEnd().Split("\r\n");
        Assert.Equal(2, zeilen.Length);
        Assert.Equal($"{mitglied.Personalnummer};14.03.2026;Einsatz;25;PA-07;M-12;;Brandrauch;Hauptstraße;", zeilen[1]);

        var pdf = await wart.GetAsync(pfad + "?handler=Exposition");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Pressluft", pdf.Content.Headers.ContentDisposition?.FileName);

        Assert.NotEqual(HttpStatusCode.OK, (await Client(Rollen.Fuehrungskraft).GetAsync(pfad + "?handler=Exposition")).StatusCode);
    }
}
