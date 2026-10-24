using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class DokumenteUndGremienTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex("/Dokumente/Datei/([0-9a-f-]{36})")]
    private static partial Regex DateiLink();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    /// <summary>Die Testanmeldung braucht ein echtes Konto, weil Lesebestätigungen darauf verweisen.</summary>
    private async Task TestkontoAnlegenAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Users.AnyAsync(u => u.Id == TestAnmeldung.BenutzerId))
        {
            db.Users.Add(new Benutzer { Id = TestAnmeldung.BenutzerId, UserName = "test@ff-adelberg.test", Email = "test@ff-adelberg.test" });
            await db.SaveChangesAsync();
        }
    }

    private static MultipartFormDataContent Hochladen(string token, string dateiname, string inhalt, Dictionary<string, string> felder)
    {
        var form = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        foreach (var (name, wert) in felder)
        {
            form.Add(new StringContent(wert), name);
        }

        var datei = new ByteArrayContent(Encoding.UTF8.GetBytes(inhalt));
        datei.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(datei, "Datei", dateiname);
        return form;
    }

    private async Task<Guid> KategorieAsync(string code)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dokumentkategorien.Where(k => k.Code == code).Select(k => k.Id).SingleAsync();
    }

    [Fact]
    public async Task Dienstanweisung_hochladen_lesen_bestaetigen_und_neue_Version()
    {
        await TestkontoAnlegenAsync();
        var factory = TestAnmeldung.Factory(fixture);
        var kategorie = await KategorieAsync("DA");

        // Ein einfaches Mitglied darf nicht hochladen.
        var mitglied = Angemeldet(factory, Rollen.Mitglied);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Dokumente/Neu")).StatusCode);

        var schriftfuehrer = Angemeldet(factory, Rollen.Schriftfuehrer);
        var seite = await schriftfuehrer.GetStringAsync("/Dokumente/Neu");
        var titel = "Dienstanweisung Atemschutz " + Guid.NewGuid().ToString("N")[..6];
        var felder = new Dictionary<string, string>
        {
            ["Formular.Titel"] = titel,
            ["Formular.KategorieId"] = kategorie.ToString(),
            ["Formular.Sichtbarkeit"] = "Alle",
            ["Formular.LesebestaetigungErforderlich"] = "true",
        };

        // Nicht erlaubter Dateityp wird abgewiesen.
        var abgewiesen = await schriftfuehrer.PostAsync("/Dokumente/Neu", Hochladen(Token(seite), "seite.html", "<script>", felder));
        Assert.Equal(HttpStatusCode.OK, abgewiesen.StatusCode);
        Assert.Contains("Dateityp nicht erlaubt", WebUtility.HtmlDecode(await abgewiesen.Content.ReadAsStringAsync()));

        var angelegt = await schriftfuehrer.PostAsync("/Dokumente/Neu", Hochladen(Token(seite), @"C:\Ablage\da-atemschutz.txt", "Version 1", felder));
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var detail = angelegt.Headers.Location!.OriginalString;

        // Mitglied sieht das Dokument, lädt es (Typ nach Endung, nicht nach Browserangabe) und bestätigt.
        var liste = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Dokumente"));
        Assert.Contains(titel, liste);
        Assert.Contains("deine Lesebestätigung", liste);
        seite = await mitglied.GetStringAsync(detail);
        var datei = await mitglied.GetAsync(DateiLink().Match(seite).Value);
        Assert.Equal("text/plain", datei.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", datei.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("Version 1", await datei.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await mitglied.PostAsync($"{detail}?handler=Bestaetigen",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }))).StatusCode);
        Assert.Contains("als gelesen bestätigt", WebUtility.HtmlDecode(await mitglied.GetStringAsync(detail)));

        // Neue Version: Bestätigung ist wieder offen, die alte Version bleibt abrufbar.
        seite = await schriftfuehrer.GetStringAsync(detail);
        var version = new MultipartFormDataContent { { new StringContent(Token(seite)), "__RequestVerificationToken" }, { new StringContent("Atemschutzüberwachung ergänzt"), "Aenderungshinweis" } };
        version.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Version 2")), "Datei", "da-atemschutz-2.txt");
        var hochgeladen = await schriftfuehrer.PostAsync($"{detail}?handler=Version", version);
        Assert.Equal(HttpStatusCode.Redirect, hochgeladen.StatusCode);

        seite = WebUtility.HtmlDecode(await mitglied.GetStringAsync(detail));
        Assert.Contains("Gelesen und zur Kenntnis genommen", seite);
        Assert.Contains("Frühere Versionen", seite);
        Assert.Equal(2, DateiLink().Matches(seite).Select(m => m.Value).Distinct().Count());
    }

    [Fact]
    public async Task Vertrauliches_Dokument_nur_fuer_die_Fuehrung()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var seite = await kommandant.GetStringAsync("/Dokumente/Neu");
        var titel = "Ausschussprotokoll " + Guid.NewGuid().ToString("N")[..6];
        var angelegt = await kommandant.PostAsync("/Dokumente/Neu", Hochladen(Token(seite), "protokoll.pdf", "%PDF-1.4", new()
        {
            ["Formular.Titel"] = titel,
            ["Formular.KategorieId"] = (await KategorieAsync("PROT")).ToString(),
            ["Formular.Sichtbarkeit"] = "Fuehrung",
        }));
        var detail = angelegt.Headers.Location!.OriginalString;
        var link = DateiLink().Match(await kommandant.GetStringAsync(detail)).Value;

        var mitglied = Angemeldet(factory, Rollen.Mitglied);
        Assert.DoesNotContain(titel, await mitglied.GetStringAsync("/Dokumente"));
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.GetAsync(link)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task Hauptversammlung_mit_Beschluessen_und_Kommandantenwahl()
    {
        Guid gewaehlterId, bisherigerId, kommandantFunktion;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ea = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
            kommandantFunktion = await db.Funktionen.Where(f => f.Code == "KDT").Select(f => f.Id).SingleAsync();
            Mitglied Neu(string name) => new()
            {
                Personalnummer = "W-" + Guid.NewGuid().ToString("N")[..8], Vorname = name, Nachname = "Wahl", Geburtsdatum = new(1985, 1, 1),
                Mitgliedschaften = [new() { AbteilungId = ea.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) }],
            };
            var gewaehlter = Neu("Gerd");
            var bisheriger = Neu("Bernd");
            var dritter = Neu("Dora");
            db.Mitglieder.AddRange(gewaehlter, bisheriger, dritter);
            db.FunktionZuordnungen.Add(new FunktionZuordnung { Mitglied = bisheriger, FunktionId = kommandantFunktion, Art = FunktionArt.Wahl, GueltigAb = new(2036, 4, 1), GueltigBis = new(2041, 3, 31) });
            await db.SaveChangesAsync();
            (gewaehlterId, bisherigerId) = (gewaehlter.Id, bisheriger.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var schriftfuehrer = Angemeldet(factory, Rollen.Schriftfuehrer);
        var seite = await schriftfuehrer.GetStringAsync("/Gremien/Sitzung?art=Hauptversammlung");
        var angelegt = await schriftfuehrer.PostAsync("/Gremien/Sitzung", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Art"] = "Hauptversammlung",
            ["Formular.Titel"] = "Hauptversammlung 2041",
            ["Formular.Beginn"] = "08.03.2041 19:30",
            ["Formular.Ende"] = "22:00",
            ["Formular.EingeladenAm"] = "2041-03-01",
            ["Formular.Status"] = "Durchgefuehrt",
        }));
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var pfad = angelegt.Headers.Location!.OriginalString;
        seite = WebUtility.HtmlDecode(await schriftfuehrer.GetStringAsync(pfad));
        Assert.Contains("Einladungsfrist von 14 Tagen ist nicht eingehalten", seite);

        // Beschlüsse bekommen laufende Nummern im Jahr; Stimmengleichheit ist abgelehnt.
        foreach (var (gegenstand, ja, nein) in new[] { ("Jahresbericht wird entgegengenommen", 30, 0), ("Antrag Grillfest", 15, 15) })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await schriftfuehrer.PostAsync($"{pfad}?handler=Beschluss", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(seite),
                ["NeuerBeschluss.Gegenstand"] = gegenstand,
                ["NeuerBeschluss.JaStimmen"] = ja.ToString(),
                ["NeuerBeschluss.NeinStimmen"] = nein.ToString(),
            }))).StatusCode);
        }

        Guid sitzungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sitzung = await db.Sitzungen.Include(s => s.Beschluesse).SingleAsync(s => s.Titel == "Hauptversammlung 2041");
            sitzungId = sitzung.Id;
            Assert.Equal(["2041/01", "2041/02"], sitzung.Beschluesse.OrderBy(b => b.Nummer).Select(b => b.Kennung));
            Assert.Equal(BeschlussErgebnis.Abgelehnt, sitzung.Beschluesse.Single(b => b.Nummer == 2).Ergebnis);
        }

        // Teilnehmer: Bezug der Hauptversammlung ist die ganze Einsatzabteilung, nicht nur die erfassten zwei.
        Assert.Equal(HttpStatusCode.Redirect, (await schriftfuehrer.PostAsync($"{pfad}?handler=Teilnahme", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Teilnahmen[0].MitgliedId"] = gewaehlterId.ToString(),
            ["Teilnahmen[0].Status"] = "Anwesend",
            ["Teilnahmen[0].Stimmberechtigt"] = "true",
            ["Teilnahmen[1].MitgliedId"] = bisherigerId.ToString(),
            ["Teilnahmen[1].Status"] = "Entschuldigt",
            ["Teilnahmen[1].Stimmberechtigt"] = "true",
        }))).StatusCode);
        Assert.Contains("Stimmberechtigten: nicht beschlussfähig", WebUtility.HtmlDecode(await schriftfuehrer.GetStringAsync(pfad)));

        // Kommandantenwahl: Ergebnis feststellen, Annahme und Gemeinderat eintragen, übernehmen.
        seite = await schriftfuehrer.GetStringAsync($"/Gremien/Wahl?sitzungId={sitzungId}");
        var wahl = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.FunktionId"] = kommandantFunktion.ToString(),
            ["Formular.Verfahren"] = "AbsoluteMehrheit",
            ["Formular.Sitze"] = "1",
            ["Formular.Wahlgang"] = "1",
            ["Formular.Stimmberechtigte"] = "30",
            ["Formular.GueltigeStimmen"] = "30",
            ["Formular.Amtsbeginn"] = "2041-04-01",
            ["Formular.ZustimmungGemeinderatErforderlich"] = "true",
            ["Bewerber[0].MitgliedId"] = gewaehlterId.ToString(),
            ["Bewerber[0].Stimmen"] = "22",
            ["Bewerber[1].MitgliedId"] = bisherigerId.ToString(),
            ["Bewerber[1].Stimmen"] = "8",
        };
        var festgestellt = await schriftfuehrer.PostAsync($"/Gremien/Wahl?sitzungId={sitzungId}&handler=Feststellen", new FormUrlEncodedContent(wahl));
        Assert.Equal(HttpStatusCode.Redirect, festgestellt.StatusCode);
        var wahlPfad = festgestellt.Headers.Location!.OriginalString;
        seite = WebUtility.HtmlDecode(await schriftfuehrer.GetStringAsync(wahlPfad));
        Assert.Contains("Zustimmung des Gemeinderats steht noch aus", seite);
        Assert.Contains("Übernehmen dürfen Kommandant und Stellvertreter", seite);

        wahl["__RequestVerificationToken"] = Token(seite);
        wahl["Bewerber[0].Gewaehlt"] = "true";
        wahl["Bewerber[0].AngenommenAm"] = "2041-03-08";
        wahl["Formular.ZustimmungGemeinderatAm"] = "2041-03-20";
        Assert.Equal(HttpStatusCode.Redirect, (await schriftfuehrer.PostAsync(wahlPfad, new FormUrlEncodedContent(wahl))).StatusCode);

        seite = await schriftfuehrer.GetStringAsync(wahlPfad);
        Assert.Equal(HttpStatusCode.Forbidden, (await schriftfuehrer.PostAsync($"{wahlPfad}?handler=Uebernehmen",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }))).StatusCode);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        seite = await kommandant.GetStringAsync(wahlPfad);
        Assert.Equal(HttpStatusCode.Redirect, (await kommandant.PostAsync($"{wahlPfad}?handler=Uebernehmen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["BisherigeBeenden"] = "true",
        }))).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var neu = await db.FunktionZuordnungen.SingleAsync(f => f.MitgliedId == gewaehlterId && f.FunktionId == kommandantFunktion);
            Assert.Equal(new DateOnly(2041, 4, 1), neu.GueltigAb);
            Assert.Equal(new DateOnly(2046, 3, 31), neu.GueltigBis);
            Assert.Equal(new DateOnly(2041, 3, 8), neu.Wahldatum);
            Assert.Equal(new DateOnly(2041, 3, 20), neu.Beschlussdatum);
            var alt = await db.FunktionZuordnungen.SingleAsync(f => f.MitgliedId == bisherigerId && f.FunktionId == kommandantFunktion);
            Assert.Equal(new DateOnly(2041, 3, 31), alt.GueltigBis);
            Assert.Equal(WahlStatus.Uebernommen, (await db.Wahlen.SingleAsync(w => w.SitzungId == sitzungId)).Status);
        }

        Assert.Equal(HttpStatusCode.OK, (await kommandant.GetAsync("/Gremien/Aemter")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Mitglied).GetAsync("/Gremien")).StatusCode);
    }
}
