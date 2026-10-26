using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Lehrgangswünsche (P5: V54): Wunsch mit Voraussetzungsprüfung, Entscheidung, Herbstmeldung.</summary>
[Collection(AnwendungCollection.Name)]
public partial class LehrgangswunschTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, Guid? benutzer, string rolle)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        if (benutzer is { } id)
        {
            client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, id.ToString());
        }

        return client;
    }

    [Fact]
    public async Task Mitglied_wuenscht_Gruppenfuehrer_ohne_Truppfuehrer_Kommandant_meldet_und_meldet_an()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var jahr = heute.Year + 2;
        var nachname = "Wunsch" + Guid.NewGuid().ToString("N")[..6];
        Guid mitgliedId, kontoId, gf;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            gf = (await db.Qualifikationen.SingleAsync(q => q.Code == Qualifikation.CodeGruppenfuehrer)).Id;
            var m = new Mitglied { Personalnummer = "LW-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Lena", Nachname = nachname, Geburtsdatum = new(1995, 4, 4) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2015, 1, 1) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            mitgliedId = m.Id;
            var email = $"l-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
            var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(konto)).Succeeded);
            kontoId = konto.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var mitglied = Angemeldet(factory, kontoId, Rollen.Mitglied);
        var seite = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Lehrgaenge"));
        Assert.Contains("Gruppenführer (Voraussetzung fehlt)", seite);

        Task<HttpResponseMessage> Wuenschen() => mitglied.PostAsync("/Konto/Lehrgaenge?handler=Eintragen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite), ["QualifikationId"] = gf.ToString(), ["Jahr"] = jahr.ToString(), ["Begruendung"] = "TF im Frühjahr",
        }));
        Assert.Equal(HttpStatusCode.Redirect, (await Wuenschen()).StatusCode);
        Assert.Contains("schon einen offenen Wunsch", WebUtility.HtmlDecode(await (await Wuenschen()).Content.ReadAsStringAsync()));

        // Das Kommando sieht die fehlende Voraussetzung, meldet an und gibt die Herbstmeldung aus.
        var kommandant = Angemeldet(factory, null, Rollen.Kommandant);
        var liste = WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Personal/Lehrgangswuensche?jahr={jahr}"));
        Assert.Contains($"{nachname}, Lena", liste);
        Assert.Contains("fehlt: Truppführer", liste);
        Assert.Contains("TF im Frühjahr", liste);

        var csv = await kommandant.GetStringAsync($"/Personal/Lehrgangswuensche?handler=Csv&jahr={jahr}");
        Assert.Contains($";Gruppenführer;Landesfeuerwehrschule;{nachname};Lena;04.04.1995;nein: Truppführer;gewünscht", csv);
        Assert.DoesNotContain("TF im Frühjahr", csv);

        Guid wunschId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            wunschId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Lehrgangswuensche.SingleAsync(w => w.MitgliedId == mitgliedId)).Id;
        }

        Assert.Equal(HttpStatusCode.Redirect, (await kommandant.PostAsync($"/Personal/Lehrgangswuensche?handler=Stand&id={wunschId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(liste), ["status"] = nameof(WunschStatus.Angemeldet), ["prioritaet"] = "1", ["bemerkung"] = "",
        }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await kommandant.PostAsync($"/Personal/Lehrgangswuensche?handler=Gemeldet&jahr={jahr}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(liste),
        }))).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wunsch = await db.Lehrgangswuensche.Include(w => w.Teilnahme).SingleAsync(w => w.Id == wunschId);
            Assert.Equal(WunschStatus.Angemeldet, wunsch.Status);
            Assert.Equal(1, wunsch.Prioritaet);
            Assert.Equal(heute, wunsch.GemeldetAm);
            Assert.Equal(heute, wunsch.Teilnahme?.AngemeldetAm);
            Assert.Equal(gf, wunsch.Teilnahme?.QualifikationId);
        }

        // Angemeldet lässt sich nicht mehr selbst zurückziehen; Kassier und Führungskraft sehen die Liste nicht.
        var eigene = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Lehrgaenge"));
        Assert.Contains("angemeldet", eigene);
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.PostAsync($"/Konto/Lehrgaenge?handler=Zurueckziehen&id={wunschId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(eigene),
        }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, null, Rollen.Kassier).GetAsync("/Personal/Lehrgangswuensche")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, null, Rollen.Fuehrungskraft).GetAsync("/Personal/Lehrgangswuensche")).StatusCode);
    }
}
