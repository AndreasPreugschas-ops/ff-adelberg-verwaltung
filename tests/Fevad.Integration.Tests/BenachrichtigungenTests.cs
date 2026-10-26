using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Benachrichtigungen in der Anwendung, Sammelmail und Löschregel L64 (V72).</summary>
[Collection(AnwendungCollection.Name)]
public partial class BenachrichtigungenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(Guid kontoId)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, kontoId.ToString());
        return client;
    }

    private async Task<(Guid Id, string Email)> KontoAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var email = $"b-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(konto)).Succeeded);
        return (konto.Id, email);
    }

    private async Task<int> SendenAsync(Guid konto, Benachrichtigungsdienst.Inhalt inhalt)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Benachrichtigungsdienst>().SendenAsync([konto], inhalt);
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string handler, string? id = null)
    {
        var html = await client.GetStringAsync(seite);
        var felder = new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value) };
        return await client.PostAsync($"{seite}?handler={handler}" + (id is null ? "" : $"&id={id}"), new FormUrlEncodedContent(felder));
    }

    [Fact]
    public void Nur_interne_Links_gelten()
    {
        Assert.True(Benachrichtigung.IstInternerLink("/Mitteilungen/Details/1"));
        Assert.True(Benachrichtigung.IstInternerLink("/"));
        Assert.False(Benachrichtigung.IstInternerLink("//boese.example"));
        Assert.False(Benachrichtigung.IstInternerLink("/\\boese.example"));
        Assert.False(Benachrichtigung.IstInternerLink("https://boese.example"));
        Assert.False(Benachrichtigung.IstInternerLink(null));
    }

    [Fact]
    public async Task Glocke_zeigt_Ungelesene_und_Oeffnen_vermerkt_das_Lesen()
    {
        var (konto, _) = await KontoAsync();
        var (fremd, _) = await KontoAsync();
        var bezug = Guid.NewGuid();
        Assert.Equal(1, await SendenAsync(konto, new(Benachrichtigungsart.Hinweis, "Fahrzeug außer Betrieb: HLF Test", "/Technik", bezug)));
        // Derselbe Bezug wird nicht doppelt gemeldet, solange er ungelesen ist.
        Assert.Equal(0, await SendenAsync(konto, new(Benachrichtigungsart.Hinweis, "Fahrzeug außer Betrieb: HLF Test", "/Technik", bezug)));
        await SendenAsync(fremd, new(Benachrichtigungsart.Hinweis, "Nur für andere", "/Technik"));

        var client = Client(konto);
        var start = WebUtility.HtmlDecode(await client.GetStringAsync("/Konto"));
        Assert.Contains("Benachrichtigungen, 1 neue", start);
        var liste = WebUtility.HtmlDecode(await client.GetStringAsync("/Benachrichtigungen"));
        Assert.Contains("Fahrzeug außer Betrieb: HLF Test", liste);
        Assert.DoesNotContain("Nur für andere", liste);

        Guid id;
        Guid fremdeId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto).Select(b => b.Id).SingleAsync();
            fremdeId = await db.Benachrichtigungen.Where(b => b.EmpfaengerId == fremd).Select(b => b.Id).SingleAsync();
        }

        // Fremde Benachrichtigungen lassen sich weder öffnen noch als gelesen markieren.
        var fremdOeffnen = await AbschickenAsync(client, "/Benachrichtigungen", "Oeffnen", fremdeId.ToString());
        Assert.Equal("/Benachrichtigungen", fremdOeffnen.Headers.Location?.OriginalString);

        var oeffnen = await AbschickenAsync(client, "/Benachrichtigungen", "Oeffnen", id.ToString());
        Assert.Equal(HttpStatusCode.Redirect, oeffnen.StatusCode);
        Assert.Equal("/Technik", oeffnen.Headers.Location?.OriginalString);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.Benachrichtigungen.Where(b => b.Id == id).Select(b => b.GelesenAm).SingleAsync());
            Assert.Null(await db.Benachrichtigungen.Where(b => b.Id == fremdeId).Select(b => b.GelesenAm).SingleAsync());
        }

        Assert.Contains("Benachrichtigungen, keine neuen", WebUtility.HtmlDecode(await client.GetStringAsync("/Konto")));
    }

    [Fact]
    public async Task Sammelmail_nennt_nur_Anzahlen_einmal_am_Tag_und_ohne_abbestellte_Arten()
    {
        var (konto, email) = await KontoAsync();
        var client = Client(konto);
        // Hinweise abbestellen, wie es das Mitglied im Konto tut: nur Mitteilungen bleiben angehakt.
        var html = await client.GetStringAsync("/Konto");
        var abbestellt = await client.PostAsync("/Konto?handler=Sammelmail", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)),
            new("arten", nameof(Benachrichtigungsart.Mitteilung)),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, abbestellt.StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ohne = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == konto).Select(u => u.SammelmailOhne).SingleAsync();
            Assert.Equal(Benachrichtigungsart.Hinweis, ohne & Benachrichtigungsart.Hinweis);
            Assert.Equal(Benachrichtigungsart.Keine, ohne & Benachrichtigungsart.Mitteilung);
        }

        await SendenAsync(konto, new(Benachrichtigungsart.Mitteilung, "Geheimer Titel der Mitteilung", "/"));
        await SendenAsync(konto, new(Benachrichtigungsart.Hinweis, "Hinweis ohne Mail", "/"));

        var mail = new MailSammler();
        var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMailVersand>(mail)));
        var heute = DateTimeOffset.UtcNow;
        var abends = new DateTimeOffset(heute.Year, heute.Month, heute.Day, 17, 30, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sammelmail = scope.ServiceProvider.GetRequiredService<Sammelmail>();
            await sammelmail.AusfuehrenAsync(abends.AddHours(-12), default);
            Assert.DoesNotContain(mail.Gesendet, m => m.Empfaenger == email);
            await sammelmail.AusfuehrenAsync(abends, default);
        }

        var gesendet = Assert.Single(mail.Gesendet, m => m.Empfaenger == email);
        Assert.Contains("Mitteilungen: 1", gesendet.Text);
        Assert.DoesNotContain("Hinweise", gesendet.Text);
        Assert.DoesNotContain("Geheimer Titel", gesendet.Text);

        // Am selben Tag keine zweite Mail, auch wenn Neues dazukommt.
        await SendenAsync(konto, new(Benachrichtigungsart.Mitteilung, "Zweite Mitteilung", "/"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Sammelmail>().AusfuehrenAsync(abends.AddMinutes(30), default);
        }

        Assert.Single(mail.Gesendet, m => m.Empfaenger == email);
    }

    [Fact]
    public async Task Loeschlauf_entfernt_gelesene_nach_der_Frist()
    {
        var (konto, _) = await KontoAsync();
        await SendenAsync(konto, new(Benachrichtigungsart.Termin, "Alt und gelesen", "/"));
        await SendenAsync(konto, new(Benachrichtigungsart.Termin, "Neu und gelesen", "/"));
        await SendenAsync(konto, new(Benachrichtigungsart.Termin, "Ungelesen", "/"));
        var jetzt = DateTimeOffset.UtcNow;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto && b.Titel == "Alt und gelesen")
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.GelesenAm, jetzt.AddDays(-91)));
            await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto && b.Titel == "Neu und gelesen")
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.GelesenAm, jetzt.AddDays(-5)));
            var ergebnis = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            Assert.Contains(ergebnis, e => e.Regel == Loeschlauf.RegelBenachrichtigungen && e.Anzahl >= 1);
            var uebrig = await db.Benachrichtigungen.Where(b => b.EmpfaengerId == konto).Select(b => b.Titel).OrderBy(t => t).ToListAsync();
            Assert.Equal(["Neu und gelesen", "Ungelesen"], uebrig);
        }
    }

    private sealed class MailSammler : IMailVersand
    {
        public List<(string Empfaenger, string Betreff, string Text)> Gesendet { get; } = [];

        public bool Aktiv => true;

        public Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default)
        {
            lock (Gesendet)
            {
                Gesendet.Add((empfaenger, betreff, text));
            }

            return Task.CompletedTask;
        }
    }
}
