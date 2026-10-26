using System.IO.Compression;
using System.Net;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Briefvorlagen, Serienbriefe und Quartalsbericht (P5: V56).</summary>
[Collection(AnwendungCollection.Name)]
public class SerienbriefTests(AnwendungFixture fixture)
{
    private readonly WebApplicationFactory<Program> factory = TestAnmeldung.Factory(fixture);

    private HttpClient Client(string rolle)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Serienbrief_fuellt_Platzhalter_und_schirmt_die_Jugend_ab()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var nachname = "Brief" + Guid.NewGuid().ToString("N")[..6];
        var elternname = "Eltern" + Guid.NewGuid().ToString("N")[..6];
        Guid dank, eltern, einsatzId, jugendId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dank = await db.Briefvorlagen.Where(v => v.Code == "DANK").Select(v => v.Id).SingleAsync();
            eltern = await db.Briefvorlagen.Where(v => v.Code == "ELTERN").Select(v => v.Id).SingleAsync();
            einsatzId = await db.Abteilungen.Where(a => a.Code == AbteilungCode.Einsatz).Select(a => a.Id).SingleAsync();
            jugendId = await db.Abteilungen.Where(a => a.Code == AbteilungCode.Jugend).Select(a => a.Id).SingleAsync();
            var m = new Mitglied
            {
                Personalnummer = "SB-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Eva", Nachname = nachname, Geschlecht = Geschlecht.Weiblich,
                Geburtsdatum = new(1990, 5, 5), Strasse = "Hauptstraße 1", Plz = "73099", Ort = "Adelberg",
            };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatzId, Status = MitgliedStatus.Aktiv, GueltigAb = new(2015, 1, 1) });
            var kind = new Mitglied { Personalnummer = "SB-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Tim", Nachname = elternname, Geburtsdatum = heute.AddYears(-12) };
            kind.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jugendId, Status = MitgliedStatus.Aktiv, GueltigAb = heute.AddYears(-1) });
            kind.Erziehungsberechtigte.Add(new Erziehungsberechtigter { Nummer = 1, Verhaeltnis = Sorgeverhaeltnis.Vater, Vorname = "Paul", Nachname = elternname });
            db.Mitglieder.AddRange(m, kind);
            await db.SaveChangesAsync();
        }

        var schrift = Client(Rollen.Schriftfuehrer);
        Assert.Contains("Dankschreiben", WebUtility.HtmlDecode(await schrift.GetStringAsync("/Vorlagen/Index")));
        var seite = WebUtility.HtmlDecode(await schrift.GetStringAsync($"/Vorlagen/Serienbrief/{dank}?abteilungId={einsatzId}"));
        Assert.Contains($"{nachname}, Eva", seite);

        var docx = await schrift.GetAsync($"/Vorlagen/Serienbrief/{dank}?handler=Docx&abteilungId={einsatzId}&datum={heute:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, docx.StatusCode);
        using (var zip = new ZipArchive(await docx.Content.ReadAsStreamAsync()))
        {
            using var leser = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var xml = await leser.ReadToEndAsync();
            Assert.Contains($"Sehr geehrte Frau {nachname},", xml);
            Assert.Contains("73099 Adelberg", xml);
            Assert.Contains($"im Jahr {heute.Year}", xml);
        }

        var pdf = await schrift.GetAsync($"/Vorlagen/Serienbrief/{dank}?handler=Pdf&abteilungId={einsatzId}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        // Ohne Zugang zur Jugendfeuerwehr keine Eltern, der Jugendwart sieht sie.
        Assert.DoesNotContain(elternname, WebUtility.HtmlDecode(await schrift.GetStringAsync($"/Vorlagen/Serienbrief/{eltern}")));
        Assert.Contains($"{elternname}, Paul", WebUtility.HtmlDecode(await Client(Rollen.Jugendwart).GetStringAsync($"/Vorlagen/Serienbrief/{eltern}?abteilungId={jugendId}")));

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync("/Vorlagen/Bearbeiten")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync("/Vorlagen/Index")).StatusCode);
    }

    [Fact]
    public async Task Quartalsbericht_als_PDF()
    {
        var antwort = await Client(Rollen.Kommandant).GetAsync($"/Auswertungen/Jahresstatistik?handler=Quartal&jahr={DateTime.Today.Year}&quartal=1");
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("application/pdf", antwort.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client(Rollen.Kommandant).GetAsync("/Auswertungen/Jahresstatistik?handler=Quartal&quartal=5")).StatusCode);
    }
}
