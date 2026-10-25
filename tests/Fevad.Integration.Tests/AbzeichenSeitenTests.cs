using Fevad.Web.Infrastruktur;
using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class AbzeichenSeitenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static DateOnly Heute => Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private async Task<Mitglied> MitgliedAnlegenAsync(string abteilung, DateOnly geburtsdatum, DateOnly seit, params (string Code, DateOnly Erworben)[] qualifikationen)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abt = await db.Abteilungen.SingleAsync(a => a.Code == abteilung);
        var nummer = "A" + Guid.NewGuid().ToString("N")[..8];
        var mitglied = new Mitglied { Personalnummer = nummer, Vorname = "Lena", Nachname = "Abzeichen-" + nummer, Geburtsdatum = geburtsdatum };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abt.Id, Status = MitgliedStatus.Aktiv, GueltigAb = seit });
        db.Mitglieder.Add(mitglied);
        foreach (var (code, erworben) in qualifikationen)
        {
            var q = await db.Qualifikationen.SingleAsync(x => x.Code == code);
            var n = new QualifikationNachweis { Mitglied = mitglied, QualifikationId = q.Id, ErworbenAm = erworben };
            n.GueltigkeitVorbelegen(q);
            db.QualifikationNachweise.Add(n);
        }

        await db.SaveChangesAsync();
        return mitglied;
    }

    [Fact]
    public async Task Stufenfolgen_sind_im_Katalog_verknuepft()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var katalog = await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Code);

        Assert.Equal(katalog["LA-B"].Id, katalog["LA-S"].VorstufeId);
        Assert.Equal(katalog["LA-S"].Id, katalog["LA-G"].VorstufeId);
        Assert.Equal(katalog[Qualifikation.CodeJugendflamme2].Id, katalog[Qualifikation.CodeJugendflamme3].VorstufeId);
        Assert.Equal(QualifikationArt.Abzeichen, katalog[Qualifikation.CodeLeistungsspange].Art);
        Assert.Equal(12, katalog[Qualifikation.CodeLeistungsspange].MindestJugendMonate);
    }

    [Fact]
    public async Task Jugendlicher_ohne_Leistungsspange_bekommt_Hinweis_bis_er_sie_hat()
    {
        var jugend = await MitgliedAnlegenAsync("JF", Heute.AddYears(-15).AddDays(-10), Heute.AddYears(-3));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        var akte = await SeiteAsync(client, $"/Personal/Akte/{jugend.Id}");
        Assert.Contains("Leistungsspange DJF fehlt", akte);
        Assert.DoesNotContain("<td>Jährliche Unterweisung (UVV)</td>", akte); // Pflichtnachweise gelten nur für die Einsatzabteilung
        Assert.Contains(jugend.Nachname, await SeiteAsync(client, $"/Personal?Alle=true&Suche={jugend.Nachname}"));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var spange = await db.Qualifikationen.SingleAsync(q => q.Code == Qualifikation.CodeLeistungsspange);
            db.QualifikationNachweise.Add(new QualifikationNachweis { MitgliedId = jugend.Id, QualifikationId = spange.Id, ErworbenAm = Heute.AddMonths(-1) });
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain("Leistungsspange DJF fehlt", await SeiteAsync(client, $"/Personal/Akte/{jugend.Id}"));
    }

    [Fact]
    public async Task Planungshilfe_zeigt_wer_Silber_ablegen_kann_und_Atemschutz_nur_mit_Recht()
    {
        var bereit = await MitgliedAnlegenAsync("EA", new(1995, 1, 1), new(2015, 1, 1),
            ("TM1", new(2015, 6, 1)), ("SPRF", new(2016, 6, 1)), ("LA-B", Heute.AddYears(-1)));
        var zuFrueh = await MitgliedAnlegenAsync("EA", new(1996, 1, 1), new(2015, 1, 1),
            ("TM1", new(2015, 6, 1)), ("SPRF", new(2016, 6, 1)), ("LA-B", Heute));

        Guid silber;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            silber = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Qualifikationen.SingleAsync(q => q.Code == "LA-S")).Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var pfad = $"/Personal/Leistungsabzeichen?Stufe={silber}&Abnahmetag={Heute:yyyy-MM-dd}&NurMoegliche=true";
        var fuehrung = await SeiteAsync(Angemeldet(factory, Rollen.Fuehrungskraft), pfad);
        Assert.Contains(bereit.Nachname, fuehrung);
        Assert.DoesNotContain(zuFrueh.Nachname, fuehrung);
        Assert.DoesNotContain("<th>Atemschutz</th>", fuehrung);

        var kommandant = await SeiteAsync(Angemeldet(factory, Rollen.Kommandant), pfad.Replace("&NurMoegliche=true", ""));
        Assert.Contains(zuFrueh.Nachname, kommandant);
        Assert.Contains("frühestens " + (Heute.Year + 1), kommandant);
        Assert.Contains("<th>Atemschutz</th>", kommandant);

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Kassier).GetAsync(pfad)).StatusCode);
    }

    [Fact]
    public async Task Abzeichen_eintragen_speichert_Abnahmeort_und_Urkunde_auch_ohne_Vorstufe()
    {
        var mitglied = await MitgliedAnlegenAsync("EA", new(1995, 1, 1), new(2015, 1, 1), ("TM1", new(2015, 6, 1)));
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        var html = await client.GetStringAsync($"/Personal/Akte/{mitglied.Id}");

        Guid silber;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            silber = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Qualifikationen.SingleAsync(q => q.Code == "LA-S")).Id;
        }

        var antwort = await client.PostAsync($"/Personal/Akte/{mitglied.Id}?handler=Qualifikation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["QualifikationId"] = silber.ToString(),
            ["ErworbenAm"] = "2024-07-06",
            ["Lehrgangsort"] = "Göppingen",
            ["Urkunde"] = "BZ 2024/117",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using var pruefung = fixture.Factory.Services.CreateAsyncScope();
        var gespeichert = await pruefung.ServiceProvider.GetRequiredService<AppDbContext>().QualifikationNachweise
            .SingleAsync(n => n.MitgliedId == mitglied.Id && n.QualifikationId == silber);
        Assert.Equal("BZ 2024/117", gespeichert.Urkunde);
        Assert.Equal("Göppingen", gespeichert.Lehrgangsort);
        Assert.Null(gespeichert.GueltigBis);
    }

    [Fact]
    public async Task Jugendwart_traegt_Jugendabzeichen_nur_fuer_die_Jugendfeuerwehr_ein()
    {
        var jugend = await MitgliedAnlegenAsync("JF", Heute.AddYears(-12), Heute.AddYears(-2));
        var einsatz = await MitgliedAnlegenAsync("EA", new(1990, 1, 1), new(2010, 1, 1));
        var factory = TestAnmeldung.Factory(fixture);
        var client = Angemeldet(factory, Rollen.Jugendwart);

        var seite = await SeiteAsync(client, "/Abteilungen/Jugendabzeichen");
        Assert.Contains(jugend.Nachname, seite);
        Assert.DoesNotContain(einsatz.Nachname, seite);
        // Wer Mitglieder der Jugendfeuerwehr lesen darf, sieht auch ihre Abzeichen; der Gerätewart liest keine Mitglieder.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Geraetewart).GetAsync("/Abteilungen/Jugendabzeichen")).StatusCode);

        Guid flamme;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            flamme = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Qualifikationen.SingleAsync(q => q.Code == Qualifikation.CodeJugendflamme1)).Id;
        }

        var html = await client.GetStringAsync("/Abteilungen/Jugendabzeichen");
        var antwort = await client.PostAsync("/Abteilungen/Jugendabzeichen", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Token(html)),
            new("AbzeichenId", flamme.ToString()),
            new("Datum", Heute.ToString("yyyy-MM-dd")),
            new("Abnahmeort", "Adelberg"),
            new("Mitglieder", jugend.Id.ToString()),
            new("Mitglieder", einsatz.Id.ToString()),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using var pruefung = fixture.Factory.Services.CreateAsyncScope();
        var db = pruefung.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Adelberg", (await db.QualifikationNachweise.SingleAsync(n => n.MitgliedId == jugend.Id && n.QualifikationId == flamme)).Lehrgangsort);
        Assert.False(await db.QualifikationNachweise.AnyAsync(n => n.MitgliedId == einsatz.Id && n.QualifikationId == flamme));
    }
}
