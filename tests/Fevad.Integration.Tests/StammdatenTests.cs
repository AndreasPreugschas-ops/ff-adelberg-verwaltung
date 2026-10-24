using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class StammdatenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Angemeldet(params string[] rollen)
    {
        var client = TestAnmeldung.Factory(fixture).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, string handler, Dictionary<string, string> felder)
    {
        var seite = await client.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        var trenner = pfad.Contains('?') ? '&' : '?';
        return await client.PostAsync($"{pfad}{trenner}handler={handler}", new FormUrlEncodedContent(felder));
    }

    private static async Task<string> TextAsync(HttpResponseMessage antwort) => WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync());

    [Fact]
    public async Task Stammdaten_zeigen_nur_erlaubte_Bereiche()
    {
        var kommandant = WebUtility.HtmlDecode(await Angemeldet(Rollen.Kommandant).GetStringAsync("/Stammdaten"));
        Assert.Contains("Abteilungen und Funktionen", kommandant);
        Assert.Contains("Dienstgrade, Beförderung und Ehrungen", kommandant);
        Assert.Contains("href=\"/Stammdaten\"", kommandant);

        var geraetewart = WebUtility.HtmlDecode(await Angemeldet(Rollen.Geraetewart).GetStringAsync("/Stammdaten"));
        Assert.Contains("Gerätearten und Prüfintervalle", geraetewart);
        Assert.DoesNotContain("Abteilungen und Funktionen", geraetewart);

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Mitglied).GetAsync("/Stammdaten")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Geraetewart).GetAsync("/Stammdaten/Organisation")).StatusCode);
    }

    [Fact]
    public async Task Abteilungen_folgen_den_Regeln_der_Gliederung()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var name = "Abt" + Guid.NewGuid().ToString("N")[..6];
        Guid jugend, einsatz;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            jugend = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Jugend)).Id;
            einsatz = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id;
        }

        Dictionary<string, string> Felder(AbteilungArt art, Guid? oben = null, bool aktiv = true) => new()
        {
            ["Abteilung.Bezeichnung"] = name, ["Abteilung.Art"] = art.ToString(), ["Abteilung.UebergeordneteId"] = oben?.ToString() ?? "",
            ["Abteilung.Sortierung"] = "90", ["Abteilung.Aktiv"] = aktiv ? "true" : "false",
            ["Abteilung.Strasse"] = "Kirchstraße 5", ["Abteilung.Plz"] = "73099", ["Abteilung.Ort"] = "Adelberg",
        };

        // Ohne Anschrift keine Abteilung: sie steht im Kopf der Formulare.
        var ohneAnschrift = Felder(AbteilungArt.Kinder, jugend);
        ohneAnschrift.Remove("Abteilung.Strasse");
        Assert.Contains("Straße fehlt.", await TextAsync(await AbschickenAsync(client, "/Stammdaten/Organisation", "Abteilung", ohneAnschrift)));

        // Kinderfeuerwehr nur unter einer Jugendfeuerwehr.
        var ohne = await AbschickenAsync(client, "/Stammdaten/Organisation", "Abteilung", Felder(AbteilungArt.Kinder));
        Assert.Equal(HttpStatusCode.OK, ohne.StatusCode);
        Assert.Contains("gehört zu einer Jugendfeuerwehr", await TextAsync(ohne));

        var angelegt = await AbschickenAsync(client, "/Stammdaten/Organisation", "Abteilung", Felder(AbteilungArt.Kinder, jugend));
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Bezeichnung == name);
            Assert.Equal((AbteilungArt.Kinder, jugend), (abteilung.Art, abteilung.UebergeordneteId!.Value));
            Assert.Equal("Kirchstraße 5, 73099 Adelberg", abteilung.Anschrift);
            Assert.StartsWith("KF", abteilung.Code);
            id = abteilung.Id;

            var mitglied = new Mitglied { Personalnummer = "S" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = name, Geburtsdatum = Heute.AddYears(-8) };
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
            db.Mitglieder.Add(mitglied);
            await db.SaveChangesAsync();
        }

        List<Abteilung> weitere = [];
        try
        {
            // Mit Mitgliedschaften bleibt die Art fest, mit aktuellen Mitgliedern bleibt die Abteilung aktiv.
            var art = await AbschickenAsync(client, $"/Stammdaten/Organisation?abteilung={id}&id={id}", "Abteilung", Felder(AbteilungArt.Jugend));
            Assert.Contains("ihre Art bleibt deshalb fest", await TextAsync(art));
            var aus = await AbschickenAsync(client, $"/Stammdaten/Organisation?abteilung={id}&id={id}", "Abteilung", Felder(AbteilungArt.Kinder, jugend, aktiv: false));
            Assert.Contains("noch aktuelle Mitglieder", await TextAsync(aus));

            // Die letzte aktive Einsatzabteilung lässt sich nicht abschalten; dazu die anderen vier kurz abschalten.
            await using (var vorher = fixture.Factory.Services.CreateAsyncScope())
            {
                var vorherDb = vorher.ServiceProvider.GetRequiredService<AppDbContext>();
                weitere = await vorherDb.Abteilungen.Where(a => a.Art == AbteilungArt.Einsatz && a.Aktiv && a.Id != einsatz).ToListAsync();
                weitere.ForEach(a => a.Aktiv = false);
                await vorherDb.SaveChangesAsync();
            }

            var ea = await AbschickenAsync(client, $"/Stammdaten/Organisation?abteilung={einsatz}&id={einsatz}", "Abteilung", Felder(AbteilungArt.Einsatz, aktiv: false));
            Assert.Contains("Mindestens eine Einsatzabteilung", await TextAsync(ea));

            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(AbteilungArt.Kinder, (await db.Abteilungen.SingleAsync(a => a.Id == id)).Art);
            Assert.True((await db.Abteilungen.SingleAsync(a => a.Id == einsatz)).Aktiv);
        }
        finally
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Mitglieder.RemoveRange(await db.Mitglieder.Where(m => m.Nachname == name).ToListAsync());
            db.Abteilungen.Remove(await db.Abteilungen.SingleAsync(a => a.Id == id));
            var ids = weitere.Select(a => a.Id).ToList();
            foreach (var a in await db.Abteilungen.Where(a => ids.Contains(a.Id)).ToListAsync())
            {
                a.Aktiv = true;
            }

            await db.SaveChangesAsync();
        }
    }

    /// <summary>Ein gültiges PNG mit 1 × 1 Pixel.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Feuerwehr_Gemeinde_und_Logo_stehen_auf_den_Formularen()
    {
        var client = Angemeldet(Rollen.Kommandant);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Geraetewart).GetAsync("/Stammdaten/Feuerwehr")).StatusCode);

        Dictionary<string, string> Felder() => new()
        {
            ["Formular.FeuerwehrName"] = "Freiwillige Feuerwehr Adelberg", ["Formular.FeuerwehrStrasse"] = "Kirchstraße 5",
            ["Formular.FeuerwehrPlz"] = "73099", ["Formular.FeuerwehrOrt"] = "Adelberg",
            ["Formular.GemeindeName"] = "Gemeinde Adelberg", ["Formular.GemeindeVertretung"] = "vertreten durch den Bürgermeister",
            ["Formular.GemeindeStrasse"] = "Vordere Hauptstraße 2", ["Formular.GemeindePlz"] = "73099", ["Formular.GemeindeOrt"] = "Adelberg",
            ["Formular.GemeindeEmail"] = "info@adelberg.example",
        };

        var ohne = Felder();
        ohne.Remove("Formular.GemeindeStrasse");
        Assert.Contains("Straße fehlt.", await TextAsync(await AbschickenAsync(client, "/Stammdaten/Feuerwehr", "Anschriften", ohne)));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(client, "/Stammdaten/Feuerwehr", "Anschriften", Felder())).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var organisation = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OrganisationAsync();
            Assert.Equal("Vordere Hauptstraße 2, 73099 Adelberg", organisation.GemeindeAnschrift);
            Assert.Equal("info@adelberg.example", organisation.GemeindeEmail);
        }

        async Task<HttpResponseMessage> HochladenAsync(byte[] inhalt, string dateiname)
        {
            var seite = await client.GetStringAsync("/Stammdaten/Feuerwehr");
            var form = new MultipartFormDataContent
            {
                { new StringContent(WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value)), "__RequestVerificationToken" },
                { new ByteArrayContent(inhalt), "Datei", dateiname },
            };
            return await client.PostAsync("/Stammdaten/Feuerwehr?handler=Logo", form);
        }

        static async Task<string> PdfAsync(HttpClient c) =>
            System.Text.Encoding.Latin1.GetString(await c.GetByteArrayAsync("/Antraege?handler=Blanko&art=JugendEintritt"));

        try
        {
            // Erkannt am Inhalt: eine umbenannte Textdatei ist kein Logo.
            await HochladenAsync("kein Bild"u8.ToArray(), "logo.png");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Stammdaten/Feuerwehr?handler=Logo")).StatusCode);
            Assert.DoesNotContain("/Subtype /Image", await PdfAsync(client));

            Assert.Equal(HttpStatusCode.Redirect, (await HochladenAsync(Png, "logo.png")).StatusCode);
            var logo = await client.GetAsync("/Stammdaten/Feuerwehr?handler=Logo");
            Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
            Assert.Contains("/Subtype /Image", await PdfAsync(client));

            // SVG geht auch; direkt aufgerufen läuft darin nichts.
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"#c00\"/></svg>"u8.ToArray();
            Assert.Equal(HttpStatusCode.Redirect, (await HochladenAsync(svg, "logo.svg")).StatusCode);
            var svgLogo = await client.GetAsync("/Stammdaten/Feuerwehr?handler=Logo");
            Assert.Equal("image/svg+xml", svgLogo.Content.Headers.ContentType?.MediaType);
            Assert.Contains("sandbox", svgLogo.Headers.GetValues("Content-Security-Policy").Single());
            Assert.StartsWith("%PDF", await PdfAsync(client));
        }
        finally
        {
            await AbschickenAsync(client, "/Stammdaten/Feuerwehr", "LogoEntfernen", []);
        }
    }

    [Fact]
    public async Task Funktion_braucht_einen_eindeutigen_Code()
    {
        var client = Angemeldet(Rollen.Administrator);
        var code = "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Dictionary<string, string> Felder() => new()
        {
            ["Funktion.Code"] = code.ToLowerInvariant(), ["Funktion.Bezeichnung"] = "Testwart", ["Funktion.AmtszeitJahre"] = "5",
            ["Funktion.VorgeschlageneRolle"] = Rollen.Geraetewart, ["Funktion.Sortierung"] = "500", ["Funktion.Aktiv"] = "true",
        };

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(client, "/Stammdaten/Organisation", "Funktion", Felder())).StatusCode);
        var doppelt = await AbschickenAsync(client, "/Stammdaten/Organisation", "Funktion", Felder());
        Assert.Contains("Diesen Code gibt es schon", await TextAsync(doppelt));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var funktion = await db.Funktionen.SingleAsync(f => f.Code == code);
        Assert.Equal((5, Rollen.Geraetewart), (funktion.AmtszeitJahre!.Value, funktion.VorgeschlageneRolle));
        db.Funktionen.Remove(funktion);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Lehrgangskatalog_kennt_die_Ausbildungsfolge()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kurse = await db.Qualifikationen.Where(q => new[] { "TM2", "AGT", "GF", "JFW" }.Contains(q.Code)).ToDictionaryAsync(q => q.Code);
        Assert.Equal("TM1", kurse["TM2"].Voraussetzungen);
        Assert.Equal(("TM1", (int?)18), (kurse["AGT"].Voraussetzungen, kurse["AGT"].MindestalterJahre));
        Assert.Equal("TF", kurse["GF"].Voraussetzungen);
        Assert.Equal("JGL|GF", kurse["JFW"].Voraussetzungen);
    }
}
