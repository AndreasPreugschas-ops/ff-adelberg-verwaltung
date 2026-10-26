using System.Net;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Nacharbeit P6: Lieferantenpreise je Artikel und der Nachbedarf der Kleiderkammer in den Haushaltsvorschlägen.</summary>
public partial class HaushaltTests
{
    [Fact]
    public async Task Nachbedarf_der_Kleiderkammer_kommt_mit_dem_Lieferantenpreis_in_die_Vorschlaege()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var jahr = Random.Shared.Next(2040, 2100);
        var artikelId = await MitDbAsync(async db =>
        {
            await db.Haushaltsanmeldungen.Where(a => a.Jahr == jahr).ExecuteDeleteAsync();
            var artikel = new Artikel { Code = "K" + kennung, Bezeichnung = "Wollmütze " + kennung, Groessenart = Groessenart.Einheitsgroesse, Kategorie = ArtikelKategorie.Dienstkleidung };
            artikel.Bestaende.Add(new Lagerbestand { Groesse = "", Menge = 1, Mindestmenge = 4 });
            db.Artikel.Add(artikel);
            await db.SaveChangesAsync();
            return artikel.Id;
        });

        var factory = TestAnmeldung.Factory(fixture);
        var kleiderwart = Angemeldet(factory, Rollen.Kleiderwart);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);

        // Lieferanten anlegen; der Gerätewart liest nur.
        var seite = await kleiderwart.GetStringAsync("/Kleiderkammer/Lieferanten");
        foreach (var name in new[] { "Teuer " + kennung, "Billig " + kennung })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kleiderwart, "/Kleiderkammer/Lieferanten",
                new() { ["__RequestVerificationToken"] = Token(seite), ["Formular.Name"] = name, ["Formular.Aktiv"] = "true" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(geraetewart, "/Kleiderkammer/Lieferanten",
            new() { ["__RequestVerificationToken"] = Token(await geraetewart.GetStringAsync("/Kleiderkammer/Lieferanten")), ["Formular.Name"] = "X" + kennung })).StatusCode);
        var teuer = await MitDbAsync(db => db.Lieferanten.Where(l => l.Name == "Teuer " + kennung).Select(l => l.Id).SingleAsync());
        var billig = await MitDbAsync(db => db.Lieferanten.Where(l => l.Name == "Billig " + kennung).Select(l => l.Id).SingleAsync());

        // Preise am Artikel: ohne Bevorzugung gilt der günstigste, mit Bevorzugung der bevorzugte.
        var artikelSeite = $"/Kleiderkammer/ArtikelBearbeiten/{artikelId}";
        Task<HttpResponseMessage> Preis(Guid lieferant, string preis, bool bevorzugt = false) => PostAsync(kleiderwart, artikelSeite + "?handler=Preis", new()
        {
            ["__RequestVerificationToken"] = Token(seite), ["lieferantId"] = lieferant.ToString(), ["preis"] = preis, ["bestellnummer"] = "M-1",
            ["standAm"] = "15.01.2026", ["bevorzugt"] = bevorzugt ? "true" : "false",
        });
        seite = await kleiderwart.GetStringAsync(artikelSeite);
        Assert.Equal(HttpStatusCode.Redirect, (await Preis(teuer, "19,90")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Preis(billig, "12,50")).StatusCode);
        var html = WebUtility.HtmlDecode(await kleiderwart.GetStringAsync(artikelSeite));
        Assert.Contains("19,90 €", html);
        Assert.Contains("12,50 €", html);
        Assert.Contains("37,50 €", WebUtility.HtmlDecode(await kleiderwart.GetStringAsync("/Kleiderkammer/Bedarf")));

        // Vorschläge: drei Stück auf EH-4271 zum Preis des günstigsten Lieferanten.
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var liste = await kommandant.GetStringAsync("/Haushalt");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, "/Haushalt", new() { ["__RequestVerificationToken"] = Token(liste), ["jahr"] = jahr.ToString() })).StatusCode);
        var pfad = $"/Haushalt/Anmeldung/{jahr}";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kommandant, pfad + "?handler=Vorschlaege",
            new() { ["__RequestVerificationToken"] = Token(await kommandant.GetStringAsync(pfad)) })).StatusCode);
        var position = await MitDbAsync(db => db.Haushaltspositionen.Include(p => p.Haushaltsstelle)
            .SingleAsync(p => p.Haushaltsanmeldung!.Jahr == jahr && p.Herkunft == $"kleiderkammer:{artikelId}"));
        Assert.Equal(("EH-4271", Haushaltsbereich.Kleiderkammer, 3m, 12.50m, Preisquelle.Lieferant),
            (position.Haushaltsstelle!.Code, position.Bereich, position.Menge, position.Einzelpreis, position.Preisquelle));
        Assert.Contains("Billig " + kennung, position.Begruendung);

        // Bevorzugter Lieferant schlägt den günstigsten; nur einer ist bevorzugt.
        Assert.Equal(HttpStatusCode.Redirect, (await Preis(teuer, "19,90", bevorzugt: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Preis(billig, "12,50", bevorzugt: true)).StatusCode);
        Assert.Equal([billig], await MitDbAsync(db => db.ArtikelPreise.Where(p => p.ArtikelId == artikelId && p.Bevorzugt).Select(p => p.LieferantId).ToListAsync()));

        await MitDbAsync(db => db.Artikel.Where(a => a.Id == artikelId).ExecuteUpdateAsync(a => a.SetProperty(x => x.Aktiv, false)));
        await MitDbAsync(db => db.Haushaltsanmeldungen.Where(a => a.Jahr == jahr).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task L69_leert_Kontaktangaben_deaktivierter_Lieferanten_nach_der_Frist()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var heute = DateOnly.FromDateTime(DateTime.Today);
        Lieferant Neu(string name, bool aktiv, DateOnly? deaktiviert) => new()
        {
            Name = name + kennung, Kundennummer = "K1", Telefon = "0711", EMail = "a@example.org", Bemerkung = "Inhaber " + kennung, Aktiv = aktiv, DeaktiviertAm = deaktiviert,
        };
        var alt = Neu("Alt", false, heute.AddYears(-3));
        var frisch = Neu("Frisch", false, heute.AddMonths(-6));
        var aktiv = Neu("Aktiv", true, null);
        await MitDbAsync(db => { db.Lieferanten.AddRange(alt, frisch, aktiv); return db.SaveChangesAsync(); });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelLieferanten).Anzahl >= 1);
        }

        var stand = await MitDbAsync(db => db.Lieferanten.Where(l => l.Name.EndsWith(kennung)).ToDictionaryAsync(l => l.Id));
        Assert.Equal(("Alt" + kennung, "K1", null, null, null), (stand[alt.Id].Name, stand[alt.Id].Kundennummer, stand[alt.Id].Telefon, stand[alt.Id].EMail, stand[alt.Id].Bemerkung));
        Assert.Equal("0711", stand[frisch.Id].Telefon);
        Assert.Equal("0711", stand[aktiv.Id].Telefon);

        // Deaktivieren über die Seite setzt den Tag, ab dem die Frist läuft.
        var kleiderwart = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kleiderwart);
        var seite = await kleiderwart.GetStringAsync($"/Kleiderkammer/Lieferanten?id={aktiv.Id}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kleiderwart, "/Kleiderkammer/Lieferanten", new()
        {
            ["__RequestVerificationToken"] = Token(seite), ["Formular.Id"] = aktiv.Id.ToString(), ["Formular.Name"] = aktiv.Name, ["Formular.Aktiv"] = "false",
        })).StatusCode);
        Assert.Equal(heute, await MitDbAsync(db => db.Lieferanten.Where(l => l.Id == aktiv.Id).Select(l => l.DeaktiviertAm).SingleAsync()));
    }
}
