using Fevad.Web.Infrastruktur;
using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class PersonalpruefungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

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

    private async Task<(Guid Id, string Name)> EinsatzkraftAnlegenAsync()
    {
        var name = "Pruef" + Guid.NewGuid().ToString("N")[..6];
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var einsatz = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied { Personalnummer = "P" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = name, Geburtsdatum = Heute.AddYears(-35) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-10) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();
        return (m.Id, name);
    }

    private async Task<List<Pruefhinweis>> PruefenAsync(Guid id)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Personalpruefung>().PruefenAsync([id], DateTimeOffset.UtcNow, default);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Pruefhinweise.AsNoTracking()
            .Where(p => p.MitgliedId == id && p.Art == PruefhinweisArt.Funktion).ToListAsync();
    }

    [Fact]
    public async Task Neuer_Lehrgang_loest_Pruefung_aus_und_erledigt_den_Hinweis()
    {
        var (id, _) = await EinsatzkraftAnlegenAsync();
        var warteschlange = fixture.Factory.Services.GetRequiredService<Pruefwarteschlange>();
        while (warteschlange.Leser.TryRead(out _)) { }

        Guid zuordnung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gw = await db.Funktionen.SingleAsync(f => f.Code == "GW");
            var z = new FunktionZuordnung { MitgliedId = id, FunktionId = gw.Id, Art = FunktionArt.Bestellung, GueltigAb = Heute.AddMonths(-2) };
            db.FunktionZuordnungen.Add(z);
            await db.SaveChangesAsync();
            zuordnung = z.Id;
        }

        // Die Änderung meldet das Mitglied zur Prüfung.
        var gemeldet = new List<Guid?>();
        while (warteschlange.Leser.TryRead(out var x)) { gemeldet.Add(x); }
        Assert.Contains(id, gemeldet);

        var hinweis = Assert.Single(await PruefenAsync(id));
        Assert.Equal(PruefhinweisStatus.Offen, hinweis.Status);
        Assert.Equal($"Funktion:{zuordnung}:Voraussetzung", hinweis.Schluessel);
        Assert.Contains("Gerätewart", hinweis.Text);

        // Ein zweiter Lauf legt nichts doppelt an.
        Assert.Single(await PruefenAsync(id));

        // In einer Transaktion wird erst nach dem Commit gemeldet.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lehrgang = await db.Qualifikationen.SingleAsync(q => q.Code == "GW");
            await using var transaktion = await db.Database.BeginTransactionAsync();
            db.QualifikationNachweise.Add(new QualifikationNachweis { MitgliedId = id, QualifikationId = lehrgang.Id, ErworbenAm = Heute.AddDays(-1) });
            await db.SaveChangesAsync();
            Assert.False(warteschlange.Leser.TryRead(out _));
            await transaktion.CommitAsync();
        }

        Assert.True(warteschlange.Leser.TryRead(out var nachCommit));
        Assert.Equal(id, nachCommit);

        var erledigt = Assert.Single(await PruefenAsync(id));
        Assert.Equal(PruefhinweisStatus.Entfallen, erledigt.Status);
        Assert.NotNull(erledigt.AbgeschlossenAm);
    }

    [Fact]
    public async Task Funktion_mit_Historie_eintragen_beenden_und_Pruefergebnisse_sehen()
    {
        var (id, name) = await EinsatzkraftAnlegenAsync();
        Guid jw, sib;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            jw = (await db.Funktionen.SingleAsync(f => f.Code == "JW")).Id;
            sib = (await db.Funktionen.SingleAsync(f => f.Code == "SIB")).Id;
        }

        var client = Angemeldet(Rollen.Kommandant);
        var seite = $"/Laufbahn/Mitglied/{id}";
        // Jugendfeuerwehrwart: Die Jugendfeuerwehr wird ergänzt, die fehlende Qualifikation sofort gemeldet.
        Assert.Equal(HttpStatusCode.Redirect, (await AbsendenAsync(client, seite, $"{seite}?handler=Funktion", new()
        {
            ["funktionId"] = jw.ToString(), ["art"] = nameof(FunktionArt.Wahl), ["ab"] = Heute.AddYears(-1).ToString("yyyy-MM-dd"),
        })).StatusCode);
        var nachher = await SeiteAsync(client, seite);
        Assert.Contains("Prüfung: Jugendfeuerwehrwart/in: Voraussetzung fehlt", nachher);

        // Doppelt im selben Zeitraum geht nicht.
        await AbsendenAsync(client, seite, $"{seite}?handler=Funktion", new()
        {
            ["funktionId"] = jw.ToString(), ["art"] = nameof(FunktionArt.Wahl), ["ab"] = Heute.ToString("yyyy-MM-dd"),
        });
        Assert.Contains("schon eingetragen", await SeiteAsync(client, seite));

        await AbsendenAsync(client, seite, $"{seite}?handler=Funktion", new()
        {
            ["funktionId"] = sib.ToString(), ["art"] = nameof(FunktionArt.Bestellung), ["ab"] = "2019-01-01",
        });

        Guid sibZuordnung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var zuordnungen = await db.FunktionZuordnungen.Include(f => f.Abteilung).Where(f => f.MitgliedId == id).ToListAsync();
            Assert.Equal(2, zuordnungen.Count);
            Assert.Equal(AbteilungCode.Jugend, zuordnungen.Single(f => f.FunktionId == jw).Abteilung?.Code);
            sibZuordnung = zuordnungen.Single(f => f.FunktionId == sib).Id;
        }

        // Beenden lässt die Funktion als Verlauf stehen.
        await AbsendenAsync(client, seite, $"{seite}?handler=FunktionBeenden", new()
        {
            ["zuordnungId"] = sibZuordnung.ToString(), ["bis"] = "2023-12-31",
        });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(new DateOnly(2023, 12, 31), (await db.FunktionZuordnungen.SingleAsync(f => f.Id == sibZuordnung)).GueltigBis);
        }

        var akte = await SeiteAsync(client, $"/Mitglieder/Akte/{id}");
        Assert.Contains("Sicherheitsbeauftragte/r", akte);
        Assert.Contains("31.12.2023", akte);
        Assert.Contains("Jugendfeuerwehrwart/in: Voraussetzung fehlt", akte);

        // Prüfergebnisse: alle prüfen, zurückstellen, dann meldet die Prüfung den Anlass nicht erneut.
        var liste = "/Laufbahn/Pruefergebnisse";
        Assert.Equal(HttpStatusCode.Redirect, (await AbsendenAsync(client, liste, $"{liste}?handler=AllePruefen", new())).StatusCode);
        Assert.Contains(name, await SeiteAsync(client, liste));
        var offen = Assert.Single(await PruefenAsync(id), h => h.Status == PruefhinweisStatus.Offen);
        await AbsendenAsync(client, liste, $"{liste}?handler=Zurueckstellen", new() { ["hinweisId"] = offen.Id.ToString() });
        Assert.Equal(PruefhinweisStatus.Zurueckgestellt, Assert.Single(await PruefenAsync(id), h => h.Id == offen.Id).Status);

        // Ohne Recht zum Bearbeiten kein Zurückstellen.
        var fk = Angemeldet(Rollen.Fuehrungskraft);
        Assert.Equal(HttpStatusCode.Forbidden, (await AbsendenAsync(fk, liste, $"{liste}?handler=WiederOeffnen", new() { ["hinweisId"] = offen.Id.ToString() })).StatusCode);
    }

    [Fact]
    public async Task Einstellungen_der_Abteilungen_speichern_ab_heute()
    {
        var seite = "/Stammdaten/Abteilungsregeln";
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Fuehrungskraft).GetAsync(seite)).StatusCode);

        var client = Angemeldet(Rollen.Kommandant);
        var felder = Fevad.Web.Pages.Stammdaten.AbteilungsregelnModel.Alle.ToDictionary(e => $"Werte[{e.Schluessel}]", e => e.Standard.ToString());
        felder[$"Werte[{LaufbahnParameter.VorlaufMonate}]"] = "99";
        var fehler = await AbsendenAsync(client, seite, seite, new(felder));
        Assert.Equal(HttpStatusCode.OK, fehler.StatusCode);
        Assert.Contains("erlaubt sind 0 bis 24 Monate", WebUtility.HtmlDecode(await fehler.Content.ReadAsStringAsync()));

        felder[$"Werte[{LaufbahnParameter.VorlaufMonate}]"] = "4";
        Assert.Equal(HttpStatusCode.Redirect, (await AbsendenAsync(client, seite, seite, new(felder))).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leser = scope.ServiceProvider.GetRequiredService<ParameterLeser>();
        try
        {
            Assert.Equal(4, await leser.ZahlAsync(LaufbahnParameter.VorlaufMonate, Heute.AddDays(1), 3));
            Assert.Equal(1, await leser.ZahlAsync(ParameterSchluessel.PruefungGesamtlaufBeimStart, Heute.AddDays(1), 0));
        }
        finally
        {
            await db.Parameter.Where(p => p.Quelle == "Einstellung in FEVAD" && p.Schluessel == LaufbahnParameter.VorlaufMonate).ExecuteDeleteAsync();
        }
    }
}
