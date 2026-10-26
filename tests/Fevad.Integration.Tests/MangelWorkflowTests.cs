using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Technik;
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

/// <summary>Erweiterter Mangel-Workflow (V63): Fotos, Werkstatt und Kosten, Ersatzgerät im Fach, Hinweis an den Kommandanten, Löschregel L62.</summary>
[Collection(AnwendungCollection.Name)]
public partial class MangelWorkflowTests(AnwendungFixture fixture)
{
    // Kleinstes gültiges PNG (1×1 Pixel).
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex("name=\"Formular.Version\" value=\"([^\"]+)\"")]
    private static partial Regex VersionFeld();

    [Fact]
    public async Task Ersatzgeraet_kommt_ins_Fach_und_wird_nach_der_Reparatur_zurueckgetauscht()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid mangelId, defektId, ersatzId, fahrzeugId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.FirstAsync(g => g.Code == "PA-MASKE");
            var material = new Material { Bezeichnung = "Maske " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Test " + kennung, Typ = "HLF 10" };
            var defekt = new Geraet { Inventarnummer = "MD" + kennung, Bezeichnung = "Maske", MaterialId = material.Id, GeraeteartId = art.Id, FahrzeugId = fahrzeug.Id, Lagerort = "G1" };
            var ersatz = new Geraet { Inventarnummer = "ME" + kennung, Bezeichnung = "Maske Reserve", MaterialId = material.Id, GeraeteartId = art.Id, Lagerort = "Magazin" };
            var mangel = new Mangel { GeraetId = defekt.Id, Beschreibung = "Sichtscheibe gerissen", Schwere = MangelSchwere.AusserBetrieb, Status = MangelStatus.Offen, GemeldetAm = DateTimeOffset.UtcNow };
            db.AddRange(material, fahrzeug, defekt, ersatz, mangel);
            await db.SaveChangesAsync();
            (mangelId, defektId, ersatzId, fahrzeugId) = (mangel.Id, defekt.Id, ersatz.Id, fahrzeug.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var gw = Angemeldet(factory, Rollen.Geraetewart);
        var pfad = $"/Technik/Maengel/Bearbeiten/{mangelId}";
        var seite = await gw.GetStringAsync(pfad);
        Assert.Contains($"value=\"{ersatzId}\"", seite);

        var antwort = await gw.PostAsync(pfad + "?handler=Ersatz", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["ersatzId"] = ersatzId.ToString(),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var stand = await MitDbAsync(db => db.Geraete.Where(g => g.Id == defektId || g.Id == ersatzId).ToDictionaryAsync(g => g.Id, g => (g.FahrzeugId, g.Lagerort)));
        Assert.Equal(((Guid?)fahrzeugId, "G1"), stand[ersatzId]);
        Assert.Equal(((Guid?)null, "Werkstatt"), stand[defektId]);

        // Behoben melden, mit Kosten, externer Werkstatt und Foto.
        seite = await gw.GetStringAsync(pfad);
        Assert.Contains("Wieder verladen", seite);
        using (var formular = new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(VersionFeld().Match(seite).Groups[1].Value), "Formular.Version" },
            { new StringContent(nameof(MangelStatus.Behoben)), "Formular.Status" },
            { new StringContent(nameof(MangelSchwere.AusserBetrieb)), "Formular.Schwere" },
            { new StringContent(nameof(MangelWerkstatt.Extern)), "Formular.Werkstatt" },
            { new StringContent("Feuerwehr Göppingen, Atemschutzwerkstatt"), "Formular.Werkstattname" },
            { new StringContent("84,50"), "Formular.Kosten" },
            { new StringContent("true"), "Formular.Garantiefall" },
            { new ByteArrayContent(Png), "Fotos", "riss.png" },
        })
        {
            antwort = await gw.PostAsync(pfad, formular);
        }

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var gespeichert = await MitDbAsync(db => db.Maengel.Include(m => m.Fotos).SingleAsync(m => m.Id == mangelId));
        Assert.Equal((MangelStatus.Behoben, MangelWerkstatt.Extern, 84.50m, true), (gespeichert.Status, gespeichert.Werkstatt, gespeichert.Kosten!.Value, gespeichert.Garantiefall));
        Assert.Single(gespeichert.Fotos);

        seite = await gw.GetStringAsync(pfad);
        antwort = await gw.PostAsync(pfad + "?handler=WiederVerladen", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        stand = await MitDbAsync(db => db.Geraete.Where(g => g.Id == defektId || g.Id == ersatzId).ToDictionaryAsync(g => g.Id, g => (g.FahrzeugId, g.Lagerort)));
        Assert.Equal(((Guid?)fahrzeugId, "G1"), stand[defektId]);
        Assert.Equal(((Guid?)null, "Magazin"), stand[ersatzId]);

        // Hauptamt liest die Technik, darf aber nichts ändern.
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        seite = await hauptamt.GetStringAsync(pfad);
        antwort = await hauptamt.PostAsync(pfad + "?handler=WiederVerladen", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }));
        Assert.Equal(HttpStatusCode.Forbidden, antwort.StatusCode);
    }

    [Fact]
    public async Task Fahrzeug_ausser_Betrieb_benachrichtigt_den_Kommandanten_einmal()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var mail = new MailSammler();
        var factory = TestAnmeldung.Factory(fixture).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMailVersand>(mail)));
        var email = $"kdt-{kennung}@ff-adelberg.test";
        Guid fahrzeugId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var konto = new Benutzer { UserName = email, Email = email };
            Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
            Assert.True((await benutzer.AddToRoleAsync(konto, Rollen.Kommandant)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Ausfall " + kennung, Typ = "TLF 8/18" };
            db.Fahrzeuge.Add(fahrzeug);
            await db.SaveChangesAsync();
            fahrzeugId = fahrzeug.Id;
        }

        var gw = Angemeldet(factory, Rollen.Geraetewart);
        var seite = await gw.GetStringAsync("/Melden");
        var antwort = await gw.PostAsync("/Melden", new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(fahrzeugId.ToString()), "Formular.FahrzeugId" },
            { new StringContent("Pumpe saugt nicht an"), "Formular.Beschreibung" },
            { new StringContent(nameof(MangelSchwere.AusserBetrieb)), "Formular.Schwere" },
            { new ByteArrayContent(Png), "Fotos", "pumpe.png" },
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var hinweis = Assert.Single(mail.Gesendet, m => m.Empfaenger == email && m.Betreff.Contains(kennung));
        Assert.Contains("Pumpe saugt nicht an", hinweis.Text);
        var mangel = await MitDbAsync(db => db.Maengel.Include(m => m.Fotos).SingleAsync(m => m.FahrzeugId == fahrzeugId));
        Assert.NotNull(mangel.KommandantInformiertAm);
        Assert.Single(mangel.Fotos);

        // Eine zweite Meldung zu einem anderen Mangel informiert nicht erneut über den ersten.
        await MitScopeAsync(sp => sp.GetRequiredService<Fevad.Infrastructure.Technik.Fahrzeugausfall>().OffeneMeldenAsync(default));
        Assert.Single(mail.Gesendet, m => m.Empfaenger == email && m.Betreff.Contains(kennung));
    }

    [Fact]
    public async Task L62_loescht_Fotos_behobener_Maengel_nach_der_Frist()
    {
        Guid altId, neuId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var geraetId = await db.Geraete.Select(g => g.Id).FirstAsync();
            var alt = new Mangel { GeraetId = geraetId, Beschreibung = "alt", Status = MangelStatus.Behoben, GemeldetAm = DateTimeOffset.UtcNow.AddYears(-3), BehobenAm = DateTimeOffset.UtcNow.AddMonths(-13) };
            var neu = new Mangel { GeraetId = geraetId, Beschreibung = "neu", Status = MangelStatus.Behoben, GemeldetAm = DateTimeOffset.UtcNow.AddMonths(-2), BehobenAm = DateTimeOffset.UtcNow.AddMonths(-1) };
            alt.Fotos.Add(new Mangelfoto { Dateiname = "a.png", Inhaltstyp = "image/png", Inhalt = Png });
            neu.Fotos.Add(new Mangelfoto { Dateiname = "b.png", Inhaltstyp = "image/png", Inhalt = Png });
            db.Maengel.AddRange(alt, neu);
            await db.SaveChangesAsync();
            (altId, neuId) = (alt.Id, neu.Id);
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        }

        Assert.False(await MitDbAsync(db => db.Mangelfotos.AnyAsync(f => f.MangelId == altId)));
        Assert.True(await MitDbAsync(db => db.Mangelfotos.AnyAsync(f => f.MangelId == neuId)));
        Assert.True(await MitDbAsync(db => db.Maengel.AnyAsync(m => m.Id == altId)));
    }

    [Fact]
    public async Task L11_leert_Namen_in_Pruefprotokollen_ein_Jahr_nach_Aussonderung()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var heute = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid altId, neuId, fahrzeugId, gemischtId, ganzAltId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = new Fevad.Domain.Mitglieder.Mitglied { Personalnummer = "L11" + kennung, Vorname = "Paula", Nachname = "Pruefer-" + kennung, Geburtsdatum = new(1990, 5, 5) };
            db.Add(mitglied);
            var mitgliedId = mitglied.Id;
            var art = await db.Geraetearten.FirstAsync(g => g.Code == "PA-MASKE");
            var material = new Material { Bezeichnung = "Flasche " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
            var alt = new Geraet { Inventarnummer = "LA" + kennung, Bezeichnung = "alt", MaterialId = material.Id, GeraeteartId = art.Id, AusgesondertAm = heute.AddYears(-2) };
            var neu = new Geraet { Inventarnummer = "LN" + kennung, Bezeichnung = "neu", MaterialId = material.Id, GeraeteartId = art.Id, AusgesondertAm = heute.AddMonths(-3) };
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian L11 " + kennung, Typ = "MTW", AusserDienstAm = heute.AddYears(-2) };
            db.AddRange(material, alt, neu, fahrzeug);
            db.Pruefungen.AddRange(
                new Pruefung { GeraetId = alt.Id, Pruefart = Pruefart.Funktionspruefung, Datum = heute.AddYears(-3), PrueferId = mitgliedId },
                new Pruefung { GeraetId = neu.Id, Pruefart = Pruefart.Funktionspruefung, Datum = heute.AddYears(-1), PrueferId = mitgliedId },
                new Pruefung { FahrzeugId = fahrzeug.Id, Pruefart = Pruefart.Funktionspruefung, Datum = heute.AddYears(-3), PrueferId = mitgliedId });
            db.Beladekontrollen.Add(new Beladekontrolle { FahrzeugId = fahrzeug.Id, Datum = heute.AddYears(-3), KontrolliertVonId = mitgliedId });
            db.Flaschenfuellungen.AddRange(
                new Flaschenfuellung { GeraetId = alt.Id, GefuelltAm = DateTimeOffset.UtcNow.AddYears(-3), DruckBar = 300, GefuelltVonId = mitgliedId },
                new Flaschenfuellung { GeraetId = neu.Id, GefuelltAm = DateTimeOffset.UtcNow.AddYears(-1), DruckBar = 300, GefuelltVonId = mitgliedId });
            var ganzAlt = new Werkstattauftrag { Nummer = "W-L11-A" + kennung, Werkstattname = "Test", AbgegebenAm = heute.AddYears(-3), AbgegebenVonId = mitgliedId };
            ganzAlt.Positionen.Add(new Werkstattposition { GeraetId = alt.Id });
            var gemischt = new Werkstattauftrag { Nummer = "W-L11-G" + kennung, Werkstattname = "Test", AbgegebenAm = heute.AddYears(-3), AbgegebenVonId = mitgliedId };
            gemischt.Positionen.Add(new Werkstattposition { GeraetId = alt.Id });
            gemischt.Positionen.Add(new Werkstattposition { GeraetId = neu.Id });
            db.Werkstattauftraege.AddRange(ganzAlt, gemischt);
            await db.SaveChangesAsync();
            (altId, neuId, fahrzeugId, gemischtId, ganzAltId) = (alt.Id, neu.Id, fahrzeug.Id, gemischt.Id, ganzAlt.Id);
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        }

        Assert.Null(await MitDbAsync(db => db.Pruefungen.Where(p => p.GeraetId == altId).Select(p => p.PrueferId).SingleAsync()));
        Assert.NotNull(await MitDbAsync(db => db.Pruefungen.Where(p => p.GeraetId == neuId).Select(p => p.PrueferId).SingleAsync()));
        Assert.Null(await MitDbAsync(db => db.Pruefungen.Where(p => p.FahrzeugId == fahrzeugId).Select(p => p.PrueferId).SingleAsync()));
        Assert.Null(await MitDbAsync(db => db.Beladekontrollen.Where(k => k.FahrzeugId == fahrzeugId).Select(k => k.KontrolliertVonId).SingleAsync()));
        Assert.Null(await MitDbAsync(db => db.Flaschenfuellungen.Where(f => f.GeraetId == altId).Select(f => f.GefuelltVonId).SingleAsync()));
        Assert.NotNull(await MitDbAsync(db => db.Flaschenfuellungen.Where(f => f.GeraetId == neuId).Select(f => f.GefuelltVonId).SingleAsync()));
        Assert.Null(await MitDbAsync(db => db.Werkstattauftraege.Where(a => a.Id == ganzAltId).Select(a => a.AbgegebenVonId).SingleAsync()));
        Assert.NotNull(await MitDbAsync(db => db.Werkstattauftraege.Where(a => a.Id == gemischtId).Select(a => a.AbgegebenVonId).SingleAsync()));
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<T> MitScopeAsync<T>(Func<IServiceProvider, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider);
    }

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

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
