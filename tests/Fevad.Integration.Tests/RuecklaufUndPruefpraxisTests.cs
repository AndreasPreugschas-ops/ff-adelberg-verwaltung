using System.Net;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Geräte nach dem Einsatz (V13) und Prüfpraxis mit Sammelprüfung, Storno und Plaketten (V58).</summary>
public partial class TechnikTests
{
    /// <summary>Legt ein Gerät der Geräteart LEITER an und liefert ID und Inventarnummer.</summary>
    private async Task<(Guid Id, string Nummer)> GeraetAsync(string bezeichnung, Guid? fahrzeugId = null, string? lagerort = null)
    {
        var (_, leiterArt, _, _) = await LeiterAsync();
        var nummer = Nummer("R");
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var geraet = new Geraet { Inventarnummer = nummer, Bezeichnung = bezeichnung, FahrzeugId = fahrzeugId, Lagerort = lagerort };
        Materialstamm.Zuordnen(geraet, await Materialstamm.FindenOderAnlegenAsync(db, bezeichnung, leiterArt, null, null, false, default));
        db.Geraete.Add(geraet);
        await db.SaveChangesAsync();
        return (geraet.Id, nummer);
    }

    /// <summary>Legt einen Einsatz mit einem Teilnehmer an.</summary>
    private async Task<(Guid EinsatzId, Guid TeilnahmeId, Guid MitgliedId)> EinsatzAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mitglied = new Mitglied { Personalnummer = "R-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Rita", Nachname = "Rücklauf", Geburtsdatum = new(1990, 1, 1) };
        db.Mitglieder.Add(mitglied);
        var einsatz = new Einsatz { Einsatznummer = $"R-{Guid.NewGuid().ToString("N")[..8]}", Stichwort = "B 2", Alarmiert = DateTimeOffset.UtcNow.AddHours(-5), Status = EinsatzStatus.InBearbeitung };
        var teilnahme = new EinsatzTeilnahme { MitgliedId = mitglied.Id };
        einsatz.Teilnahmen.Add(teilnahme);
        db.Einsaetze.Add(einsatz);
        await db.SaveChangesAsync();
        return (einsatz.Id, teilnahme.Id, mitglied.Id);
    }

    [Fact]
    public async Task Benutztes_Geraet_ist_bis_zur_Pruefung_gesperrt_und_Storno_sperrt_wieder()
    {
        var (geraetId, nummer) = await GeraetAsync("Steckleiter Einsatz");
        var (einsatzId, _, _) = await EinsatzAsync();
        var factory = TestAnmeldung.Factory(fixture);
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);

        // Der Einsatzleiter scannt den QR-Code; die Adresse führt zur Inventarnummer.
        var seite = await fuehrung.GetStringAsync($"/Einsaetze/Geraete/{einsatzId}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Geraet", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["nummer"] = $"https://fevad.test/g/{nummer}",
            ["belastung"] = nameof(Belastung.DynamischBelastet),
        })).StatusCode);

        var heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
        Assert.Equal(GeraetZustand.AusserBetrieb, await ZustandAsync(geraetId, heute));
        var sperre = await MitDbAsync(db => db.Maengel.SingleAsync(m => m.GeraetId == geraetId));
        Assert.Equal(einsatzId, sperre.EinsatzId);
        Assert.Contains("sofort prüfen", sperre.Beschreibung);

        // Der Gerätewart sieht das Gerät auf der Werkstattliste.
        Assert.Contains(nummer, await geraetewart.GetStringAsync("/Technik/Ruecklauf"));

        // Die Gemeinde (nur lesen) darf nichts sperren.
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(gemeinde, $"/Einsaetze/Geraete/{einsatzId}?handler=Geraet", new()
        {
            ["__RequestVerificationToken"] = Token(await gemeinde.GetStringAsync($"/Einsaetze/Geraete/{einsatzId}")),
            ["nummer"] = nummer,
        })).StatusCode);

        // Prüfung nach Gebrauch hebt die Sperre auf.
        var pfad = $"/Technik/Pruefungen/Neu?geraetId={geraetId}&einsatzId={einsatzId}&pruefart={nameof(Pruefart.NachGebrauch)}";
        var formular = await geraetewart.GetStringAsync(pfad);
        Assert.Contains("Prüfung nach Benutzung im Einsatz", formular);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", new()
        {
            ["__RequestVerificationToken"] = Token(formular),
            ["Formular.GeraetId"] = geraetId.ToString(),
            ["Formular.EinsatzId"] = einsatzId.ToString(),
            ["Formular.Pruefart"] = nameof(Pruefart.NachGebrauch),
            ["Formular.Datum"] = Pruefdatum.ToString("yyyy-MM-dd"),
            ["Formular.PrueferExtern"] = "Gerätewart Test",
            ["Formular.Ergebnis"] = nameof(PruefErgebnis.Bestanden),
        })).StatusCode);
        Assert.Equal(GeraetZustand.Einsatzbereit, await ZustandAsync(geraetId, heute));
        var pruefung = await MitDbAsync(db => db.Pruefungen.SingleAsync(p => p.GeraetId == geraetId));
        Assert.Equal(einsatzId, pruefung.EinsatzId);
        Assert.Equal(MangelStatus.Behoben, await MitDbAsync(db => db.Maengel.Where(m => m.Id == sperre.Id).Select(m => m.Status).SingleAsync()));
        await geraetewart.GetStringAsync("/Technik/Ruecklauf"); // zeigt noch die Meldung zur gespeicherten Prüfung
        Assert.DoesNotContain(nummer, await geraetewart.GetStringAsync("/Technik/Ruecklauf"));

        // Storno nur mit Grund; danach gilt die Sperre wieder und die Prüfung zählt nicht mehr.
        var details = await geraetewart.GetStringAsync($"/Technik/Pruefungen/Details/{pruefung.Id}");
        Assert.Contains("Prüfung stornieren", details);
        await PostAsync(geraetewart, $"/Technik/Pruefungen/Details/{pruefung.Id}?handler=Stornieren", new() { ["__RequestVerificationToken"] = Token(details), ["grund"] = "" });
        Assert.Null(await MitDbAsync(db => db.Pruefungen.Where(p => p.Id == pruefung.Id).Select(p => p.StorniertAm).SingleAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Technik/Pruefungen/Details/{pruefung.Id}?handler=Stornieren", new()
        {
            ["__RequestVerificationToken"] = Token(details),
            ["grund"] = "falsches Gerät gescannt",
        })).StatusCode);
        Assert.Equal(GeraetZustand.AusserBetrieb, await ZustandAsync(geraetId, heute));
        Assert.Contains("storniert", await geraetewart.GetStringAsync($"/Technik/Geraete/Details/{geraetId}"));
        Assert.DoesNotContain("Prüfung stornieren", await geraetewart.GetStringAsync($"/Technik/Pruefungen/Details/{pruefung.Id}"));
    }

    [Fact]
    public async Task Mengenartikel_wird_nicht_gesperrt_und_Waesche_landet_in_der_Kleiderkammer()
    {
        var (geraetId, _) = await GeraetAsync("Druckschlauch Muster");
        var materialId = await MitDbAsync(db => db.Geraete.Where(g => g.Id == geraetId).Select(g => g.MaterialId).SingleAsync());
        var (einsatzId, teilnahmeId, _) = await EinsatzAsync();
        var factory = TestAnmeldung.Factory(fixture);
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);

        var seite = await fuehrung.GetStringAsync($"/Einsaetze/Geraete/{einsatzId}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Menge", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["materialId"] = materialId.ToString(),
            ["anzahl"] = "6",
            ["belastung"] = nameof(Belastung.Benutzt),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(fuehrung, $"/Einsaetze/Geraete/{einsatzId}?handler=Waesche", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["teilnahmeIds"] = teilnahmeId.ToString(),
        })).StatusCode);

        Assert.Equal(GeraetZustand.Einsatzbereit, await ZustandAsync(geraetId, Anzeige.LokalesDatum(DateTimeOffset.UtcNow)));
        var zeile = await MitDbAsync(db => db.EinsatzGeraete.SingleAsync(z => z.EinsatzId == einsatzId));
        Assert.True(zeile.IstMengenartikel);
        Assert.Equal(6, zeile.Anzahl);

        // Gerätewart hakt den Mengenartikel ab.
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var liste = await geraetewart.GetStringAsync("/Technik/Ruecklauf");
        Assert.Contains("6 × Druckschlauch Muster", WebUtility.HtmlDecode(liste));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Ruecklauf?handler=Erledigt", new()
        {
            ["__RequestVerificationToken"] = Token(liste),
            ["zeileId"] = zeile.Id.ToString(),
        })).StatusCode);
        Assert.NotNull(await MitDbAsync(db => db.EinsatzGeraete.Where(z => z.Id == zeile.Id).Select(z => z.ErledigtAm).SingleAsync()));

        // Kleiderwart sieht den Namen auf der Wäscheliste und vermerkt die Wäsche.
        var kleiderwart = Angemeldet(factory, Rollen.Kleiderwart);
        var waesche = await kleiderwart.GetStringAsync("/Kleiderkammer/Waesche");
        Assert.Contains("Rücklauf, Rita", WebUtility.HtmlDecode(waesche));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kleiderwart, "/Kleiderkammer/Waesche?handler=Erledigt", new()
        {
            ["__RequestVerificationToken"] = Token(waesche),
            ["teilnahmeIds"] = teilnahmeId.ToString(),
        })).StatusCode);
        Assert.NotNull(await MitDbAsync(db => db.EinsatzTeilnahmen.Where(t => t.Id == teilnahmeId).Select(t => t.PsaWaescheErledigtAm).SingleAsync()));
    }

    [Fact]
    public async Task Sammelpruefung_mit_Ausnahme_und_Plaketten()
    {
        var fahrzeug = new Fahrzeug { Funkrufname = "Florian Sammel " + Guid.NewGuid().ToString("N")[..6], Typ = "LF 10" };
        await MitDbAsync(async db => { db.Fahrzeuge.Add(fahrzeug); return await db.SaveChangesAsync(); });
        var (erstes, nummer1) = await GeraetAsync("Leiter Fach G1", fahrzeug.Id, "G1");
        var (zweites, _) = await GeraetAsync("Leiter Fach G1 b", fahrzeug.Id, "G1");
        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);

        // Nachschlagen per Scan und Fach laden liefern JSON für die Liste.
        var treffer = await geraetewart.GetStringAsync($"/Technik/Pruefungen/Sammel?handler=Geraet&pruefart=Funktionspruefung&nummer={Uri.EscapeDataString("https://x.test/g/" + nummer1)}");
        Assert.Contains(erstes.ToString(), treffer);
        Assert.Equal(HttpStatusCode.NotFound, (await geraetewart.GetAsync("/Technik/Pruefungen/Sammel?handler=Geraet&pruefart=Funktionspruefung&nummer=GIBTSNICHT")).StatusCode);
        var fach = await geraetewart.GetStringAsync($"/Technik/Pruefungen/Sammel?handler=Fach&pruefart=Funktionspruefung&fahrzeugId={fahrzeug.Id}&lagerort=G1");
        Assert.Contains(zweites.ToString(), fach);

        // Ohne Mitglied am Konto braucht es einen externen Prüfer.
        var seite = await geraetewart.GetStringAsync("/Technik/Pruefungen/Sammel?pruefart=Funktionspruefung");
        var felder = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Pruefart"] = nameof(Pruefart.Funktionspruefung),
            ["Formular.Datum"] = Pruefdatum.ToString("yyyy-MM-dd"),
            ["Formular.Zeilen[0].GeraetId"] = erstes.ToString(),
            ["Formular.Zeilen[0].Ergebnis"] = nameof(PruefErgebnis.Bestanden),
            ["Formular.Zeilen[1].GeraetId"] = zweites.ToString(),
            ["Formular.Zeilen[1].Ergebnis"] = nameof(PruefErgebnis.BestandenMitMaengeln),
            ["Formular.Zeilen[1].Mangel"] = "Fußpunkt locker",
        };
        var abgelehnt = await PostAsync(geraetewart, "/Technik/Pruefungen/Sammel", felder);
        Assert.Equal(HttpStatusCode.OK, abgelehnt.StatusCode);
        var abgelehntText = WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync());
        Assert.Contains("externen Prüfer", abgelehntText);
        Assert.Contains(nummer1, abgelehntText);

        felder["Formular.PrueferExtern"] = "Prüfdienst Muster";
        var gespeichert = await PostAsync(geraetewart, "/Technik/Pruefungen/Sammel", felder);
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);
        Assert.Contains(erstes.ToString(), gespeichert.Headers.Location!.ToString());

        var pruefungen = await MitDbAsync(db => db.Pruefungen.Where(p => p.GeraetId == erstes || p.GeraetId == zweites).ToListAsync());
        Assert.Equal(2, pruefungen.Count);
        Assert.All(pruefungen, p => Assert.Equal("Prüfdienst Muster", p.PrueferExtern));
        Assert.All(pruefungen, p => Assert.NotNull(p.NaechsteFaellig));
        Assert.Equal("Fußpunkt locker", await MitDbAsync(db => db.Maengel.Where(m => m.GeraetId == zweites).Select(m => m.Beschreibung).SingleAsync()));

        // Plaketten für Etikettendrucker und A4-Bogen sowie die Fachliste sind PDFs.
        foreach (var format in new[] { "Drucker", "Bogen" })
        {
            var pdf = await geraetewart.GetAsync($"/Technik/Geraete/Plaketten?ids={erstes},{zweites}&format={format}");
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);
        }

        var fachliste = await geraetewart.GetAsync($"/Technik/Fahrzeuge/Fachliste/{fahrzeug.Id}");
        Assert.Equal("application/pdf", fachliste.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("A-123", "A-123")]
    [InlineData(" https://fevad.example/g/A%2F7 ", "A/7")]
    [InlineData("http://localhost/g/L-55?x=1", "L-55")]
    [InlineData("", "")]
    public void Scan_liefert_die_Inventarnummer(string eingabe, string erwartet) =>
        Assert.Equal(erwartet, Geraetecode.NummerAusScan(eingabe));

    private async Task<GeraetZustand> ZustandAsync(Guid geraetId, DateOnly stichtag)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<Pruefstaende>().GeraeteAsync(stichtag, geraetId: geraetId)).Single().Zustand;
    }
}
