using System.Net;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Brandmeldeanlagen mit Serienvorgängen und Kalkulation des Personalstundensatzes (P3: V24).</summary>
public partial class EinsatzberichtTests
{
    [Fact]
    public async Task Bma_Fehlalarme_werden_gezaehlt_und_als_Serie_angelegt()
    {
        Guid objektId;
        var einsaetze = new List<Guid>();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var objekt = new BmaObjekt { Bezeichnung = "Seniorenzentrum 2059", Aufschaltnummer = "BMA-2059", BetreiberName = "Pflege gGmbH 2059", BetreiberAnschrift = "Hauptstraße 1, 73099 Adelberg" };
            db.Add(objekt);
            await db.SaveChangesAsync();
            objektId = objekt.Id;
            foreach (var monat in new[] { 2, 6 })
            {
                var e = await scope.ServiceProvider.GetRequiredService<Einsatzberichte>().NeuAnlegenAsync(new DateTimeOffset(2059, monat, 3, 9, 0, 0, TimeSpan.Zero));
                einsaetze.Add(e.Id);
            }

            await db.Einsaetze.Where(e => einsaetze.Contains(e.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.BmaObjektId, objektId).SetProperty(e => e.BmaFehlalarm, true));
        }

        var factory = TestAnmeldung.Factory(fixture);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var seite = WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Kostenersatz/Bma?jahr=2059"));
        Assert.Contains("Seniorenzentrum 2059", seite);
        Assert.Contains("Vorgänge anlegen (2)", seite);

        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        var gemeindeSeite = await gemeinde.GetStringAsync("/Kostenersatz/Bma?jahr=2059");
        Assert.DoesNotContain("Vorgänge anlegen (", WebUtility.HtmlDecode(gemeindeSeite));
        Assert.Equal(HttpStatusCode.Forbidden, (await gemeinde.PostAsync("/Kostenersatz/Bma?handler=Serie&jahr=2059",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(gemeindeSeite) }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await kommandant.PostAsync("/Kostenersatz/Bma?handler=Serie&jahr=2059",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(seite) }))).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vorgaenge = await db.Kostenersatzvorgaenge.Include(v => v.Schuldner).Where(v => einsaetze.Contains(v.EinsatzId)).ToListAsync();
            Assert.Equal(2, vorgaenge.Count);
            Assert.All(vorgaenge, v =>
            {
                Assert.Equal(Kostentatbestand.Abs1Nr6Brandmeldeanlage, v.Tatbestand);
                Assert.Equal(SchuldnerArt.Betreiber, Assert.Single(v.Schuldner).Art);
                Assert.Empty(v.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null));
            });
            Assert.Contains(vorgaenge, v => v.Sachverhalt!.Contains("2. Fehlalarm"));
            Assert.All(await db.Einsaetze.Where(e => einsaetze.Contains(e.Id)).ToListAsync(), e => Assert.True(e.KostenersatzOffen));
        }

        seite = WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Kostenersatz/Bma?jahr=2059"));
        Assert.DoesNotContain("Vorgänge anlegen (", seite);
    }

    [Fact]
    public async Task Stundensatz_wird_kalkuliert_und_als_Pdf_ausgegeben()
    {
        var factory = TestAnmeldung.Factory(fixture);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Kostenersatz/Kalkulation")).StatusCode);
        var kaemmerei = Angemeldet(factory, Rollen.Kaemmerei);
        var seite = await kaemmerei.GetStringAsync("/Kostenersatz/Kalkulation");
        Assert.Contains("value=\"80", seite);

        var antwort = await kaemmerei.PostAsync("/Kostenersatz/Kalkulation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite), ["Formular.ErstesJahr"] = "2061", ["Formular.Kosten1"] = "40000", ["Formular.Kosten2"] = "44000",
            ["Formular.Kosten3"] = "48000", ["Formular.Kosten4"] = "52000", ["Formular.Angehoerige"] = "46", ["Formular.StundenJeAngehoerigem"] = "80",
            ["Formular.Entschaedigungssatz"] = "9", ["Formular.Erlaeuterung"] = "Test 2061",
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var k = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Satzkalkulationen.SingleAsync(x => x.ErstesJahr == 2061);
            // 46.000 € / (46 × 80 h) = 12,50 € + 9 € Entschädigung.
            Assert.Equal((46000m, 12.50m, 21.50m), (k.Mittelwert, k.KostenJeStunde, k.Stundensatz));
            id = k.Id;
        }

        var pdf = await kaemmerei.GetAsync($"/Kostenersatz/Kalkulation?handler=Pdf&id={id}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task L47_loescht_abgeschaltete_Bma_Objekte_erst_nach_Abschluss_des_Kostenersatzes()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var heute = DateOnly.FromDateTime(DateTime.UtcNow);
        var alt = new BmaObjekt { Bezeichnung = "L47 alt", BetreiberName = "Betreiber alt", Aktiv = false, AbgeschaltetAm = heute.AddMonths(-24) };
        var offen = new BmaObjekt { Bezeichnung = "L47 offen", BetreiberName = "Betreiber offen", Aktiv = false, AbgeschaltetAm = heute.AddMonths(-24) };
        var neu = new BmaObjekt { Bezeichnung = "L47 neu", BetreiberName = "Betreiber neu", Aktiv = false, AbgeschaltetAm = heute.AddMonths(-2) };
        db.AddRange(alt, offen, neu);
        await db.SaveChangesAsync();
        var berichte = scope.ServiceProvider.GetRequiredService<Einsatzberichte>();
        var e1 = await berichte.NeuAnlegenAsync(new DateTimeOffset(2063, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var e2 = await berichte.NeuAnlegenAsync(new DateTimeOffset(2063, 3, 2, 9, 0, 0, TimeSpan.Zero));
        await db.Einsaetze.Where(e => e.Id == e1.Id).ExecuteUpdateAsync(u => u.SetProperty(e => e.BmaObjektId, alt.Id).SetProperty(e => e.BmaFehlalarm, true));
        await db.Einsaetze.Where(e => e.Id == e2.Id).ExecuteUpdateAsync(u => u.SetProperty(e => e.BmaObjektId, offen.Id).SetProperty(e => e.KostenersatzOffen, true));

        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);

        Assert.True(ergebnisse.Single(r => r.Regel == Loeschlauf.RegelBmaObjekte).Anzahl >= 1);
        var uebrig = await db.BmaObjekte.AsNoTracking().IgnoreQueryFilters().Where(o => o.Bezeichnung.StartsWith("L47")).Select(o => o.Bezeichnung).ToListAsync();
        Assert.Equal(["L47 neu", "L47 offen"], uebrig.Order());
        Assert.Null((await db.Einsaetze.AsNoTracking().SingleAsync(e => e.Id == e1.Id)).BmaObjektId);
        Assert.Equal(offen.Id, (await db.Einsaetze.AsNoTracking().SingleAsync(e => e.Id == e2.Id)).BmaObjektId);
    }
}
