using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>AAO mit Soll-Ist im Einsatzbericht, Objektakte und Verzeichnis externer Stellen (V20).</summary>
[Collection(AnwendungCollection.Name)]
public partial class AaoTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, Guid.NewGuid().ToString());
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string? handler, IEnumerable<KeyValuePair<string, string>> felder)
    {
        var html = await client.GetStringAsync(seite);
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)) };
        liste.AddRange(felder);
        return await client.PostAsync(handler is null ? seite : $"{seite}{(seite.Contains('?') ? '&' : '?')}handler={handler}", new FormUrlEncodedContent(liste));
    }

    private static string Zeile(string html, string text)
    {
        var i = html.IndexOf($">{text}</a>", StringComparison.Ordinal);
        var beginn = html.LastIndexOf("<tr", i, StringComparison.Ordinal);
        return html[beginn..html.IndexOf("</tr>", i, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Beschlossene_AAO_ist_gesperrt_und_liefert_Soll_Ist_im_Bericht()
    {
        var stichwort = "X " + Random.Shared.Next(100, 999);
        Guid fahrzeugId, zweitesId, einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var f1 = new Fahrzeug { Funkrufname = "Florian Aao " + stichwort, Typ = "HLF 10" };
            var f2 = new Fahrzeug { Funkrufname = "Florian Bbo " + stichwort, Typ = "TLF 8/18" };
            db.Fahrzeuge.AddRange(f1, f2);
            var e = new Einsatz
            {
                Einsatznummer = $"AAO-{Guid.NewGuid().ToString("N")[..8]}", Stichwort = stichwort.ToLowerInvariant(), Ort = "Adelberg",
                Alarmiert = DateTimeOffset.UtcNow.AddHours(-3),
            };
            e.Fahrzeuge.Add(new EinsatzFahrzeug { FahrzeugId = f1.Id });
            db.Einsaetze.Add(e);
            await db.SaveChangesAsync();
            (fahrzeugId, zweitesId, einsatzId) = (f1.Id, f2.Id, e.Id);
        }

        var kommandant = Client(Rollen.Kommandant);
        var neu = await AbschickenAsync(kommandant, "/Einsaetze/Aao", "Neu", []);
        Assert.Equal(HttpStatusCode.Redirect, neu.StatusCode);
        var seite = neu.Headers.Location!.OriginalString;
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Regel",
            [new("Regel.Stichwort", stichwort), new("Regel.Fahrzeuge", fahrzeugId.ToString()), new("Regel.Fahrzeuge", zweitesId.ToString()), new("Regel.Mindeststaerke", "9")])).StatusCode);
        // Doppeltes Stichwort im selben Stand wird abgelehnt.
        Assert.Equal(HttpStatusCode.OK, (await AbschickenAsync(kommandant, seite, "Regel", [new("Regel.Stichwort", stichwort.Replace(" ", ""))])).StatusCode);

        // Entwurf: noch kein Soll-Ist.
        Assert.DoesNotContain("Soll-Ist", WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Einsaetze/Bericht/{einsatzId}")));

        var gestern = DateTime.Today.AddDays(-1).ToString("dd.MM.yyyy");
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Kopf",
            [new("Kopf.Bezeichnung", "AAO Test"), new("Kopf.GueltigAb", gestern), new("Kopf.BeschlossenAm", gestern)])).StatusCode);
        var bericht = WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Einsaetze/Bericht/{einsatzId}"));
        Assert.Contains("Soll-Ist nach", bericht);
        Assert.Contains("Nicht ausgerückt: Florian Bbo", bericht);
        Assert.Contains("Stärke 0 statt mindestens 9", bericht);

        // Gesperrt: Regeln bleiben, Mitteilung an den Bürgermeister lässt sich noch eintragen.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Regel", [new("Regel.Stichwort", "TH 9")])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Kopf",
            [new("Kopf.Bezeichnung", "geändert"), new("Kopf.BuergermeisterMitgeteiltAm", gestern)])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stand = await db.AaoStaende.Include(s => s.Regeln).SingleAsync(s => s.Regeln.Any(r => r.Stichwort == stichwort));
            Assert.Equal("AAO Test", stand.Bezeichnung);
            Assert.NotNull(stand.BuergermeisterMitgeteiltAm);
            Assert.Single(stand.Regeln);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Fuehrungskraft), "/Einsaetze/Aao", "Neu", [])).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync("/Einsaetze/Aao")).StatusCode);
    }

    [Fact]
    public async Task Externe_Stelle_wird_im_Bericht_uebernommen()
    {
        var kommandant = Client(Rollen.Kommandant);
        var name = "Polizeirevier Test " + Guid.NewGuid().ToString("N")[..6];
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, "/Einsaetze/ExterneStellen", null,
            [new("Formular.Art", nameof(OrganisationArt.Polizei)), new("Formular.Bezeichnung", name), new("Formular.Telefon", "07161 000"), new("Formular.Aktiv", "true")])).StatusCode);
        Guid stelleId, einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            stelleId = (await db.ExterneStellen.SingleAsync(s => s.Bezeichnung == name)).Id;
            var e = new Einsatz { Einsatznummer = $"EXT-{Guid.NewGuid().ToString("N")[..8]}", Stichwort = "TH 1", Ort = "Adelberg", Alarmiert = DateTimeOffset.UtcNow.AddHours(-2) };
            db.Einsaetze.Add(e);
            await db.SaveChangesAsync();
            einsatzId = e.Id;
        }

        var seite = $"/Einsaetze/Bericht/{einsatzId}";
        var html = await kommandant.GetStringAsync(seite);
        Assert.Contains(name, WebUtility.HtmlDecode(html));
        var hinzu = await AbschickenAsync(kommandant, seite + "?was=organisation", "Hinzufuegen", [new("neuStelleId", stelleId.ToString())]);
        var formular = WebUtility.HtmlDecode(await hinzu.Content.ReadAsStringAsync());
        Assert.Contains($"value=\"{name}\"", formular);
        Assert.Contains("value=\"07161 000\"", formular);
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Fuehrungskraft), "/Einsaetze/ExterneStellen", null, [new("Formular.Bezeichnung", "x")])).StatusCode);
    }

    [Fact]
    public async Task Sonderobjekt_mit_Begehung_erscheint_nicht_als_BMA()
    {
        var kommandant = Client(Rollen.Kommandant);
        var name = "Pflegeheim Test " + Guid.NewGuid().ToString("N")[..6];
        var anlegen = await AbschickenAsync(kommandant, $"/Einsaetze/Objekte/{Guid.Empty}", null,
            [new("Formular.Bezeichnung", name), new("Formular.BetreiberName", "Träger gGmbH"), new("Formular.Nutzung", "Pflegeheim"), new("Formular.Aktiv", "true")]);
        Assert.Equal(HttpStatusCode.Redirect, anlegen.StatusCode);
        var seite = anlegen.Headers.Location!.OriginalString;
        Assert.Contains("fällig", Zeile(WebUtility.HtmlDecode(await kommandant.GetStringAsync(seite)), name));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Begehung",
            [new("Begehung.Am", DateTime.Today.ToString("dd.MM.yyyy")), new("Begehung.Ergebnis", "Laufkarten aktuell")])).StatusCode);
        var akte = WebUtility.HtmlDecode(await kommandant.GetStringAsync(seite));
        Assert.Contains("Laufkarten aktuell", akte);
        Assert.DoesNotContain("fällig", Zeile(akte, name));
        Assert.DoesNotContain(name, WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Kostenersatz/Bma")));
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Fuehrungskraft), seite, "Begehung", [new("Begehung.Am", "01.01.2026")])).StatusCode);
    }
}
