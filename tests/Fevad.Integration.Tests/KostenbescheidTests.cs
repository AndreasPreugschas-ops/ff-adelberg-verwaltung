using System.Net;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kostenersatzbescheid: Statuskette, PDF und Forderungsübersicht (P3: V22).</summary>
public partial class EinsatzberichtTests
{
    [Fact]
    public async Task Kostenbescheid_von_der_Anhoerung_bis_zur_Zahlung()
    {
        Guid einsatzId, schuldnerId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await scope.ServiceProvider.GetRequiredService<Einsatzberichte>()
                .NeuAnlegenAsync(new DateTimeOffset(2051, 4, 2, 8, 0, 0, TimeSpan.Zero));
            einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatz.Id);
            einsatz.EinsatzbereitschaftHergestellt = einsatz.Alarmiert.AddMinutes(100);
            einsatz.KostenersatzOffen = true;
            var tlf = await db.Fahrzeuge.SingleAsync(f => f.Funkrufname == "Florian Adelberg 21");
            db.Add(new EinsatzFahrzeug { EinsatzId = einsatz.Id, FahrzeugId = tlf.Id, Ausgerueckt = einsatz.Alarmiert.AddMinutes(5), Einsatzbereit = einsatz.Alarmiert.AddMinutes(95) });
            // Ohne Verwaltungsgebühr enthält die Berechnung keinen Platzhalter-Satz und lässt sich festsetzen.
            var vorgang = new Kostenersatzvorgang
            {
                EinsatzId = einsatz.Id, Tatbestand = Kostentatbestand.Abs1Nr2Fahrzeug, Verwaltungsgebuehr = false,
                Empfehlung = KostenersatzErgebnis.Kostenpflichtig, Entscheidung = KostenersatzErgebnis.Kostenpflichtig, EntschiedenAm = new DateOnly(2051, 4, 20),
            };
            db.Add(vorgang);
            var schuldner = new Kostenschuldner { KostenersatzvorgangId = vorgang.Id, Art = SchuldnerArt.Halter, Name = "Bruno Bescheid", Anschrift = "Hauptstraße 1, 73099 Adelberg", Zustellweg = Zustellweg.Brief };
            db.Add(schuldner);
            await db.SaveChangesAsync();
            (einsatzId, schuldnerId) = (einsatz.Id, schuldner.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        async Task<HttpResponseMessage> Post(HttpClient client, string seitenPfad, string handler, Dictionary<string, string>? felder = null)
        {
            var seite = await client.GetStringAsync(seitenPfad);
            var daten = felder ?? [];
            daten["__RequestVerificationToken"] = Token(seite);
            return await client.PostAsync($"{seitenPfad}&handler={handler}", new FormUrlEncodedContent(daten));
        }

        var vorgangPfad = $"/Kostenersatz/Vorgang?einsatzId={einsatzId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(kommandant, vorgangPfad, $"Bescheid&schuldnerId={schuldnerId}")).StatusCode);
        var angelegt = await Post(hauptamt, vorgangPfad, $"Bescheid&schuldnerId={schuldnerId}");
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var pfad = angelegt.Headers.Location!.OriginalString;
        Assert.Contains("/Kostenersatz/Bescheid", pfad);

        // Die Feuerwehr liest mit, erfassen kann nur das Hauptamt.
        Assert.Contains("Bruno Bescheid", await kommandant.GetStringAsync(pfad));
        Dictionary<string, string> Schritt(BescheidSchritt schritt, string datum, string betrag = "") =>
            new() { ["Schritt.Schritt"] = schritt.ToString(), ["Schritt.Datum"] = datum, ["Schritt.Betrag"] = betrag };
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(kommandant, pfad, "Schritt", Schritt(BescheidSchritt.Anhoerung, "2051-05-01"))).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Anhoerung, "2051-05-01"))).StatusCode);
        var vorBekanntgabe = WebUtility.HtmlDecode(await (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Bekanntgabe, "2051-05-20"))).Content.ReadAsStringAsync());
        Assert.Contains("Bekanntgegeben werden kann nur ein festgesetzter Bescheid.", vorBekanntgabe);

        // Festsetzung ohne Betrag übernimmt die Berechnung: TLF 2000 1,5 h × 155 € = 232,50 €.
        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Festsetzung, "2051-05-20"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Bekanntgabe, "2051-06-01"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Zahlung, "2051-06-20", "100"))).StatusCode);

        var seite = WebUtility.HtmlDecode(await hauptamt.GetStringAsync(pfad));
        Assert.Contains("15.05.2051", seite); // Anhörung 14 Tage
        Assert.Contains("05.06.2051", seite); // Zugangsfiktion 4 Tage beim Brief
        Assert.Contains("05.07.2051", seite); // fällig einen Monat nach Bekanntgabe
        Assert.Contains("31.12.2056", seite); // Verjährung fünf Jahre ab Ende des Fälligkeitsjahres
        Assert.Contains("132,50 €", seite);

        var pdf = await hauptamt.GetAsync($"{pfad}&handler=Pdf&anhoerung=false");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);

        var uebersicht = WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Kostenersatz?jahr=2051"));
        Assert.Contains("Forderungen aus Bescheiden", uebersicht);
        Assert.Contains("232,50 €", uebersicht);

        // Mit der Restzahlung ist die Forderung erledigt und der Kostenersatz des Einsatzes abgeschlossen (Beginn L3).
        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, pfad, "Schritt", Schritt(BescheidSchritt.Zahlung, "2051-07-01", "132,50"))).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bescheid = await db.Kostenbescheide.Include(b => b.Ereignisse).SingleAsync(b => b.KostenschuldnerId == schuldnerId);
            Assert.Equal((BescheidStand.Bezahlt, 232.50m, 0m), (bescheid.Stand, bescheid.Betrag!.Value, bescheid.Offen));
            var einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatzId);
            Assert.False(einsatz.KostenersatzOffen);
            Assert.NotNull(einsatz.KostenersatzAbgeschlossenAm);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await kommandant.GetAsync("/Kostenersatz/Texte")).StatusCode);
        Assert.Contains("Rechtsbehelfsbelehrung", await hauptamt.GetStringAsync("/Kostenersatz/Texte"));
    }
}
