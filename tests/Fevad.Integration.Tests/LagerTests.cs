using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Verbrauchsmaterial (V16): Lagerartikel, Eingang, Entnahme im Einsatz und Übernahme in den Kostenersatz.</summary>
[Collection(AnwendungCollection.Name)]
public partial class LagerTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static DateOnly Heute => Anzeige.Heute(TimeProvider.System);

    [Fact]
    public async Task Entnahme_im_Einsatz_wird_Kostenersatz_Posten_und_Mindestmenge_meldet_sich()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var bezeichnung = "Ölbinder " + kennung;
        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);

        // Der Gerätewart legt den Artikel an und bucht einen Eingang mit Verfallsdatum.
        var liste = await geraetewart.GetStringAsync("/Technik/Lager");
        var anlegen = await PostAsync(geraetewart, "/Technik/Lager", new()
        {
            ["__RequestVerificationToken"] = Token(liste),
            ["Formular.Bezeichnung"] = bezeichnung,
            ["Formular.Kategorie"] = nameof(Lagerkategorie.Bindemittel),
            ["Formular.Einheit"] = "Sack",
            ["Formular.Mindestmenge"] = "5",
            ["Formular.Stueckpreis"] = "18,90",
        });
        Assert.Equal(HttpStatusCode.Redirect, anlegen.StatusCode);
        var artikelId = await MitDbAsync(db => db.Lagerartikel.Where(a => a.Bezeichnung == bezeichnung).Select(a => a.Id).SingleAsync());
        var artikelSeite = await geraetewart.GetStringAsync($"/Technik/Lager/Artikel/{artikelId}");
        var eingang = await PostAsync(geraetewart, $"/Technik/Lager/Artikel/{artikelId}?handler=Eingang", new()
        {
            ["__RequestVerificationToken"] = Token(artikelSeite),
            ["Buchung.Menge"] = "8",
            ["Buchung.Datum"] = Heute.ToString("dd.MM.yyyy"),
            ["Buchung.VerfallAm"] = Heute.AddYears(3).ToString("dd.MM.yyyy"),
        });
        Assert.Equal(HttpStatusCode.Redirect, eingang.StatusCode);

        Guid einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = new Einsatz { Einsatznummer = "V-" + kennung, Stichwort = "TH 1", Alarmiert = DateTimeOffset.UtcNow.AddHours(-3), Status = EinsatzStatus.InBearbeitung };
            db.Einsaetze.Add(einsatz);
            await db.SaveChangesAsync();
            einsatzId = einsatz.Id;
        }

        // Die Führungskraft trägt den Verbrauch am Einsatz ein; mehr als vorhanden geht nicht.
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);
        var einsatzSeite = WebUtility.HtmlDecode(await fuehrung.GetStringAsync($"/Einsaetze/Geraete/{einsatzId}"));
        Assert.Contains(bezeichnung, einsatzSeite);
        Dictionary<string, string> Verbrauch(string menge) => new()
        {
            ["__RequestVerificationToken"] = Token(einsatzSeite),
            ["artikelId"] = artikelId.ToString(),
            ["menge"] = menge,
        };
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Verbrauch", Verbrauch("20"))).StatusCode);
        Assert.Equal(0, await MitDbAsync(db => db.Lagerbewegungen.CountAsync(b => b.EinsatzId == einsatzId)));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Verbrauch", Verbrauch("4"))).StatusCode);
        Assert.Equal(4, await MitDbAsync(db => db.Lagerchargen.Where(c => c.LagerartikelId == artikelId).SumAsync(c => c.Menge)));

        // Wer den Kostenersatz-Vorgang anlegt, findet das Material schon als Posten.
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var pfad = $"/Kostenersatz/Vorgang?einsatzId={einsatzId}";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "&handler=Anlegen", new()
        {
            ["__RequestVerificationToken"] = Token(await kommandant.GetStringAsync(pfad)),
        })).StatusCode);
        var posten = await MitDbAsync(db => db.Set<Zusatzkosten>().SingleAsync(z => z.Kostenersatzvorgang!.EinsatzId == einsatzId));
        Assert.Equal((ZusatzkostenArt.Verbrauch, 75.60m), (posten.Art, posten.Betrag));
        Assert.Contains(bezeichnung, posten.Bezeichnung);
        Assert.Equal(0, await MitDbAsync(db => db.Lagerbewegungen.CountAsync(b => b.EinsatzId == einsatzId && b.ZusatzkostenId == null)));

        // Spätere Entnahme bleibt offen, bis sie über den Knopf übernommen wird.
        einsatzSeite = WebUtility.HtmlDecode(await fuehrung.GetStringAsync($"/Einsaetze/Geraete/{einsatzId}"));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Verbrauch", Verbrauch("1"))).StatusCode);
        var vorgangSeite = await kommandant.GetStringAsync(pfad);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "&handler=Lager", new() { ["__RequestVerificationToken"] = Token(vorgangSeite) })).StatusCode);
        Assert.Equal(2, await MitDbAsync(db => db.Set<Zusatzkosten>().CountAsync(z => z.Kostenersatzvorgang!.EinsatzId == einsatzId)));

        // Bestand 3 liegt unter der Mindestmenge 5: Hinweis für den Gerätewart.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var hinweise = await scope.ServiceProvider.GetRequiredService<Lagerverwaltung>().FaelligeAsync(Heute, default);
            Assert.Contains(hinweise, h => h.Gegenstand == "Lager: " + bezeichnung && h.Bezeichnung.StartsWith("Mindestmenge"));
        }

        // Hauptamt liest Technik, bucht aber nicht.
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var hauptamtSeite = await hauptamt.GetStringAsync($"/Technik/Lager/Artikel/{artikelId}");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(hauptamt, $"/Technik/Lager/Artikel/{artikelId}?handler=Eingang", new()
        {
            ["__RequestVerificationToken"] = Token(hauptamtSeite),
            ["Buchung.Menge"] = "1",
        })).StatusCode);
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string adresse, Dictionary<string, string> felder) =>
        client.PostAsync(adresse, new FormUrlEncodedContent(felder));

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
}
