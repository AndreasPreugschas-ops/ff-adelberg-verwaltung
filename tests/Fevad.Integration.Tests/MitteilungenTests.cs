using System.Net;
using System.Text.RegularExpressions;
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

/// <summary>Mitteilungen mit Lesebestätigung und Elternpost (V42).</summary>
[Collection(AnwendungCollection.Name)]
public partial class MitteilungenTests(AnwendungFixture fixture)
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

    private async Task<(Guid MitgliedId, Guid? KontoId, Guid? ElternkontoId)> MitgliedAsync(string code, string name, bool konto, bool elternkonto = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == code);
        var m = new Mitglied { Personalnummer = "M" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kim", Nachname = name, Geburtsdatum = Heute.AddYears(code == AbteilungCode.Einsatz ? -30 : -13) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-2) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();
        Guid? kontoId = null, elternId = null;
        if (konto)
        {
            var email = $"m-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
            var k = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
            Assert.True((await benutzer.CreateAsync(k)).Succeeded);
            kontoId = k.Id;
        }

        if (elternkonto)
        {
            var email = $"e-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
            var k = new Benutzer { UserName = email, Email = email };
            Assert.True((await benutzer.CreateAsync(k)).Succeeded);
            db.Erziehungsberechtigte.Add(new Erziehungsberechtigter { MitgliedId = m.Id, Nummer = 1, Vorname = "Eva", Nachname = name, KontoId = k.Id });
            await db.SaveChangesAsync();
            elternId = k.Id;
        }

        return (m.Id, kontoId, elternId);
    }

    private async Task<Guid> EntwurfAsync(MitteilungZiel ziel, string code, string titel, bool lesebestaetigung = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = new Mitteilung
        {
            Titel = titel, Text = "Bitte beachten.", Ziel = ziel, Lesebestaetigung = lesebestaetigung,
            Abteilungen = [await db.Abteilungen.Where(a => a.Code == code).Select(a => a.Id).SingleAsync()],
        };
        db.Mitteilungen.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private async Task<int?> VeroeffentlichenAsync(Guid id)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Mitteilungen>().VeroeffentlichenAsync(id, "Kommandant Test");
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string handler, IEnumerable<KeyValuePair<string, string>>? felder = null)
    {
        var html = await client.GetStringAsync(seite);
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)) };
        liste.AddRange(felder ?? []);
        return await client.PostAsync(handler.Length == 0 ? seite : $"{seite}?handler={handler}", new FormUrlEncodedContent(liste));
    }

    [Fact]
    public void Pruefen_verlangt_Titel_Text_und_Ziel()
    {
        var m = new Mitteilung { Titel = "", Text = "", Ziel = MitteilungZiel.Mitglieder };
        Assert.Equal(3, m.Pruefen().Count);
        m.Ziel = MitteilungZiel.Veranstaltung;
        Assert.Contains("Bitte die Veranstaltung wählen.", m.Pruefen());
    }

    [Fact]
    public async Task Mitteilung_erreicht_Mitglieder_mit_Konto_und_Lesestand_ist_nachvollziehbar()
    {
        var name = "Mt" + Guid.NewGuid().ToString("N")[..6];
        var (_, empfaenger, _) = await MitgliedAsync(AbteilungCode.Einsatz, name, konto: true);
        var (_, jugend, eltern) = await MitgliedAsync(AbteilungCode.Jugend, name + "j", konto: true, elternkonto: true);
        var id = await EntwurfAsync(MitteilungZiel.Mitglieder, AbteilungCode.Einsatz, "Dienstplan " + name, lesebestaetigung: true);
        Assert.True(await VeroeffentlichenAsync(id) >= 1);
        // Doppelt veröffentlichen geht nicht.
        Assert.Null(await VeroeffentlichenAsync(id));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var konten = await db.MitteilungEmpfaenger.Where(e => e.MitteilungId == id).Select(e => e.KontoId).ToListAsync();
            Assert.Contains(empfaenger!.Value, konten);
            Assert.DoesNotContain(jugend!.Value, konten);
            Assert.DoesNotContain(eltern!.Value, konten);
            Assert.True(await db.Benachrichtigungen.AnyAsync(b => b.EmpfaengerId == empfaenger && b.BezugId == id && b.Art == Benachrichtigungsart.Mitteilung));
        }

        var leser = Client(empfaenger!.Value, Rollen.Mitglied);
        Assert.Contains("Dienstplan " + name, WebUtility.HtmlDecode(await leser.GetStringAsync("/Mitteilungen")));
        var seite = WebUtility.HtmlDecode(await leser.GetStringAsync($"/Mitteilungen/Details/{id}"));
        Assert.Contains("Gelesen und verstanden", seite);
        Assert.DoesNotContain("Zurückziehen", seite);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(leser, $"/Mitteilungen/Details/{id}", "Bestaetigen")).StatusCode);

        // Wer nicht Empfänger ist, sieht sie nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(jugend.Value, Rollen.Mitglied).GetAsync($"/Mitteilungen/Details/{id}")).StatusCode);

        var kommandant = Client(Guid.NewGuid(), Rollen.Kommandant);
        var verwaltung = WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Mitteilungen/Details/{id}"));
        Assert.Contains($"{name}, Kim", verwaltung);
        Assert.Contains("bestätigt", verwaltung);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.MitteilungEmpfaenger.SingleAsync(x => x.MitteilungId == id && x.KontoId == empfaenger);
            Assert.NotNull(e.GelesenAm);
            Assert.NotNull(e.BestaetigtAm);
            Assert.NotNull(await db.Benachrichtigungen.Where(b => b.EmpfaengerId == empfaenger && b.BezugId == id).Select(b => b.GelesenAm).SingleAsync());
        }

        // Zurückgezogen: Empfänger sehen sie nicht mehr.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, $"/Mitteilungen/Details/{id}", "Zurueckziehen")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.GetAsync($"/Mitteilungen/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task Elternpost_geht_an_Elternkonten_und_braucht_Jugendzugang()
    {
        var name = "Ep" + Guid.NewGuid().ToString("N")[..6];
        var (kind, _, eltern) = await MitgliedAsync(AbteilungCode.Jugend, name, konto: false, elternkonto: true);
        var id = await EntwurfAsync(MitteilungZiel.Eltern, AbteilungCode.Jugend, "Zeltlager " + name);
        Assert.True(await VeroeffentlichenAsync(id) >= 1);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.MitteilungEmpfaenger.SingleAsync(x => x.MitteilungId == id && x.KontoId == eltern);
            Assert.Equal(kind, e.MitgliedId);
        }

        Assert.Contains("Zeltlager " + name, WebUtility.HtmlDecode(await Client(eltern!.Value, Rollen.Erziehungsberechtigt).GetStringAsync($"/Mitteilungen/Details/{id}")));

        // Schriftführung ohne Zugang zur Jugend: Jugendfeuerwehr nicht wählbar, fremde Elternpost nicht verwaltbar.
        var schrift = Client(Guid.NewGuid(), Rollen.Schriftfuehrer);
        var formular = WebUtility.HtmlDecode(await schrift.GetStringAsync("/Mitteilungen/Bearbeiten"));
        Assert.DoesNotContain("Elternpost", formular);
        Assert.Equal(HttpStatusCode.Forbidden, (await schrift.GetAsync($"/Mitteilungen/Details/{id}")).StatusCode);
        Guid jf;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            jf = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Abteilungen.Where(a => a.Code == AbteilungCode.Jugend).Select(a => a.Id).SingleAsync();
        }

        var abgelehnt = await AbschickenAsync(schrift, "/Mitteilungen/Bearbeiten", "",
            [new("Formular.Titel", "Versuch"), new("Formular.Text", "Text"), new("Formular.Ziel", nameof(MitteilungZiel.Mitglieder)), new("Formular.Abteilungen", jf.ToString())]);
        Assert.Equal(HttpStatusCode.OK, abgelehnt.StatusCode);
        Assert.Contains("An diese Abteilung darfst du nicht schreiben.", WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Entwurf_ueber_das_Formular_und_Loeschlauf_nach_der_Frist()
    {
        var kommandant = Client(Guid.NewGuid(), Rollen.Kommandant);
        Guid ea;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            ea = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Abteilungen.Where(a => a.Code == AbteilungCode.Einsatz).Select(a => a.Id).SingleAsync();
        }

        var titel = "Formular " + Guid.NewGuid().ToString("N")[..6];
        var gespeichert = await AbschickenAsync(kommandant, "/Mitteilungen/Bearbeiten", "",
            [new("Formular.Titel", titel), new("Formular.Text", "Text"), new("Formular.Ziel", nameof(MitteilungZiel.Mitglieder)), new("Formular.Abteilungen", ea.ToString())]);
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);
        var details = gespeichert.Headers.Location!.OriginalString;
        Assert.Contains("Erreicht heute", WebUtility.HtmlDecode(await kommandant.GetStringAsync(details)));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, details, "Veroeffentlichen")).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Mitteilungen.SingleAsync(x => x.Titel == titel);
            Assert.Equal(MitteilungStatus.Veroeffentlicht, m.Status);
            await db.Mitteilungen.Where(x => x.Id == m.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.VeroeffentlichtAm, DateTimeOffset.UtcNow.AddMonths(-25)));
            var ergebnis = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.Contains(ergebnis, e => e.Regel == Loeschlauf.RegelMitteilungen && e.Anzahl >= 1);
            Assert.False(await db.Mitteilungen.IgnoreQueryFilters().AnyAsync(x => x.Id == m.Id));
            Assert.False(await db.MitteilungEmpfaenger.AnyAsync(x => x.MitteilungId == m.Id));
        }
    }
}
