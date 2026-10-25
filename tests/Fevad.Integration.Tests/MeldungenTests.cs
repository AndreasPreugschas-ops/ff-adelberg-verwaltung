using Fevad.Domain.Gemeinsam;
using System.Net;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Unfall, Verbandbuch, Beinaheunfall und Sachschaden durch das Mitglied melden (V40).</summary>
public partial class UnfallTests
{
    // Kleinstes gültiges PNG (1×1 Pixel).
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    private async Task<(Guid MitgliedId, HttpClient Client)> MitgliedMitKontoAsync(string nachname)
    {
        var mitgliedId = await MitgliedAsync(nachname);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"u-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = mitgliedId };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        var client = Client(Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, konto.Id.ToString());
        return (mitgliedId, client);
    }

    private static async Task<HttpResponseMessage> MitFotosAsync(HttpClient client, string pfad, Dictionary<string, string> felder, params (string Name, byte[] Inhalt)[] fotos)
    {
        var seite = await client.GetStringAsync(pfad);
        var inhalt = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value)), "__RequestVerificationToken" },
        };
        foreach (var (name, wert) in felder)
        {
            inhalt.Add(new StringContent(wert), name);
        }

        foreach (var (name, bytes) in fotos)
        {
            inhalt.Add(new ByteArrayContent(bytes), "Fotos", name);
        }

        return await client.PostAsync(pfad, inhalt);
    }

    [Fact]
    public async Task Mitglied_meldet_Verbandbucheintrag_mit_Foto_aus_dem_Dienst()
    {
        var name = "Verband" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);
        Guid dienstId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Gestern 18:00 Uhr Ortszeit, damit Datum und Uhrzeit der Meldung auch kurz nach Mitternacht nicht in der Zukunft liegen.
            var beginn = Ortszeit.TagesbeginnUtc(Heute.AddDays(-1)).AddHours(18);
            var dienst = new Dienst
            {
                Titel = "Übung " + name, Beginn = beginn, Ende = beginn.AddHours(2), Status = DienstStatus.Geplant,
                Abteilungen = [await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)],
            };
            db.Dienste.Add(dienst);
            await db.SaveChangesAsync();
            dienstId = dienst.Id;
        }

        Assert.Contains("Unfall melden", await mitglied.GetStringAsync($"/Termine/Termin/{dienstId}"));
        var formular = await mitglied.GetStringAsync($"/Konto/Unfall?dienstId={dienstId}");
        Assert.Contains($"selected=\"selected\" value=\"{dienstId}\"", formular);

        var felder = new Dictionary<string, string>
        {
            ["Formular.Art"] = nameof(UnfallArt.Verbandbuch), ["Formular.Datum"] = Heute.AddDays(-1).ToString("dd.MM.yyyy"), ["Formular.Uhrzeit"] = "19:30",
            ["Formular.Anlass"] = nameof(UnfallAnlass.Dienst), ["Formular.DienstId"] = dienstId.ToString(), ["Formular.Ort"] = "Gerätehaus",
            ["Formular.Hergang"] = "Am Rollladen geschnitten", ["Formular.VerletzungsartId"] = (await VerletzungsartAsync("SCHNITT")).ToString(),
            ["Formular.Zeuge"] = "Max Zeuge",
        };

        // Kein Foto: wird abgelehnt.
        var falsch = await MitFotosAsync(mitglied, "/Konto/Unfall", felder, ("notiz.txt", "Hallo"u8.ToArray()));
        Assert.Equal(HttpStatusCode.OK, falsch.StatusCode);
        Assert.Contains("kein Foto", await falsch.Content.ReadAsStringAsync());

        var antwort = await MitFotosAsync(mitglied, "/Konto/Unfall", felder, ("finger.png", Png));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid id, fotoId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Unfaelle.Include(x => x.Zeugen).Include(x => x.Fotos).Include(x => x.Verletzungsart).SingleAsync(x => x.MitgliedId == mitgliedId);
            Assert.True(u.Selbstmeldung);
            Assert.Equal((UnfallArt.Verbandbuch, dienstId, "SCHNITT"), (u.Art, u.DienstId!.Value, u.Verletzungsart!.Code));
            Assert.False(u.Anzeigepflichtig);
            Assert.Equal("Max Zeuge", Assert.Single(u.Zeugen).Name);
            id = u.Id;
            fotoId = Assert.Single(u.Fotos).Id;
        }

        Assert.Contains("Kleinverletzung (Verbandbuch)", await mitglied.GetStringAsync("/Konto/Unfall"));
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Unfaelle/Bearbeiten/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Unfaelle/Bearbeiten/{id}?handler=Foto&fotoId={fotoId}")).StatusCode);

        var kommandant = Client(Rollen.Kommandant);
        Assert.Contains(name, await kommandant.GetStringAsync("/Unfaelle"));
        Assert.Contains("Vom Mitglied selbst gemeldet", await kommandant.GetStringAsync($"/Unfaelle/Bearbeiten/{id}"));
        var foto = await kommandant.GetAsync($"/Unfaelle/Bearbeiten/{id}?handler=Foto&fotoId={fotoId}");
        Assert.Equal("image/png", foto.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Mitglied_meldet_Sachschaden_und_Kommandant_traegt_die_Erstattung_ein()
    {
        var name = "Brille" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);
        var antwort = await MitFotosAsync(mitglied, "/Konto/Sachschaden", new()
        {
            ["Formular.Datum"] = Heute.AddDays(-1).ToString("dd.MM.yyyy"), ["Formular.Uhrzeit"] = "03:10", ["Formular.Anlass"] = nameof(UnfallAnlass.Sonstiges),
            ["Formular.Ort"] = "Hauptstraße", ["Formular.Gegenstand"] = "Brille " + name, ["Formular.Hergang"] = "Beim Absteigen heruntergefallen",
            ["Formular.Betrag"] = "249,90",
        }, ("brille.png", Png));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var s = await db.Sachschaeden.Include(x => x.Fotos).SingleAsync(x => x.MitgliedId == mitgliedId);
            Assert.Equal((249.90m, SachschadenStatus.Gemeldet, 1), (s.Betrag, s.Status, s.Fotos.Count));
            id = s.Id;
        }

        // Das Rathaus liest mit, ändern darf es nicht.
        var gemeinde = Client(Rollen.GemeindeLesen);
        Assert.Contains("Brille " + name, await gemeinde.GetStringAsync("/Unfaelle/Sachschaeden"));
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(gemeinde, $"/Unfaelle/Sachschaden/{id}", new()
        {
            ["Formular.Status"] = nameof(SachschadenStatus.Abgelehnt),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync($"/Unfaelle/Sachschaden/{id}")).StatusCode);

        var kommandant = Client(Rollen.Kommandant);
        var ohneBetrag = await AbschickenAsync(kommandant, $"/Unfaelle/Sachschaden/{id}", new() { ["Formular.Status"] = nameof(SachschadenStatus.Erstattet) });
        Assert.Equal(HttpStatusCode.OK, ohneBetrag.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, $"/Unfaelle/Sachschaden/{id}", new()
        {
            ["Formular.Status"] = nameof(SachschadenStatus.Erstattet), ["Formular.ErstattetBetrag"] = "200",
        })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var s = await db.Sachschaeden.SingleAsync(x => x.Id == id);
            Assert.Equal((SachschadenStatus.Erstattet, 200m, Heute), (s.Status, s.ErstattetBetrag!.Value, s.AbgeschlossenAm!.Value));
            Assert.NotNull(s.WeitergeleitetAm);
        }

        Assert.Contains("erstattet 200,00", await mitglied.GetStringAsync("/Konto/Sachschaden"));
    }

    [Fact]
    public async Task Loeschlauf_loescht_alte_Verbandbucheintraege_und_erledigte_Sachschaeden()
    {
        var mitgliedId = await MitgliedAsync("Alt" + Guid.NewGuid().ToString("N")[..6]);
        Guid verbandbuch, frisch, schaden;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Unfall Eintrag(int jahre) => new()
            {
                MitgliedId = mitgliedId, Art = UnfallArt.Verbandbuch, Ort = "Gerätehaus", Hergang = "Splitter",
                Zeitpunkt = DateTimeOffset.UtcNow.AddYears(-jahre), Fotos = [new Meldungsfoto { Dateiname = "a.png", Inhaltstyp = "image/png", Inhalt = Png }],
            };
            var alt = Eintrag(6);
            var neu = Eintrag(1);
            var s = new Sachschaden
            {
                MitgliedId = mitgliedId, Ort = "Ort", Gegenstand = "Jacke", Hergang = "Riss", Betrag = 50, Zeitpunkt = DateTimeOffset.UtcNow.AddYears(-5),
                Status = SachschadenStatus.Abgelehnt, AbgeschlossenAm = Heute.AddYears(-4),
            };
            db.AddRange(alt, neu, s);
            await db.SaveChangesAsync();
            (verbandbuch, frisch, schaden) = (alt.Id, neu.Id, s.Id);

            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelVerbandbuch).Anzahl >= 1);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelSachschaeden).Anzahl >= 1);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Unfaelle.IgnoreQueryFilters().AnyAsync(u => u.Id == verbandbuch));
            Assert.False(await db.Meldungsfotos.IgnoreQueryFilters().AnyAsync(f => f.UnfallId == verbandbuch));
            Assert.True(await db.Unfaelle.AnyAsync(u => u.Id == frisch));
            Assert.False(await db.Sachschaeden.IgnoreQueryFilters().AnyAsync(x => x.Id == schaden));
        }
    }
}
