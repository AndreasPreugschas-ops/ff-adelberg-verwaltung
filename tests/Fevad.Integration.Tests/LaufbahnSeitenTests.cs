using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class LaufbahnSeitenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.UtcNow);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private async Task<Guid> MitgliedAnlegenAsync(string nachname, DateOnly geburtsdatum, string abteilungCode, MitgliedStatus status, DateOnly ab)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == abteilungCode);
        var mitglied = new Mitglied
        {
            Personalnummer = "L" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = nachname, Geburtsdatum = geburtsdatum,
        };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = status, GueltigAb = ab });
        db.Mitglieder.Add(mitglied);
        await db.SaveChangesAsync();
        return mitglied.Id;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, string handler, Dictionary<string, string> felder)
    {
        var seite = await client.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = TokenFeld().Match(seite).Groups[1].Value;
        return await client.PostAsync($"{pfad}?handler={handler}", new FormUrlEncodedContent(felder));
    }

    [Fact]
    public async Task Jugendwart_sieht_nur_die_Jugendfeuerwehr()
    {
        var name = "Jw" + Guid.NewGuid().ToString("N")[..6];
        var jugend = await MitgliedAnlegenAsync(name + "Jugend", Heute.AddYears(-14), AbteilungCode.Jugend, MitgliedStatus.Aktiv, Heute.AddYears(-2));
        var einsatz = await MitgliedAnlegenAsync(name + "Einsatz", Heute.AddYears(-30), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-10));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Jugendwart);

        var liste = await client.GetStringAsync($"/Mitglieder?Suche={name}");
        Assert.Contains(name + "Jugend", liste);
        Assert.DoesNotContain(name + "Einsatz", liste);

        var abteilung = await client.GetStringAsync("/Abteilungen?code=EA");
        Assert.Contains(name + "Jugend", abteilung);
        Assert.DoesNotContain(name + "Einsatz", abteilung);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Mitglieder/Bearbeiten/{jugend}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Mitglieder/Bearbeiten/{einsatz}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Laufbahn")).StatusCode);
    }

    [Fact]
    public async Task Fuehrungskraft_sieht_alle_darf_aber_nicht_bearbeiten()
    {
        var name = "Fk" + Guid.NewGuid().ToString("N")[..6];
        var einsatz = await MitgliedAnlegenAsync(name, Heute.AddYears(-30), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-10));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Fuehrungskraft);

        Assert.Contains(name, await client.GetStringAsync($"/Mitglieder?Suche={name}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Mitglieder/Bearbeiten/{einsatz}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Laufbahn/Mitglied/{einsatz}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Laufbahn/Regeln")).StatusCode);
    }

    [Fact]
    public async Task Kommandant_fuehrt_Uebertritt_aus_Jugendfeuerwehr_durch()
    {
        var name = "Ue" + Guid.NewGuid().ToString("N")[..6];
        var id = await MitgliedAnlegenAsync(name, Heute.AddYears(-18).AddDays(20), AbteilungCode.Jugend, MitgliedStatus.Aktiv, Heute.AddYears(-5));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        var uebersicht = await client.GetStringAsync("/Laufbahn");
        Assert.Contains(name, uebersicht);

        Guid jugendId, einsatzAbteilung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            jugendId = (await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id)).Id;
            einsatzAbteilung = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id;
        }

        var antwort = await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Uebertritt", new()
        {
            ["mitgliedschaftId"] = jugendId.ToString(),
            ["zielAbteilungId"] = einsatzAbteilung.ToString(),
            ["status"] = nameof(MitgliedStatus.Probezeit),
            ["ab"] = Heute.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abschnitte = await db.Mitgliedschaften.Include(m => m.Abteilung).Where(m => m.MitgliedId == id).OrderBy(m => m.GueltigAb).ToListAsync();
            Assert.Equal(2, abschnitte.Count);
            Assert.Equal((AbteilungCode.Jugend, Heute.AddDays(-1), Austrittsgrund.Uebertritt), (abschnitte[0].Abteilung!.Code, abschnitte[0].GueltigBis!.Value, abschnitte[0].Austrittsgrund!.Value));
            Assert.Equal((AbteilungCode.Einsatz, MitgliedStatus.Probezeit, (DateOnly?)null), (abschnitte[1].Abteilung!.Code, abschnitte[1].Status, abschnitte[1].GueltigBis));
        }

        // Probezeit bestehen: gleicher Tag geht nicht (Beginn = Ende), einen Tag später schon.
        Guid probeId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            probeId = (await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id && m.GueltigBis == null)).Id;
        }

        await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Probezeit", new() { ["mitgliedschaftId"] = probeId.ToString(), ["ab"] = Heute.AddDays(1).ToString("yyyy-MM-dd") });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var offen = await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id && m.GueltigBis == null);
            Assert.Equal((MitgliedStatus.Aktiv, Heute.AddDays(1)), (offen.Status, offen.GueltigAb));
        }
    }

    [Fact]
    public async Task Zu_junges_Mitglied_kommt_nicht_in_die_Einsatzabteilung()
    {
        var id = await MitgliedAnlegenAsync("Jung" + Guid.NewGuid().ToString("N")[..6], Heute.AddYears(-15), AbteilungCode.Jugend, MitgliedStatus.Aktiv, Heute.AddYears(-3));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        Guid jugendId, einsatzAbteilung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            jugendId = (await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id)).Id;
            einsatzAbteilung = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id;
        }

        await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Uebertritt", new()
        {
            ["mitgliedschaftId"] = jugendId.ToString(), ["zielAbteilungId"] = einsatzAbteilung.ToString(),
            ["status"] = nameof(MitgliedStatus.Probezeit), ["ab"] = Heute.ToString("yyyy-MM-dd"),
        });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null((await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id)).GueltigBis);
        }
    }

    [Fact]
    public async Task Faellige_Ehrung_erscheint_in_der_Erinnerung_bis_sie_erfasst_ist()
    {
        var name = "Eh" + Guid.NewGuid().ToString("N")[..6];
        var id = await MitgliedAnlegenAsync(name, Heute.AddYears(-50), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-25).AddDays(10));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var abfrage = scope.ServiceProvider.GetRequiredService<Laufbahnabfrage>();
            var hinweise = await abfrage.FaelligeAsync(Heute, CancellationToken.None);
            var ehrung = Assert.Single(hinweise, h => h.Gegenstand.StartsWith(name) && h.Bezeichnung.Contains("Silber"));
            Assert.Equal(("Ehrungen", Berechtigungen.ErinnerungLaufbahn), (ehrung.Bereich, ehrung.Empfaenger));
        }

        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        Guid silber;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            silber = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Ehrungsarten.SingleAsync(e => e.Code == "FEZ25")).Id;
        }

        var antwort = await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Ehrung", new()
        {
            ["ehrungsartId"] = silber.ToString(), ["status"] = nameof(EhrungStatus.Beantragt),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var hinweise = await scope.ServiceProvider.GetRequiredService<Laufbahnabfrage>().FaelligeAsync(Heute, CancellationToken.None);
            Assert.DoesNotContain(hinweise, h => h.Gegenstand.StartsWith(name) && h.Bezeichnung.Contains("Silber"));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(Heute, (await db.Ehrungen.SingleAsync(e => e.MitgliedId == id)).BeantragtAm);
        }

        Assert.Contains(name, await client.GetStringAsync($"/Laufbahn/Ehrungen?jahr={Heute.Year}"));
    }

    [Fact]
    public async Task Regeln_und_Abteilungsseiten_laden()
    {
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        var regeln = await client.GetStringAsync("/Laufbahn/Regeln");
        Assert.Contains("Feuerwehr-Ehrenzeichen in Bronze", regeln);
        Assert.Contains("Weg B", regeln);

        foreach (var code in new[] { AbteilungCode.Einsatz, AbteilungCode.Jugend, AbteilungCode.Alters })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Abteilungen?code={code}")).StatusCode);
        }
    }
}
