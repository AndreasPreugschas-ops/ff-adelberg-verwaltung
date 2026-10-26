using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Pflege-Lebenslauf, Sammelpflege und Schlauchbuch (V63).</summary>
[Collection(AnwendungCollection.Name)]
public partial class PflegeTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static DateOnly Heute => Anzeige.Heute(TimeProvider.System);

    /// <summary>Jacke als Einzelstück, an ein Mitglied ausgegeben, das nach einem Einsatz in die Wäsche muss.</summary>
    private async Task<(Guid JackeId, string Nummer, Guid EinsatzId, Guid TeilnahmeId)> AusgegebeneJackeAsync(string kennung)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var art = await db.Geraetearten.SingleAsync(g => g.Code == "PSA-EJ");
        var material = new Material { Bezeichnung = "Jacke " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
        var jacke = new Geraet { Inventarnummer = "J" + kennung, Bezeichnung = "Einsatzjacke", MaterialId = material.Id, GeraeteartId = art.Id, Groesse = "52" };
        var mitglied = new Mitglied { Personalnummer = "P" + kennung, Vorname = "Paula", Nachname = "Pflege" + kennung, Geburtsdatum = new(1990, 1, 1) };
        var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.GeraeteartId == art.Id)
            ?? new Artikel { Code = "J" + kennung, Bezeichnung = "Jacke " + kennung, Kategorie = ArtikelKategorie.Einsatzkleidung, Groessenart = Groessenart.Jacke, GeraeteartId = art.Id };
        var ausgabe = new Kleiderkammervorgang { Nummer = "A" + kennung, Art = VorgangArt.Ausgabe, MitgliedId = mitglied.Id, Zeitpunkt = DateTimeOffset.UtcNow.AddMonths(-6) };
        var einsatz = new Einsatz { Einsatznummer = "P-" + kennung, Stichwort = "B 2", Alarmiert = DateTimeOffset.UtcNow.AddHours(-5), Status = EinsatzStatus.InBearbeitung };
        var teilnahme = new EinsatzTeilnahme { MitgliedId = mitglied.Id, PsaWaesche = true };
        einsatz.Teilnahmen.Add(teilnahme);
        db.AddRange(material, jacke, mitglied, ausgabe, einsatz);
        if (db.Entry(artikel).State == EntityState.Detached)
        {
            db.Artikel.Add(artikel);
        }

        db.Ausstattungen.Add(new Ausstattung { MitgliedId = mitglied.Id, ArtikelId = artikel.Id, Groesse = "52", GeraetId = jacke.Id, AusgegebenAm = Heute.AddMonths(-6), AusgabeVorgangId = ausgabe.Id });
        await db.SaveChangesAsync();
        return (jacke.Id, jacke.Inventarnummer, einsatz.Id, teilnahme.Id);
    }

    [Fact]
    public async Task Waesche_der_Kleiderkammer_landet_im_Pflege_Lebenslauf_und_Sammelpflege_traegt_fuer_alle_ein()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var (jackeId, nummer, einsatzId, teilnahmeId) = await AusgegebeneJackeAsync(kennung);
        var factory = TestAnmeldung.Factory(fixture);

        // Die Kleiderkammer vermerkt die Wäsche nach dem Einsatz; das Einzelstück bekommt einen Eintrag mit Einsatz.
        var kleiderwart = Angemeldet(factory, Rollen.Kleiderwart);
        var waesche = await kleiderwart.GetStringAsync("/Kleiderkammer/Waesche");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kleiderwart, "/Kleiderkammer/Waesche?handler=Erledigt", new()
        {
            ["__RequestVerificationToken"] = Token(waesche),
            ["teilnahmeIds"] = teilnahmeId.ToString(),
        })).StatusCode);
        var eintrag = await MitDbAsync(db => db.Pflegeeintraege.SingleAsync(p => p.GeraetId == jackeId));
        Assert.Equal((Pflegeart.Waesche, einsatzId), (eintrag.Art, eintrag.EinsatzId!.Value));

        // Der Gerätewart lädt die Kleidung des Einsatzes in die Sammelpflege, ohne Namen zu sehen.
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var json = await geraetewart.GetStringAsync($"/Technik/Pflege?handler=Einsatz&einsatzId={einsatzId}");
        Assert.Contains(nummer, json);
        Assert.DoesNotContain("Pflege" + kennung, json);
        using (var dokument = JsonDocument.Parse(json))
        {
            Assert.Equal(1, dokument.RootElement[0].GetProperty("waeschen").GetInt32());
            Assert.Equal(25, dokument.RootElement[0].GetProperty("grenze").GetInt32());
        }

        Assert.Contains(nummer, await geraetewart.GetStringAsync($"/Technik/Pflege?handler=Geraet&nummer={nummer}"));
        var seite = await geraetewart.GetStringAsync("/Technik/Pflege");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Pflege", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Art"] = nameof(Pflegeart.Impraegnierung),
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"),
            ["Formular.EinsatzId"] = einsatzId.ToString(),
            ["Formular.Durchfuehrung"] = "Wäscherei",
            ["Formular.GeraetIds"] = jackeId.ToString(),
        })).StatusCode);
        Assert.Equal(2, await MitDbAsync(db => db.Pflegeeintraege.CountAsync(p => p.GeraetId == jackeId)));

        // Zukunftsdatum wird abgelehnt.
        var falsch = await PostAsync(geraetewart, "/Technik/Pflege", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Art"] = nameof(Pflegeart.Waesche),
            ["Formular.Datum"] = Heute.AddDays(3).ToString("dd.MM.yyyy"),
            ["Formular.GeraetIds"] = jackeId.ToString(),
        });
        Assert.Equal(HttpStatusCode.OK, falsch.StatusCode);
        Assert.Equal(2, await MitDbAsync(db => db.Pflegeeintraege.CountAsync(p => p.GeraetId == jackeId)));

        // Die Geräteakte zeigt Lebenslauf und Wäschegrenze; ein Eintrag kommt auch einzeln dazu.
        var akte = await geraetewart.GetStringAsync($"/Technik/Geraete/Details/{jackeId}");
        Assert.Contains("Pflege-Lebenslauf", akte);
        Assert.Contains("P-" + kennung, akte);
        Assert.Contains("von 25 (Hersteller)", akte);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Technik/Geraete/Details/{jackeId}?handler=Pflege", new()
        {
            ["__RequestVerificationToken"] = Token(akte),
            ["NeuePflege.Art"] = nameof(Pflegeart.Reparatur),
            ["NeuePflege.Bemerkung"] = "Reflexstreifen genäht",
        })).StatusCode);
        Assert.Equal(3, await MitDbAsync(db => db.Pflegeeintraege.CountAsync(p => p.GeraetId == jackeId)));
    }

    [Fact]
    public async Task Schlauchbuch_zeigt_Groesse_Laenge_und_Werkstattlaeufe()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.SingleAsync(g => g.Code == "DRUCKSCHLAUCH");
            var material = new Material { Bezeichnung = "Schlauch " + kennung, GeraeteartId = art.Id, Typ = "B 75-20", Inventurpflichtig = false };
            var schlauch = new Geraet { Inventarnummer = "S" + kennung, Bezeichnung = "Druckschlauch B", MaterialId = material.Id, GeraeteartId = art.Id, Baujahr = 2015 };
            var auftrag = new Werkstattauftrag { Nummer = "W-S-" + kennung, Werkstattname = "Schlauchwerkstatt", Werkstatt = Ruecklauf.Schlauchwerkstatt, AbgegebenAm = Heute.AddDays(-20), ZurueckAm = Heute.AddDays(-10) };
            auftrag.Positionen.Add(new Werkstattposition { GeraetId = schlauch.Id, Leistungen = Werkstattleistung.Gereinigt | Werkstattleistung.Geprueft });
            db.AddRange(material, schlauch, auftrag);
            await db.SaveChangesAsync();
        }

        var geraetewart = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Geraetewart);
        var seite = WebUtility.HtmlDecode(await geraetewart.GetStringAsync("/Technik/Schlauchbuch?groesse=B"));
        var zeile = Regex.Match(seite, $"S{kennung}.*?</tr>", RegexOptions.Singleline).Value;
        Assert.Contains("20 m", zeile);
        Assert.Contains("gereinigt, geprüft", zeile);
        Assert.Contains("2015", zeile);
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string adresse, Dictionary<string, string> felder) =>
        client.PostAsync(adresse, new FormUrlEncodedContent(felder));

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
}
