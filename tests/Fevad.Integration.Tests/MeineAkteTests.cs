using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>„Meine Akte“ und Erinnerungen an das Mitglied selbst (V38).</summary>
[Collection(AnwendungCollection.Name)]
public partial class MeineAkteTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(Guid kontoId)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, kontoId.ToString());
        return client;
    }

    private async Task<(Guid MitgliedId, Guid KontoId, string Email)> MitgliedMitKontoAsync(string nachname, Action<Benutzer>? konto = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied { Personalnummer = "K" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kim", Nachname = nachname, Geburtsdatum = Heute.AddYears(-30) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
        m.Dienstgrade.Add(new DienstgradZuordnung { DienstgradId = await db.Dienstgrade.OrderBy(d => d.Sortierung).Select(d => d.Id).FirstAsync(), Ab = Heute.AddYears(-4) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();

        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"k-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var neu = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        konto?.Invoke(neu);
        Assert.True((await benutzer.CreateAsync(neu)).Succeeded);
        return (m.Id, neu.Id, email);
    }

    [Fact]
    public async Task Mitglied_sieht_eigene_Akte_mit_Teilnahme_je_Jahr()
    {
        var name = "Akte" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, kontoId, _) = await MitgliedMitKontoAsync(name);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beginn = new DateTimeOffset(Heute.Year - 1, 6, 10, 17, 0, 0, TimeSpan.Zero);
            var dienst = new Dienst { Titel = "Übung " + name, Beginn = beginn, Ende = beginn.AddHours(2), Status = DienstStatus.Durchgefuehrt, ZaehltFuerFortbildung = true };
            dienst.Anwesenheiten.Add(new Anwesenheit { MitgliedId = mitgliedId, Status = AnwesenheitStatus.Anwesend });
            db.Dienste.Add(dienst);
            await db.SaveChangesAsync();
        }

        var seite = WebUtility.HtmlDecode(await Client(kontoId).GetStringAsync("/Konto/Akte"));
        Assert.Contains("Meine Akte", seite);
        Assert.Contains(name, seite);
        Assert.Contains("Jährliche Unterweisung", seite);
        Assert.Matches($"<td>{Heute.Year - 1}</td><td>1</td><td>0</td><td>0</td>\\s*<td>2</td><td>2</td>", seite);
    }

    [Fact]
    public async Task Erinnerung_geht_an_das_Mitglied_ausser_bei_Abwesenheit_oder_abbestellt()
    {
        var name = "Erin" + Guid.NewGuid().ToString("N")[..6];
        var (_, kontoId, email) = await MitgliedMitKontoAsync(name);
        var (abwesendId, _, abwesendEmail) = await MitgliedMitKontoAsync(name + "a");
        var (_, abbestelltId, abbestelltEmail) = await MitgliedMitKontoAsync(name + "b");
        var jetzt = DateTimeOffset.UtcNow;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Abwesenheiten.Add(new Abwesenheit { MitgliedId = abwesendId, Von = Heute.AddDays(-1), Bis = Heute.AddDays(7), Art = AbwesenheitArt.Urlaub });
            db.Dienste.Add(new Dienst
            {
                Titel = "Übung " + name, Beginn = jetzt.AddDays(5), Ende = jetzt.AddDays(5).AddHours(2), Status = DienstStatus.Geplant,
                Abteilungen = [await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)],
            });
            await db.SaveChangesAsync();
        }

        // Abbestellen über das Konto, wie es das Mitglied tut.
        var client = Client(abbestelltId);
        var seite = await client.GetStringAsync("/Konto");
        var antwort = await client.PostAsync("/Konto?handler=Erinnerungen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value),
            ["fristen"] = "false", ["ausstattung"] = "false", ["rueckmeldungen"] = "false",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var mail = new MailSammler();
        var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMailVersand>(mail)));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<EigeneErinnerung>().SendenAsync(jetzt, Heute, "https://fevad.test", default) >= 1);
        }

        var eigene = Assert.Single(mail.Gesendet, m => m.Empfaenger == email);
        Assert.Contains("Jährliche Unterweisung", eigene.Text);
        Assert.Contains("Übung " + name, eigene.Text);
        Assert.Contains("https://fevad.test/Konto/Akte", eigene.Text);
        Assert.DoesNotContain(mail.Gesendet, m => m.Empfaenger == abwesendEmail);
        Assert.DoesNotContain(mail.Gesendet, m => m.Empfaenger == abbestelltEmail);
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
