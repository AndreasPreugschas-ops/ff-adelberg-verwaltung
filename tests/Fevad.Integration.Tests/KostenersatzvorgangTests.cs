using System.Net;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kostenersatz-Vorgang: Feuerwehr empfiehlt, Hauptamt entscheidet (P3: V21, V15).</summary>
public partial class EinsatzberichtTests
{
    [Fact]
    public async Task Kostenersatz_Vorgang_Feuerwehr_empfiehlt_Hauptamt_entscheidet()
    {
        Guid einsatzId, beteiligterId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await scope.ServiceProvider.GetRequiredService<Einsatzberichte>()
                .NeuAnlegenAsync(new DateTimeOffset(2049, 4, 2, 8, 0, 0, TimeSpan.Zero));
            einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatz.Id);
            einsatz.EinsatzbereitschaftHergestellt = einsatz.Alarmiert.AddMinutes(100);
            // Das TLF 8/18 hat keinen eigenen Satz und rechnet als TLF 2000 (Andi, F1).
            var tlf = await db.Fahrzeuge.SingleAsync(f => f.Funkrufname == "Florian Adelberg 21");
            db.Add(new EinsatzFahrzeug { EinsatzId = einsatz.Id, FahrzeugId = tlf.Id, Ausgerueckt = einsatz.Alarmiert.AddMinutes(5), Einsatzbereit = einsatz.Alarmiert.AddMinutes(95) });
            var beteiligter = new EinsatzBeteiligter { EinsatzId = einsatz.Id, Rolle = BeteiligtenRolle.Verursacher, Name = "Erika Halterin", Kennzeichen = "GP-EH 77", Versicherung = "Beispiel-Versicherung" };
            db.Add(beteiligter);
            await db.SaveChangesAsync();
            (einsatzId, beteiligterId) = (einsatz.Id, beteiligter.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var pfad = $"/Kostenersatz/Vorgang?einsatzId={einsatzId}";
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var hauptamt = Angemeldet(factory, Rollen.Hauptamt);
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);

        Assert.Equal(HttpStatusCode.Forbidden, (await fuehrung.GetAsync(pfad)).StatusCode);
        async Task<HttpResponseMessage> Post(HttpClient client, string handler, Dictionary<string, string>? felder = null)
        {
            var seite = await client.GetStringAsync(pfad);
            var daten = felder ?? [];
            daten["__RequestVerificationToken"] = Token(seite);
            return await client.PostAsync($"{pfad}&handler={handler}", new FormUrlEncodedContent(daten));
        }

        // Das Hauptamt kann keinen Vorgang anlegen, die Feuerwehr schon.
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(hauptamt, "Anlegen")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(kommandant, "Anlegen")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await Post(kommandant, "Speichern", new()
        {
            ["Angaben.Tatbestand"] = nameof(Kostentatbestand.Abs1Nr2Fahrzeug), ["Angaben.Sachverhalt"] = "Ölspur nach Verkehrsunfall",
            ["Angaben.Gesamtschuld"] = "true", ["Angaben.Verwaltungsgebuehr"] = "true",
        })).StatusCode);

        // Ohne Schuldner lässt sich kein Kostenersatz empfehlen.
        var ohne = WebUtility.HtmlDecode(await (await Post(kommandant, "Empfehlen", new() { ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Kostenpflichtig) })).Content.ReadAsStringAsync());
        Assert.Contains("Mindestens ein Kostenschuldner fehlt.", ohne);

        Assert.Equal(HttpStatusCode.Redirect, (await Post(kommandant, $"Uebernehmen&beteiligterId={beteiligterId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(kommandant, "Zusatz", new()
        {
            ["NeueKosten.Art"] = nameof(ZusatzkostenArt.Verbrauch), ["NeueKosten.Bezeichnung"] = "Bindemittel", ["NeueKosten.Betrag"] = "40",
        })).StatusCode);

        // TLF 2000 1,5 h × 155 = 232,50; Bindemittel 40 + 10 % = 44,00; Gebühr 50 (vorläufig).
        var seite = WebUtility.HtmlDecode(await kommandant.GetStringAsync(pfad));
        Assert.Contains("(TLF 2000)", seite);
        Assert.Contains("232,50 €", seite);
        Assert.Contains("44,00 €", seite);
        Assert.Contains("326,50 €", seite);
        Assert.Contains("Vorläufige Sätze", seite);

        Assert.Equal(HttpStatusCode.Redirect, (await Post(kommandant, "Empfehlen", new()
        {
            ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Kostenpflichtig), ["Ergebnis.Begruendung"] = "Betrieb eines Kraftfahrzeugs",
        })).StatusCode);

        // Nach der Empfehlung ändert die Feuerwehr nichts mehr; die Gemeinde liest nur, das Hauptamt entscheidet.
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(kommandant, "Zusatz", new()
        {
            ["NeueKosten.Art"] = nameof(ZusatzkostenArt.Sonstiges), ["NeueKosten.Bezeichnung"] = "Nachtrag", ["NeueKosten.Betrag"] = "1",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(kommandant, "Entscheiden", new() { ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Kostenpflichtig) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(gemeinde, "Entscheiden", new() { ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Kostenpflichtig) })).StatusCode);
        Assert.Contains("Erika Halterin", await gemeinde.GetStringAsync(pfad));

        // Abweichen nur mit Begründung.
        var ohneGrund = WebUtility.HtmlDecode(await (await Post(hauptamt, "Entscheiden", new()
        {
            ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Verzicht), ["Ergebnis.Grund"] = nameof(Verzichtsgrund.UnbilligeHaerte),
        })).Content.ReadAsStringAsync());
        Assert.Contains("Begründung", ohneGrund);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(hauptamt, "Entscheiden", new()
        {
            ["Ergebnis.Ergebnis"] = nameof(KostenersatzErgebnis.Verzicht), ["Ergebnis.Grund"] = nameof(Verzichtsgrund.UnbilligeHaerte),
            ["Ergebnis.Begruendung"] = "Schuldnerin mittellos",
        })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vorgang = await db.Kostenersatzvorgaenge.Include(v => v.Schuldner).SingleAsync(v => v.EinsatzId == einsatzId);
            Assert.Equal((KostenersatzErgebnis.Kostenpflichtig, KostenersatzErgebnis.Verzicht), (vorgang.Empfehlung!.Value, vorgang.Entscheidung!.Value));
            var schuldner = Assert.Single(vorgang.Schuldner);
            Assert.Equal((SchuldnerArt.Halter, "GP-EH 77"), (schuldner.Art, schuldner.Kennzeichen));
            // Verzicht schließt den Kostenersatz ab; ab hier läuft die Löschfrist der Beteiligten (L3).
            var einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatzId);
            Assert.False(einsatz.KostenersatzOffen);
            Assert.NotNull(einsatz.KostenersatzAbgeschlossenAm);
        }

        Assert.Contains(einsatzId.ToString(), await kommandant.GetStringAsync("/Kostenersatz?jahr=2049"));
    }
}
