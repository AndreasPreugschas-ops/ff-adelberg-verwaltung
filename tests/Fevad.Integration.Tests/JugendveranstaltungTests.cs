using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
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
using static System.Net.WebUtility;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class JugendveranstaltungTests(AnwendungFixture fixture)
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
        return await client.PostAsync(ziel, new FormUrlEncodedContent(felder));
    }

    /// <summary>Zeltlager der Jugendfeuerwehr in 30 Tagen über drei Tage, ein Kind mit Elternkonto und ein Betreuer ohne Zeugnis.</summary>
    private async Task<(Guid Dienst, Guid Kind, Guid Eltern, Guid Betreuer)> AnlegenAsync(string name)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var jf = await db.Abteilungen.SingleAsync(a => a.Code == "JF");
        var kind = new Mitglied
        {
            Personalnummer = "V-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Mia", Nachname = name, Geburtsdatum = Heute.AddYears(-12),
            Strasse = "Hauptstraße 1", Plz = "73099", Ort = "Adelberg",
        };
        kind.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
        db.Mitglieder.Add(kind);
        var email = $"v-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        db.Erziehungsberechtigte.Add(new Erziehungsberechtigter
        {
            MitgliedId = kind.Id, Nummer = 1, Vorname = "Paul", Nachname = name, KontoId = konto.Id, KontoZugeordnetAm = DateTimeOffset.UtcNow,
        });
        var beginn = Ortszeit.TagesbeginnUtc(Heute.AddDays(30)).AddHours(10);
        var dienst = new Dienst { Art = DienstArt.Zeltlager, Titel = "Zeltlager " + name, Beginn = beginn, Ende = beginn.AddDays(2).AddHours(4), Abteilungen = [jf] };
        db.Dienste.Add(dienst);
        var betreuer = new Jugendbetreuer
        {
            Vorname = "Udo", Nachname = name, AbteilungId = jf.Id, Aufgabe = "Eltern-Helfer", Von = Heute, ZeugnisNoetig = false, ZeugnisNichtNoetigGrund = "nur Küche",
        };
        db.Jugendbetreuer.Add(betreuer);
        await db.SaveChangesAsync();
        return (dienst.Id, kind.Id, konto.Id, betreuer.Id);
    }

    [Fact]
    public async Task Eltern_melden_mit_Einwilligung_an_und_der_Jugendwart_sieht_Betreuer_ohne_Zeugnis_rot()
    {
        var name = "Zelt" + Guid.NewGuid().ToString("N")[..6];
        var (dienst, kind, eltern, betreuer) = await AnlegenAsync(name);
        var termin = $"/Termine/Termin/{dienst}";
        var elternClient = Angemeldet(eltern, Rollen.Erziehungsberechtigt);
        Assert.Contains("Mia anmelden", HtmlDecode(await elternClient.GetStringAsync(termin)));

        // Ohne Einwilligung keine Anmeldung.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(elternClient, termin, termin + "?handler=Anmelden",
            new() { ["mitgliedId"] = kind.ToString() })).StatusCode);
        Assert.False(await MitDbAsync(db => db.Veranstaltungsanmeldungen.AnyAsync(a => a.MitgliedId == kind)));

        // Fremde Eltern dürfen das Kind nicht anmelden.
        var (_, _, andere, _) = await AnlegenAsync(name + "b");
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Angemeldet(andere, Rollen.Erziehungsberechtigt), termin, termin + "?handler=Anmelden",
            new() { ["mitgliedId"] = kind.ToString(), ["einwilligung"] = "true" })).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(elternClient, termin, termin + "?handler=Anmelden",
            new() { ["mitgliedId"] = kind.ToString(), ["einwilligung"] = "true" })).StatusCode);
        var anmeldung = await MitDbAsync(db => db.Veranstaltungsanmeldungen.AsNoTracking().SingleAsync(a => a.MitgliedId == kind));
        Assert.Equal((Anmeldeweg.Elternkonto, true), (anmeldung.Weg, anmeldung.Einwilligung));
        Assert.True(await MitDbAsync(db => db.Rueckmeldungen.AnyAsync(r => r.DienstId == dienst && r.MitgliedId == kind && r.Antwort == RueckmeldungAntwort.Zusage)));
        Assert.Contains("Mia</span> ist angemeldet", HtmlDecode(await elternClient.GetStringAsync(termin)));

        // Jugendwart teilt den Helfer ein: beim Zeltlager ohne gültiges Zeugnis rot.
        var jugendwart = Angemeldet(null, Rollen.Jugendwart);
        var seite = $"/Jugend/Veranstaltung/{dienst}";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendwart, seite, seite + "?handler=Betreuer",
            new() { ["betreuerId"] = betreuer.ToString() })).StatusCode);
        var text = HtmlDecode(await jugendwart.GetStringAsync(seite));
        Assert.Contains($"{name}, Mia", text);
        Assert.Contains("nicht nötig – nicht einsetzen", text);

        var pdf = await jugendwart.GetAsync(seite + "?handler=Teilnehmerliste");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(name, pdf.Content.Headers.ContentDisposition?.FileName ?? "");
        Assert.Contains("Zeltlager " + name, HtmlDecode(await jugendwart.GetStringAsync($"/Jugend/Veranstaltungen?jahr={Heute.AddDays(30).Year}")));

        // Der Stellvertreter plant mit, sieht aber den Stand der Zeugnisse nicht; der Gerätewart kommt nicht hinein.
        Assert.DoesNotContain("nicht einsetzen", HtmlDecode(await Angemeldet(null, Rollen.Stellvertreter).GetStringAsync(seite)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Geraetewart).GetAsync(seite)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Geraetewart).GetAsync("/Jugend/Veranstaltungen")).StatusCode);
    }

    [Fact]
    public async Task Eltern_sehen_die_Akte_ihres_Kindes_und_melden_Aenderungen_der_Jugendwart_bekommt_den_Elterntext()
    {
        var name = "Akte" + Guid.NewGuid().ToString("N")[..6];
        var (dienst, kind, eltern, _) = await AnlegenAsync(name);
        var (_, fremdesKind, _, _) = await AnlegenAsync(name + "f");
        var client = Angemeldet(eltern, Rollen.Erziehungsberechtigt);

        Assert.Contains("Akte von Mia", HtmlDecode(await client.GetStringAsync($"/Konto/Akte?kind={kind}")));
        Assert.Contains("Meine Kinder", HtmlDecode(await client.GetStringAsync("/Konto")));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Konto/Akte?kind={fremdesKind}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Konto/Aenderung?kind={fremdesKind}")).StatusCode);

        var seite = $"/Konto/Aenderung?kind={kind}";
        var formular = HtmlDecode(await client.GetStringAsync(seite));
        Assert.DoesNotContain("Beruf und Arbeitgeber", formular);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(client, seite, seite, new()
        {
            ["Werte[Strasse]"] = "Klosterstraße 5", ["Werte[Plz]"] = "73099", ["Werte[Ort]"] = "Adelberg",
        })).StatusCode);
        var antrag = await MitDbAsync(db => db.Aenderungsantraege.AsNoTracking().Include(a => a.Felder).SingleAsync(a => a.MitgliedId == kind));
        Assert.Equal("Klosterstraße 5", antrag.Felder.Single(f => f.Feld == AenderungFeld.Strasse).Neu);

        var termin = HtmlDecode(await Angemeldet(null, Rollen.Jugendwart).GetStringAsync($"/Termine/Termin/{dienst}"));
        Assert.Contains("Text für die Eltern-Gruppe", termin);
        Assert.Contains($"/Termine/Termin/{dienst}", termin);
        Assert.DoesNotContain("Text für die Eltern-Gruppe", HtmlDecode(await client.GetStringAsync($"/Termine/Termin/{dienst}")));
    }

    [Fact]
    public async Task Warteliste_mit_Wiedervorlage_Schnupperdienst_nur_mit_Zustimmung_und_L56()
    {
        var name = "Wl" + Guid.NewGuid().ToString("N")[..6];
        var (dienst, _, _, _) = await AnlegenAsync(name);
        var jf = await MitDbAsync(db => db.Abteilungen.Where(a => a.Code == "JF").Select(a => a.Id).SingleAsync());
        var jugendwart = Angemeldet(null, Rollen.Jugendwart);
        var geboren = Heute.AddYears(-8);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendwart, "/Jugend/Warteliste", "/Jugend/Warteliste", new()
        {
            ["Formular.Vorname"] = "Ben", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = geboren.ToString("yyyy-MM-dd"),
            ["Formular.AbteilungId"] = jf.ToString(), ["Formular.Ansprechpartner"] = "Eva " + name, ["Formular.Telefon"] = "0171 1",
        })).StatusCode);
        var eintrag = await MitDbAsync(db => db.Interessenten.AsNoTracking().SingleAsync(i => i.Nachname == name));
        Assert.Equal(geboren.AddYears(10), eintrag.Wiedervorlage);
        var seite = HtmlDecode(await jugendwart.GetStringAsync("/Jugend/Warteliste"));
        Assert.Contains($"{name}, Ben", seite);
        Assert.Contains("Wir löschen sie 12 Monate nach der Aufnahme oder Absage", seite);

        // Ohne Zustimmung der Eltern kein Schnupperdienst.
        await AbschickenAsync(jugendwart, "/Jugend/Warteliste", "/Jugend/Warteliste?handler=Schnupper", new() { ["eintrag"] = eintrag.Id.ToString(), ["dienstId"] = dienst.ToString() });
        Assert.Null(await MitDbAsync(db => db.Interessenten.Where(i => i.Id == eintrag.Id).Select(i => i.SchnupperdienstId).SingleAsync()));
        await AbschickenAsync(jugendwart, "/Jugend/Warteliste", "/Jugend/Warteliste?handler=Schnupper",
            new() { ["eintrag"] = eintrag.Id.ToString(), ["dienstId"] = dienst.ToString(), ["zustimmung"] = "true" });
        var schnupper = await MitDbAsync(db => db.Interessenten.AsNoTracking().SingleAsync(i => i.Id == eintrag.Id));
        Assert.Equal((dienst, Heute), (schnupper.SchnupperdienstId!.Value, schnupper.SchnupperZustimmungAm!.Value));

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Geraetewart).GetAsync("/Jugend/Warteliste")).StatusCode);

        // L56: abgeschlossen vor mehr als zwölf Monaten wird gelöscht.
        await MitDbAsync(async db =>
        {
            db.Interessenten.Add(new Interessent
            {
                Vorname = "Alt", Nachname = name, Ansprechpartner = "X", AbteilungId = jf, Geburtsdatum = Heute.AddYears(-12),
                EingetragenAm = Heute.AddYears(-3), Wiedervorlage = Heute.AddYears(-2), ErledigtAm = Heute.AddMonths(-13), Ergebnis = Wartelistenergebnis.KeinInteresse,
            });
            return await db.SaveChangesAsync();
        });
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        var uebrig = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Interessenten.IgnoreQueryFilters()
            .Where(i => i.Nachname == name).Select(i => i.Vorname).ToListAsync();
        Assert.Equal(["Ben"], uebrig);
    }

    [Fact]
    public async Task L55_loescht_Anmeldungen_nach_der_Frist_und_beachtet_die_Aufbewahrung_des_Zuschussgebers()
    {
        var name = "L55" + Guid.NewGuid().ToString("N")[..6];
        var (alt, kindAlt, _, _) = await AnlegenAsync(name);
        var (lang, kindLang, _, _) = await AnlegenAsync(name + "z");
        await MitDbAsync(async db =>
        {
            var geber = new Zuschussgeber { Name = "Test " + name, AufbewahrungMonate = 60 };
            db.Zuschussgeber.Add(geber);
            foreach (var (dienstId, kindId, mitGeber) in new[] { (alt, kindAlt, false), (lang, kindLang, true) })
            {
                var dienst = await db.Dienste.SingleAsync(d => d.Id == dienstId);
                dienst.Beginn = DateTimeOffset.UtcNow.AddMonths(-14);
                dienst.Ende = dienst.Beginn.AddDays(2);
                var v = new Jugendveranstaltung { DienstId = dienstId, Zuschussgeber = mitGeber ? geber : null };
                v.Anmeldungen.Add(new Veranstaltungsanmeldung { MitgliedId = kindId, AngemeldetAm = Heute.AddMonths(-15), Einwilligung = true });
                db.Jugendveranstaltungen.Add(v);
            }

            await db.SaveChangesAsync();
            return 0;
        });

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelJugendveranstaltungen).Anzahl >= 1);
        var uebrig = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Veranstaltungsanmeldungen.IgnoreQueryFilters()
            .Where(a => a.MitgliedId == kindAlt || a.MitgliedId == kindLang).Select(a => a.MitgliedId).ToListAsync();
        Assert.Equal([kindLang], uebrig);
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
