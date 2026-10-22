using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class DienstplanungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

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

    private static async Task<string> TokenAsync(HttpClient client, string seite)
    {
        var html = await client.GetStringAsync(seite);
        return TokenFeld().Match(html).Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string ziel, Dictionary<string, string> felder, bool htmx = false)
    {
        felder["__RequestVerificationToken"] = await TokenAsync(client, seite);
        var anfrage = new HttpRequestMessage(HttpMethod.Post, ziel) { Content = new FormUrlEncodedContent(felder) };
        if (htmx)
        {
            anfrage.Headers.Add("HX-Request", "true");
        }

        return await client.SendAsync(anfrage);
    }

    /// <summary>Legt ein Mitglied der Abteilung mit verknüpftem Konto an und gibt beide IDs zurück.</summary>
    private async Task<(Guid MitgliedId, Guid BenutzerId)> MitgliedMitKontoAsync(string abteilungCode, string nachname)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == abteilungCode);
        var mitglied = new Mitglied { Personalnummer = "P-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = nachname, Geburtsdatum = new(1990, 1, 1) };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2020, 1, 1) });
        db.Mitglieder.Add(mitglied);
        await db.SaveChangesAsync();

        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"m-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = mitglied.Id };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        Assert.True((await benutzer.AddToRoleAsync(konto, Rollen.Mitglied)).Succeeded);
        return (mitglied.Id, konto.Id);
    }

    private async Task<Dienst> DienstAnlegenAsync(string titel, string? abteilungCode, DateTimeOffset beginn)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dienst = new Dienst
        {
            Titel = titel,
            Beginn = beginn,
            Ende = beginn.AddHours(2),
            Status = DienstStatus.Geplant,
            AbteilungId = abteilungCode is null ? null : (await db.Abteilungen.SingleAsync(a => a.Code == abteilungCode)).Id,
        };
        db.Dienste.Add(dienst);
        await db.SaveChangesAsync();
        return dienst;
    }

    private static DateTimeOffset InTagen(int tage) =>
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(tage).AddHours(17), TimeSpan.Zero);

    [Fact]
    public async Task Serie_legt_alle_Termine_mit_gleicher_Ortszeit_an()
    {
        var titel = "Serie " + Guid.NewGuid().ToString("N")[..6];
        var fuehrung = Angemeldet(null, Rollen.Fuehrungskraft);

        // Über die Zeitumstellung am 26.10.2031 hinweg: 19:30 Ortszeit bleibt 19:30.
        var antwort = await AbschickenAsync(fuehrung, "/Dienste", "/Dienste", new()
        {
            ["Neu.Titel"] = titel,
            ["Neu.Art"] = nameof(DienstArt.Uebung),
            ["Neu.Datum"] = "2031-10-16",
            ["Neu.Beginn"] = "19:30",
            ["Neu.Ende"] = "21:30",
            ["Neu.Ort"] = "Gerätehaus",
            ["Neu.RueckmeldungErbeten"] = "true",
            ["Neu.ZaehltFuerFortbildung"] = "true",
            ["Wiederholung"] = nameof(Wiederholung.Zweiwoechentlich),
            ["WiederholenBis"] = "2031-11-27",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var termine = await db.Dienste.Where(d => d.Titel == titel).OrderBy(d => d.Beginn).ToListAsync();
        Assert.Equal(4, termine.Count);
        Assert.Single(termine.Select(d => d.SerieId).Distinct());
        Assert.Equal(new DateTimeOffset(2031, 10, 16, 17, 30, 0, TimeSpan.Zero), termine[0].Beginn);
        Assert.Equal(new DateTimeOffset(2031, 10, 30, 18, 30, 0, TimeSpan.Zero), termine[1].Beginn);
        Assert.All(termine, d => Assert.Equal(TimeSpan.FromHours(2), d.Ende - d.Beginn));
    }

    [Fact]
    public async Task Absage_und_Verschiebung_gelten_fuer_die_folgenden_Termine()
    {
        var titel = "Folge " + Guid.NewGuid().ToString("N")[..6];
        var fuehrung = Angemeldet(null, Rollen.Fuehrungskraft);
        var felder = new Dictionary<string, string>
        {
            ["Neu.Titel"] = titel, ["Neu.Art"] = nameof(DienstArt.Uebung), ["Neu.Datum"] = "2032-03-04",
            ["Neu.Beginn"] = "19:30", ["Neu.Ende"] = "21:30", ["Neu.RueckmeldungErbeten"] = "true", ["Neu.ZaehltFuerFortbildung"] = "true",
            ["Wiederholung"] = nameof(Wiederholung.Woechentlich), ["WiederholenBis"] = "2032-03-25",
        };
        await AbschickenAsync(fuehrung, "/Dienste", "/Dienste", felder);

        List<Dienst> termine;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            termine = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dienste.AsNoTracking()
                .Where(d => d.Titel == titel).OrderBy(d => d.Beginn).ToListAsync();
        }

        // Den zweiten Termin (11.03.) um zehn Tage verschieben und absagen: der dritte und vierte zählen trotzdem als folgend.
        var seite = $"/Dienste/Bearbeiten/{termine[1].Id}";
        var antwort = await AbschickenAsync(fuehrung, seite, seite, new()
        {
            ["Formular.Titel"] = titel, ["Formular.Art"] = nameof(DienstArt.Uebung), ["Formular.Datum"] = "2032-03-21",
            ["Formular.Beginn"] = "19:00", ["Formular.Ende"] = "21:00", ["Formular.RueckmeldungErbeten"] = "true",
            ["Formular.ZaehltFuerFortbildung"] = "true", ["Status"] = nameof(DienstStatus.Abgesagt), ["FolgendeAnpassen"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var nachher = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dienste.AsNoTracking()
                .Where(d => d.Titel == titel).OrderBy(d => d.Id).ToDictionaryAsync(d => d.Id);
            Assert.Equal(DienstStatus.Geplant, nachher[termine[0].Id].Status);
            Assert.All(termine.Skip(1), t => Assert.Equal(DienstStatus.Abgesagt, nachher[t.Id].Status));
            Assert.Equal(new DateTimeOffset(2032, 3, 25, 18, 0, 0, TimeSpan.Zero), nachher[termine[3].Id].Beginn);
        }
    }

    [Fact]
    public async Task Mitglied_sagt_zu_und_ab_und_Fuehrung_sieht_die_Rueckmeldungen()
    {
        var (mitgliedId, benutzerId) = await MitgliedMitKontoAsync("EA", "Zusager");
        var dienst = await DienstAnlegenAsync("Uebung Zusage " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(10));
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);

        var meine = await mitglied.GetStringAsync("/Termine");
        Assert.Contains(dienst.Titel, meine);

        var zusage = await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) }, htmx: true);
        Assert.Equal(HttpStatusCode.OK, zusage.StatusCode);
        Assert.Contains("Du hast zugesagt", await zusage.Content.ReadAsStringAsync());

        var absage = await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage), ["begruendung"] = "Nachtschicht" }, htmx: true);
        Assert.Contains("Du hast abgesagt", await absage.Content.ReadAsStringAsync());

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var r = await db.Rueckmeldungen.SingleAsync(r => r.DienstId == dienst.Id);
            Assert.Equal((mitgliedId, RueckmeldungAntwort.Absage, "Nachtschicht"), (r.MitgliedId, r.Antwort, r.Begruendung));
        }

        var uebersicht = await Angemeldet(null, Rollen.Fuehrungskraft).GetStringAsync($"/Termine/Termin/{dienst.Id}");
        Assert.Contains("Zusager, Test", uebersicht);
        Assert.Contains("Nachtschicht", uebersicht);
    }

    [Fact]
    public async Task Wer_nicht_zur_Zielgruppe_gehoert_kann_nicht_zusagen()
    {
        var (_, benutzerId) = await MitgliedMitKontoAsync("JF", "Jugend");
        var dienst = await DienstAnlegenAsync("Uebung EA " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(11));
        var jugend = Angemeldet(benutzerId, Rollen.Mitglied);
        var eigener = await DienstAnlegenAsync("Uebung JF " + Guid.NewGuid().ToString("N")[..6], "JF", InTagen(11));

        var meine = await jugend.GetStringAsync("/Termine");
        Assert.Contains(eigener.Titel, meine);
        Assert.DoesNotContain(dienst.Titel, meine);
        var antwort = await AbschickenAsync(jugend, "/Termine", $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) }, htmx: true);
        Assert.Equal(HttpStatusCode.Forbidden, antwort.StatusCode);
    }

    [Fact]
    public async Task Rueckmeldungen_werden_in_die_Anwesenheit_uebernommen()
    {
        var (zusagerId, zusagerKonto) = await MitgliedMitKontoAsync("EA", "Kommt");
        var (absagerId, absagerKonto) = await MitgliedMitKontoAsync("EA", "Fehlt");
        var dienst = await DienstAnlegenAsync("Uebernahme " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(12));
        var ziel = $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden";
        await AbschickenAsync(Angemeldet(zusagerKonto, Rollen.Mitglied), "/Termine", ziel, new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) });
        await AbschickenAsync(Angemeldet(absagerKonto, Rollen.Mitglied), "/Termine", ziel, new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });

        var fuehrung = Angemeldet(null, Rollen.Fuehrungskraft);
        var seite = $"/Dienste/Anwesenheit/{dienst.Id}";
        Assert.Contains("hat zugesagt", await fuehrung.GetStringAsync(seite));
        var antwort = await AbschickenAsync(fuehrung, seite, $"{seite}?handler=Uebernehmen", []);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var anwesenheit = await db.Anwesenheiten.Where(a => a.DienstId == dienst.Id).ToDictionaryAsync(a => a.MitgliedId);
        Assert.Equal(AnwesenheitStatus.Anwesend, anwesenheit[zusagerId].Status);
        Assert.Equal(AnwesenheitStatus.Entschuldigt, anwesenheit[absagerId].Status);
        Assert.All(anwesenheit.Values, a => Assert.Equal(ErfassungsWeg.Rueckmeldung, a.ErfasstUeber));
    }

    [Fact]
    public async Task Kalender_Abo_liefert_nur_eigene_Termine_ohne_Anmeldung()
    {
        var (_, benutzerId) = await MitgliedMitKontoAsync("EA", "Abo");
        var eigener = await DienstAnlegenAsync("Abo EA " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(13));
        var fremder = await DienstAnlegenAsync("Abo JF " + Guid.NewGuid().ToString("N")[..6], "JF", InTagen(13));
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);

        var antwort = await AbschickenAsync(mitglied, "/Termine", "/Termine?handler=Abo", []);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var link = Regex.Match(await mitglied.GetStringAsync("/Termine"), "/kalender/([0-9a-f]+)\\.ics").Value;
        Assert.NotEmpty(link);

        var anonym = fixture.Factory.CreateClient();
        var ics = await anonym.GetAsync(link);
        Assert.Equal(HttpStatusCode.OK, ics.StatusCode);
        Assert.Equal("text/calendar", ics.Content.Headers.ContentType!.MediaType);
        var inhalt = await ics.Content.ReadAsStringAsync();
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", inhalt);
        Assert.Contains($"UID:{eigener.Id}@fevad", inhalt);
        Assert.DoesNotContain(fremder.Id.ToString(), inhalt);

        Assert.Equal(HttpStatusCode.NotFound, (await anonym.GetAsync("/kalender/" + new string('0', 48) + ".ics")).StatusCode);
    }

    [Fact]
    public async Task Kalender_Abo_ohne_verknuepftes_Mitglied_liefert_nichts()
    {
        var (_, benutzerId) = await MitgliedMitKontoAsync("EA", "Entknuepft");
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        await AbschickenAsync(mitglied, "/Termine", "/Termine?handler=Abo", []);
        var link = Regex.Match(await mitglied.GetStringAsync("/Termine"), "/kalender/([0-9a-f]+)\\.ics").Value;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var konto = (await benutzer.FindByIdAsync(benutzerId.ToString()))!;
            konto.MitgliedId = null;
            await benutzer.UpdateAsync(konto);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Factory.CreateClient().GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task Kalender_und_Termine_sind_fuer_Mitglieder_erreichbar()
    {
        var (_, benutzerId) = await MitgliedMitKontoAsync("EA", "Kalender");
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        Assert.Equal(HttpStatusCode.OK, (await mitglied.GetAsync("/Termine/Kalender")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mitglied.GetAsync("/Termine/Kalender?monat=2031-02")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Dienste/Anwesenheit/" + Guid.NewGuid())).StatusCode);
    }
}
