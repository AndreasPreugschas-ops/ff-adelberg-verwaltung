using System.Net;
using static System.Net.WebUtility;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class EinwilligungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _factory = TestAnmeldung.Factory(fixture);

    private HttpClient Angemeldet(Guid? benutzerId, params string[] rollen)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        if (benutzerId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, benutzerId.ToString());
        }

        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string ziel, Dictionary<string, string> felder)
    {
        felder["__RequestVerificationToken"] = TokenFeld().Match(await client.GetStringAsync(seite)).Groups[1].Value;
        return await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, ziel) { Content = new FormUrlEncodedContent(felder) });
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Kind in der Jugendfeuerwehr mit Elternkonto; gibt Kind, Elternkonto und eigenes Konto des Kindes zurück.</summary>
    private async Task<(Guid Kind, Guid Eltern, Guid KindKonto)> KindMitElternkontoAsync(string name, int alter)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var jf = await db.Abteilungen.SingleAsync(a => a.Code == "JF");
        var kind = new Mitglied { Personalnummer = "E-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Mia", Nachname = name, Geburtsdatum = Heute.AddYears(-alter).AddDays(-10) };
        kind.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
        db.Mitglieder.Add(kind);
        await db.SaveChangesAsync();

        async Task<Guid> KontoAsync(Guid? mitglied, string rolle)
        {
            var email = $"e-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
            var konto = new Benutzer { UserName = email, Email = email, MitgliedId = mitglied };
            Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
            Assert.True((await benutzer.AddToRoleAsync(konto, rolle)).Succeeded);
            return konto.Id;
        }

        var eltern = await KontoAsync(null, Rollen.Erziehungsberechtigt);
        db.Erziehungsberechtigte.Add(new Erziehungsberechtigter { MitgliedId = kind.Id, Nummer = 1, Vorname = "Petra", Nachname = name, KontoId = eltern, KontoZugeordnetAm = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return (kind.Id, eltern, await KontoAsync(kind.Id, Rollen.Mitglied));
    }

    private Task<Guid> ArtAsync(string code) => MitDbAsync(db => db.Einwilligungsarten.Where(a => a.Code == code).Select(a => a.Id).SingleAsync());

    [Fact]
    public async Task Eltern_willigen_im_Elternkonto_ein_und_ab_16_stimmt_das_Kind_zu()
    {
        var name = "Einw" + Guid.NewGuid().ToString("N")[..6];
        var (kind, eltern, kindKonto) = await KindMitElternkontoAsync(name, 16);
        var homepage = await ArtAsync(Einwilligungsart.CodeFotoHomepage);
        var whatsapp = await ArtAsync(Einwilligungsart.CodeWhatsApp);

        var elternteil = Angemeldet(eltern, Rollen.Erziehungsberechtigt);
        var seite = HtmlDecode(await elternteil.GetStringAsync("/Konto/Einwilligungen"));
        Assert.Contains("Für Mia", seite);
        Assert.Contains("Mitnahme im privaten Pkw", seite);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(elternteil, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Erklaeren",
            new() { ["mitgliedId"] = kind.ToString(), ["artId"] = homepage.ToString(), ["erteilt"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(elternteil, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Erklaeren",
            new() { ["mitgliedId"] = kind.ToString(), ["artId"] = whatsapp.ToString(), ["erteilt"] = "true" })).StatusCode);
        var e = await MitDbAsync(db => db.Einwilligungen.AsNoTracking().SingleAsync(x => x.MitgliedId == kind && x.EinwilligungsartId == homepage));
        Assert.Equal((EinwilligungDurch.Erziehungsberechtigter, EinwilligungWeg.Konto, eltern, $"Petra {name}"), (e.Durch, e.Weg, e.KontoId, e.DurchName));

        // Fremde Kinder gehen nicht.
        var fremd = await KindMitElternkontoAsync(name + "x", 12);
        Assert.Equal(HttpStatusCode.NotFound, (await AbschickenAsync(elternteil, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Erklaeren",
            new() { ["mitgliedId"] = fremd.Kind.ToString(), ["artId"] = homepage.ToString(), ["erteilt"] = "true" })).StatusCode);

        // Mit 16 wirkt es erst mit der Zustimmung des Kindes; selbst erteilen darf es nicht.
        var jugendliche = Angemeldet(kindKonto, Rollen.Mitglied);
        Assert.Contains("Zustimmung des Jugendlichen fehlt", HtmlDecode(await jugendliche.GetStringAsync("/Konto/Einwilligungen")));
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(jugendliche, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Erklaeren",
            new() { ["mitgliedId"] = kind.ToString(), ["artId"] = homepage.ToString(), ["erteilt"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendliche, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Zustimmen",
            new() { ["artId"] = homepage.ToString() })).StatusCode);

        // Der Jugendwart sieht den Stand; die Übersicht zeigt nur noch fehlende.
        var jugendwart = Angemeldet(null, Rollen.Jugendwart);
        var register = HtmlDecode(await jugendwart.GetStringAsync($"/Mitglieder/Einwilligungen/{kind}"));
        Assert.Contains("Fotos auf der Homepage", register);
        var liste = HtmlDecode(await jugendwart.GetStringAsync($"/Mitglieder/Einwilligungsliste?Jugend=true&NurFehlende=true"));
        Assert.Contains($"{name}, Mia", liste);

        // Widerruf durch das Kind wirkt sofort.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendliche, "/Konto/Einwilligungen", "/Konto/Einwilligungen?handler=Widerrufen",
            new() { ["mitgliedId"] = kind.ToString(), ["artId"] = homepage.ToString() })).StatusCode);
        Assert.Equal(Heute, (await MitDbAsync(db => db.Einwilligungen.AsNoTracking().SingleAsync(x => x.MitgliedId == kind && x.EinwilligungsartId == homepage))).WiderrufenAm);
    }

    [Fact]
    public async Task Verwaltung_traegt_Papier_fuer_alle_Fotokanaele_ein_und_der_Geraetewart_kommt_nicht_dran()
    {
        var name = "Papier" + Guid.NewGuid().ToString("N")[..6];
        var (kind, _, _) = await KindMitElternkontoAsync(name, 12);
        var jugendwart = Angemeldet(null, Rollen.Jugendwart);
        var seite = $"/Mitglieder/Einwilligungen/{kind}";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendwart, seite, seite + "?handler=Eintragen", new()
        {
            ["Formular.ArtId"] = "", ["Formular.Erteilt"] = "true", ["Formular.Am"] = Heute.AddDays(-2).ToString("yyyy-MM-dd"),
            ["Formular.Durch"] = nameof(EinwilligungDurch.Erziehungsberechtigter), ["Formular.DurchName"] = "Petra", ["Formular.Nachweis"] = "Ordner Jugend",
        })).StatusCode);
        var eintraege = await MitDbAsync(db => db.Einwilligungen.AsNoTracking().Where(e => e.MitgliedId == kind).Include(e => e.Einwilligungsart).ToListAsync());
        Assert.Equal(4, eintraege.Count);
        Assert.All(eintraege, e => Assert.True(e.Einwilligungsart!.Foto && e.Weg == EinwilligungWeg.Papier && e.Nachweis == "Ordner Jugend"));

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Geraetewart).GetAsync(seite)).StatusCode);
    }

    [Fact]
    public async Task Loeschanfrage_wird_gestellt_beantwortet_und_nach_L52_geloescht()
    {
        var name = "Loesch" + Guid.NewGuid().ToString("N")[..6];
        var (kind, _, kindKonto) = await KindMitElternkontoAsync(name, 12);
        var mitglied = Angemeldet(kindKonto, Rollen.Mitglied);
        var seite = HtmlDecode(await mitglied.GetStringAsync("/Konto/Datenschutz"));
        Assert.Contains("Was passiert mit meinen Daten?", seite);
        Assert.Contains("Monate nach dem Austritt gelöscht (L6)", seite);
        Assert.Equal("application/pdf", (await mitglied.GetAsync("/Konto/Datenschutz?handler=Information")).Content.Headers.ContentType?.MediaType);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Konto/Datenschutz", "/Konto/Datenschutz?handler=Anfrage",
            new() { ["Text"] = "Bitte meine Fotos löschen." })).StatusCode);
        var anfrage = await MitDbAsync(db => db.Loeschanfragen.AsNoTracking().SingleAsync(l => l.KontoId == kindKonto));
        Assert.Equal((kind, $"Mia {name}"), (anfrage.MitgliedId, anfrage.Name));

        var kommandant = Angemeldet(null, Rollen.Kommandant);
        Assert.Contains("Bitte meine Fotos löschen.", HtmlDecode(await kommandant.GetStringAsync("/Verwaltung/Datenschutz/Loeschanfragen")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, "/Verwaltung/Datenschutz/Loeschanfragen",
            $"/Verwaltung/Datenschutz/Loeschanfragen?handler=Erledigen&id={anfrage.Id}", new() { ["antwort"] = "Fotos entfernt." })).StatusCode);
        Assert.Contains("Fotos entfernt.", HtmlDecode(await mitglied.GetStringAsync("/Konto/Datenschutz")));
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Verwaltung/Datenschutz/Loeschanfragen")).StatusCode);

        // Nach der Frist löscht L52 die erledigte Anfrage, L51 widerrufene Einwilligungen.
        var art = await ArtAsync(Einwilligungsart.CodeWhatsApp);
        await MitDbAsync(async db =>
        {
            await db.Loeschanfragen.Where(l => l.Id == anfrage.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.ErledigtAm, DateTimeOffset.UtcNow.AddMonths(-40)));
            db.Einwilligungen.Add(new Einwilligung { MitgliedId = kind, EinwilligungsartId = art, Erteilt = true, Am = Heute.AddYears(-4), WiderrufenAm = Heute.AddMonths(-37) });
            db.Einwilligungen.Add(new Einwilligung { MitgliedId = kind, EinwilligungsartId = art, Erteilt = true, Am = Heute.AddMonths(-1) });
            return await db.SaveChangesAsync();
        });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelLoeschanfragen).Anzahl >= 1);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelEinwilligungen).Anzahl >= 1);
        }

        Assert.False(await MitDbAsync(db => db.Loeschanfragen.AnyAsync(l => l.Id == anfrage.Id)));
        Assert.Equal(Heute.AddMonths(-1), (await MitDbAsync(db => db.Einwilligungen.AsNoTracking().SingleAsync(e => e.MitgliedId == kind && e.EinwilligungsartId == art))).Am);
    }
}
