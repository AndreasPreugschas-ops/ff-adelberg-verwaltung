using System.Net;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class KleiderkammerTests(AnwendungFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _abteilungen = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Testabteilungen wieder ausblenden, sonst stimmen die Stammdatenzählungen anderer Tests nicht.</summary>
    public async Task DisposeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Abteilungen.RemoveRange(await db.Abteilungen.Where(a => _abteilungen.Contains(a.Id)).ToListAsync());
        await db.SaveChangesAsync();
    }

    private static string Kennung() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed record Testdaten(Guid MitgliedId, Guid Handschuhe, Guid Helm, Guid HelmArt, Guid AbteilungId);

    /// <summary>Eigene Artikel, Geräteart und Abteilung je Test, damit sich die Tests den Bestand nicht teilen.</summary>
    private async Task<Testdaten> AnlegenAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var k = Kennung();

        // Musik statt Einsatz: die Grundausstattung gilt je Abteilungsart, die echten Einsatzabteilungen bleiben außen vor.
        var abteilung = new Abteilung { Code = "T" + k, Bezeichnung = "Testabteilung " + k, Art = AbteilungArt.Musik };
        var art = new Geraeteart
        {
            Code = "H" + k,
            Bezeichnung = "Testhelm " + k,
            AussonderungNachJahren = 10,
            Pruefplan = [new PruefplanPosition { Pruefart = Pruefart.Sichtpruefung, IntervallMonate = 12 }],
        };
        var mitglied = new Mitglied { Personalnummer = "K-" + k, Vorname = "Kim", Nachname = "Kleider" + k, Geburtsdatum = new(1990, 5, 1) };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { Abteilung = abteilung, Status = MitgliedStatus.Aktiv, GueltigAb = new(2020, 1, 1) });
        var handschuhe = new Artikel { Code = "HS" + k, Bezeichnung = "Handschuhe " + k, Groessenart = Groessenart.Handschuhe, GrundausstattungArt = AbteilungArt.Musik };
        var helm = new Artikel { Code = "HE" + k, Bezeichnung = "Helm " + k, Groessenart = Groessenart.Kopf, Geraeteart = art, GrundausstattungArt = AbteilungArt.Musik };
        db.AddRange(abteilung, art, mitglied, handschuhe, helm);
        await db.SaveChangesAsync();
        _abteilungen.Add(abteilung.Id);
        return new Testdaten(mitglied.Id, handschuhe.Id, helm.Id, art.Id, abteilung.Id);
    }

    [Fact]
    public async Task Ausgabe_und_Teilruecknahme_buchen_Lager_und_Quittungsnummern()
    {
        var t = await AnlegenAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var buchungen = scope.ServiceProvider.GetRequiredService<Kleiderkammerbuchungen>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await buchungen.ZugangBuchenAsync(t.Handschuhe, " 10 ", 5);
        var helm = await buchungen.EinzelstueckAnlegenAsync(t.Helm, "PSA-" + Kennung(), "m", "Dräger", null, new DateOnly(2020, 3, 1));

        var ausgabe = await buchungen.AusgebenAsync(t.MitgliedId,
            [new AusgabePosition(t.Handschuhe, "10", 3), new AusgabePosition(t.Helm, "", 1, helm.Id)], "Erstausstattung");
        Assert.Matches(@"^KK-\d{4}-\d{4}$", ausgabe.Nummer);
        Assert.Equal(2, (await db.Lagerbestaende.SingleAsync(l => l.ArtikelId == t.Handschuhe && l.Groesse == "10")).Menge);

        // Dasselbe Stück kann nicht zweimal ausgegeben werden, mehr als im Lager auch nicht.
        await Assert.ThrowsAsync<KleiderkammerFehler>(() => buchungen.AusgebenAsync(t.MitgliedId, [new AusgabePosition(t.Helm, "", 1, helm.Id)], null));
        await Assert.ThrowsAsync<KleiderkammerFehler>(() => buchungen.AusgebenAsync(t.MitgliedId, [new AusgabePosition(t.Handschuhe, "10", 3)], null));

        var zeilen = await db.Ausstattungen.AsNoTracking().Where(a => a.MitgliedId == t.MitgliedId).ToListAsync();
        var handschuhZeile = zeilen.Single(a => a.ArtikelId == t.Handschuhe);
        var helmZeile = zeilen.Single(a => a.ArtikelId == t.Helm);
        Assert.Equal("M", helmZeile.Groesse);

        var ruecknahme = await buchungen.ZuruecknehmenAsync(t.MitgliedId,
            [new RuecknahmePosition(handschuhZeile.Id, 1, RuecknahmeZustand.Einlagern), new RuecknahmePosition(helmZeile.Id, 1, RuecknahmeZustand.Aussondern)], null);
        Assert.NotEqual(ausgabe.Nummer, ruecknahme.Nummer);

        db.ChangeTracker.Clear();
        var offen = await db.Ausstattungen.Where(a => a.MitgliedId == t.MitgliedId && a.ZurueckAm == null).ToListAsync();
        Assert.Equal(2, Assert.Single(offen).Menge);
        Assert.Equal(3, (await db.Lagerbestaende.SingleAsync(l => l.ArtikelId == t.Handschuhe && l.Groesse == "10")).Menge);
        var ausgesondert = await db.Geraete.SingleAsync(g => g.Id == helm.Id);
        Assert.NotNull(ausgesondert.AusgesondertAm);
        Assert.Equal($"Rücknahme Kleiderkammer {ruecknahme.Nummer}", ausgesondert.Aussonderungsgrund);
    }

    [Fact]
    public async Task Bedarf_aus_fehlender_Grundausstattung_und_Mindestmenge_wird_zur_Beschaffung()
    {
        var t = await AnlegenAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var buchungen = scope.ServiceProvider.GetRequiredService<Kleiderkammerbuchungen>();
        var stand = scope.ServiceProvider.GetRequiredService<Kleiderkammerstand>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var heute = new DateOnly(2026, 10, 22);

        await buchungen.GroessenSetzenAsync(t.MitgliedId, new Dictionary<Groessenart, string?> { [Groessenart.Handschuhe] = "9", [Groessenart.Kopf] = "L" });
        await buchungen.MindestmengeSetzenAsync(t.Handschuhe, "10", 2);

        var bedarf = (await stand.BedarfAsync(heute)).Where(z => z.ArtikelId == t.Handschuhe || z.ArtikelId == t.Helm).ToList();
        var groesse9 = Assert.Single(bedarf, z => z.ArtikelId == t.Handschuhe && z.Groesse == "9");
        Assert.Equal(1, groesse9.Bedarf.Offen);
        Assert.Single(groesse9.OhneGrundausstattung);
        Assert.Equal(2, Assert.Single(bedarf, z => z.ArtikelId == t.Handschuhe && z.Groesse == "10").Bedarf.Offen);
        Assert.Equal(1, Assert.Single(bedarf, z => z.ArtikelId == t.Helm && z.Groesse == "L").Bedarf.Offen);

        var anzahl = await stand.NachbestellungenAnlegenAsync(heute, [(t.Handschuhe, "9"), (t.Helm, "L")]);
        Assert.Equal(2, anzahl);
        var helmBeschaffung = await db.Beschaffungen.SingleAsync(b => b.ArtikelId == t.Helm);
        Assert.Equal(BeschaffungStatus.Bedarf, helmBeschaffung.Status);
        Assert.Equal(t.HelmArt, helmBeschaffung.GeraeteartId);

        // Offene Beschaffungen decken den Bedarf: kein zweites Mal bestellen.
        var danach = (await stand.BedarfAsync(heute)).Single(z => z.ArtikelId == t.Helm && z.Groesse == "L");
        Assert.Equal(0, danach.Bedarf.Offen);
    }

    [Fact]
    public async Task Grundausstattung_gilt_fuer_jede_Abteilung_ihrer_Art()
    {
        var t = await AnlegenAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var buchungen = scope.ServiceProvider.GetRequiredService<Kleiderkammerbuchungen>();
        var stand = scope.ServiceProvider.GetRequiredService<Kleiderkammerstand>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var k = Kennung();
        var zweite = new Abteilung { Code = "T" + k, Bezeichnung = "Zweite Testabteilung " + k, Art = AbteilungArt.Musik };
        var andere = new Mitglied { Personalnummer = "K-" + k, Vorname = "Kai", Nachname = "Kleider" + k, Geburtsdatum = new(1992, 3, 1) };
        andere.Mitgliedschaften.Add(new Mitgliedschaft { Abteilung = zweite, Status = MitgliedStatus.Aktiv, GueltigAb = new(2021, 1, 1) });
        db.AddRange(zweite, andere);
        await db.SaveChangesAsync();
        _abteilungen.Add(zweite.Id);

        await buchungen.GroessenSetzenAsync(t.MitgliedId, new Dictionary<Groessenart, string?> { [Groessenart.Handschuhe] = "7" });
        await buchungen.GroessenSetzenAsync(andere.Id, new Dictionary<Groessenart, string?> { [Groessenart.Handschuhe] = "7" });

        var groesse7 = (await stand.BedarfAsync(new DateOnly(2026, 10, 23))).Single(z => z.ArtikelId == t.Handschuhe && z.Groesse == "7");
        Assert.Equal(2, groesse7.OhneGrundausstattung.Count);
    }

    [Fact]
    public async Task Einzelstueck_erscheint_mit_Traeger_und_Lebensdauer_in_den_Fristen()
    {
        var t = await AnlegenAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var buchungen = scope.ServiceProvider.GetRequiredService<Kleiderkammerbuchungen>();
        var stand = scope.ServiceProvider.GetRequiredService<Kleiderkammerstand>();

        var helm = await buchungen.EinzelstueckAnlegenAsync(t.Helm, "PSA-" + Kennung(), "L", null, null, new DateOnly(2016, 11, 1));
        await buchungen.AusgebenAsync(t.MitgliedId, [new AusgabePosition(t.Helm, "", 1, helm.Id)], null);

        var psa = Assert.Single(await stand.PsaAsync(new DateOnly(2026, 10, 22)), p => p.Stand.Id == helm.Id);
        Assert.Equal(t.MitgliedId, psa.MitgliedId);
        Assert.Equal(new DateOnly(2026, 11, 1), psa.Stand.AussonderungFaellig);

        var hinweise = (await stand.FaelligeAsync(new DateOnly(2026, 10, 22), default)).Where(h => h.Gegenstand.StartsWith(helm.Inventarnummer, StringComparison.Ordinal)).ToList();
        Assert.Contains(hinweise, h => h.Bezeichnung == "Aussonderung (Lebensdauer)" && h.Empfaenger == Berechtigungen.KleiderkammerBearbeiten);
        Assert.Contains(hinweise, h => h.Bezeichnung.Contains("noch nie geprüft", StringComparison.Ordinal));

        // Läuft die Lebensdauer bald ab, wird Ersatz als Bedarf gezählt.
        var bedarf = (await stand.BedarfAsync(new DateOnly(2026, 10, 22))).Single(z => z.ArtikelId == t.Helm && z.Groesse == "L");
        Assert.Equal(1, bedarf.Bedarf.ErsatzFuerAusgegebene);
    }

    [Fact]
    public async Task Seiten_sind_fuer_Kleiderwart_erreichbar_und_fuer_Mitglieder_gesperrt()
    {
        var t = await AnlegenAsync();
        var factory = TestAnmeldung.Factory(fixture);
        var kleiderwart = Client(factory, Rollen.Kleiderwart);
        var mitglied = Client(factory, Rollen.Mitglied);

        foreach (var seite in new[] { "/Kleiderkammer", "/Kleiderkammer/Mitglieder", "/Kleiderkammer/Bedarf", "/Kleiderkammer/Fristen", "/Kleiderkammer/Artikel",
                     $"/Kleiderkammer/ArtikelBearbeiten/{t.Helm}", $"/Kleiderkammer/Mitglied/{t.MitgliedId}", $"/Kleiderkammer/Ausgabe/{t.MitgliedId}" })
        {
            Assert.Equal(HttpStatusCode.OK, (await kleiderwart.GetAsync(seite)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await mitglied.GetAsync(seite)).StatusCode);
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory, string rolle)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }
}
