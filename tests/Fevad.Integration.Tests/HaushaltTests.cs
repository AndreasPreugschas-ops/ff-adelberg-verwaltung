using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Haushaltsanmeldung (V29): Vorschläge aus FEVAD, Jugendwart nur JF, Übergabe erst ohne offene Punkte, Rückmeldung der Gemeinde.</summary>
[Collection(AnwendungCollection.Name)]
public partial class HaushaltTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [Fact]
    public async Task Anmeldung_vom_Vorschlag_bis_zur_Rueckmeldung()
    {
        var kennung = Guid.NewGuid().ToString("N")[..8];
        var jahr = Random.Shared.Next(2040, 2100);
        await MitDbAsync(async db =>
        {
            await db.Haushaltsanmeldungen.Where(a => a.Jahr == jahr).ExecuteDeleteAsync();
            var beschaffung = new Beschaffung { Bezeichnung = "TLF " + kennung, Status = BeschaffungStatus.Angemeldet, Haushaltsjahr = jahr, Kostenschaetzung = 420000m, Begruendung = "TLF 1986" };
            var mtw = await db.Foerdergegenstaende.SingleAsync(g => g.Code == "ZF-TLF3000");
            db.AddRange(beschaffung, new Foerderantrag { BeschaffungId = beschaffung.Id, Antragsjahr = jahr, FoerdergegenstandId = mtw.Id });
            return await db.SaveChangesAsync();
        });

        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var jugendwart = Angemeldet(factory, Rollen.Jugendwart);
        var liste = await kommandant.GetStringAsync("/Haushalt");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, "/Haushalt", new() { ["__RequestVerificationToken"] = Token(liste), ["jahr"] = jahr.ToString() })).StatusCode);
        var pfad = $"/Haushalt/Anmeldung/{jahr}";

        // Der Jugendwart darf keine Vorschläge erzeugen und nur für die Jugendfeuerwehr zumelden.
        var jwSeite = await jugendwart.GetStringAsync(pfad);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(jugendwart, pfad + "?handler=Vorschlaege", new() { ["__RequestVerificationToken"] = Token(jwSeite) })).StatusCode);
        var jfStelle = await MitDbAsync(db => db.Haushaltsstellen.Where(h => h.Code == "EH-4291-JF").Select(h => h.Id).SingleAsync());
        var formular = await jugendwart.GetStringAsync($"/Haushalt/Position?jahr={jahr}");
        Dictionary<string, string> Position(string bereich) => new()
        {
            ["__RequestVerificationToken"] = Token(formular), ["Formular.HaushaltsstelleId"] = jfStelle.ToString(), ["Formular.Bereich"] = bereich,
            ["Formular.Bezeichnung"] = "Zeltlager " + kennung, ["Formular.Menge"] = "1", ["Formular.Einzelpreis"] = "850,00",
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(jugendwart, $"/Haushalt/Position?jahr={jahr}", Position(nameof(Haushaltsbereich.Technik)))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(jugendwart, $"/Haushalt/Position?jahr={jahr}", Position(nameof(Haushaltsbereich.Jugend)))).StatusCode);

        // Vorschläge: die Beschaffung als Fahrzeug-Investition mit Z-Feu-Zuwendung, dazu die Zuschüsse an die Kasse.
        var seite = await kommandant.GetStringAsync(pfad);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Vorschlaege", new() { ["__RequestVerificationToken"] = Token(seite) })).StatusCode);
        var positionen = await MitDbAsync(db => db.Haushaltspositionen.Include(p => p.Haushaltsstelle).Where(p => p.Haushaltsanmeldung!.Jahr == jahr).ToListAsync());
        var tlf = Assert.Single(positionen, p => p.Bezeichnung == "TLF " + kennung);
        Assert.Equal(("FH-7832", PositionStatus.Vorschlag, 140000m), (tlf.Haushaltsstelle!.Code, tlf.Status, tlf.Zuwendung!.Value));
        Assert.Contains(positionen, p => p.Herkunft == "zuschuss:kasse" && p.Betrag == 1500m);
        var anzahl = positionen.Count;
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Vorschlaege", new() { ["__RequestVerificationToken"] = Token(seite) })).StatusCode);
        Assert.Equal(anzahl, await MitDbAsync(db => db.Haushaltspositionen.CountAsync(p => p.Haushaltsanmeldung!.Jahr == jahr)));

        // Übergabe geht erst nach Ausschuss und ohne offene Vorschläge.
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Status", new()
        {
            ["__RequestVerificationToken"] = Token(seite), ["ziel"] = nameof(AnmeldungStatus.Beraten), ["beschluss"] = "Ausschuss 5, TOP 2",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Status", new() { ["__RequestVerificationToken"] = Token(seite), ["ziel"] = nameof(AnmeldungStatus.Eingereicht) })).StatusCode);
        Assert.Equal(AnmeldungStatus.Beraten, await StatusAsync(jahr));
        foreach (var p in positionen.Where(p => p.Status == PositionStatus.Vorschlag && p.Id != tlf.Id))
        {
            await PostAsync(kommandant, pfad + "?handler=Position", new() { ["__RequestVerificationToken"] = Token(seite), ["positionId"] = p.Id.ToString(), ["aktion"] = "streichen" });
        }

        await PostAsync(kommandant, pfad + "?handler=Position", new() { ["__RequestVerificationToken"] = Token(seite), ["positionId"] = tlf.Id.ToString(), ["aktion"] = "bestaetigen" });
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Status", new() { ["__RequestVerificationToken"] = Token(seite), ["ziel"] = nameof(AnmeldungStatus.Eingereicht) })).StatusCode);
        Assert.Equal(AnmeldungStatus.Eingereicht, await StatusAsync(jahr));

        // Hauptamt liest, trägt aber keine Rückmeldung ein; der Kommandant schon.
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var hauptamtSeite = WebUtility.HtmlDecode(await hauptamt.GetStringAsync(pfad));
        Assert.Contains("TLF " + kennung, hauptamtSeite);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(hauptamt, pfad + "?handler=Rueckmeldung", new()
        {
            ["__RequestVerificationToken"] = Token(hauptamtSeite), ["positionId"] = tlf.Id.ToString(), ["ergebnis"] = nameof(Haushaltsergebnis.Beschlossen),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Rueckmeldung", new()
        {
            ["__RequestVerificationToken"] = Token(seite), ["positionId"] = tlf.Id.ToString(), ["ergebnis"] = nameof(Haushaltsergebnis.Gekuerzt), ["betrag"] = "380000",
        })).StatusCode);
        var rueckmeldung = await MitDbAsync(db => db.Haushaltspositionen.SingleAsync(p => p.Id == tlf.Id));
        Assert.Equal((Haushaltsergebnis.Gekuerzt, 380000m), (rueckmeldung.Ergebnis!.Value, rueckmeldung.BeschlossenerBetrag!.Value));

        // PDF und CSV für die Kämmerei.
        var pdf = await hauptamt.GetAsync(pfad + "?handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var csv = await hauptamt.GetStringAsync(pfad + "?handler=Export");
        Assert.Contains("TLF " + kennung, csv);
        Assert.Contains("7832", csv);

        // Überwachung: das Ist aus SAP übersteigt den gekürzten Ansatz; nur wer alle Bereiche bearbeitet, spielt es ein.
        var ueberwachung = $"/Haushalt/Ueberwachung?jahr={jahr}";
        var ueSeite = await kommandant.GetStringAsync(ueberwachung);
        MultipartFormDataContent Datei() => new()
        {
            { new StringContent(Token(ueSeite)), "__RequestVerificationToken" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes($"Sachkonto;Kostenstelle;Innenauftrag;Ist;Obligo;Stand\r\n7832;;;390.000,00;0;30.06.{jahr}\r\n9999;1;;5,00;0;\r\n")), "Datei", "ist.csv" },
        };
        var haSeite = await hauptamt.GetStringAsync(ueberwachung);
        Assert.DoesNotContain("Ist-Stand aus SAP einspielen", haSeite);
        Assert.Equal(HttpStatusCode.Forbidden, (await hauptamt.PostAsync(ueberwachung, new MultipartFormDataContent
        {
            { new StringContent(Token(haSeite)), "__RequestVerificationToken" },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await kommandant.PostAsync(ueberwachung, Datei())).StatusCode);
        Assert.Equal(2, await MitDbAsync(db => db.HaushaltIst.CountAsync(i => i.Jahr == jahr)));
        var stand = WebUtility.HtmlDecode(await kommandant.GetStringAsync(ueberwachung));
        Assert.Contains("überschritten", stand);
        Assert.Contains("§ 84 GemO", stand);
        Assert.Contains("passen zu keiner Haushaltsstelle", stand);
        await MitDbAsync(db => db.HaushaltIst.Where(i => i.Jahr == jahr).ExecuteDeleteAsync());
        await MitDbAsync(db => db.Haushaltsanmeldungen.Where(a => a.Jahr == jahr).ExecuteDeleteAsync());
    }

    private Task<AnmeldungStatus> StatusAsync(int jahr) => MitDbAsync(db => db.Haushaltsanmeldungen.Where(a => a.Jahr == jahr).Select(a => a.Status).SingleAsync());

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
