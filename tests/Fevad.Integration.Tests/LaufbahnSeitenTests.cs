using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
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
    public async Task Jugendwart_plant_und_erfasst_nur_Dienste_der_Jugendfeuerwehr()
    {
        var name = "JwD" + Guid.NewGuid().ToString("N")[..6];
        var jugend = await MitgliedAnlegenAsync(name, Heute.AddYears(-14), AbteilungCode.Jugend, MitgliedStatus.Aktiv, Heute.AddYears(-2));
        Guid jfDienst, eaDienst;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beginn = DateTimeOffset.UtcNow.AddDays(3);
            Dienst Neu(string code) => new()
            {
                Titel = $"{name} {code}", Beginn = beginn, Ende = beginn.AddHours(2), Status = DienstStatus.Geplant,
                AbteilungId = db.Abteilungen.Single(a => a.Code == code).Id,
            };
            var jf = Neu(AbteilungCode.Jugend);
            var ea = Neu(AbteilungCode.Einsatz);
            db.Dienste.AddRange(jf, ea);
            await db.SaveChangesAsync();
            (jfDienst, eaDienst) = (jf.Id, ea.Id);
        }

        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Jugendwart);
        Assert.Contains("Dienst oder Übung planen", await client.GetStringAsync("/Dienste"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Dienste/Bearbeiten/{jfDienst}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Dienste/Bearbeiten/{eaDienst}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Dienste/Anwesenheit/{eaDienst}")).StatusCode);

        var liste = await client.GetStringAsync($"/Dienste/Anwesenheit/{jfDienst}");
        Assert.Contains(name, liste);
        var antwort = await AbschickenAsync(client, $"/Dienste/Anwesenheit/{jfDienst}", "Umschalten", new() { ["mitgliedId"] = jugend.ToString() });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(AnwesenheitStatus.Anwesend, (await db.Anwesenheiten.SingleAsync(a => a.DienstId == jfDienst && a.MitgliedId == jugend)).Status);
        }
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
    public async Task Befreiung_und_Bestaetigung_65plus_wirken_auf_die_Anrechnung()
    {
        var name = "Bf" + Guid.NewGuid().ToString("N")[..6];
        var id = await MitgliedAnlegenAsync(name, Heute.AddYears(-40), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-10));
        var alt = await MitgliedAnlegenAsync(name + "Alt", Heute.AddYears(-66), AbteilungCode.Alters, MitgliedStatus.Aktiv, Heute.AddYears(-1));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        Guid einsatz, alters;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            einsatz = (await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == id)).Id;
            alters = (await db.Mitgliedschaften.SingleAsync(m => m.MitgliedId == alt)).Id;
        }

        var antwort = await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Befreiung", new()
        {
            ["mitgliedschaftId"] = einsatz.ToString(), ["grund"] = nameof(Befreiungsgrund.Kindererziehung),
            ["von"] = Heute.AddYears(-3).ToString("yyyy-MM-dd"), ["bis"] = Heute.AddYears(-1).AddDays(-1).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abschnitte = await db.Mitgliedschaften.Where(m => m.MitgliedId == id).OrderBy(m => m.GueltigAb).ToListAsync();
            Assert.Equal([MitgliedStatus.Aktiv, MitgliedStatus.Befreit, MitgliedStatus.Aktiv], abschnitte.Select(a => a.Status));
            Assert.Equal(Befreiungsgrund.Kindererziehung, abschnitte[1].Befreiungsgrund);
            Assert.Null(abschnitte[2].GueltigBis);
        }

        var seite = await client.GetStringAsync($"/Laufbahn/Mitglied/{id}");
        Assert.Contains("Kindererziehung", seite);
        Assert.Contains("nur 12 Monate angerechnet", seite);

        Assert.Contains("nicht angerechnet, weil keine Aufgaben", await client.GetStringAsync($"/Laufbahn/Mitglied/{alt}"));
        antwort = await AbschickenAsync(client, $"/Laufbahn/Mitglied/{alt}", "Bestaetigen", new() { ["mitgliedschaftId"] = alters.ToString(), ["bestaetigt"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.DoesNotContain("nicht angerechnet, weil keine Aufgaben", await client.GetStringAsync($"/Laufbahn/Mitglied/{alt}"));
    }

    [Fact]
    public async Task Verbandsehrung_mit_Vorstufe_und_Antragsschluss()
    {
        var name = "Vb" + Guid.NewGuid().ToString("N")[..6];
        var id = await MitgliedAnlegenAsync(name, Heute.AddYears(-45), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-20));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        Guid gold;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Ehrungsarten.Include(e => e.Vorstufe).SingleAsync(e => e.Code == "DFV-EKG");
            Assert.Equal("DFV-EKS", art.Vorstufe?.Code);
            Assert.True(art.MitAntragsschluss);
            gold = art.Id;
        }

        await AbschickenAsync(client, $"/Laufbahn/Mitglied/{id}", "Ehrung", new()
        {
            ["ehrungsartId"] = gold.ToString(), ["status"] = nameof(EhrungStatus.Vorgeschlagen),
        });
        Assert.Contains("die hier nicht als verliehen erfasst ist", await client.GetStringAsync($"/Laufbahn/Mitglied/{id}"));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var hinweise = await scope.ServiceProvider.GetRequiredService<Laufbahnabfrage>().FaelligeAsync(Heute, CancellationToken.None);
            Assert.Contains(hinweise, h => h.Gegenstand.StartsWith(name) && h.Bezeichnung.Contains("Antrag bis"));
        }

        var regeln = await client.GetStringAsync("/Laufbahn/Regeln");
        Assert.Contains("Traditionsnadel der Jugendfeuerwehr BW", regeln);
        Assert.Contains("Ehrenmedaille des LFV BW in Gold", regeln);
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

    [Fact]
    public async Task Befreiungsgrund_sehen_nur_Kommandant_und_Stellvertreter()
    {
        var id = await MitgliedAnlegenAsync("Bg" + Guid.NewGuid().ToString("N")[..6], Heute.AddYears(-35), AbteilungCode.Einsatz, MitgliedStatus.Aktiv, Heute.AddYears(-10));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Mitgliedschaften.Add(new Mitgliedschaft
            {
                MitgliedId = id, AbteilungId = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id,
                Status = MitgliedStatus.Befreit, Befreiungsgrund = Befreiungsgrund.Mutterschutz, GueltigAb = Heute.AddYears(-12), GueltigBis = Heute.AddYears(-11),
            });
            // Länger als die angerechnete Kindererziehungszeit: erzeugt einen Hinweis, der den Grund nennt.
            db.Mitgliedschaften.Add(new Mitgliedschaft
            {
                MitgliedId = id, AbteilungId = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id,
                Status = MitgliedStatus.Befreit, Befreiungsgrund = Befreiungsgrund.Kindererziehung, GueltigAb = Heute.AddYears(-16), GueltigBis = Heute.AddYears(-13),
            });
            await db.SaveChangesAsync();
        }

        var kommandant = await Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant).GetStringAsync($"/Laufbahn/Mitglied/{id}");
        Assert.Contains("(Mutterschutz)", kommandant);
        Assert.Contains("Kindererziehung ab", kommandant);

        var fuehrung = await Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Fuehrungskraft).GetStringAsync($"/Laufbahn/Mitglied/{id}");
        Assert.Contains("befreit", fuehrung);
        Assert.DoesNotContain("Mutterschutz", fuehrung);
        Assert.DoesNotContain("Kindererziehung", fuehrung);
        Assert.Contains("nur teilweise angerechnet", fuehrung);
    }
}
