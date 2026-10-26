using System.Net;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Bestandsverzeichnis, Jahresabschluss, Ausnahmebuchung und Löschregel L42 (P2: V04, V05).</summary>
public partial class KasseTests
{
    /// <summary>Eigene Kasse, damit Abschlüsse die gemeinsamen Kassen der übrigen Tests nicht sperren.</summary>
    private async Task<(Kasse Kasse, Guid Giro, Guid Einnahme, Guid Ausgabe)> EigeneKasseAsync(DateOnly beginn)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kasse = Kassenfuehrung.Neu("Testkasse " + Guid.NewGuid().ToString("N")[..8], null, 99, beginn, Kassenregeln.MusterKameradschaftskasse);
        db.Kassen.Add(kasse);
        await db.SaveChangesAsync();
        return (kasse, kasse.Konten.Single(k => k.Art == KontoArt.Girokonto).Id, kasse.Positionen.Single(p => p.Nummer == "1.1").Id,
            kasse.Positionen.Single(p => p.Nummer == "6.1").Id);
    }

    private async Task<Buchung> BuchenAsync(Guid kasse, Guid konto, Guid position, BuchungArt art, decimal betrag, DateOnly datum, string text)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var buchung = new Buchung
        {
            KasseId = kasse, Art = art, Betrag = betrag, KontoId = konto, PlanpositionId = position, Datum = datum, Text = text,
            Belegart = Belegart.Eigenbeleg, EigenbelegBegruendung = "Test",
        };
        await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().BuchenAsync(buchung);
        return buchung;
    }

    [Fact]
    public async Task Kassier_nimmt_Ausgabe_ins_Bestandsverzeichnis_auf_und_vermerkt_den_Abgang()
    {
        var (kasse, giro, _, position, _) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var zelt = await BuchenAsync(kasse.Id, giro, position, BuchungArt.Ausgabe, 750m, Heute, "Faltpavillon " + kennung);
        var fest = await BuchenAsync(kasse.Id, giro, position, BuchungArt.Ausgabe, 600m, Heute, "Grillfest " + kennung);
        var kassier = Client(Rollen.Kassier);

        Assert.Contains("/Kasse/Bestand", await kassier.GetStringAsync("/"));
        var seite = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Bestand?kasse={kasse.Id}"));
        Assert.Contains($"buchung={zelt.Id}", seite);
        Assert.Contains($"buchung={fest.Id}", seite);

        // Verbrauch ist kein Gegenstand; der Vorschlag verschwindet.
        var antwort = await AbschickenAsync(kassier, $"/Kasse/Bestand?kasse={kasse.Id}", $"/Kasse/Bestand?kasse={kasse.Id}&buchungId={fest.Id}&handler=KeinGegenstand", new());
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var pfad = $"/Kasse/Gegenstand?kasse={kasse.Id}&buchung={zelt.Id}";
        Assert.Contains("Faltpavillon " + kennung, WebUtility.HtmlDecode(await kassier.GetStringAsync(pfad)));
        antwort = await AbschickenAsync(kassier, pfad, pfad, new()
        {
            ["Formular.Inventarnummer"] = "K-" + kennung, ["Formular.Bezeichnung"] = "Faltpavillon " + kennung, ["Formular.Anschaffung"] = Heute.ToString("dd.MM.yyyy"),
            ["Formular.Wert"] = "750,00", ["Formular.Standort"] = "Gerätehaus, Lager OG",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid gegenstandId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var g = await db.Vermoegensgegenstaende.SingleAsync(v => v.BuchungId == zelt.Id);
            Assert.Equal((750m, "Gerätehaus, Lager OG", kasse.Id), (g.Wert, g.Standort, g.KasseId));
            Assert.True((await db.Buchungen.SingleAsync(b => b.Id == fest.Id)).KeinGegenstand);
            gegenstandId = g.Id;
        }

        seite = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Bestand?kasse={kasse.Id}"));
        Assert.DoesNotContain($"buchung={zelt.Id}", seite);
        Assert.DoesNotContain($"buchung={fest.Id}", seite);
        Assert.Contains("K-" + kennung, seite);

        // Abgang braucht einen Grund.
        pfad = $"/Kasse/Gegenstand?id={gegenstandId}";
        var felder = new Dictionary<string, string>
        {
            ["Formular.Inventarnummer"] = "K-" + kennung, ["Formular.Bezeichnung"] = "Faltpavillon " + kennung, ["Formular.Anschaffung"] = Heute.ToString("dd.MM.yyyy"),
            ["Formular.Wert"] = "750,00", ["Formular.AbgangAm"] = Heute.ToString("dd.MM.yyyy"),
        };
        antwort = await AbschickenAsync(kassier, pfad, pfad, felder);
        Assert.Contains("Grund des Abgangs", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        felder["Formular.AbgangGrund"] = "Sturmschaden";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, pfad, pfad, felder)).StatusCode);

        var csv = await kassier.GetAsync($"/Kasse/Bestand?kasse={kasse.Id}&handler=Csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Sturmschaden", await csv.Content.ReadAsStringAsync());

        // Die Jugendwartin sieht die Kameradschaftskasse nicht und pflegt nichts.
        var jugendwart = Client(Rollen.Jugendwart);
        Assert.DoesNotContain("K-" + kennung, await jugendwart.GetStringAsync("/Kasse/Bestand"));
        Assert.Equal(HttpStatusCode.NotFound, (await jugendwart.GetAsync(pfad)).StatusCode);
    }

    [Fact]
    public async Task Jahresabschluss_sperrt_das_Jahr_und_Ausnahmebuchungen_brauchen_eine_Begruendung()
    {
        var jahr = Heute.Year - 3;
        var (kasse, giro, einnahme, ausgabe) = await EigeneKasseAsync(new DateOnly(jahr, 1, 1));
        await BuchenAsync(kasse.Id, giro, einnahme, BuchungArt.Einnahme, 1000m, new DateOnly(jahr, 3, 1), "Zuwendung");
        var ausgabeBuchung = await BuchenAsync(kasse.Id, giro, ausgabe, BuchungArt.Ausgabe, 200m, new DateOnly(jahr, 6, 1), "Kameradschaftsabend");
        var kassier = Client(Rollen.Kassier);
        var seite = $"/Kasse/Abschluss?kasse={kasse.Id}&Jahr={jahr}";

        // Nicht festgeschrieben: Abschließen geht nicht.
        Assert.Contains("noch nicht festgeschrieben", WebUtility.HtmlDecode(await kassier.GetStringAsync(seite)));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, seite, seite + "&handler=Abschliessen", new())).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Jahresabschluesse.AnyAsync(j => j.KasseId == kasse.Id));
            await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().FestschreibenAsync(kasse.Id, new DateOnly(jahr, 12, 31), null);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, seite, seite + "&handler=Abschliessen", new())).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abschluss = await db.Jahresabschluesse.Include(j => j.Endbestaende).SingleAsync(j => j.KasseId == kasse.Id);
            Assert.Equal((jahr, 1000m, 200m), (abschluss.Jahr, abschluss.Einnahmen, abschluss.Ausgaben));
            Assert.Equal(800m, abschluss.Endbestaende.Single(e => e.KontoId == giro).Bestand);

            // Ein Storno in das abgeschlossene Jahr geht nicht.
            var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<Kassenfuehrung>()
                .StornierenAsync(ausgabeBuchung.Id, new DateOnly(jahr, 12, 1), "Test", null));
            Assert.Contains("abgeschlossen", fehler.Message);
        }

        var pdf = await kassier.GetAsync(seite + "&handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));

        // Buchung in das abgeschlossene Jahr nur mit Begründung.
        var neu = $"/Kasse/Buchung?kasse={kasse.Id}";
        var felder = new Dictionary<string, string>
        {
            ["Formular.Datum"] = new DateOnly(jahr, 12, 20).ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "50,00",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = ausgabe.ToString(), ["Formular.Text"] = "Nachzügler-Rechnung",
            ["Formular.Belegart"] = nameof(Belegart.Eigenbeleg), ["Formular.EigenbelegBegruendung"] = "Quittung verloren",
        };
        var antwort = await AbschickenAsync(kassier, neu, neu, felder);
        Assert.Contains($"Das Jahr {jahr} ist abgeschlossen", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        felder["Formular.AusnahmeBegruendung"] = "Rechnung kam erst nach dem Abschluss";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, neu, neu, felder)).StatusCode);

        var html = WebUtility.HtmlDecode(await kassier.GetStringAsync(seite));
        Assert.Contains("Rechnung kam erst nach dem Abschluss", html);
        Assert.Contains("Abweichung", html);
        Assert.Contains("Ausnahme", WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Buch?kasse={kasse.Id}&jahr={jahr}")));

        // Vorlage an den Bürgermeister vermerken.
        antwort = await AbschickenAsync(kassier, seite, seite + "&handler=Vermerk", new() { ["Vermerk.VorgelegtAm"] = new DateOnly(jahr + 1, 2, 15).ToString("dd.MM.yyyy") });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(new DateOnly(jahr + 1, 2, 15), (await db.Jahresabschluesse.SingleAsync(j => j.KasseId == kasse.Id)).VorgelegtAm);
            Assert.Equal("Rechnung kam erst nach dem Abschluss", (await db.Buchungen.SingleAsync(b => b.KasseId == kasse.Id && b.Betrag == 50m)).AusnahmeBegruendung);
        }

        // Der Jugendwart darf die Kasse ohne Abteilung nicht sehen.
        Assert.Equal(HttpStatusCode.NotFound, (await Client(Rollen.Jugendwart).GetAsync(seite)).StatusCode);
    }

    [Fact]
    public async Task Loeschlauf_L42_loescht_Kassendaten_nach_zehn_Jahren_und_traegt_die_Bestaende_vor()
    {
        var jahr = Heute.Year - 14;
        var (kasse, giro, einnahme, ausgabe) = await EigeneKasseAsync(new DateOnly(jahr, 1, 1));
        var alt = await BuchenAsync(kasse.Id, giro, einnahme, BuchungArt.Einnahme, 500m, new DateOnly(jahr, 3, 1), "Zuwendung");
        await BuchenAsync(kasse.Id, giro, ausgabe, BuchungArt.Ausgabe, 100m, new DateOnly(jahr, 5, 1), "Material");
        var folgejahr = await BuchenAsync(kasse.Id, giro, ausgabe, BuchungArt.Ausgabe, 30m, new DateOnly(jahr + 1, 5, 1), "Getränke");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beleg = Kassenfuehrung.Beleg("a.pdf", "application/pdf", Pdf);
            beleg.BuchungId = alt.Id;
            db.Kassenbelege.Add(beleg);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().FestschreibenAsync(kasse.Id, new DateOnly(jahr, 12, 31), null);
            await scope.ServiceProvider.GetRequiredService<Kassenabschluss>().AbschliessenAsync(kasse, jahr, null, Heute);
            // Abschluss und Buchungen liegen in Wirklichkeit lange zurück.
            var damals = new DateTimeOffset(jahr + 1, 2, 1, 12, 0, 0, TimeSpan.Zero);
            await db.Jahresabschluesse.Where(j => j.KasseId == kasse.Id).ExecuteUpdateAsync(s => s.SetProperty(j => j.AbgeschlossenAm, damals));
            await db.Buchungen.Where(b => b.KasseId == kasse.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.ErstelltAm, damals));
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var abschluss = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Jahresabschluesse
                .Where(j => j.KasseId == kasse.Id && j.Jahr == jahr).Select(j => j.Id).SingleAsync();
            var ergebnisse = await Archivhilfe.AnbietenUndLoeschenAsync(fixture.Factory.Services, DateTimeOffset.UtcNow, Fevad.Domain.Gemeinsam.Archivgut.Kassenjahr, abschluss);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelKassendaten).Anzahl >= 2);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rest = await db.Buchungen.IgnoreQueryFilters().Where(b => b.KasseId == kasse.Id).ToListAsync();
            Assert.Equal(folgejahr.Id, Assert.Single(rest).Id);
            Assert.False(await db.Kassenbelege.IgnoreQueryFilters().AnyAsync(b => b.BuchungId == alt.Id));
            var konto = await db.Zahlungsmittelkonten.SingleAsync(k => k.Id == giro);
            Assert.Equal((400m, new DateOnly(jahr + 1, 1, 1)), (konto.Anfangsbestand, konto.Eroeffnet));
            var stand = await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().KontostaendeAsync(kasse.Id, Heute);
            Assert.Equal(370m, stand.Single(s => s.Konto.Id == giro).Bestand);
            Assert.True(await db.Jahresabschluesse.AnyAsync(j => j.KasseId == kasse.Id && j.Jahr == jahr));
        }
    }
}
