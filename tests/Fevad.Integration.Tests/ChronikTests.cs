using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Gremien;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Chronik, Jahresbericht als Word-Datei und Jubiläumsvorschau (P5: V56, F23, F25).</summary>
[Collection(AnwendungCollection.Name)]
public partial class ChronikTests(AnwendungFixture fixture)
{
    private readonly WebApplicationFactory<Program> factory = TestAnmeldung.Factory(fixture);

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private HttpClient Client(string rolle)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    [Fact]
    public async Task Nur_freigegebene_Eintraege_kommen_in_Jahresbericht_und_Textausgabe()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var schrift = Client(Rollen.Schriftfuehrer);
        var formular = await schrift.GetStringAsync("/Chronik/Eintrag");

        async Task AnlegenAsync(string titel, bool frei)
        {
            var felder = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(formular), ["Formular.Datum"] = heute.ToString("dd.MM.yyyy"), ["Formular.Titel"] = titel,
                ["Formular.Text"] = "Beschreibung " + titel,
            };
            if (frei)
            {
                felder["Formular.Veroeffentlichen"] = "true";
            }

            var antwort = await schrift.PostAsync("/Chronik/Eintrag", new FormUrlEncodedContent(felder));
            Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        }

        await AnlegenAsync($"Indienststellung {kennung}", frei: true);
        await AnlegenAsync($"Intern {kennung}", frei: false);

        var mitglied = Client(Rollen.Mitglied);
        var chronik = WebUtility.HtmlDecode(await mitglied.GetStringAsync($"/Chronik?jahr={heute.Year}"));
        Assert.Contains($"Indienststellung {kennung}", chronik);
        Assert.Contains($"Intern {kennung}", chronik);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Chronik/Eintrag")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Chronik/Jubilaeen")).StatusCode);

        var text = await mitglied.GetStringAsync($"/Chronik?handler=Text&jahr={heute.Year}");
        Assert.Contains($"Indienststellung {kennung}", text);
        Assert.DoesNotContain($"Intern {kennung}", text);

        var docx = await Client(Rollen.Kommandant).GetAsync($"/Auswertungen/Jahresstatistik?handler=Docx&jahr={heute.Year}");
        Assert.Equal(HttpStatusCode.OK, docx.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx.Content.Headers.ContentType?.MediaType);
        using var zip = new ZipArchive(await docx.Content.ReadAsStreamAsync());
        using var leser = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = await leser.ReadToEndAsync();
        Assert.Contains($"Jahresbericht {heute.Year}", xml);
        Assert.Contains("Bericht des Kommandanten", xml);
        Assert.Contains($"Indienststellung {kennung}", xml);
        Assert.DoesNotContain($"Intern {kennung}", xml);
    }

    [Fact]
    public async Task Jubilaeen_fuer_alle_mit_Personalverwaltung_im_eigenen_Datenbereich()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var nachname = "Jubel" + Guid.NewGuid().ToString("N")[..6];
        var jugendname = "Jung" + Guid.NewGuid().ToString("N")[..6];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            var m = new Mitglied { Personalnummer = "JU-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Jo", Nachname = nachname, Geburtsdatum = new(heute.Year - 60, 6, 15) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(heute.Year - 25, 4, 1) });
            db.Mitglieder.Add(m);
            // Nur in der Jugendfeuerwehr: kein runder Geburtstag in der Vorschau.
            var jugend = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Jugend);
            var j = new Mitglied { Personalnummer = "JU-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Jule", Nachname = jugendname, Geburtsdatum = new(heute.Year - 50, 6, 15) };
            j.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jugend.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(heute.Year - 3, 4, 1) });
            db.Mitglieder.Add(j);
            await db.SaveChangesAsync();
        }

        var seite = WebUtility.HtmlDecode(await Client(Rollen.Schriftfuehrer).GetStringAsync($"/Chronik/Jubilaeen?jahr={heute.Year}"));
        Assert.Contains($"{nachname}, Jo", seite);
        Assert.Contains("25 Jahre Mitglied", seite);
        Assert.Contains("60. Geburtstag", seite);
        Assert.DoesNotContain(jugendname, seite);
        Assert.Equal(HttpStatusCode.OK, (await Client(Rollen.Kommandant).GetAsync("/Chronik/Jubilaeen")).StatusCode);
        // Wer die Personalverwaltung sieht, sieht auch die Geburtstage; der Jugendwart nur seine Abteilung.
        Assert.Contains("60. Geburtstag", WebUtility.HtmlDecode(await Client(Rollen.Stellvertreter).GetStringAsync($"/Chronik/Jubilaeen?jahr={heute.Year}")));
        Assert.Contains($"{nachname}, Jo", WebUtility.HtmlDecode(await Client(Rollen.Kassier).GetStringAsync($"/Chronik/Jubilaeen?jahr={heute.Year}")));
        var jugendwart = WebUtility.HtmlDecode(await Client(Rollen.Jugendwart).GetStringAsync($"/Chronik/Jubilaeen?jahr={heute.Year}"));
        Assert.DoesNotContain(nachname, jugendwart);
    }
}
