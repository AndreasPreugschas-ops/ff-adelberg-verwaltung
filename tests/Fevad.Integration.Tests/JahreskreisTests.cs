using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Jahreskreis der Führungspflichten (P5: V51): Pflicht über die Funktion, Vermerk für den Nachfolger.</summary>
[Collection(AnwendungCollection.Name)]
public partial class JahreskreisTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, Guid? benutzer, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        if (benutzer is { } id)
        {
            client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, id.ToString());
        }

        return client;
    }

    [Fact]
    public async Task Pflicht_erscheint_beim_Inhaber_der_Funktion_und_bleibt_als_Vermerk()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var bezeichnung = $"Testpflicht {code}";
        Guid pflichtId, kassierKonto, anderesKonto;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var kas = await db.Funktionen.SingleAsync(f => f.Code == "KAS");
            var pflicht = new Fuehrungspflicht { Code = code, Bezeichnung = bezeichnung, Monat = heute.Month, Tag = heute.Day, VorlaufTage = 0, Verantwortlich = "KAS" };
            db.Fuehrungspflichten.Add(pflicht);
            var karla = new Mitglied { Personalnummer = "J-" + code, Vorname = "Karla", Nachname = "Kasse", Geburtsdatum = new(1980, 1, 1) };
            var otto = new Mitglied { Personalnummer = "K-" + code, Vorname = "Otto", Nachname = "Ohneamt", Geburtsdatum = new(1980, 1, 1) };
            db.Mitglieder.AddRange(karla, otto);
            db.FunktionZuordnungen.Add(new FunktionZuordnung { Mitglied = karla, FunktionId = kas.Id, Art = FunktionArt.Wahl, GueltigAb = heute.AddYears(-1) });
            await db.SaveChangesAsync();
            pflichtId = pflicht.Id;

            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            async Task<Guid> KontoAsync(Mitglied m)
            {
                var email = $"j-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
                var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
                Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
                return konto.Id;
            }

            kassierKonto = await KontoAsync(karla);
            anderesKonto = await KontoAsync(otto);
        }

        var factory = TestAnmeldung.Factory(fixture);

        // Ohne Funktion: nichts zu sehen.
        var anderer = Angemeldet(factory, anderesKonto, Rollen.Mitglied);
        Assert.DoesNotContain(bezeichnung, WebUtility.HtmlDecode(await anderer.GetStringAsync("/Konto/Pflichten")));

        // Inhaber der Funktion sieht die Pflicht und vermerkt sie.
        var kassier = Angemeldet(factory, kassierKonto, Rollen.Mitglied);
        var seite = WebUtility.HtmlDecode(await kassier.GetStringAsync("/Konto/Pflichten"));
        Assert.Contains(bezeichnung, seite);
        Assert.Equal(HttpStatusCode.Redirect, (await kassier.PostAsync("/Konto/Pflichten?handler=Erledigt", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite), ["pflichtId"] = pflichtId.ToString(), ["bemerkung"] = "Belege beim Kassenprüfer abgegeben",
        }))).StatusCode);

        // Übersicht für das Kommando zeigt Inhaber und Vermerk; den Katalog pflegt nur, wer Gremien bearbeiten darf.
        var kommando = Angemeldet(factory, null, Rollen.Kassier);
        var uebersicht = WebUtility.HtmlDecode(await kommando.GetStringAsync("/Gremien/Jahreskreis"));
        Assert.Contains("Z-Feu-Antrag beim Landratsamt", uebersicht);
        Assert.Contains("Karla Kasse", uebersicht);
        Assert.Contains("Belege beim Kassenprüfer abgegeben", uebersicht);
        Assert.Equal(HttpStatusCode.Forbidden, (await kommando.GetAsync("/Gremien/Fuehrungspflicht")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, null, Rollen.Schriftfuehrer).GetAsync($"/Gremien/Fuehrungspflicht/{pflichtId}")).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vermerk = await db.PflichtErledigungen.SingleAsync(e => e.PflichtId == pflichtId);
            Assert.Equal(heute, vermerk.Stichtag);
        }
    }
}
