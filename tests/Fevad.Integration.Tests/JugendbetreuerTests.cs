using System.Net;
using static System.Net.WebUtility;
using System.Text.RegularExpressions;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class JugendbetreuerTests(AnwendungFixture fixture)
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

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, Dictionary<string, string> felder)
    {
        felder["__RequestVerificationToken"] = TokenFeld().Match(await client.GetStringAsync(seite)).Groups[1].Value;
        return await client.PostAsync(seite, new FormUrlEncodedContent(felder));
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Jugendwart_vermerkt_das_Fuehrungszeugnis_und_druckt_die_Bestaetigung()
    {
        var name = "Btr" + Guid.NewGuid().ToString("N")[..6];
        var jf = await MitDbAsync(db => db.Abteilungen.Where(a => a.Code == "JF").Select(a => a.Id).SingleAsync());
        var jugendwart = Angemeldet(Rollen.Jugendwart);
        var felder = new Dictionary<string, string>
        {
            ["Formular.Vorname"] = "Ole", ["Formular.Nachname"] = name, ["Formular.AbteilungId"] = jf.ToString(), ["Formular.Aufgabe"] = "Betreuer",
            ["Formular.Von"] = Heute.AddDays(-10).ToString("yyyy-MM-dd"), ["Formular.ZeugnisNoetig"] = "true",
            ["Formular.EingesehenAm"] = Heute.ToString("yyyy-MM-dd"), ["Formular.ZeugnisVom"] = Heute.AddMonths(-4).ToString("yyyy-MM-dd"),
            ["Formular.OhneEintrag"] = "true",
        };

        // Zu altes Zeugnis wird nicht angenommen.
        var fehler = await AbschickenAsync(jugendwart, "/Jugend/BetreuerBearbeiten", felder);
        Assert.Equal(HttpStatusCode.OK, fehler.StatusCode);
        Assert.Contains("höchstens 3 Monate alt", HtmlDecode(await fehler.Content.ReadAsStringAsync()));

        felder["Formular.ZeugnisVom"] = Heute.AddDays(-20).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendwart, "/Jugend/BetreuerBearbeiten", felder)).StatusCode);
        var b = await MitDbAsync(db => db.Jugendbetreuer.AsNoTracking().SingleAsync(x => x.Nachname == name));
        Assert.Equal((Heute, (bool?)true), (b.EingesehenAm!.Value, b.OhneEintrag));

        var liste = HtmlDecode(await jugendwart.GetStringAsync("/Jugend/Betreuer"));
        Assert.Contains($"{name}, Ole", liste);
        Assert.Contains("gültig", liste);
        Assert.Contains(Anzeige.Datum(Heute.AddYears(5)), liste);

        var pdf = await jugendwart.GetAsync($"/Jugend/BetreuerBearbeiten/{b.Id}?handler=Bescheinigung");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(name, pdf.Content.Headers.ContentDisposition?.FileName ?? "");

        // Das Ergebnis sehen nur Kommandant und Jugendwart.
        Assert.Contains($"{name}, Ole", HtmlDecode(await Angemeldet(Rollen.Kommandant).GetStringAsync("/Jugend/Betreuer")));
        foreach (var rolle in new[] { Rollen.Stellvertreter, Rollen.BetreuerJugend, Rollen.Geraetewart })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(rolle).GetAsync("/Jugend/Betreuer")).StatusCode);
        }
    }

    [Fact]
    public async Task L53_loescht_drei_Monate_nach_Ende_und_nicht_aufgenommene_Taetigkeiten_sofort()
    {
        var name = "L53" + Guid.NewGuid().ToString("N")[..6];
        var ids = await MitDbAsync(async db =>
        {
            var jf = await db.Abteilungen.Where(a => a.Code == "JF").Select(a => a.Id).SingleAsync();
            Jugendbetreuer Neu(DateOnly von, DateOnly? bis) =>
                new() { Vorname = "X", Nachname = name, Aufgabe = "Betreuer", AbteilungId = jf, Von = von, Bis = bis, EingesehenAm = von, ZeugnisVom = von, OhneEintrag = true };
            var alt = Neu(Heute.AddYears(-2), Heute.AddMonths(-4));
            var nie = Neu(Heute.AddDays(10), Heute);
            var kurz = Neu(Heute.AddYears(-1), Heute.AddMonths(-1));
            var aktiv = Neu(Heute.AddYears(-1), null);
            db.Jugendbetreuer.AddRange(alt, nie, kurz, aktiv);
            await db.SaveChangesAsync();
            return new[] { alt.Id, nie.Id, kurz.Id, aktiv.Id };
        });

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelBetreuer).Anzahl >= 2);
        var uebrig = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Jugendbetreuer.IgnoreQueryFilters()
            .Where(b => b.Nachname == name).Select(b => b.Id).ToListAsync();
        Assert.Equal(new[] { ids[2], ids[3] }.Order(), uebrig.Order());
    }
}
