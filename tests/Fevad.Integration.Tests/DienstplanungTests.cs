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
            Abteilungen = abteilungCode is null ? [] : [await db.Abteilungen.SingleAsync(a => a.Code == abteilungCode)],
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
        var dienst = await DienstAnlegenAsync("Ohne Rechte " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(9));
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Dienste/Anwesenheit/{dienst.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Dienste/Meldungen/{dienst.Id}")).StatusCode);
    }

    [Fact]
    public async Task Absage_steht_gleich_als_entschuldigt_und_eine_Zusage_nimmt_das_zurueck()
    {
        var (mitgliedId, benutzerId) = await MitgliedMitKontoAsync("EA", "Wechsel");
        var dienst = await DienstAnlegenAsync("Entschuldigt " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(13));
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        var ziel = $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden";

        await AbschickenAsync(mitglied, "/Termine", ziel, new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = await db.Anwesenheiten.SingleAsync(a => a.DienstId == dienst.Id && a.MitgliedId == mitgliedId);
            Assert.Equal((AnwesenheitStatus.Entschuldigt, ErfassungsWeg.Rueckmeldung), (a.Status, a.ErfasstUeber));
        }

        await AbschickenAsync(mitglied, "/Termine", ziel, new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Anwesenheiten.AnyAsync(a => a.DienstId == dienst.Id && a.MitgliedId == mitgliedId));
        }
    }

    [Fact]
    public async Task Elternkonto_sieht_die_Termine_des_Kindes_und_meldet_dafuer()
    {
        var name = "Kind" + Guid.NewGuid().ToString("N")[..6];
        var email = $"eltern-{name}@ff-adelberg.test".ToLowerInvariant();
        var heute = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid kind;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jf = await db.Abteilungen.SingleAsync(a => a.Code == "JF");
            var mitglied = new Mitglied { Personalnummer = "P-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Lena", Nachname = name, Geburtsdatum = heute.AddYears(-14) };
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf.Id, Status = MitgliedStatus.Aktiv, GueltigAb = heute.AddYears(-2) });
            mitglied.Erziehungsberechtigte.Add(new Erziehungsberechtigter { Nummer = 1, Vorname = "Petra", Nachname = name, Email = email });
            db.Mitglieder.Add(mitglied);
            await db.SaveChangesAsync();
            kind = mitglied.Id;
        }

        var jfDienst = await DienstAnlegenAsync("JF-Dienst " + name, "JF", InTagen(3));
        var eaDienst = await DienstAnlegenAsync("EA-Dienst " + name, "EA", InTagen(3));

        // Der Kommandant schaltet das Elternkonto in den Stammdaten des Kindes frei.
        var kommandant = Angemeldet(null, Rollen.Kommandant);
        var seite = $"/Mitglieder/Bearbeiten/{kind}";
        var html = await kommandant.GetStringAsync(seite);
        var felder = new Dictionary<string, string>
        {
            ["Formular.Id"] = kind.ToString(), ["Formular.Version"] = Regex.Match(html, "name=\"Formular.Version\" value=\"(\\d+)\"").Groups[1].Value,
            ["Formular.Personalnummer"] = Regex.Match(html, "name=\"Formular.Personalnummer\" value=\"([^\"]+)\"").Groups[1].Value,
            ["Formular.Vorname"] = "Lena", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = heute.AddYears(-14).ToString("yyyy-MM-dd"),
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Petra", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
            ["Formular.Erziehungsberechtigte[0].Email"] = email, ["Formular.Erziehungsberechtigte[0].Elternkonto"] = "true",
        };

        // Ohne bestätigte Datenschutzinformation gibt es kein Konto.
        Assert.Equal(HttpStatusCode.OK, (await AbschickenAsync(kommandant, seite, seite, felder)).StatusCode);
        felder["Formular.Erziehungsberechtigte[0].DatenschutzinformationErhalten"] = "true";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, seite, felder)).StatusCode);

        Guid konto;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eltern = await db.Erziehungsberechtigte.SingleAsync(e => e.MitgliedId == kind);
            Assert.NotNull(eltern.KontoId);
            Assert.NotNull(eltern.DatenschutzinformationAm);
            konto = eltern.KontoId.Value;
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var user = await benutzer.FindByIdAsync(konto.ToString());
            Assert.Equal(email, user!.Email);
            Assert.Contains(Rollen.Erziehungsberechtigt, await benutzer.GetRolesAsync(user));
        }

        // Das Elternkonto sieht nur den JF-Termin, meldet für das Kind und kommt nicht an Mitgliederdaten oder Dokumente.
        var elternteil = Angemeldet(konto, Rollen.Erziehungsberechtigt);
        var termine = await elternteil.GetStringAsync("/Termine");
        Assert.Contains(jfDienst.Titel, termine);
        Assert.DoesNotContain(eaDienst.Titel, termine);
        Assert.Contains("Für Lena", termine);
        Assert.Equal(HttpStatusCode.Forbidden, (await elternteil.GetAsync("/Mitglieder")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elternteil.GetAsync($"/Mitglieder/Akte/{kind}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await elternteil.GetAsync("/Dokumente")).StatusCode);

        var ziel = $"/Termine/Termin/{jfDienst.Id}?handler=Rueckmelden&mitgliedId={kind}";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(elternteil, "/Termine", ziel, new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(elternteil, "/Termine", $"/Termine/Termin/{eaDienst.Id}?handler=Rueckmelden&mitgliedId={kind}",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) })).StatusCode);
        var fremd = await MitgliedMitKontoAsync("JF", "Fremd" + name);
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(elternteil, "/Termine", $"/Termine/Termin/{jfDienst.Id}?handler=Rueckmelden&mitgliedId={fremd.MitgliedId}",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(RueckmeldungAntwort.Zusage, (await db.Rueckmeldungen.SingleAsync(r => r.DienstId == jfDienst.Id && r.MitgliedId == kind)).Antwort);
            Assert.Contains("zugesagt", await elternteil.GetStringAsync("/Termine"));

            // Endet die Jugendzeit (mit 18 löscht L15 den Erziehungsberechtigten gleich ganz), trennt der Löschlauf die
            // Zuordnung und deaktiviert das verwaiste Elternkonto (L35).
            await db.Mitgliedschaften.Where(m => m.MitgliedId == kind).ExecuteUpdateAsync(s => s.SetProperty(m => m.GueltigBis, heute.AddDays(-1)));
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Fevad.Infrastructure.Datenschutz.Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Fevad.Infrastructure.Datenschutz.Loeschlauf.RegelElternzuordnung).Anzahl >= 1);
            Assert.Null((await db.Erziehungsberechtigte.SingleAsync(e => e.MitgliedId == kind)).KontoId);
            Assert.NotNull((await db.Users.SingleAsync(u => u.Id == konto)).DeaktiviertAm);
        }
    }

    [Fact]
    public async Task Gemeinsame_Uebung_mehrerer_Abteilungen_wird_geplant_und_erreicht_beide()
    {
        var (_, jugendKonto) = await MitgliedMitKontoAsync("JF", "Gemeinsam");
        var (_, altersKonto) = await MitgliedMitKontoAsync("AA", "Senior");
        var leiter = await MitgliedMitKontoAsync("EA", "Leitet");
        var dienst = await DienstAnlegenAsync("Gemeinsam " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(1));
        var kommandant = Angemeldet(null, Rollen.Kommandant);
        var seite = $"/Dienste/Bearbeiten/{dienst.Id}";

        Guid ea, jf;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ea = (await db.Abteilungen.SingleAsync(a => a.Code == "EA")).Id;
            jf = (await db.Abteilungen.SingleAsync(a => a.Code == "JF")).Id;
        }

        var felder = new List<KeyValuePair<string, string>>
        {
            new("Formular.Titel", dienst.Titel), new("Formular.Art", nameof(DienstArt.Uebung)),
            new("Formular.Datum", Fevad.Web.Infrastruktur.Anzeige.LokalesDatum(dienst.Beginn).ToString("yyyy-MM-dd")),
            new("Formular.Beginn", "19:30"), new("Formular.Ende", "21:30"),
            new("Formular.AbteilungIds", ea.ToString()), new("Formular.AbteilungIds", jf.ToString()),
            new("Formular.VerantwortlicherId", leiter.MitgliedId.ToString()),
            new("Formular.AlsDienst", "true"), new("Formular.RueckmeldungErbeten", "true"), new("Formular.ZaehltFuerFortbildung", "true"),
            new("Status", nameof(DienstStatus.Geplant)), new("Abgeschlossen", "true"),
            new("__RequestVerificationToken", await TokenAsync(kommandant, seite)),
        };
        var antwort = await kommandant.PostAsync(seite, new FormUrlEncodedContent(felder));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gespeichert = await db.Dienste.Include(d => d.Abteilungen).SingleAsync(d => d.Id == dienst.Id);
            Assert.Equal(new[] { ea, jf }.Order(), gespeichert.Abteilungen.Select(a => a.Id).Order());
            Assert.Equal(leiter.MitgliedId, gespeichert.VerantwortlicherId);
            Assert.True(gespeichert.Abgeschlossen);
        }

        Assert.Contains(dienst.Titel, await Angemeldet(jugendKonto, Rollen.Mitglied).GetStringAsync("/Termine"));
        Assert.DoesNotContain(dienst.Titel, await Angemeldet(altersKonto, Rollen.Mitglied).GetStringAsync("/Termine"));

        // In der Dienstliste öffnet ein Klick für Bearbeiter gleich das Bearbeiten, für Mitglieder die Terminansicht.
        Assert.Contains(seite, await kommandant.GetStringAsync("/Dienste"));
        var liste = await Angemeldet(jugendKonto, Rollen.Mitglied).GetStringAsync("/Dienste");
        Assert.DoesNotContain(seite, liste);
        Assert.Contains($"/Termine/Termin/{dienst.Id}", liste);

        // Die Führungskraft ohne Zugang zur Jugendfeuerwehr darf die gemeinsame Übung nicht ändern.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Fuehrungskraft).GetAsync(seite)).StatusCode);
    }

    [Fact]
    public async Task Uebungsleiter_setzt_alle_ohne_Rueckmeldung_nach_dem_Dienst_auf_unentschuldigt()
    {
        var (leiterId, leiterKonto) = await MitgliedMitKontoAsync("EA", "Uebungsleiter");
        var (offenId, offenKonto) = await MitgliedMitKontoAsync("EA", "Vergessen");
        var (anderesId, _) = await MitgliedMitKontoAsync("EA", "Auch");
        var dienst = await DienstAnlegenAsync("Nachtrag " + Guid.NewGuid().ToString("N")[..6], "EA", DateTimeOffset.UtcNow.AddHours(-3));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Dienste.SingleAsync(d => d.Id == dienst.Id)).VerantwortlicherId = leiterId;
            await db.SaveChangesAsync();
        }

        // Selbst ist nach dem Beginn keine Rückmeldung mehr möglich.
        await AbschickenAsync(Angemeldet(offenKonto, Rollen.Mitglied), "/Termine", $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });

        var leiter = Angemeldet(leiterKonto, Rollen.Mitglied);
        var seite = $"/Dienste/Meldungen/{dienst.Id}";
        var html = await leiter.GetStringAsync(seite);
        Assert.Contains("Vergessen, Test", html);
        Assert.Contains("data-offen=\"ja\"", html);

        var felder = new List<KeyValuePair<string, string>>
        {
            new("markiert", offenId.ToString()), new("markiert", anderesId.ToString()),
            new("meldung", nameof(Fevad.Infrastructure.Dienstbetrieb.Dienstplanung.Sammelmeldung.Unentschuldigt)),
            new("__RequestVerificationToken", await TokenAsync(leiter, seite)),
        };
        var antwort = await leiter.PostAsync($"{seite}?handler=Zuordnen", new FormUrlEncodedContent(felder));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Rueckmeldungen.AnyAsync(r => r.DienstId == dienst.Id && r.MitgliedId == offenId));
            var eintraege = await db.Anwesenheiten.Where(a => a.DienstId == dienst.Id).ToDictionaryAsync(a => a.MitgliedId);
            Assert.Equal(AnwesenheitStatus.Unentschuldigt, eintraege[offenId].Status);
            Assert.Equal(AnwesenheitStatus.Unentschuldigt, eintraege[anderesId].Status);
            Assert.Equal(ErfassungsWeg.Web, eintraege[offenId].ErfasstUeber);
            Assert.Equal(DienstStatus.Durchgefuehrt, (await db.Dienste.SingleAsync(d => d.Id == dienst.Id)).Status);
        }
    }

    [Fact]
    public async Task Nur_Termin_ohne_Dienst_bekommt_keinen_Anwesenheitseintrag()
    {
        var (mitgliedId, benutzerId) = await MitgliedMitKontoAsync("EA", "Kameradschaft");
        var dienst = await DienstAnlegenAsync("Grillfest " + Guid.NewGuid().ToString("N")[..6], "EA", InTagen(14));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Dienste.SingleAsync(d => d.Id == dienst.Id)).AlsDienst = false;
            await db.SaveChangesAsync();
        }

        await AbschickenAsync(Angemeldet(benutzerId, Rollen.Mitglied), "/Termine", $"/Termine/Termin/{dienst.Id}?handler=Rueckmelden",
            new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });

        await using (var pruefung = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = pruefung.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Rueckmeldungen.AnyAsync(r => r.DienstId == dienst.Id && r.MitgliedId == mitgliedId));
            Assert.False(await db.Anwesenheiten.AnyAsync(a => a.DienstId == dienst.Id));
        }

        var fuehrung = Angemeldet(null, Rollen.Fuehrungskraft);
        Assert.DoesNotContain(dienst.Titel, await fuehrung.GetStringAsync("/Dienste"));
        Assert.Contains(dienst.Titel, await fuehrung.GetStringAsync("/Dienste?alletermine=true"));
    }
}
