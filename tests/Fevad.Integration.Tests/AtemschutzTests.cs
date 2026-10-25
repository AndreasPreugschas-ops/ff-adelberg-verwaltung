using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kennzeichen „aktiver Atemschutzgeräteträger“ und Selbstmeldung „nicht einsetzbar“ (V62).</summary>
[Collection(AnwendungCollection.Name)]
public partial class AtemschutzTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private readonly WebApplicationFactory<Program> _factory = TestAnmeldung.Factory(fixture);

    private HttpClient Angemeldet(Guid? benutzerId, params string[] rollen)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, (benutzerId ?? Guid.NewGuid()).ToString());
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private async Task<(Guid MitgliedId, Guid BenutzerId)> GeraetetraegerAsync(string nachname, bool aktiv)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
        var agt = await db.Qualifikationen.SingleAsync(q => q.Code == "AGT");
        var mitglied = new Mitglied { Personalnummer = "A-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Paul", Nachname = nachname, Geburtsdatum = new(1990, 1, 1), AtemschutzAktiv = aktiv };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2015, 1, 1) });
        db.Mitglieder.Add(mitglied);
        db.QualifikationNachweise.Add(new() { MitgliedId = mitglied.Id, QualifikationId = agt.Id, ErworbenAm = new(2016, 5, 1) });
        await db.SaveChangesAsync();

        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"a-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = mitglied.Id };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        Assert.True((await benutzer.AddToRoleAsync(konto, Rollen.Mitglied)).Succeeded);
        return (mitglied.Id, konto.Id);
    }

    [Fact]
    public async Task Selbstmeldung_sperrt_sofort_und_braucht_ab_zwei_Wochen_Genehmigung()
    {
        var (mitgliedId, benutzerId) = await GeraetetraegerAsync("Selbstmelder", aktiv: true);
        var heute = Anzeige.Heute(TimeProvider.System);
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);

        Assert.Contains("Atemschutz: nicht einsetzbar melden", await mitglied.GetStringAsync("/Konto/Index"));
        var seite = await mitglied.GetStringAsync("/Konto/Atemschutz");
        Assert.Equal(HttpStatusCode.Redirect, (await mitglied.PostAsync("/Konto/Atemschutz", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Von"] = heute.ToString("dd.MM.yyyy"),
            ["Bis"] = heute.AddDays(20).ToString("dd.MM.yyyy"),
            ["__RequestVerificationToken"] = Token(seite),
        }))).StatusCode);

        AtemschutzSperre sperre;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            sperre = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AtemschutzSperren.SingleAsync(s => s.MitgliedId == mitgliedId);
            Assert.True(sperre.Selbstmeldung);
            Assert.True(sperre.GenehmigungOffen(14));
        }

        // Der Atemschutzwart sieht die Abmeldung, der Geräteträger ist rot, und genehmigt.
        var wart = Angemeldet(null, Rollen.Atemschutzgeraetewart);
        var uebersicht = await wart.GetStringAsync("/Atemschutz/Index");
        Assert.Contains("Selbstmelder, Paul", uebersicht);
        Assert.Contains("Genehmigung offen", uebersicht);
        Assert.Contains("nicht einsetzbar bis", uebersicht);
        Assert.Equal(HttpStatusCode.Redirect, (await wart.PostAsync($"/Atemschutz/Index?handler=Genehmigen&sperreId={sperre.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(uebersicht) }))).StatusCode);

        // Das Mitglied meldet sich wieder einsatzbereit.
        seite = await mitglied.GetStringAsync("/Konto/Atemschutz");
        Assert.DoesNotContain("Genehmigung offen", seite);
        Assert.Equal(HttpStatusCode.Redirect, (await mitglied.PostAsync($"/Konto/Atemschutz?handler=Aufheben&sperreId={sperre.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }))).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var aufgehoben = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AtemschutzSperren.SingleAsync(s => s.Id == sperre.Id);
            Assert.NotNull(aufgehoben.GenehmigtAm);
            Assert.NotNull(aufgehoben.AufgehobenAm);
        }
    }

    [Fact]
    public async Task Loeschlauf_entfernt_alte_Abmeldungen_nach_L38()
    {
        var (mitgliedId, _) = await GeraetetraegerAsync("Altabmeldung", aktiv: true);
        var heute = Anzeige.Heute(TimeProvider.System);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // L38: 24 Monate nach Ende (Andi, 25.10.2026); eine Abmeldung von vor 19 Monaten bleibt noch.
        var alt = new AtemschutzSperre { MitgliedId = mitgliedId, Von = heute.AddMonths(-26), Bis = heute.AddMonths(-25), Selbstmeldung = true, GemeldetAm = DateTimeOffset.UtcNow.AddMonths(-26) };
        var mittel = new AtemschutzSperre { MitgliedId = mitgliedId, Von = heute.AddMonths(-20), Bis = heute.AddMonths(-19), Selbstmeldung = true, GemeldetAm = DateTimeOffset.UtcNow.AddMonths(-20) };
        var aktuell = new AtemschutzSperre { MitgliedId = mitgliedId, Von = heute, Bis = heute.AddDays(3), Selbstmeldung = true, GemeldetAm = DateTimeOffset.UtcNow };
        db.AtemschutzSperren.AddRange(alt, mittel, aktuell);
        await db.SaveChangesAsync();

        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Fevad.Infrastructure.Datenschutz.Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.True(ergebnisse.Single(e => e.Regel == Fevad.Infrastructure.Datenschutz.Loeschlauf.RegelAtemschutzSperren).Anzahl >= 1);
        var uebrig = await db.AtemschutzSperren.AsNoTracking().IgnoreQueryFilters().Where(s => s.MitgliedId == mitgliedId).Select(s => s.Id).ToListAsync();
        Assert.Equal(new[] { mittel.Id, aktuell.Id }.Order(), uebrig.Order());
    }

    [Fact]
    public async Task Ohne_Kennzeichen_keine_Selbstmeldung_und_Kennzeichnen_durch_den_Wart()
    {
        var (mitgliedId, benutzerId) = await GeraetetraegerAsync("Ungekennzeichnet", aktiv: false);
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        Assert.DoesNotContain("nicht einsetzbar melden", await mitglied.GetStringAsync("/Konto/Index"));
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.GetAsync("/Konto/Atemschutz")).StatusCode);

        // Normale Mitglieder kommen nicht in die Übersicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Atemschutz/Index")).StatusCode);

        var wart = Angemeldet(null, Rollen.Atemschutzgeraetewart);
        var uebersicht = await wart.GetStringAsync("/Atemschutz/Index");
        Assert.Contains("Ungekennzeichnet, Paul", uebersicht);
        Assert.Equal(HttpStatusCode.Redirect, (await wart.PostAsync($"/Atemschutz/Index?handler=Kennzeichnen&mitgliedId={mitgliedId}&aktiv=true",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(uebersicht) }))).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Mitglieder.SingleAsync(m => m.Id == mitgliedId)).AtemschutzAktiv);
        Assert.Equal(HttpStatusCode.OK, (await mitglied.GetAsync("/Konto/Atemschutz")).StatusCode);
    }
}
