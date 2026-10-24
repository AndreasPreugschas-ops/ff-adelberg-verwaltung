using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Pages.Mitglieder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class AntraegeTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex(@"/Antraege/Bearbeiten/([0-9a-f-]{36})")]
    private static partial Regex AntragAdresse();

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Angemeldet(params string[] rollen)
    {
        var client = TestAnmeldung.Factory(fixture).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    private static async Task<HttpResponseMessage> AbsendenAsync(HttpClient client, string seite, string ziel, Dictionary<string, string> felder)
    {
        felder["__RequestVerificationToken"] = TokenFeld().Match(await SeiteAsync(client, seite)).Groups[1].Value;
        return await client.PostAsync(ziel, new FormUrlEncodedContent(felder));
    }

    private async Task<Guid> AbteilungAsync(string code)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Abteilungen.Where(a => a.Code == code).Select(a => a.Id).SingleAsync();
    }

    private static string Name() => "Antrag" + Guid.NewGuid().ToString("N")[..6];

    /// <summary>Speichert das Formular und liefert die ID des Antrags aus der Weiterleitung.</summary>
    private static async Task<Guid> SpeichernAsync(HttpClient client, string seite, Dictionary<string, string> felder)
    {
        var antwort = await AbsendenAsync(client, seite, "/Antraege/Bearbeiten", felder);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        return Guid.Parse(AntragAdresse().Match(antwort.Headers.Location!.OriginalString).Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> HandlerAsync(HttpClient client, Guid id, string handler, Dictionary<string, string>? felder = null) =>
        await AbsendenAsync(client, $"/Antraege/Bearbeiten/{id}", $"/Antraege/Bearbeiten/{id}?handler={handler}", felder ?? []);

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Aufnahme_in_die_Jugendfeuerwehr_ausfuellen_drucken_und_annehmen()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var jf = await AbteilungAsync(AbteilungCode.Jugend);
        var name = Name();
        var seite = "/Antraege/Bearbeiten?art=JugendEintritt";
        Assert.Contains("Angaben zum Kind", await SeiteAsync(client, seite));

        var id = await SpeichernAsync(client, seite, new()
        {
            ["Formular.Art"] = nameof(AntragArt.JugendEintritt), ["Formular.AbteilungId"] = jf.ToString(),
            ["Formular.Vorname"] = "Lena", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-12).ToString("yyyy-MM-dd"),
            ["Formular.Strasse"] = "Klosterstraße 3", ["Formular.Plz"] = "73099", ["Formular.Ort"] = "Adelberg",
            ["Formular.Erziehungsberechtigte[0].Verhaeltnis"] = nameof(Sorgeverhaeltnis.Mutter),
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Anna", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
            ["Formular.Erziehungsberechtigte[0].Mobil"] = "+4917011111",
            ["Formular.FotoEinwilligung"] = "true",
        });

        // Ausgefüllt als PDF zum Unterschreiben.
        var pdf = await client.GetAsync($"/Antraege/Bearbeiten/{id}?handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);

        // Annehmen erst nach dem Eingang des unterschriebenen Antrags.
        await HandlerAsync(client, id, "Annehmen", new() { ["personalnummer"] = "A" + name });
        Assert.Equal(AntragStatus.Entwurf, await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));

        var eingang = Heute.AddDays(-3);
        await HandlerAsync(client, id, "Eingegangen", new() { ["am"] = eingang.ToString("yyyy-MM-dd") });
        var antwort = await HandlerAsync(client, id, "Annehmen", new()
        {
            ["ab"] = Heute.ToString("yyyy-MM-dd"), ["personalnummer"] = "A" + name, ["probezeit"] = "Voll",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var antrag = await MitDbAsync(db => db.Antraege.AsNoTracking().SingleAsync(a => a.Id == id));
        Assert.Equal(AntragStatus.Angenommen, antrag.Status);
        var mitglied = await MitDbAsync(db => db.Mitglieder.AsNoTracking().Include(m => m.Mitgliedschaften).Include(m => m.Erziehungsberechtigte)
            .SingleAsync(m => m.Id == antrag.MitgliedId));
        Assert.Equal("A" + name, mitglied.Personalnummer);
        var mitgliedschaft = Assert.Single(mitglied.Mitgliedschaften);
        Assert.Equal(jf, mitgliedschaft.AbteilungId);
        Assert.Equal(MitgliedStatus.Aktiv, mitgliedschaft.Status);
        Assert.Equal("Anna", Assert.Single(mitglied.Erziehungsberechtigte).Vorname);
        // Die Einwilligung gilt ab der Unterschrift.
        Assert.Equal(eingang, mitglied.FotoEinwilligungAm);
        Assert.True(mitglied.FotoEinwilligung);

        // Abgeschlossen: nur noch lesbar.
        var gesperrt = await AbsendenAsync(client, $"/Antraege/Bearbeiten/{id}", "/Antraege/Bearbeiten", new()
        {
            ["Formular.Id"] = id.ToString(), ["Formular.AbteilungId"] = jf.ToString(), ["Formular.Vorname"] = "Geändert", ["Formular.Nachname"] = name,
            ["Formular.Geburtsdatum"] = Heute.AddYears(-12).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, gesperrt.StatusCode);
        Assert.Equal("Lena", await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.Vorname).SingleAsync()));
    }

    [Theory]
    [InlineData(AntragArt.JugendEintritt, "Aufnahme-Jugendfeuerwehr-blanko.pdf")]
    [InlineData(AntragArt.UebertrittEinsatz, "Uebertritt-Einsatzabteilung-blanko.pdf")]
    [InlineData(AntragArt.EinsatzEintritt, "Aufnahme-Einsatzabteilung-blanko.pdf")]
    [InlineData(AntragArt.Volljaehrigkeit, "Volljaehrigkeit-blanko.pdf")]
    [InlineData(AntragArt.Altersabteilung, "Wechsel-Altersabteilung-blanko.pdf")]
    [InlineData(AntragArt.Ausscheiden, "Ausscheiden-blanko.pdf")]
    public async Task Blanko_Formulare_lassen_sich_drucken(AntragArt art, string datei)
    {
        var antwort = await Angemeldet(Rollen.Schriftfuehrer).GetAsync($"/Antraege?handler=Blanko&art={art}");
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("application/pdf", antwort.Content.Headers.ContentType?.MediaType);
        Assert.Equal(datei, antwort.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        // Dahinter immer die Datenschutzinformation nach Art. 13 DSGVO: mindestens zwei Seiten.
        var seiten = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.Latin1.GetString(await antwort.Content.ReadAsByteArrayAsync()), @"/Count (\d+)");
        Assert.True(int.Parse(seiten.Groups[1].Value) >= 2);
    }

    [Fact]
    public async Task Uebertritt_mit_17_wechselt_in_die_Einsatzabteilung_und_der_Jugendwart_entscheidet_nicht()
    {
        var jf = await AbteilungAsync(AbteilungCode.Jugend);
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var name = Name();
        var mitgliedId = await MitDbAsync(async db =>
        {
            var m = new Mitglied { Personalnummer = "U" + name, Vorname = "Tom", Nachname = name, Geburtsdatum = Heute.AddYears(-17).AddDays(-10), Ort = "Adelberg" };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
            m.Erziehungsberechtigte.Add(new Erziehungsberechtigter { Nummer = 1, Vorname = "Eva", Nachname = name, Mobil = "+4917022222" });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            return m.Id;
        });

        // Der Jugendwart bereitet den Antrag vor: aus dem Mitglied vorbelegt, Zielabteilung frei wählbar.
        var jugendwart = Angemeldet(Rollen.Jugendwart);
        var seite = $"/Antraege/Bearbeiten?art=UebertrittEinsatz&mitglied={mitgliedId}";
        var html = await SeiteAsync(jugendwart, seite);
        Assert.Contains($"value=\"{name}\"", html);
        Assert.Contains("value=\"Eva\"", html);
        var id = await SpeichernAsync(jugendwart, seite, new()
        {
            ["Formular.Art"] = nameof(AntragArt.UebertrittEinsatz), ["Formular.MitgliedId"] = mitgliedId.ToString(), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Tom", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-17).AddDays(-10).ToString("yyyy-MM-dd"),
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Eva", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
            ["Formular.Ort"] = "Pluderhausen", ["Formular.Mobil"] = "+4917033333",
        });
        await HandlerAsync(jugendwart, id, "Eingegangen");
        Assert.Contains(name, await SeiteAsync(jugendwart, "/Antraege"));

        // Entscheiden darf nur, wessen Freigabe die Einsatzabteilung umfasst.
        Assert.Equal(HttpStatusCode.Forbidden, (await HandlerAsync(jugendwart, id, "Annehmen", new() { ["probezeit"] = "Voll" })).StatusCode);

        var kommandant = Angemeldet(Rollen.Kommandant);
        await HandlerAsync(kommandant, id, "Annehmen", new() { ["ab"] = Heute.ToString("yyyy-MM-dd"), ["probezeit"] = "Voll" });

        var mitgliedschaften = await MitDbAsync(db => db.Mitgliedschaften.AsNoTracking().Where(s => s.MitgliedId == mitgliedId).ToListAsync());
        var jugend = Assert.Single(mitgliedschaften, s => s.AbteilungId == jf);
        Assert.Equal(Heute.AddDays(-1), jugend.GueltigBis);
        Assert.Equal(Austrittsgrund.Uebertritt, jugend.Austrittsgrund);
        var einsatz = Assert.Single(mitgliedschaften, s => s.AbteilungId == ea);
        Assert.Equal(MitgliedStatus.Probezeit, einsatz.Status);
        Assert.Equal(AntragStatus.Angenommen, await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));

        // Geänderte Angaben aus dem Antrag stehen jetzt in den Stammdaten; das Mitglied ist in der Einsatzabteilung geführt.
        var mitglied = await MitDbAsync(db => db.Mitglieder.AsNoTracking().SingleAsync(m => m.Id == mitgliedId));
        Assert.Equal(("Pluderhausen", "+4917033333"), (mitglied.Ort, mitglied.TelefonMobil));
        Assert.Contains(name, await SeiteAsync(kommandant, $"/Mitglieder?Suche={name}"));

        // Beim Mitglied nur dessen Anträge, auch abgeschlossene, ohne die übrigen Bereiche.
        var anderer = await MitDbAsync(async db =>
        {
            var a = new Antrag { Art = AntragArt.EinsatzEintritt, Status = AntragStatus.Entwurf, AbteilungId = ea, Vorname = "X", Nachname = name + "Fremd", Geburtsdatum = Heute.AddYears(-30) };
            db.Antraege.Add(a);
            await db.SaveChangesAsync();
            return a.Id;
        });
        var reiter = await SeiteAsync(kommandant, $"/Antraege?mitglied={mitgliedId}");
        Assert.Contains("Übertritt", reiter);
        Assert.DoesNotContain(name + "Fremd", reiter);
        Assert.DoesNotContain("Blanko drucken", reiter);
        Assert.Equal(HttpStatusCode.NotFound, (await Angemeldet(Rollen.Kommandant).GetAsync($"/Antraege?mitglied={Guid.NewGuid()}")).StatusCode);
        await MitDbAsync(async db => { db.Antraege.Remove(await db.Antraege.SingleAsync(a => a.Id == anderer)); return await db.SaveChangesAsync(); });
    }

    [Fact]
    public async Task Jugendwart_legt_keine_Aufnahme_in_die_Einsatzabteilung_an()
    {
        var jugendwart = Angemeldet(Rollen.Jugendwart);
        Assert.Equal(HttpStatusCode.Forbidden, (await jugendwart.GetAsync("/Antraege/Bearbeiten?art=EinsatzEintritt")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jugendwart.GetAsync("/Antraege/Bearbeiten?art=JugendEintritt")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Mitglied).GetAsync("/Antraege")).StatusCode);
    }

    [Fact]
    public async Task Aufnahme_in_die_Einsatzabteilung_uebernimmt_die_Vordienstzeit()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var name = Name();
        var seite = "/Antraege/Bearbeiten?art=EinsatzEintritt";
        Assert.Contains("keine Angaben zur Gesundheit", await SeiteAsync(client, seite));
        var id = await SpeichernAsync(client, seite, new()
        {
            ["Formular.Art"] = nameof(AntragArt.EinsatzEintritt), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Max", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-30).ToString("yyyy-MM-dd"),
            ["Formular.FrueherFeuerwehr"] = "true", ["Formular.FrueherFeuerwehrName"] = "FF Schorndorf",
            ["Formular.FrueherVon"] = "2012-03-01", ["Formular.FrueherBis"] = "2020-06-30",
            ["Formular.FrueherAustrittsgrund"] = nameof(Ausscheidensgrund.Umzug), ["Formular.FrueherDienstgrad"] = "Oberfeuerwehrmann",
            // Volljährige haben keine Erziehungsberechtigten auf dem Antrag; mitgeschickte Angaben werden verworfen.
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Eva", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
        });
        Assert.Empty(await MitDbAsync(db => db.AntragElternteile.Where(e => e.AntragId == id).ToListAsync()));
        await HandlerAsync(client, id, "Eingegangen");
        await HandlerAsync(client, id, "Annehmen", new() { ["ab"] = Heute.ToString("yyyy-MM-dd"), ["personalnummer"] = "E" + name, ["probezeit"] = "Voll" });

        var mitgliedId = await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.MitgliedId).SingleAsync());
        var mitgliedschaften = await MitDbAsync(db => db.Mitgliedschaften.AsNoTracking().Where(s => s.MitgliedId == mitgliedId).ToListAsync());
        var vordienst = Assert.Single(mitgliedschaften, s => s.Vordienstzeit);
        Assert.Equal(new DateOnly(2012, 3, 1), vordienst.GueltigAb);
        Assert.Equal(Austrittsgrund.Wohnortwechsel, vordienst.Austrittsgrund);
        Assert.Equal("FF Schorndorf", vordienst.Bemerkung);
        Assert.Equal(MitgliedStatus.Probezeit, Assert.Single(mitgliedschaften, s => !s.Vordienstzeit).Status);
        // Ohne Angabe keine Einwilligung.
        Assert.Null(await MitDbAsync(db => db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.FotoEinwilligungAm).SingleAsync()));
    }

    [Fact]
    public async Task Aufnahme_in_die_Einsatzabteilung_ohne_Uebertritt_erst_ab_18()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var name = Name();
        var seite = "/Antraege/Bearbeiten?art=EinsatzEintritt";
        Assert.DoesNotContain("1. Erziehungsberechtigte(r)", await SeiteAsync(client, seite));
        var antwort = await AbsendenAsync(client, seite, "/Antraege/Bearbeiten", new()
        {
            ["Formular.Art"] = nameof(AntragArt.EinsatzEintritt), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Tim", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-15).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("erst ab 18 Jahren", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        Assert.False(await MitDbAsync(db => db.Antraege.AnyAsync(a => a.Nachname == name)));
    }

    [Fact]
    public async Task Antraege_hoechstens_sechs_Monate_im_Voraus()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var name = Name();
        var seite = "/Antraege/Bearbeiten?art=EinsatzEintritt";
        var antwort = await AbsendenAsync(client, seite, "/Antraege/Bearbeiten", new()
        {
            ["Formular.Art"] = nameof(AntragArt.EinsatzEintritt), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Ole", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-30).ToString("yyyy-MM-dd"),
            ["Formular.GewuenschtAb"] = Heute.AddMonths(7).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("höchstens 6 Monate im Voraus", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        Assert.False(await MitDbAsync(db => db.Antraege.AnyAsync(a => a.Nachname == name)));
    }

    [Fact]
    public async Task Aufnahme_in_die_Einsatzabteilung_bietet_die_eigene_Jugendfeuerwehr_an()
    {
        var jf = await AbteilungAsync(AbteilungCode.Jugend);
        var name = Name();
        await MitDbAsync(async db =>
        {
            var m = new Mitglied { Personalnummer = "W" + name, Vorname = "Paul", Nachname = name, Geburtsdatum = Heute.AddYears(-17).AddDays(30) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-4) });
            db.Mitglieder.Add(m);
            return await db.SaveChangesAsync();
        });
        var html = await SeiteAsync(Angemeldet(Rollen.Kommandant), "/Antraege/Bearbeiten?art=EinsatzEintritt");
        Assert.Contains("Aus der eigenen Jugendfeuerwehr?", html);
        Assert.Contains(name, html);
    }

    /// <summary>Legt ein Mitglied der Einsatzabteilung an; Altersabteilung wird bei Bedarf angelegt.</summary>
    private async Task<(Guid Mitglied, Guid Einsatz, Guid Alters)> EinsatzmitgliedAsync(string name, int alter)
    {
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var aa = await AbteilungAsync(AbteilungCode.Alters);
        var id = await MitDbAsync(async db =>
        {
            var m = new Mitglied { Personalnummer = "Z" + name, Vorname = "Karl", Nachname = name, Geburtsdatum = Heute.AddYears(-alter).AddDays(-10) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = ea, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-20) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            return m.Id;
        });
        return (id, ea, aa);
    }

    [Fact]
    public async Task Wechsel_in_die_Altersabteilung_ab_55()
    {
        var client = Angemeldet(Rollen.Kommandant);
        var name = Name();
        var (mitgliedId, ea, aa) = await EinsatzmitgliedAsync(name, 56);

        // Beim Mitglied ohne laufenden Antrag: Knöpfe für neue Anträge.
        var reiter = await SeiteAsync(client, $"/Antraege?mitglied={mitgliedId}");
        Assert.Contains("Neuer Antrag:", reiter);
        Assert.Contains("Wechsel in die Altersabteilung", reiter);
        Assert.Contains("Ausscheiden aus der Feuerwehr", reiter);

        var seite = $"/Antraege/Bearbeiten?art=Altersabteilung&mitglied={mitgliedId}";
        var id = await SpeichernAsync(client, seite, new()
        {
            ["Formular.Art"] = nameof(AntragArt.Altersabteilung), ["Formular.MitgliedId"] = mitgliedId.ToString(), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Karl", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-56).AddDays(-10).ToString("yyyy-MM-dd"),
            ["Formular.GewuenschtAb"] = Heute.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Antraege/Bearbeiten/{id}?handler=Pdf")).StatusCode);
        await HandlerAsync(client, id, "Eingegangen");
        await HandlerAsync(client, id, "Annehmen", new() { ["ab"] = Heute.ToString("yyyy-MM-dd") });

        Assert.Equal(AntragStatus.Angenommen, await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));
        var mitgliedschaften = await MitDbAsync(db => db.Mitgliedschaften.AsNoTracking().Where(s => s.MitgliedId == mitgliedId).ToListAsync());
        Assert.Equal(Heute.AddDays(-1), Assert.Single(mitgliedschaften, s => s.AbteilungId == ea).GueltigBis);
        Assert.Null(Assert.Single(mitgliedschaften, s => s.AbteilungId == aa).GueltigBis);

        // Der laufende bzw. erledigte Wechsel wird nicht noch einmal angeboten; mit 50 geht er nicht.
        var juenger = Name();
        var (juengerId, _, _) = await EinsatzmitgliedAsync(juenger, 50);
        var antwort = await AbsendenAsync(client, $"/Antraege/Bearbeiten?art=Altersabteilung&mitglied={juengerId}", "/Antraege/Bearbeiten", new()
        {
            ["Formular.Art"] = nameof(AntragArt.Altersabteilung), ["Formular.MitgliedId"] = juengerId.ToString(), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Karl", ["Formular.Nachname"] = juenger, ["Formular.Geburtsdatum"] = Heute.AddYears(-50).AddDays(-10).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("erst ab 55 Jahren", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ausscheiden_aus_der_Einsatzabteilung(bool inAltersabteilung)
    {
        var client = Angemeldet(Rollen.Kommandant);
        var name = Name();
        var (mitgliedId, ea, aa) = await EinsatzmitgliedAsync(name, 40);
        var bis = Heute.AddDays(14);
        var felder = new Dictionary<string, string>
        {
            ["Formular.Art"] = nameof(AntragArt.Ausscheiden), ["Formular.MitgliedId"] = mitgliedId.ToString(), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Karl", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-40).AddDays(-10).ToString("yyyy-MM-dd"),
            ["Formular.GewuenschtAb"] = bis.ToString("yyyy-MM-dd"), ["Formular.Entlassungsgrund"] = nameof(Entlassungsgrund.Wohnung),
        };
        if (inAltersabteilung)
        {
            felder["Formular.InAltersabteilung"] = "true";
        }

        var seite = $"/Antraege/Bearbeiten?art=Ausscheiden&mitglied={mitgliedId}";
        Assert.Contains("§ 4", await SeiteAsync(client, seite));
        var id = await SpeichernAsync(client, seite, felder);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Antraege/Bearbeiten/{id}?handler=Pdf")).StatusCode);
        await HandlerAsync(client, id, "Eingegangen");
        await HandlerAsync(client, id, "Annehmen", new() { ["ab"] = bis.ToString("yyyy-MM-dd") });

        Assert.Equal(AntragStatus.Angenommen, await MitDbAsync(db => db.Antraege.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));
        var mitgliedschaften = await MitDbAsync(db => db.Mitgliedschaften.AsNoTracking().Where(s => s.MitgliedId == mitgliedId).ToListAsync());
        var einsatz = Assert.Single(mitgliedschaften, s => s.AbteilungId == ea);
        Assert.Equal(bis, einsatz.GueltigBis);
        if (inAltersabteilung)
        {
            Assert.Equal(bis.AddDays(1), Assert.Single(mitgliedschaften, s => s.AbteilungId == aa).GueltigAb);
        }
        else
        {
            Assert.Equal(Austrittsgrund.Wohnortwechsel, einsatz.Austrittsgrund);
            Assert.DoesNotContain(mitgliedschaften, s => s.AbteilungId == aa);
        }
    }

    [Fact]
    public async Task Volljaehrigkeit_erzeugt_einen_Pruefhinweis_bis_die_Erklaerung_angenommen_ist()
    {
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        var name = Name();
        var mitgliedId = await MitDbAsync(async db =>
        {
            var m = new Mitglied { Personalnummer = "V" + name, Vorname = "Jana", Nachname = name, Geburtsdatum = Heute.AddYears(-18).AddDays(-20) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = ea, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
            m.FotoEinwilligungAm = Heute.AddYears(-1);
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            return m.Id;
        });

        async Task<Pruefhinweis?> HinweisAsync()
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<Personalpruefung>().PruefenAsync([mitgliedId], DateTimeOffset.UtcNow, default);
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Pruefhinweise.AsNoTracking()
                .SingleOrDefaultAsync(p => p.MitgliedId == mitgliedId && p.Schluessel == "Volljaehrigkeit");
        }

        Assert.Equal(PruefhinweisStatus.Offen, (await HinweisAsync())?.Status);

        var client = Angemeldet(Rollen.Kommandant);
        var seite = $"/Antraege/Bearbeiten?art=Volljaehrigkeit&mitglied={mitgliedId}";
        var id = await SpeichernAsync(client, seite, new()
        {
            ["Formular.Art"] = nameof(AntragArt.Volljaehrigkeit), ["Formular.MitgliedId"] = mitgliedId.ToString(), ["Formular.AbteilungId"] = ea.ToString(),
            ["Formular.Vorname"] = "Jana", ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-18).AddDays(-20).ToString("yyyy-MM-dd"),
            ["Formular.FotoEinwilligung"] = "false",
        });
        await HandlerAsync(client, id, "Eingegangen");
        await HandlerAsync(client, id, "Annehmen");

        Assert.Equal(PruefhinweisStatus.Entfallen, (await HinweisAsync())?.Status);
        // Das Nein der Volljährigen gilt als Widerruf der früheren Einwilligung der Eltern.
        var mitglied = await MitDbAsync(db => db.Mitglieder.AsNoTracking().SingleAsync(m => m.Id == mitgliedId));
        Assert.False(mitglied.FotoEinwilligung);
        Assert.Equal(Heute, mitglied.FotoWiderrufAm);
    }

    [Fact]
    public void Foto_Einwilligung_neu_erteilen_und_widerrufen()
    {
        var m = new Mitglied { Personalnummer = "x", Vorname = "a", Nachname = "b" };
        BearbeitenModel.FotoEinwilligungUebernehmen(m, true, null, Heute);
        Assert.Equal(Heute, m.FotoEinwilligungAm);

        BearbeitenModel.FotoEinwilligungUebernehmen(m, false, null, Heute.AddDays(5));
        Assert.False(m.FotoEinwilligung);
        Assert.Equal(Heute, m.FotoEinwilligungAm);
        Assert.Equal(Heute.AddDays(5), m.FotoWiderrufAm);

        BearbeitenModel.FotoEinwilligungUebernehmen(m, true, Heute.AddDays(9), Heute.AddDays(10));
        Assert.True(m.FotoEinwilligung);
        Assert.Equal(Heute.AddDays(9), m.FotoEinwilligungAm);
        Assert.Null(m.FotoWiderrufAm);
    }

    [Fact]
    public async Task Loeschlauf_entfernt_entschiedene_Antraege_und_alte_Entwuerfe()
    {
        var ea = await AbteilungAsync(AbteilungCode.Einsatz);
        Antrag Neu(AntragStatus status) => new()
        {
            Art = AntragArt.EinsatzEintritt, Status = status, AbteilungId = ea, Vorname = "L", Nachname = Name(), Geburtsdatum = Heute.AddYears(-25),
            Erziehungsberechtigte = [new AntragElternteil { Nummer = 1, Vorname = "E", Nachname = "L" }],
        };

        var (abgelehnt, frisch, entwurf, verworfen, aktuell) = await MitDbAsync(async db =>
        {
            var a = Neu(AntragStatus.Abgelehnt);
            a.EntschiedenAm = Heute.AddMonths(-13);
            var b = Neu(AntragStatus.Abgelehnt);
            b.EntschiedenAm = Heute.AddMonths(-1);
            var c = Neu(AntragStatus.Entwurf);
            var d = Neu(AntragStatus.Entwurf);
            var e = Neu(AntragStatus.Entwurf);
            db.Antraege.AddRange(a, b, c, d, e);
            await db.SaveChangesAsync();
            db.Antraege.Remove(d);
            await db.SaveChangesAsync();
            var alt = DateTimeOffset.UtcNow.AddMonths(-7);
            await db.Antraege.Where(x => x.Id == c.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ErstelltAm, alt).SetProperty(x => x.GeaendertAm, alt));
            return (a.Id, b.Id, c.Id, d.Id, e.Id);
        });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelAntraege).Anzahl >= 1);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelAntragsentwuerfe).Anzahl >= 2);
        }

        var uebrig = await MitDbAsync(db => db.Antraege.IgnoreQueryFilters().Select(a => a.Id).ToListAsync());
        Assert.DoesNotContain(abgelehnt, uebrig);
        Assert.DoesNotContain(entwurf, uebrig);
        Assert.DoesNotContain(verworfen, uebrig);
        Assert.Contains(frisch, uebrig);
        Assert.Contains(aktuell, uebrig);
        Assert.False(await MitDbAsync(db => db.AntragElternteile.IgnoreQueryFilters().AnyAsync(e => e.AntragId == abgelehnt)));
    }
}
