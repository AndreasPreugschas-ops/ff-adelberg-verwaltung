using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Z-Feu-Assistent (V30): Antrag an der Beschaffung, Bestellsperre vor dem Bescheid, Checkliste und Zweckbindung am Fahrzeug.</summary>
[Collection(AnwendungCollection.Name)]
public partial class FoerderungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static DateOnly Heute => Anzeige.Heute(TimeProvider.System);

    private static string Datum(DateOnly tag) => tag.ToString("dd.MM.yyyy");

    [Fact]
    public async Task Antrag_sperrt_Bestellung_bis_zum_Bescheid_und_bringt_Zweckbindung_ans_Fahrzeug()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        Guid beschaffungId, altId, neuId, mtwId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var alt = new Fahrzeug { Funkrufname = "Florian Alt " + kennung, Typ = "MTW", Baujahr = 1996 };
            var neu = new Fahrzeug { Funkrufname = "Florian Neu " + kennung, Typ = "MTW", Baujahr = Heute.Year, IndienstAm = Heute.AddDays(-5) };
            var beschaffung = new Beschaffung { Bezeichnung = "MTW " + kennung, FahrzeugId = alt.Id, Status = BeschaffungStatus.Genehmigt, Kostenschaetzung = 80000m };
            db.AddRange(alt, neu, beschaffung);
            await db.SaveChangesAsync();
            (beschaffungId, altId, neuId) = (beschaffung.Id, alt.Id, neu.Id);
            mtwId = await db.Foerdergegenstaende.Where(g => g.Code == "ZF-MTW").Select(g => g.Id).SingleAsync();
        }

        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var pfad = $"/Beschaffung/Foerderung/Antrag/{beschaffungId}";
        var seite = await kommandant.GetStringAsync(pfad);
        Assert.Contains("Antrag vorbereiten", seite);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Anlegen", new() { ["__RequestVerificationToken"] = Token(seite) })).StatusCode);
        var antrag = await MitDbAsync(db => db.Foerderantraege.SingleAsync(a => a.BeschaffungId == beschaffungId));
        Assert.Equal((Foerderstatus.Vorbereitung, altId), (antrag.Status, antrag.AltfahrzeugId!.Value));
        Assert.True(antrag.Antragsjahr >= Heute.Year);

        // Vor dem Bescheid lässt sich die Beschaffung nicht bestellen.
        var beschaffungSeite = await kommandant.GetStringAsync($"/Beschaffung/Bearbeiten/{beschaffungId}");
        Assert.Contains("Z-Feu-Antrag (in Vorbereitung)", WebUtility.HtmlDecode(beschaffungSeite));
        var version = Regex.Match(beschaffungSeite, "name=\"Formular.Version\" value=\"(\\d+)\"").Groups[1].Value;
        var bestellen = await PostAsync(kommandant, $"/Beschaffung/Bearbeiten/{beschaffungId}", new()
        {
            ["__RequestVerificationToken"] = Token(beschaffungSeite), ["Formular.Id"] = beschaffungId.ToString(), ["Formular.Version"] = version,
            ["Formular.Bezeichnung"] = "MTW " + kennung, ["Formular.Menge"] = "1", ["ziel"] = nameof(BeschaffungStatus.Bestellt),
        });
        Assert.Equal(HttpStatusCode.OK, bestellen.StatusCode);
        Assert.Contains("Bestellen erst nach dem Zuwendungsbescheid", WebUtility.HtmlDecode(await bestellen.Content.ReadAsStringAsync()));

        Dictionary<string, string> Felder(string? ziel, bool checkliste = false)
        {
            var felder = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(seite),
                ["Formular.FoerdergegenstandId"] = mtwId.ToString(), ["Formular.Antragsjahr"] = antrag.Antragsjahr.ToString(),
                ["Formular.KbmAbgestimmtAm"] = Datum(Heute.AddDays(-90)), ["Formular.GemeinderatsbeschlussAm"] = Datum(Heute.AddDays(-60)),
                ["Formular.BeantragtAm"] = Datum(Heute.AddDays(-50)), ["Formular.BescheidAm"] = Datum(Heute.AddDays(-20)),
                ["Formular.BescheidAktenzeichen"] = "AZ " + kennung, ["Formular.Zuwendung"] = "25000,00",
                ["Formular.AltfahrzeugId"] = altId.ToString(), ["Formular.FahrzeugId"] = neuId.ToString(),
                ["Formular.NachweisEingereichtAm"] = Datum(Heute),
            };
            if (checkliste)
            {
                foreach (var punkt in new[] { "RechnungLiegtVor", "RichtigkeitsbescheinigungLiegtVor", "AbnahmeberichtLiegtVor", "BeladungserklaerungLiegtVor" })
                {
                    felder["Formular." + punkt] = "true";
                }
            }

            if (ziel is not null)
            {
                felder["ziel"] = ziel;
            }

            return felder;
        }

        foreach (var ziel in new[] { Foerderstatus.KbmAbgestimmt, Foerderstatus.GemeinderatBeschlossen, Foerderstatus.Beantragt, Foerderstatus.Bewilligt })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad, Felder(ziel.ToString()))).StatusCode);
        }

        // Ohne Checkliste und solange das Altfahrzeug im Dienst ist, kein Verwendungsnachweis.
        var ohne = await PostAsync(kommandant, pfad, Felder(nameof(Foerderstatus.NachweisEingereicht)));
        Assert.Equal(HttpStatusCode.OK, ohne.StatusCode);
        var meldung = WebUtility.HtmlDecode(await ohne.Content.ReadAsStringAsync());
        Assert.Contains("Rechnung", meldung);
        Assert.Contains("Altfahrzeug außer Dienst gestellt", meldung);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(kommandant, pfad, Felder(nameof(Foerderstatus.NachweisEingereicht), checkliste: true))).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Fahrzeuge.SingleAsync(f => f.Id == altId)).AusserDienstAm = Heute.AddDays(-1);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad, Felder(nameof(Foerderstatus.NachweisEingereicht), checkliste: true))).StatusCode);
        var fahrzeug = await MitDbAsync(db => db.Fahrzeuge.SingleAsync(f => f.Id == neuId));
        Assert.Equal((25000m, Heute.AddDays(-5).AddYears(10), "AZ " + kennung), (fahrzeug.Foerdermittel!.Value, fahrzeug.ZweckbindungBis!.Value, fahrzeug.ZuwendungsbescheidAktenzeichen!));
        Assert.Equal(Foerderstatus.NachweisEingereicht, await MitDbAsync(db => db.Foerderantraege.Where(a => a.BeschaffungId == beschaffungId).Select(a => a.Status).SingleAsync()));

        // Übersicht zeigt den Antrag; das Hauptamt liest mit, ändert aber nichts.
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        Assert.Contains("MTW " + kennung, await hauptamt.GetStringAsync("/Beschaffung/Foerderung"));
        var hauptamtSeite = await hauptamt.GetStringAsync(pfad);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(hauptamt, pfad, new() { ["__RequestVerificationToken"] = Token(hauptamtSeite), ["Formular.Antragsjahr"] = "2030" })).StatusCode);
    }

    [Fact]
    public async Task Antragsfrist_meldet_sich_und_nur_die_Fuehrung_pflegt_den_Katalog()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beschaffung = new Beschaffung { Bezeichnung = "TLF " + kennung, Status = BeschaffungStatus.Angemeldet };
            db.AddRange(beschaffung, new Foerderantrag { BeschaffungId = beschaffung.Id, Antragsjahr = Heute.AddDays(30).Year, Status = Foerderstatus.KbmAbgestimmt });
            await db.SaveChangesAsync();
            var verfahren = scope.ServiceProvider.GetRequiredService<Foerderverfahren>();
            var stichtag = new DateOnly(Heute.AddDays(30).Year, 2, 1);
            var hinweise = await verfahren.FaelligeAsync(stichtag, default);
            Assert.Contains(hinweise, h => h.Gegenstand == "Z-Feu: TLF " + kennung && h.FaelligAm == new DateOnly(stichtag.Year, 2, 15));
        }

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var gegenstand = await MitDbAsync(db => db.Foerdergegenstaende.SingleAsync(g => g.Code == "ZF-GWL1"));
        var seite = await kommandant.GetStringAsync($"/Beschaffung/Foerderung?bearbeiten={gegenstand.Id}");
        Dictionary<string, string> Felder(string token) => new()
        {
            ["__RequestVerificationToken"] = token, ["Formular.Festbetrag"] = "61500,00", ["Formular.ZweckbindungJahre"] = "20",
            ["Formular.GueltigBis"] = "31.12.2031", ["Formular.Aktiv"] = "true",
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(geraetewart, $"/Beschaffung/Foerderung?handler=Katalog&id={gegenstand.Id}", Felder(Token(await geraetewart.GetStringAsync("/Beschaffung/Foerderung"))))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, $"/Beschaffung/Foerderung?handler=Katalog&id={gegenstand.Id}", Felder(Token(seite)))).StatusCode);
        Assert.Equal(61500m, await MitDbAsync(db => db.Foerdergegenstaende.Where(g => g.Id == gegenstand.Id).Select(g => g.Festbetrag).SingleAsync()));
        await MitDbAsync(async db =>
        {
            var g = await db.Foerdergegenstaende.SingleAsync(x => x.Id == gegenstand.Id);
            g.Festbetrag = gegenstand.Festbetrag;
            return await db.SaveChangesAsync();
        });
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
