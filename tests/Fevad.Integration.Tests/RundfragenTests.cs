using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kommunikation;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kommunikation;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Rundfragen, Umfragen und Kontaktliste mit Einwilligung (V42).</summary>
[Collection(AnwendungCollection.Name)]
public partial class RundfragenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(Guid kontoId, string rolle)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, kontoId.ToString());
        return client;
    }

    private async Task<(Guid MitgliedId, Guid KontoId)> MitgliedAsync(string code, string name, int alter = 30, string? qualifikation = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == code);
        var m = new Mitglied
        {
            Personalnummer = "R" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kim", Nachname = name, Geburtsdatum = Heute.AddYears(-alter),
            TelefonMobil = "0171 " + Random.Shared.Next(1000000, 9999999), Email = $"{name.ToLowerInvariant()}@privat.test",
        };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-2) });
        db.Mitglieder.Add(m);
        if (qualifikation is not null)
        {
            var q = await db.Qualifikationen.SingleAsync(x => x.Code == qualifikation);
            db.QualifikationNachweise.Add(new QualifikationNachweis { MitgliedId = m.Id, QualifikationId = q.Id, ErworbenAm = Heute.AddYears(-1) });
        }

        await db.SaveChangesAsync();
        var email = $"r-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var k = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(k)).Succeeded);
        return (m.Id, k.Id);
    }

    private async Task<Guid> VerschickenAsync(Rundfrage r, string code = AbteilungCode.Einsatz)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        r.Abteilungen = [await db.Abteilungen.Where(a => a.Code == code).Select(a => a.Id).SingleAsync()];
        db.Rundfragen.Add(r);
        await db.SaveChangesAsync();
        Assert.True(await scope.ServiceProvider.GetRequiredService<Rundfragen>().VerschickenAsync(r.Id, "Kommandant Test") >= 1);
        return r.Id;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string handler, IEnumerable<KeyValuePair<string, string>>? felder = null)
    {
        var html = await client.GetStringAsync(seite);
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)) };
        liste.AddRange(felder ?? []);
        return await client.PostAsync(handler.Length == 0 ? seite : $"{seite}?handler={handler}", new FormUrlEncodedContent(liste));
    }

    private async Task EinwilligenAsync(Guid mitgliedId, bool widerrufen = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var art = await db.Einwilligungsarten.SingleAsync(a => a.Code == Einwilligungsart.CodeKontaktliste);
        db.Einwilligungen.Add(new Einwilligung
        {
            MitgliedId = mitgliedId, EinwilligungsartId = art.Id, Erteilt = true, Am = Heute.AddDays(-1), Weg = EinwilligungWeg.Konto,
            WiderrufenAm = widerrufen ? Heute : null,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Pruefen_und_Auswahl()
    {
        var r = new Rundfrage { Titel = "Termin?", Art = RundfrageArt.Umfrage, Ziel = MitteilungZiel.Mitglieder, Abteilungen = [Guid.NewGuid()], Optionen = ["Freitag"] };
        Assert.Contains(r.Pruefen(), f => f.Contains("2 bis"));
        r.Optionen.Add("Samstag");
        Assert.Empty(r.Pruefen());
        Assert.True(r.AuswahlGueltig([1]));
        Assert.False(r.AuswahlGueltig([0, 1]));
        r.Mehrfachauswahl = true;
        Assert.True(r.AuswahlGueltig([0, 1]));
        Assert.False(r.AuswahlGueltig([2]));

        var wer = new Rundfrage { Titel = "Wer kann?", Art = RundfrageArt.Verfuegbarkeit, Ziel = MitteilungZiel.Mitglieder, Abteilungen = [Guid.NewGuid()], Anonym = true };
        Assert.Contains("„Wer kann?“ lässt sich nicht anonym fragen.", wer.Pruefen());
        Assert.False(wer.AuswahlGueltig([0, 1]));
    }

    [Fact]
    public async Task Wer_kann_zeigt_Antworten_und_Zusagen_nach_Qualifikation()
    {
        var name = "Rf" + Guid.NewGuid().ToString("N")[..6];
        var (_, agt) = await MitgliedAsync(AbteilungCode.Einsatz, name + "a", qualifikation: "AGT");
        var (_, ohne) = await MitgliedAsync(AbteilungCode.Einsatz, name + "b");
        var (_, jugend) = await MitgliedAsync(AbteilungCode.Jugend, name + "j", alter: 13);
        var id = await VerschickenAsync(new Rundfrage { Titel = "Altpapier " + name, Art = RundfrageArt.Verfuegbarkeit, Ziel = MitteilungZiel.Mitglieder, Tag = Heute.AddDays(3) });

        var seite = $"/Rundfragen/Details/{id}";
        var a = Client(agt, Rollen.Mitglied);
        Assert.Contains("Altpapier " + name, WebUtility.HtmlDecode(await a.GetStringAsync("/Rundfragen")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(a, seite, "Antworten", [new("auswahl", "0")])).StatusCode);
        var b = Client(ohne, Rollen.Mitglied);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(b, seite, "Antworten", [new("auswahl", "1"), new("bemerkung", "erst ab 10:00 Uhr")])).StatusCode);
        // Antwort ändern geht, solange die Rundfrage offen ist.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(b, seite, "Antworten", [new("auswahl", "1"), new("bemerkung", "erst ab 11:00 Uhr")])).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(jugend, Rollen.Mitglied).GetAsync(seite)).StatusCode);
        Assert.DoesNotContain("Zusagen nach Qualifikation", WebUtility.HtmlDecode(await a.GetStringAsync(seite)));

        var kommandant = Client(Guid.NewGuid(), Rollen.Kommandant);
        var auswertung = WebUtility.HtmlDecode(await kommandant.GetStringAsync(seite));
        Assert.Contains("2 von 2 haben geantwortet", auswertung);
        Assert.Contains("Zusagen nach Qualifikation", auswertung);
        Assert.Matches(@"Atemschutzgeräteträger</td><td class=""text-right"">1</td><td class=""text-right"">0</td>", auswertung);
        Assert.Contains($"{name}b, Kim", auswertung);
        Assert.Contains("erst ab 11:00 Uhr", auswertung);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Beenden")).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.Equal("Die Rundfrage ist beendet.", await scope.ServiceProvider.GetRequiredService<Rundfragen>().AntwortenAsync(id, agt, [2], null));

        // Die eigene Antwort gehört zur Auskunft nach Art. 15.
        var auskunft = await scope.ServiceProvider.GetRequiredService<Selbstauskunft>().LadenAsync(ohne);
        var zeile = Assert.Single(auskunft.Single(x => x.Titel == "Rundfragen und Umfragen").Zeilen, z => Equals(z[0], "Altpapier " + name));
        Assert.Equal("Vielleicht", zeile[3]);
        Assert.Equal("erst ab 11:00 Uhr", zeile[4]);
    }

    [Fact]
    public async Task Anonyme_Umfrage_speichert_ohne_Konto_und_nur_einmal()
    {
        var name = "Ua" + Guid.NewGuid().ToString("N")[..6];
        var (_, konto) = await MitgliedAsync(AbteilungCode.Einsatz, name);
        var id = await VerschickenAsync(new Rundfrage
        {
            Titel = "Zufriedenheit " + name, Art = RundfrageArt.Umfrage, Ziel = MitteilungZiel.Mitglieder, Anonym = true, Optionen = ["gut", "geht so", "schlecht"],
        });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<Rundfragen>();
            Assert.Null(await dienst.AntwortenAsync(id, konto, [0], "soll nicht gespeichert werden"));
            Assert.Contains("schon abgestimmt", await dienst.AntwortenAsync(id, konto, [2], null));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var antwort = await db.RundfrageAntworten.SingleAsync(x => x.RundfrageId == id);
            Assert.Null(antwort.EmpfaengerId);
            Assert.Null(antwort.Bemerkung);
            Assert.Equal([0], antwort.Auswahl);

            var auskunft = await scope.ServiceProvider.GetRequiredService<Selbstauskunft>().LadenAsync(konto);
            var zeile = Assert.Single(auskunft.Single(x => x.Titel == "Rundfragen und Umfragen").Zeilen);
            Assert.Equal("anonym, keiner Person zugeordnet", zeile[3]);
        }

        var auswertung = WebUtility.HtmlDecode(await Client(Guid.NewGuid(), Rollen.Kommandant).GetStringAsync($"/Rundfragen/Details/{id}"));
        Assert.Contains("1 von", auswertung);
        Assert.DoesNotContain(name + ", Kim", auswertung);
    }

    [Fact]
    public async Task Kontaktliste_zeigt_nur_Volljaehrige_mit_Einwilligung()
    {
        var name = "Kl" + Guid.NewGuid().ToString("N")[..6];
        var (dabei, dabeiKonto) = await MitgliedAsync(AbteilungCode.Einsatz, name + "a");
        var (widerrufen, _) = await MitgliedAsync(AbteilungCode.Einsatz, name + "w");
        await MitgliedAsync(AbteilungCode.Einsatz, name + "o");
        var (kind, kindKonto) = await MitgliedAsync(AbteilungCode.Jugend, name + "k", alter: 15);
        await EinwilligenAsync(dabei);
        await EinwilligenAsync(widerrufen, widerrufen: true);
        await EinwilligenAsync(kind);

        var liste = WebUtility.HtmlDecode(await Client(dabeiKonto, Rollen.Mitglied).GetStringAsync("/Kontaktliste"));
        Assert.Contains($"{name}a, Kim", liste);
        Assert.Contains($"{name.ToLowerInvariant()}a@privat.test", liste);
        Assert.DoesNotContain($"{name}w, Kim", liste);
        Assert.DoesNotContain($"{name}o, Kim", liste);
        Assert.DoesNotContain($"{name}k, Kim", liste);
        Assert.Contains("Austragen kannst du dich", liste);

        // Minderjährige bekommen die Art im Konto nicht angeboten; Elternkonten und Fremde sehen die Liste nicht.
        Assert.DoesNotContain("Kontaktliste für Mitglieder", WebUtility.HtmlDecode(await Client(kindKonto, Rollen.Mitglied).GetStringAsync("/Konto/Einwilligungen")));
        Assert.Contains("Kontaktliste für Mitglieder", WebUtility.HtmlDecode(await Client(dabeiKonto, Rollen.Mitglied).GetStringAsync("/Konto/Einwilligungen")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Guid.NewGuid(), Rollen.Erziehungsberechtigt).GetAsync("/Kontaktliste")).StatusCode);
    }

    [Fact]
    public async Task Loeschlauf_entfernt_beendete_Rundfragen_nach_der_Frist()
    {
        var name = "Lr" + Guid.NewGuid().ToString("N")[..6];
        var (_, konto) = await MitgliedAsync(AbteilungCode.Einsatz, name);
        var id = await VerschickenAsync(new Rundfrage { Titel = "Alt " + name, Art = RundfrageArt.Verfuegbarkeit, Ziel = MitteilungZiel.Mitglieder });
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<Rundfragen>().AntwortenAsync(id, konto, [0], null));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Rundfragen.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, RundfrageStatus.Beendet).SetProperty(x => x.BeendetAm, DateTimeOffset.UtcNow.AddMonths(-13)));
        var ergebnis = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.Contains(ergebnis, e => e.Regel == Loeschlauf.RegelRundfragen && e.Anzahl >= 1);
        Assert.False(await db.Rundfragen.IgnoreQueryFilters().AnyAsync(x => x.Id == id));
        Assert.False(await db.RundfrageAntworten.AnyAsync(x => x.RundfrageId == id));
        Assert.False(await db.Benachrichtigungen.AnyAsync(b => b.BezugId == id));
    }
}
