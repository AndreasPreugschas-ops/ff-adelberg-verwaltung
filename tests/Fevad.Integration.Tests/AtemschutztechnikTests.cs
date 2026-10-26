using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Atemschutztechnik (V61): Gerätearten, Werkstattauftrag mit Rückgabe, Füllprotokoll, Bauteile und Vorschau.</summary>
[Collection(AnwendungCollection.Name)]
public partial class AtemschutztechnikTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex("name=\"Rueckgabe.Zeilen\\[0\\].PositionId\" value=\"([^\"]+)\"")]
    private static partial Regex PositionFeld();

    [Fact]
    public async Task Atemschutz_Geraetearten_mit_inaktivem_CSA_und_Bauteilarten_sind_angelegt()
    {
        var arten = await MitDbAsync(db => db.Geraetearten.Where(g => g.Code.StartsWith("PA") || g.Code == "CSA").ToDictionaryAsync(g => g.Code, g => g));
        Assert.Contains("PA-LA", arten.Keys);
        Assert.Contains("PA-MASKE", arten.Keys);
        Assert.Contains("PA-FLASCHE-CFK", arten.Keys);
        Assert.Contains("PA-KOMPRESSOR", arten.Keys);
        Assert.False(arten["CSA"].Aktiv);
        Assert.Equal(Ruecklauf.Atemschutzwerkstatt, arten["PA-FLASCHE"].Ruecklauf);
        var membran = await MitDbAsync(db => db.Bauteilarten.SingleAsync(b => b.Code == "LA-MEMBRAN"));
        Assert.Equal((72, 96), (membran.EingebautMonate!.Value, membran.GesamtMonate!.Value));
        Assert.Equal(arten["PA-LA"].Id, membran.GeraeteartId);
    }

    [Fact]
    public async Task Abgabe_nach_Einsatz_und_Rueckgabe_aus_Goeppingen_hebt_die_Sperre_auf()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid flascheId, abgelaufenId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.SingleAsync(g => g.Code == "PA-FLASCHE-CFK");
            var material = new Material { Bezeichnung = "Atemluftflasche CFK " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
            var flasche = new Geraet { Inventarnummer = "AF" + kennung, Bezeichnung = "Atemluftflasche", MaterialId = material.Id, GeraeteartId = art.Id, Herstelldatum = new(2015, 3, 1), NutzbarBis = new(2045, 3, 1) };
            var abgelaufen = new Geraet { Inventarnummer = "AX" + kennung, Bezeichnung = "Atemluftflasche alt", MaterialId = material.Id, GeraeteartId = art.Id, Herstelldatum = new(2000, 3, 1), NutzbarBis = new(2020, 3, 1) };
            var einsatz = new Einsatz { Einsatznummer = "T-AS-" + kennung, Alarmiert = DateTimeOffset.UtcNow.AddDays(-1), Status = EinsatzStatus.InBearbeitung };
            db.AddRange(material, flasche, abgelaufen, einsatz);
            db.Maengel.Add(new Mangel { GeraetId = flasche.Id, EinsatzId = einsatz.Id, SperreBisPruefung = true, Beschreibung = "Nach Einsatz benutzt", Schwere = MangelSchwere.AusserBetrieb, Status = MangelStatus.Offen, GemeldetAm = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            (flascheId, abgelaufenId) = (flasche.Id, abgelaufen.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var agw = Angemeldet(factory, Rollen.Atemschutzgeraetewart);
        var neu = await agw.GetStringAsync($"/Technik/Werkstatt/Neu?werkstatt={Ruecklauf.Atemschutzwerkstatt}");
        Assert.Contains($"id=\"g-{flascheId}\"", neu);
        Assert.Matches($"id=\"g-{flascheId}\"[^>]*checked", neu);
        var antwort = await agw.PostAsync("/Technik/Werkstatt/Neu", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Token(neu)),
            new("Formular.Werkstatt", nameof(Ruecklauf.Atemschutzwerkstatt)),
            new("Formular.Werkstattname", "Feuerwehr Göppingen, Atemschutzwerkstatt"),
            new("Formular.AbgegebenAm", Anzeige.Heute(TimeProvider.System).ToString("yyyy-MM-dd")),
            new("Formular.Geraete", flascheId.ToString()),
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var details = antwort.Headers.Location!.OriginalString;

        var stand = await MitScopeAsync(sp => sp.GetRequiredService<Pruefstaende>().GeraeteAsync(Anzeige.Heute(TimeProvider.System), geraetId: flascheId));
        Assert.Equal(GeraetZustand.InWerkstatt, Assert.Single(stand).Zustand);
        var gesperrt = await MitScopeAsync(sp => sp.GetRequiredService<Pruefstaende>().GeraeteAsync(Anzeige.Heute(TimeProvider.System), geraetId: abgelaufenId));
        Assert.Equal(GeraetZustand.AusserBetrieb, Assert.Single(gesperrt).Zustand);

        var pdf = await agw.GetAsync(details + "?handler=Lieferschein");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        // Hauptamt liest mit, bucht aber nicht zurück und sieht keinen Namen.
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        Assert.DoesNotContain("Rückgabe buchen", await hauptamt.GetStringAsync(details));

        var seite = await agw.GetStringAsync(details);
        var position = PositionFeld().Match(seite).Groups[1].Value;
        var nachGebrauch = await MitDbAsync(db => db.PruefplanPositionen.Where(p => p.Geraeteart!.Code == "PA-FLASCHE-CFK" && p.Pruefart == Pruefart.NachGebrauch).Select(p => p.Id).SingleAsync());
        using var formular = new MultipartFormDataContent
        {
            { new StringContent(Token(seite)), "__RequestVerificationToken" },
            { new StringContent(Anzeige.Heute(TimeProvider.System).ToString("yyyy-MM-dd")), "Rueckgabe.ZurueckAm" },
            { new StringContent("GP-4711"), "Rueckgabe.Beleg" },
            { new StringContent(position), "Rueckgabe.Zeilen[0].PositionId" },
            { new StringContent("true"), "Rueckgabe.Zeilen[0].Gereinigt" },
            { new StringContent("true"), "Rueckgabe.Zeilen[0].Geprueft" },
            { new StringContent("true"), "Rueckgabe.Zeilen[0].Gefuellt" },
            { new StringContent(nachGebrauch.ToString()), "Rueckgabe.Zeilen[0].PruefplanPositionId" },
            { new StringContent(nameof(PruefErgebnis.Bestanden)), "Rueckgabe.Zeilen[0].Ergebnis" },
        };
        var protokoll = new ByteArrayContent("%PDF-1.4 Werkstattprotokoll"u8.ToArray());
        protokoll.Headers.ContentType = new("application/pdf");
        formular.Add(protokoll, "Dateien", "Protokoll Göppingen.pdf");
        var zurueck = await agw.PostAsync(details, formular);
        Assert.Equal(HttpStatusCode.Redirect, zurueck.StatusCode);

        var pruefung = await MitDbAsync(db => db.Pruefungen.SingleAsync(p => p.GeraetId == flascheId));
        Assert.Equal(("Feuerwehr Göppingen, Atemschutzwerkstatt", Pruefart.NachGebrauch), (pruefung.PrueferExtern, pruefung.Pruefart));
        Assert.True(await MitDbAsync(db => db.Maengel.Where(m => m.GeraetId == flascheId).AllAsync(m => m.Status == MangelStatus.Behoben)));
        var auftrag = await MitDbAsync(db => db.Werkstattauftraege.Include(w => w.Positionen).Include(w => w.Protokolle).SingleAsync(w => w.Positionen.Any(p => p.GeraetId == flascheId)));
        Assert.Equal("GP-4711", auftrag.BelegWerkstatt);
        Assert.Equal(Werkstattleistung.Gereinigt | Werkstattleistung.Geprueft | Werkstattleistung.Gefuellt, Assert.Single(auftrag.Positionen).Leistungen);
        Assert.Single(auftrag.Protokolle);
        stand = await MitScopeAsync(sp => sp.GetRequiredService<Pruefstaende>().GeraeteAsync(Anzeige.Heute(TimeProvider.System), geraetId: flascheId));
        Assert.Equal(GeraetZustand.Einsatzbereit, Assert.Single(stand).Zustand);
    }

    [Fact]
    public async Task Fuellprotokoll_ist_abschaltbar_und_weist_abgelaufene_Flaschen_ab()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid gut;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.SingleAsync(g => g.Code == "PA-FLASCHE");
            var material = new Material { Bezeichnung = "Atemluftflasche Stahl " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
            var flasche = new Geraet { Inventarnummer = "FS" + kennung, Bezeichnung = "Atemluftflasche", MaterialId = material.Id, GeraeteartId = art.Id };
            var alt = new Geraet { Inventarnummer = "FA" + kennung, Bezeichnung = "Atemluftflasche", MaterialId = material.Id, GeraeteartId = art.Id, NutzbarBis = new(2020, 1, 1) };
            db.AddRange(material, flasche, alt);
            await db.SaveChangesAsync();
            gut = flasche.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var agw = Angemeldet(factory, Rollen.Atemschutzgeraetewart);
        var seite = await agw.GetStringAsync("/Technik/Fuellprotokoll");
        Assert.Contains("ausgeschaltet", seite);
        Assert.Equal(HttpStatusCode.Forbidden, (await agw.PostAsync("/Technik/Fuellprotokoll", Formular(seite, $"FS{kennung}"))).StatusCode);

        await SchalterAsync(ParameterSchluessel.FuellprotokollAktiv, "1");
        try
        {
            seite = await agw.GetStringAsync("/Technik/Fuellprotokoll");
            var abgewiesen = await agw.PostAsync("/Technik/Fuellprotokoll", Formular(seite, $"FS{kennung} FA{kennung}"));
            Assert.Equal(HttpStatusCode.OK, abgewiesen.StatusCode);
            Assert.Contains($"FA{kennung}", WebUtility.HtmlDecode(await abgewiesen.Content.ReadAsStringAsync()));

            Assert.Equal(HttpStatusCode.Redirect, (await agw.PostAsync("/Technik/Fuellprotokoll", Formular(seite, $"fs{kennung}"))).StatusCode);
            var fuellung = await MitDbAsync(db => db.Flaschenfuellungen.SingleAsync(f => f.GeraetId == gut));
            Assert.Equal(300, fuellung.DruckBar);
        }
        finally
        {
            await SchalterAsync(ParameterSchluessel.FuellprotokollAktiv, "0");
        }

        static FormUrlEncodedContent Formular(string seite, string flaschen) => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Datum"] = Anzeige.Heute(TimeProvider.System).ToString("yyyy-MM-dd"),
            ["Formular.Uhrzeit"] = "00:05",
            ["Formular.Flaschen"] = flaschen,
            ["Formular.DruckBar"] = "300",
        });
    }

    [Fact]
    public async Task Bauteile_und_Vorschau_zeigen_Tausch_und_Nutzungsende()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var heute = Anzeige.Heute(TimeProvider.System);
        Guid la;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var art = await db.Geraetearten.SingleAsync(g => g.Code == "PA-LA");
            var flaschenart = await db.Geraetearten.SingleAsync(g => g.Code == "PA-FLASCHE-CFK");
            var material = new Material { Bezeichnung = "Lungenautomat " + kennung, GeraeteartId = art.Id, Inventurpflichtig = false };
            var flaschenmaterial = new Material { Bezeichnung = "Flasche CFK " + kennung, GeraeteartId = flaschenart.Id, Inventurpflichtig = false };
            var geraet = new Geraet { Inventarnummer = "LA" + kennung, Bezeichnung = "Lungenautomat", MaterialId = material.Id, GeraeteartId = art.Id };
            var flasche = new Geraet { Inventarnummer = "VF" + kennung, Bezeichnung = "Flasche", MaterialId = flaschenmaterial.Id, GeraeteartId = flaschenart.Id, NutzbarBis = heute.AddMonths(14) };
            db.AddRange(material, flaschenmaterial, geraet, flasche);
            await db.SaveChangesAsync();
            la = geraet.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var agw = Angemeldet(factory, Rollen.Atemschutzgeraetewart);
        var vorschau = WebUtility.HtmlDecode(await agw.GetStringAsync($"/Technik/Vorschau?Werkstatt={Ruecklauf.Atemschutzwerkstatt}"));
        Assert.Contains("Lebensdauer erreicht", vorschau);
        Assert.Contains($"VF{kennung}", vorschau);

        var seite = await agw.GetStringAsync($"/Technik/Geraete/Bauteile/{la}");
        Assert.Contains("ausgeschaltet", seite);
        await SchalterAsync(ParameterSchluessel.BauteildatenAktiv, "1");
        try
        {
            seite = await agw.GetStringAsync($"/Technik/Geraete/Bauteile/{la}");
            var membran = await MitDbAsync(db => db.Bauteilarten.Where(b => b.Code == "LA-MEMBRAN").Select(b => b.Id).SingleAsync());
            var antwort = await agw.PostAsync($"/Technik/Geraete/Bauteile/{la}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(seite),
                ["Formular.BauteilartId"] = membran.ToString(),
                ["Formular.HergestelltAm"] = heute.AddYears(-8).AddMonths(2).ToString("yyyy-MM-dd"),
                ["Formular.EingebautAm"] = heute.AddYears(-1).ToString("yyyy-MM-dd"),
            }));
            Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
            seite = WebUtility.HtmlDecode(await agw.GetStringAsync($"/Technik/Geraete/Bauteile/{la}"));
            Assert.Contains(Anzeige.Datum(heute.AddYears(-8).AddMonths(2).AddMonths(96)), seite);

            vorschau = WebUtility.HtmlDecode(await agw.GetStringAsync($"/Technik/Vorschau?Werkstatt={Ruecklauf.Atemschutzwerkstatt}"));
            Assert.Contains("Membran Lungenautomat", vorschau);
        }
        finally
        {
            await SchalterAsync(ParameterSchluessel.BauteildatenAktiv, "0");
        }
    }

    private async Task SchalterAsync(string schluessel, string wert)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parameter = await db.Parameter.SingleAsync(p => p.Schluessel == schluessel);
        parameter.Wert = wert;
        await db.SaveChangesAsync();
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
}
