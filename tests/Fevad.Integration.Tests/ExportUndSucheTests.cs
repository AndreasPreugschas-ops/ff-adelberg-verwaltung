using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Gesamtexport und Suche über alle Bereiche (V70).</summary>
[Collection(AnwendungCollection.Name)]
public partial class ExportUndSucheTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, Guid.NewGuid().ToString());
        return client;
    }

    [Fact]
    public async Task Gesamtexport_enthaelt_Tabellen_ohne_Passwoerter_und_wird_protokolliert()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Kommandant).GetAsync("/Verwaltung/Export")).StatusCode);
        var admin = Client(Rollen.Administrator);
        var html = await admin.GetStringAsync("/Verwaltung/Export");
        var token = WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
        var antwort = await admin.PostAsync("/Verwaltung/Export", new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("application/zip", antwort.Content.Headers.ContentType?.MediaType);

        using var zip = new ZipArchive(new MemoryStream(await antwort.Content.ReadAsByteArrayAsync()));
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("LIESMICH.txt"));
        Assert.NotNull(zip.GetEntry("tabellen/mitglieder.json"));
        Assert.Null(zip.GetEntry("tabellen/data_protection_keys.json"));
        var konten = zip.GetEntry("tabellen/AspNetUsers.json")!;
        using (var leser = new StreamReader(konten.Open()))
        {
            var text = await leser.ReadToEndAsync();
            Assert.Contains("\"email\"", text);
            Assert.DoesNotContain("password_hash", text);
            Assert.DoesNotContain("security_stamp", text);
            Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(text).RootElement.ValueKind);
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEintraege.AnyAsync(a => a.Tabelle == Gesamtexport.Protokolltabelle));
    }

    [Fact]
    public async Task Suche_zeigt_nur_Bereiche_mit_Recht()
    {
        var kennung = "Zq" + Guid.NewGuid().ToString("N")[..6];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Mitglieder.Add(new Mitglied { Personalnummer = "Q" + Guid.NewGuid().ToString("N")[..8], Vorname = "Suse", Nachname = kennung, Geburtsdatum = new DateOnly(1980, 1, 1) });
            db.Fahrzeuge.Add(new Fahrzeug { Funkrufname = "Florian " + kennung, Typ = "MTW" });
            await db.SaveChangesAsync();
        }

        var kommandant = WebUtility.HtmlDecode(await Client(Rollen.Kommandant).GetStringAsync($"/Suche?q={kennung}"));
        Assert.Contains($"{kennung}, Suse", kommandant);
        Assert.Contains($"Florian {kennung}", kommandant);

        var kleiderwart = WebUtility.HtmlDecode(await Client(Rollen.Kleiderwart).GetStringAsync($"/Suche?q={kennung}"));
        Assert.DoesNotContain($"Florian {kennung}", kleiderwart);
        Assert.DoesNotContain($"{kennung}, Suse", kleiderwart);
        Assert.Contains("Keine Treffer", kleiderwart);
    }
}
