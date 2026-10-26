using System.Net;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Gemeinde-Vorgänge, Bescheide an Mitglieder und Dienstausweise (P3: V27).</summary>
public partial class EinsatzberichtTests
{
    private async Task<(Guid Aktiv, Guid Jugend)> GemeindeMitgliederAsync(string kennung)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ea = await db.Abteilungen.FirstAsync(a => a.Art == AbteilungArt.Einsatz);
        var jf = await db.Abteilungen.FirstAsync(a => a.Art == AbteilungArt.Jugend);
        var aktiv = new Mitglied { Personalnummer = "G-" + kennung, Vorname = "Gerd", Nachname = "Gemeinde-" + kennung, Geburtsdatum = new(1985, 4, 4), Strasse = "Klosterstraße 1", Plz = "73099", Ort = "Adelberg" };
        aktiv.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = ea.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2015, 1, 1) });
        var jugend = new Mitglied { Personalnummer = "GJ-" + kennung, Vorname = "Jana", Nachname = "Jugend-" + kennung, Geburtsdatum = new(2013, 4, 4) };
        jugend.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2024, 1, 1) });
        db.Mitglieder.AddRange(aktiv, jugend);
        await db.SaveChangesAsync();
        return (aktiv.Id, jugend.Id);
    }

    [Fact]
    public async Task Gemeindevorgang_Feuerwehr_bereitet_vor_Hauptamt_entscheidet()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var (aktiv, jugend) = await GemeindeMitgliederAsync(kennung);
        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);

        var neu = WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Gemeinde/Vorgang?art=Ehrenmitglied"));
        Assert.Contains("Feuerwehrsatzung § 8", neu);
        Assert.DoesNotContain("Jugend-" + kennung, neu);
        Dictionary<string, string> Form(Guid mitglied) => new()
        {
            ["__RequestVerificationToken"] = Token(neu), ["Formular.Art"] = "Ehrenmitglied", ["Formular.Betreff"] = "Ehrenmitglied " + kennung,
            ["Formular.Zustaendig"] = "Gemeinderat", ["Formular.MitgliedId"] = mitglied.ToString(), ["Formular.Beschlussvorschlag"] = "Verleihung",
        };
        Assert.Contains("nicht wählbar", WebUtility.HtmlDecode(await (await kommandant.PostAsync("/Gemeinde/Vorgang?handler=Speichern", new FormUrlEncodedContent(Form(jugend)))).Content.ReadAsStringAsync()));
        var gespeichert = await kommandant.PostAsync("/Gemeinde/Vorgang?handler=Speichern", new FormUrlEncodedContent(Form(aktiv)));
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Gemeindevorgaenge.SingleAsync(v => v.Betreff == "Ehrenmitglied " + kennung)).Id;
        }

        var seite = await kommandant.GetStringAsync($"/Gemeinde/Vorgang?id={id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await kommandant.PostAsync($"/Gemeinde/Vorgang?handler=Vorlegen&id={id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }))).StatusCode);

        var amt = await hauptamt.GetStringAsync($"/Gemeinde/Vorgang?id={id}");
        Assert.Equal(HttpStatusCode.Redirect, (await hauptamt.PostAsync($"/Gemeinde/Vorgang?handler=Vorlegen&id={id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(amt) }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await hauptamt.PostAsync($"/Gemeinde/Vorgang?handler=Entscheiden&id={id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(amt), ["Entscheidung.Ergebnis"] = "Zugestimmt", ["Entscheidung.Datum"] = "2026-10-20", ["Entscheidung.Vermerk"] = "GR 20.10., TOP 4",
        }))).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var v = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Gemeindevorgaenge.SingleAsync(x => x.Id == id);
            Assert.Equal((GemeindevorgangStand.Entschieden, new DateOnly(2026, 10, 20)), (v.Stand, v.EntschiedenAm!.Value));
        }

        Assert.Equal("application/pdf", (await kommandant.GetAsync($"/Gemeinde/Vorgang?handler=Pdf&id={id}")).Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Kaemmerei).GetAsync("/Gemeinde")).StatusCode);
    }

    [Fact]
    public async Task Geldbusse_erst_nach_Anhoerung_und_bis_zum_Hoechstbetrag()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var (aktiv, _) = await GemeindeMitgliederAsync(kennung);
        var factory = TestAnmeldung.Factory(fixture);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var neu = await hauptamt.GetStringAsync("/Gemeinde/Bescheid?art=Geldbusse");
        Assert.Equal(HttpStatusCode.Redirect, (await hauptamt.PostAsync("/Gemeinde/Bescheid?handler=Speichern", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(neu), ["Formular.Art"] = "Geldbusse", ["Formular.MitgliedId"] = aktiv.ToString(),
            ["Formular.Tenor"] = "Geldbuße 1.500 €", ["Formular.Begruendung"] = "Grober Verstoß", ["Formular.Betrag"] = "1500",
        }))).StatusCode);

        Mitgliederbescheid b;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            b = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitgliederbescheide.SingleAsync(x => x.MitgliedId == aktiv);
        }

        Assert.Equal(("Gerd Gemeinde-" + kennung, "Klosterstraße 1, 73099 Adelberg"), (b.EmpfaengerName, b.EmpfaengerAnschrift));
        var seite = await hauptamt.GetStringAsync($"/Gemeinde/Bescheid?id={b.Id}");
        async Task<string> Post(string handler)
        {
            await hauptamt.PostAsync($"/Gemeinde/Bescheid?handler={handler}&id={b.Id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }));
            return WebUtility.HtmlDecode(await hauptamt.GetStringAsync($"/Gemeinde/Bescheid?id={b.Id}"));
        }

        Assert.Contains("anzuhören", await Post("Erlassen"));
        Assert.Contains("läuft noch", (await Post("Anhoerung")) + await Post("Erlassen"));
        Assert.Equal("application/pdf", (await hauptamt.GetAsync($"/Gemeinde/Bescheid?handler=Pdf&id={b.Id}&anhoerung=true")).Content.Headers.ContentType?.MediaType);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Mitgliederbescheide.Where(x => x.Id == b.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Stellungnahme, "Stellungnahme liegt vor"));
        }

        Assert.Contains("1.000,00 €", await Post("Erlassen"));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Mitgliederbescheide.Where(x => x.Id == b.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Betrag, 200m));
        }

        await Post("Erlassen");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Equal(MitgliederbescheidStand.Erlassen, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitgliederbescheide.SingleAsync(x => x.Id == b.Id)).Stand);
        }

        Assert.Equal("application/pdf", (await hauptamt.GetAsync($"/Gemeinde/Bescheid?handler=Pdf&id={b.Id}&anhoerung=false")).Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Stellvertreter).GetAsync($"/Gemeinde/Bescheid?id={b.Id}")).StatusCode);
    }

    [Fact]
    public async Task Dienstausweis_ausstellen_ersetzt_den_alten_und_schirmt_Jugend_ab()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var (aktiv, jugend) = await GemeindeMitgliederAsync(kennung);
        var factory = TestAnmeldung.Factory(fixture);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var seite = WebUtility.HtmlDecode(await hauptamt.GetStringAsync("/Gemeinde/Dienstausweise"));
        Assert.Contains("Gemeinde-" + kennung, seite);
        Assert.DoesNotContain("Jugend-" + kennung, seite);

        MultipartFormDataContent Form() => new()
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(DateTime.Today.AddYears(3).ToString("yyyy-MM-dd")), "GueltigBis" },
            { new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]), "Lichtbild", "foto.txt" },
        };
        // Kein gültiges Bild: abgewiesen, nichts angelegt.
        var falsch = await hauptamt.PostAsync($"/Gemeinde/Dienstausweise?handler=Ausstellen&mitgliedId={aktiv}", new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(DateTime.Today.AddYears(3).ToString("yyyy-MM-dd")), "GueltigBis" },
            { new ByteArrayContent("kein Bild"u8.ToArray()), "Lichtbild", "foto.txt" },
        });
        Assert.Equal(HttpStatusCode.Redirect, falsch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hauptamt.PostAsync($"/Gemeinde/Dienstausweise?handler=Ausstellen&mitgliedId={jugend}", Form())).StatusCode);

        var ohneBild = new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(DateTime.Today.AddYears(3).ToString("yyyy-MM-dd")), "GueltigBis" },
        };
        var pdf = await hauptamt.PostAsync($"/Gemeinde/Dienstausweise?handler=Ausstellen&mitgliedId={aktiv}", ohneBild);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var zweites = new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(DateTime.Today.AddYears(3).ToString("yyyy-MM-dd")), "GueltigBis" },
        };
        Assert.Equal("application/pdf", (await hauptamt.PostAsync($"/Gemeinde/Dienstausweise?handler=Ausstellen&mitgliedId={aktiv}", zweites)).Content.Headers.ContentType?.MediaType);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var ausweise = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Dienstausweise.Where(d => d.MitgliedId == aktiv).OrderBy(d => d.Nummer).ToListAsync();
        Assert.Equal(2, ausweise.Count);
        Assert.Equal(("G-" + kennung + "-1", false, "G-" + kennung + "-2", true), (ausweise[0].Nummer, ausweise[0].Ausgegeben, ausweise[1].Nummer, ausweise[1].Ausgegeben));
    }

    [Fact]
    public async Task L48_bis_L50_Gemeindevorgaenge_Bescheide_und_Dienstausweise_nach_Frist()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var (aktiv, _) = await GemeindeMitgliederAsync(kennung);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var heute = DateOnly.FromDateTime(DateTime.UtcNow);
        var altVorgang = new Gemeindevorgang { Art = GemeindevorgangArt.Ausschluss, Betreff = "L48 alt " + kennung, Zustaendig = "Bürgermeister", MitgliedId = aktiv,
            Sachverhalt = "persönlich", Ergebnis = GemeindeErgebnis.Zugestimmt, EntschiedenAm = heute.AddYears(-4), Entscheidungsvermerk = "Vermerk" };
        var neuVorgang = new Gemeindevorgang { Art = GemeindevorgangArt.Ausschluss, Betreff = "L48 neu " + kennung, Zustaendig = "Bürgermeister", MitgliedId = aktiv,
            Sachverhalt = "persönlich", Ergebnis = GemeindeErgebnis.Zugestimmt, EntschiedenAm = heute.AddYears(-1) };
        var ablehnung = new Mitgliederbescheid { Art = MitgliederbescheidArt.AblehnungAufnahme, EmpfaengerName = "L49 Ablehnung " + kennung, BekanntgegebenAm = heute.AddMonths(-13) };
        var busse = new Mitgliederbescheid { Art = MitgliederbescheidArt.Geldbusse, EmpfaengerName = "L49 Busse " + kennung, MitgliedId = aktiv, BekanntgegebenAm = heute.AddMonths(-13) };
        var zurueck = new Dienstausweis { MitgliedId = aktiv, Nummer = "L50-" + kennung + "-1", AusgestelltAm = heute.AddYears(-6), GueltigBis = heute.AddYears(-1), ZurueckAm = heute.AddMonths(-13) };
        var abgelaufen = new Dienstausweis { MitgliedId = aktiv, Nummer = "L50-" + kennung + "-2", AusgestelltAm = heute.AddYears(-6), GueltigBis = heute.AddMonths(-6) };
        db.AddRange(altVorgang, neuVorgang, ablehnung, busse, zurueck, abgelaufen);
        await db.SaveChangesAsync();

        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);

        Assert.True(ergebnisse.Single(r => r.Regel == Loeschlauf.RegelGemeindevorgaenge).Anzahl >= 1);
        var alt = await db.Gemeindevorgaenge.AsNoTracking().IgnoreQueryFilters().SingleAsync(v => v.Id == altVorgang.Id);
        Assert.Equal((null, null, null, GemeindeErgebnis.Zugestimmt), (alt.MitgliedId, alt.Sachverhalt, alt.Entscheidungsvermerk, alt.Ergebnis));
        Assert.Equal(aktiv, (await db.Gemeindevorgaenge.AsNoTracking().SingleAsync(v => v.Id == neuVorgang.Id)).MitgliedId);
        // Ablehnung nach 12 Monaten weg, Geldbuße bleibt bis 3 Jahre nach Bestandskraft.
        var bescheide = await db.Mitgliederbescheide.AsNoTracking().IgnoreQueryFilters().Where(b => b.EmpfaengerName.EndsWith(kennung)).Select(b => b.Art).ToListAsync();
        Assert.Equal([MitgliederbescheidArt.Geldbusse], bescheide);
        var ausweise = await db.Dienstausweise.AsNoTracking().IgnoreQueryFilters().Where(d => d.MitgliedId == aktiv).Select(d => d.Nummer).ToListAsync();
        Assert.Equal(["L50-" + kennung + "-2"], ausweise);
    }
}
