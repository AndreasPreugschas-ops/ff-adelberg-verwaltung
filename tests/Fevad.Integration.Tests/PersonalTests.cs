using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class PersonalTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    /// <summary>Seite als Text; Razor kodiert Umlaute als Zeichenreferenzen.</summary>
    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    /// <summary>Legt einen Atemschutzgeräteträger der Einsatzabteilung mit Vorsorge an.</summary>
    private async Task<Mitglied> GeraetetraegerAnlegenAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ea = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
        var agt = await db.Qualifikationen.SingleAsync(q => q.Code == Qualifikation.CodeAtemschutz);
        var nummer = "P" + Guid.NewGuid().ToString("N")[..8];
        var mitglied = new Mitglied { Personalnummer = nummer, Vorname = "Anna", Nachname = "Träger-" + nummer, Geburtsdatum = new(1990, 4, 1) };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = ea.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) });
        db.Mitglieder.Add(mitglied);
        db.QualifikationNachweise.Add(new QualifikationNachweis { Mitglied = mitglied, QualifikationId = agt.Id, ErworbenAm = new(2012, 3, 1) });
        db.AtemschutzVorsorgen.Add(new AtemschutzVorsorge
        {
            Mitglied = mitglied, UntersuchtAm = new(2025, 2, 1), Ergebnis = VorsorgeErgebnis.Geeignet, NaechsteFaellig = new(2028, 2, 1), Bemerkung = "Termin Betriebsarzt",
        });
        await db.SaveChangesAsync();
        return mitglied;
    }

    [Fact]
    public async Task Personalseiten_brauchen_Ausbildungsrecht()
    {
        var factory = TestAnmeldung.Factory(fixture);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Kassier).GetAsync("/Personal")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Personal")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Personal/Lehrgaenge")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Personal/Nachweisart")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, Rollen.Kommandant).GetAsync("/Personal/Nachweisart")).StatusCode);
    }

    [Fact]
    public async Task Vorsorge_sieht_nur_wer_Atemschutz_lesen_darf()
    {
        var mitglied = await GeraetetraegerAnlegenAsync();
        var factory = TestAnmeldung.Factory(fixture);

        var fuehrung = await SeiteAsync(Angemeldet(factory, Rollen.Fuehrungskraft), $"/Personal/Akte/{mitglied.Id}");
        Assert.Contains("Belastungsübung", fuehrung);
        Assert.DoesNotContain("G 26.3", fuehrung);
        Assert.DoesNotContain("Termin Betriebsarzt", fuehrung);

        var uebersicht = await SeiteAsync(Angemeldet(factory, Rollen.Fuehrungskraft), "/Personal?Alle=true");
        Assert.DoesNotContain("G 26.3", uebersicht);

        var agw = await SeiteAsync(Angemeldet(factory, Rollen.Atemschutzgeraetewart), $"/Personal/Akte/{mitglied.Id}");
        Assert.Contains("G 26.3", agw);
        Assert.Contains("Termin Betriebsarzt", agw);
    }

    [Fact]
    public async Task Nachweis_eintragen_berechnet_Ablauf_aus_dem_Katalog()
    {
        var mitglied = await GeraetetraegerAnlegenAsync();
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Fuehrungskraft);
        var html = await client.GetStringAsync($"/Personal/Akte/{mitglied.Id}");

        Guid artId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            artId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Nachweisarten.SingleAsync(a => a.Code == "FSK")).Id;
        }

        var antwort = await client.PostAsync($"/Personal/Akte/{mitglied.Id}?handler=Nachweis", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["NachweisartId"] = artId.ToString(),
            ["Datum"] = "2026-03-15",
            ["DurchgefuehrtVon"] = "Kommandant",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        // Vorsorge darf eine Führungskraft nicht eintragen.
        var verboten = await client.PostAsync($"/Personal/Akte/{mitglied.Id}?handler=Vorsorge", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["UntersuchtAm"] = "2026-03-15",
            ["Ergebnis"] = nameof(VorsorgeErgebnis.Geeignet),
            ["NaechsteFaellig"] = "2029-03-15",
        }));
        Assert.Equal(HttpStatusCode.Forbidden, verboten.StatusCode);

        await using var pruefung = fixture.Factory.Services.CreateAsyncScope();
        var nachweis = await pruefung.ServiceProvider.GetRequiredService<AppDbContext>().Nachweise.SingleAsync(n => n.MitgliedId == mitglied.Id);
        Assert.Equal(new DateOnly(2026, 9, 15), nachweis.GueltigBis);
        Assert.Equal("Kommandant", nachweis.DurchgefuehrtVon);
    }

    [Fact]
    public async Task Sammelerfassung_traegt_Belastungsuebung_fuer_mehrere_ein_ohne_Doppel()
    {
        var erste = await GeraetetraegerAnlegenAsync();
        var zweite = await GeraetetraegerAnlegenAsync();
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Atemschutzgeraetewart);
        var html = await client.GetStringAsync("/Personal/Sammelerfassung");
        Assert.DoesNotContain("value=\"N:", html);
        Assert.Contains("value=\"A:Belastungsuebung\"", html);

        HttpContent Formular() => new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", Token(html)),
            new("Typ", "A:" + nameof(AtemschutzNachweisArt.Belastungsuebung)),
            new("Datum", "2026-10-01"),
            new("Durch", "Übungsanlage Göppingen"),
            new("Minuten", "25"),
            new("Mitglieder", erste.Id.ToString()),
            new("Mitglieder", zweite.Id.ToString()),
        ]);

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Personal/Sammelerfassung", Formular())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Personal/Sammelerfassung", Formular())).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var eintraege = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AtemschutzNachweise
            .Where(n => n.MitgliedId == erste.Id || n.MitgliedId == zweite.Id)
            .ToListAsync();
        Assert.Equal(2, eintraege.Count);
        Assert.All(eintraege, n => Assert.Equal(25, n.Minuten));
    }

    [Fact]
    public async Task Fristerinnerung_geht_nach_Rolle_und_hoechstens_einmal_je_Intervall()
    {
        var mitglied = await GeraetetraegerAnlegenAsync();
        var mail = new MailSammler();
        var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMailVersand>(mail)));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            foreach (var (email, rolle) in new[] { ("kdt-erinnerung@fevad.test", Rollen.Kommandant), ("agw-erinnerung@fevad.test", Rollen.Atemschutzgeraetewart), ("kassier-erinnerung@fevad.test", Rollen.Kassier) })
            {
                var konto = new Benutzer { UserName = email, Email = email };
                Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
                await benutzer.AddToRoleAsync(konto, rolle);
            }
        }

        // Montag 08:00 Ortszeit, nach der Stunde aus Frist.Erinnerung.AbStunde.
        var jetzt = new DateTimeOffset(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FristErinnerung>().AusfuehrenAsync(jetzt, default);
        }

        var kommandant = Assert.Single(mail.Gesendet, m => m.Empfaenger == "kdt-erinnerung@fevad.test");
        Assert.Contains(mitglied.Nachname, kommandant.Text);
        Assert.Contains("Belastungsübung", kommandant.Text);
        Assert.Contains("Jährliche Unterweisung", kommandant.Text);

        // Der Atemschutzgerätewart bekommt nur Atemschutz, keine allgemeinen Nachweise.
        var agw = Assert.Single(mail.Gesendet, m => m.Empfaenger == "agw-erinnerung@fevad.test");
        Assert.Contains("Belastungsübung", agw.Text);
        Assert.DoesNotContain("Jährliche Unterweisung", agw.Text);
        Assert.DoesNotContain(mail.Gesendet, m => m.Empfaenger == "kassier-erinnerung@fevad.test");

        // Drei Tage später: noch innerhalb des Intervalls, keine neue Mail.
        var anzahl = mail.Gesendet.Count;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FristErinnerung>().AusfuehrenAsync(jetzt.AddDays(3), default));
        }

        Assert.Equal(anzahl, mail.Gesendet.Count);
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
