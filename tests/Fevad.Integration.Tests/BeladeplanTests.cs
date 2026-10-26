using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Fahrzeugakte, Beladeplan, Beladekontrolle und Fahrerliste (V59).</summary>
[Collection(AnwendungCollection.Name)]
public partial class BeladeplanTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [Fact]
    public async Task Beladeplan_aus_Ist_Kontrolle_mit_Fehlbestand_wird_Mangel()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid fahrzeugId, c1, c2;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.Where(g => g.Code == "DRUCKSCHLAUCH").Select(g => g.Id).SingleAsync();
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Test " + kennung, Typ = "LF 10", ZulaessigeGesamtmasseKg = 13500 };
            var material = new Material { Bezeichnung = "Druckschlauch C " + kennung, GeraeteartId = art, Inventurpflichtig = false };
            var a = new Geraet { Inventarnummer = "BC1" + kennung, Bezeichnung = "Druckschlauch C", MaterialId = material.Id, GeraeteartId = art, FahrzeugId = fahrzeug.Id, Lagerort = "G2" };
            var b = new Geraet { Inventarnummer = "BC2" + kennung, Bezeichnung = "Druckschlauch C", MaterialId = material.Id, GeraeteartId = art, FahrzeugId = fahrzeug.Id, Lagerort = "G2" };
            db.AddRange(fahrzeug, material, a, b);
            await db.SaveChangesAsync();
            (fahrzeugId, c1, c2) = (fahrzeug.Id, a.Id, b.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var plan = await geraetewart.GetStringAsync($"/Technik/Fahrzeuge/Beladeplan/{fahrzeugId}");
        var uebernommen = await geraetewart.PostAsync($"/Technik/Fahrzeuge/Beladeplan/{fahrzeugId}?handler=AusIst",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(plan) }));
        Assert.Equal(HttpStatusCode.Redirect, uebernommen.StatusCode);

        var position = await MitDbAsync(db => db.Beladepositionen.SingleAsync(p => p.FahrzeugId == fahrzeugId));
        Assert.Equal(("G2", 2), (position.Fach, position.Menge));

        // Mengenposition dazu; die Gemeinde liest nur und darf den Plan nicht ändern.
        plan = await geraetewart.GetStringAsync($"/Technik/Fahrzeuge/Beladeplan/{fahrzeugId}");
        var felder = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(plan),
            ["Zeilen[0].Id"] = position.Id.ToString(),
            ["Zeilen[0].Fach"] = "G2",
            ["Zeilen[0].Bezeichnung"] = "Druckschlauch C",
            ["Zeilen[0].MaterialId"] = position.MaterialId!.Value.ToString(),
            ["Zeilen[0].Menge"] = "2",
            ["Zeilen[1].Fach"] = "G6",
            ["Zeilen[1].Bezeichnung"] = "Ölbindemittel 20 l",
            ["Zeilen[1].Menge"] = "2",
        };
        Assert.Equal(HttpStatusCode.Redirect, (await geraetewart.PostAsync($"/Technik/Fahrzeuge/Beladeplan/{fahrzeugId}", new FormUrlEncodedContent(felder))).StatusCode);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var gelesen = await hauptamt.GetStringAsync($"/Technik/Fahrzeuge/Beladeplan/{fahrzeugId}");
        Assert.Contains("Ölbindemittel 20 l", WebUtility.HtmlDecode(gelesen));
        Assert.DoesNotContain("Soll-Beladung bearbeiten", gelesen);
        Assert.Equal(HttpStatusCode.Forbidden, (await hauptamt.GetAsync($"/Technik/Fahrzeuge/Beladekontrolle/{fahrzeugId}")).StatusCode);

        // Kontrolle: ein Schlauch und ein Sack Ölbindemittel fehlen.
        var mengeId = await MitDbAsync(db => db.Beladepositionen.Where(p => p.FahrzeugId == fahrzeugId && p.MaterialId == null).Select(p => p.Id).SingleAsync());
        var kontrolle = await geraetewart.GetStringAsync($"/Technik/Fahrzeuge/Beladekontrolle/{fahrzeugId}");
        Assert.Contains("BC2" + kennung, kontrolle);
        var antwort = await geraetewart.PostAsync($"/Technik/Fahrzeuge/Beladekontrolle/{fahrzeugId}", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Token(kontrolle)),
            new("Formular.Datum", Anzeige.Heute(TimeProvider.System).ToString("yyyy-MM-dd")),
            new("Formular.Gefunden", c1.ToString()),
            new($"Formular.Gezaehlt[{mengeId}]", "1"),
            new("Formular.MangelMelden", "true"),
            new("Formular.Schwere", nameof(MangelSchwere.Einschraenkung)),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var gespeichert = await MitDbAsync(db => db.Beladekontrollen.SingleAsync(k => k.FahrzeugId == fahrzeugId));
        Assert.False(gespeichert.Vollstaendig);
        Assert.Equal(2, gespeichert.Fehlend);
        var mangel = await MitDbAsync(db => db.Maengel.SingleAsync(m => m.Id == gespeichert.MangelId));
        Assert.Equal(fahrzeugId, mangel.FahrzeugId);
        Assert.Contains("BC2" + kennung, mangel.Beschreibung);
        Assert.Contains("G6: Ölbindemittel 20 l, fehlt 1 von 2", mangel.Beschreibung);
        Assert.DoesNotContain(c2.ToString(), mangel.Beschreibung);

        var details = WebUtility.HtmlDecode(await geraetewart.GetStringAsync($"/Technik/Fahrzeuge/Details/{fahrzeugId}"));
        Assert.Contains("Fehlbestand", details);
        Assert.Contains("13.500 kg", details);
    }

    [Fact]
    public async Task Fahrerliste_zeigt_nur_ja_nein_bis_Datum()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var heute = Anzeige.Heute(TimeProvider.System);
        Guid fahrzeugId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Abteilungen.Where(a => a.Code == AbteilungCode.Einsatz).Select(a => a.Id).SingleAsync();
            var klasseC = await db.Qualifikationen.SingleAsync(q => q.Code == "FS-C");
            var klasseB = await db.Qualifikationen.SingleAsync(q => q.Code == "FS-B");
            Assert.Equal(3500, klasseB.FahrzeugmasseBisKg);
            Assert.Null(klasseC.FahrzeugmasseBisKg);

            var fahrer = new Mitglied { Personalnummer = "FL-" + kennung, Vorname = "Carla", Nachname = "Fahr" + kennung, Geburtsdatum = new(1985, 1, 1) };
            fahrer.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) });
            var nurB = new Mitglied { Personalnummer = "FB-" + kennung, Vorname = "Bernd", Nachname = "Pkw" + kennung, Geburtsdatum = new(1990, 1, 1) };
            nurB.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz, Status = MitgliedStatus.Aktiv, GueltigAb = new(2012, 1, 1) });
            db.Mitglieder.AddRange(fahrer, nurB);
            db.QualifikationNachweise.AddRange(
                new QualifikationNachweis { MitgliedId = fahrer.Id, QualifikationId = klasseC.Id, ErworbenAm = new(2008, 1, 1), GueltigBis = heute.AddYears(2) },
                new QualifikationNachweis { MitgliedId = nurB.Id, QualifikationId = klasseB.Id, ErworbenAm = new(2008, 1, 1) });
            // Die Führerscheinkontrolle (Halterpflicht) begrenzt das Datum.
            var kontrolle = await db.Nachweisarten.SingleAsync(a => a.Code == "FSK");
            db.Nachweise.Add(new Nachweis { MitgliedId = fahrer.Id, NachweisartId = kontrolle.Id, Datum = heute.AddMonths(-1), GueltigBis = heute.AddMonths(5) });
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Fahrer " + kennung, Typ = "HLF 10", ZulaessigeGesamtmasseKg = 13500 };
            db.Fahrzeuge.Add(fahrzeug);
            await db.SaveChangesAsync();
            fahrzeugId = fahrzeug.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var seite = WebUtility.HtmlDecode(await Angemeldet(factory, Rollen.Geraetewart).GetStringAsync($"/Technik/Fahrzeuge/Details/{fahrzeugId}"));
        Assert.Contains($"Fahr{kennung}, Carla", seite);
        Assert.Contains(Anzeige.Datum(heute.AddMonths(5)), seite);
        Assert.DoesNotContain($"Pkw{kennung}", seite);
        Assert.DoesNotContain("Klasse C", seite);

        // Hauptamt und Kämmerei lesen Fahrzeuge, aber keine Fahrerliste.
        var gemeinde = WebUtility.HtmlDecode(await Angemeldet(factory, Rollen.Hauptamt).GetStringAsync($"/Technik/Fahrzeuge/Details/{fahrzeugId}"));
        Assert.DoesNotContain($"Fahr{kennung}", gemeinde);
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
}
