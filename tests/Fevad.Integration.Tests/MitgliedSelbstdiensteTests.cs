using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Tagesverfügbarkeit (V43), Tausch und digitale Quittung der Ausrüstung sowie eigene Nachweise (V44).</summary>
[Collection(AnwendungCollection.Name)]
public partial class MitgliedSelbstdiensteTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle, Guid? konto = null)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, (konto ?? Guid.NewGuid()).ToString());
        return client;
    }

    private async Task<(Guid MitgliedId, Guid KontoId)> MitgliedAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = new Mitglied { Personalnummer = "S" + Guid.NewGuid().ToString("N")[..8], Vorname = "Sam", Nachname = "Selbst", Geburtsdatum = new DateOnly(1988, 5, 1) };
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();
        var email = $"s-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var k = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(k)).Succeeded);
        return (m.Id, k.Id);
    }

    private static async Task<string> TokenAsync(HttpClient client, string seite) =>
        WebUtility.HtmlDecode(TokenFeld().Match(await client.GetStringAsync(seite)).Groups[1].Value);

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string? handler, IEnumerable<KeyValuePair<string, string>> felder)
    {
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", await TokenAsync(client, seite)) };
        liste.AddRange(felder);
        return await client.PostAsync(handler is null ? seite : $"{seite}?handler={handler}", new FormUrlEncodedContent(liste));
    }

    [Fact]
    public async Task Tagesverfuegbarkeit_freiwillig_speichern_und_loeschen()
    {
        var (mitgliedId, konto) = await MitgliedAsync();
        var selbst = Client(Rollen.Mitglied, konto);
        Assert.Contains("Bisher keine Angabe", WebUtility.HtmlDecode(await selbst.GetStringAsync("/Konto/Verfuegbarkeit")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Verfuegbarkeit", null,
            [new("Tage", nameof(Werktage.Montag)), new("Tage", nameof(Werktage.Mittwoch))])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var m = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitglieder.SingleAsync(x => x.Id == mitgliedId);
            Assert.Equal(Werktage.Montag | Werktage.Mittwoch, m.Tagesverfuegbarkeit);
            Assert.NotNull(m.TagesverfuegbarkeitStand);
        }

        // Ohne Häkchen heißt „an keinem Werktag“, Löschen heißt „keine Angabe“.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Verfuegbarkeit", null, [])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Equal(Werktage.Keine, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitglieder.SingleAsync(x => x.Id == mitgliedId)).Tagesverfuegbarkeit);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Verfuegbarkeit", "Loeschen", [])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitglieder.SingleAsync(x => x.Id == mitgliedId)).Tagesverfuegbarkeit);
        }

        var kennzahlen = WebUtility.HtmlDecode(await Client(Rollen.Kommandant).GetStringAsync("/Auswertungen/Fuehrung"));
        Assert.Contains("Tagesverfügbarkeit werktags 06:00–18:00 Uhr", kennzahlen);
    }

    [Fact]
    public async Task Tausch_anfordern_bearbeiten_und_Ausgabe_digital_bestaetigen()
    {
        var (mitgliedId, konto) = await MitgliedAsync();
        Guid vorgangId, ausstattungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var artikel = new Artikel { Code = "T" + Guid.NewGuid().ToString("N")[..6], Bezeichnung = "Einsatzjacke Test" };
            var vorgang = new Kleiderkammervorgang { Nummer = "KT-" + Guid.NewGuid().ToString("N")[..8], MitgliedId = mitgliedId, Zeitpunkt = DateTimeOffset.UtcNow.AddDays(-1) };
            var stueck = new Ausstattung { MitgliedId = mitgliedId, ArtikelId = artikel.Id, Groesse = "52", AusgegebenAm = DateOnly.FromDateTime(DateTime.Today), AusgabeVorgangId = vorgang.Id };
            db.AddRange(artikel, vorgang, stueck);
            await db.SaveChangesAsync();
            (vorgangId, ausstattungId) = (vorgang.Id, stueck.Id);
        }

        var selbst = Client(Rollen.Mitglied, konto);
        var seite = WebUtility.HtmlDecode(await selbst.GetStringAsync("/Konto/Ausstattung"));
        Assert.Contains("Empfang bestätigen", seite);
        Assert.Contains("Einsatzjacke Test, Größe 52", seite);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Ausstattung", "Bestaetigen", [new("vorgangId", vorgangId.ToString())])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Ausstattung", "Tausch",
            [new("Formular.AusstattungId", ausstattungId.ToString()), new("Formular.Grund", nameof(Tauschgrund.Groesse)), new("Formular.GewuenschteGroesse", "54")])).StatusCode);
        // Doppelt anfordern geht nicht, fremde Vorgänge bestätigen auch nicht.
        Assert.Equal(HttpStatusCode.OK, (await AbschickenAsync(selbst, "/Konto/Ausstattung", "Tausch", [new("Formular.AusstattungId", ausstattungId.ToString())])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AbschickenAsync(Client(Rollen.Mitglied, (await MitgliedAsync()).KontoId), "/Konto/Ausstattung", "Bestaetigen",
            [new("vorgangId", vorgangId.ToString())])).StatusCode);

        Guid anforderungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var v = await db.Kleiderkammervorgaenge.SingleAsync(x => x.Id == vorgangId);
            Assert.True(v.QuittiertDigital);
            Assert.NotNull(v.QuittiertAm);
            var t = await db.Tauschanforderungen.SingleAsync(x => x.MitgliedId == mitgliedId);
            Assert.Equal("54", t.GewuenschteGroesse);
            anforderungId = t.Id;
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Geraetewart), "/Kleiderkammer/Tausch", null,
            [new("id", anforderungId.ToString()), new("status", nameof(TauschStatus.Erledigt))])).StatusCode);
        var kleiderwart = Client(Rollen.Kleiderwart);
        Assert.Contains("Einsatzjacke Test", WebUtility.HtmlDecode(await kleiderwart.GetStringAsync("/Kleiderkammer/Tausch")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kleiderwart, "/Kleiderkammer/Tausch", null,
            [new("id", anforderungId.ToString()), new("status", nameof(TauschStatus.Erledigt)), new("antwort", "Abholung Donnerstag 19:00 Uhr")])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(TauschStatus.Erledigt, (await db.Tauschanforderungen.SingleAsync(x => x.Id == anforderungId)).Status);
            Assert.Contains(await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto).ToListAsync(), b => b.Link == "/Konto/Ausstattung");
        }

        Assert.Contains("Abholung Donnerstag 19:00 Uhr", WebUtility.HtmlDecode(await selbst.GetStringAsync("/Konto/Ausstattung")));
    }

    [Fact]
    public async Task Eigener_Nachweis_wird_geprueft_uebernommen_und_die_Datei_geloescht()
    {
        var (mitgliedId, konto) = await MitgliedAsync();
        Guid artId, gesundheitId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = new Nachweisart { Code = "EH" + Guid.NewGuid().ToString("N")[..5], Bezeichnung = "Erste Hilfe Test", IntervallMonate = 24 };
            var gesundheit = new Nachweisart { Code = "G" + Guid.NewGuid().ToString("N")[..5], Bezeichnung = "Untersuchung Test", Gesundheitsbezogen = true };
            db.Nachweisarten.AddRange(art, gesundheit);
            await db.SaveChangesAsync();
            (artId, gesundheitId) = (art.Id, gesundheit.Id);
        }

        var selbst = Client(Rollen.Mitglied, konto);
        var seite = WebUtility.HtmlDecode(await selbst.GetStringAsync("/Konto/Nachweise"));
        Assert.Contains("Erste Hilfe Test", seite);
        Assert.DoesNotContain("Untersuchung Test", seite);

        async Task<HttpResponseMessage> Einreichen(string art)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(await TokenAsync(selbst, "/Konto/Nachweise")), "__RequestVerificationToken" },
                { new StringContent(art), "Formular.Art" },
                { new StringContent(DateTime.Today.AddDays(-3).ToString("dd.MM.yyyy")), "Formular.Datum" },
                { new StringContent("DRK Göppingen"), "Formular.Aussteller" },
            };
            var datei = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 Test"));
            datei.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(datei, "Datei", "erste-hilfe.pdf");
            return await selbst.PostAsync("/Konto/Nachweise", form);
        }

        Assert.Equal(HttpStatusCode.OK, (await Einreichen($"n:{gesundheitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Einreichen($"n:{artId}")).StatusCode);
        Guid einreichungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var e = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Nachweiseinreichungen.SingleAsync(x => x.MitgliedId == mitgliedId);
            Assert.NotNull(e.Inhalt);
            einreichungId = e.Id;
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync("/Personal/Einreichungen")).StatusCode);
        var kommandant = Client(Rollen.Kommandant);
        var datei = await kommandant.GetAsync($"/Personal/Einreichungen?handler=Datei&id={einreichungId}");
        Assert.Equal("application/pdf", datei.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, "/Personal/Einreichungen", null,
            [new("id", einreichungId.ToString()), new("uebernehmen", "true")])).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.Nachweiseinreichungen.SingleAsync(x => x.Id == einreichungId);
            Assert.Equal(EinreichungStatus.Uebernommen, e.Status);
            Assert.Null(e.Inhalt);
            var n = await db.Nachweise.SingleAsync(x => x.MitgliedId == mitgliedId && x.NachweisartId == artId);
            Assert.Equal(n.Datum.AddMonths(24), n.GueltigBis);
            Assert.Equal("DRK Göppingen", n.DurchgefuehrtVon);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await kommandant.GetAsync($"/Personal/Einreichungen?handler=Datei&id={einreichungId}")).StatusCode);
    }
}
