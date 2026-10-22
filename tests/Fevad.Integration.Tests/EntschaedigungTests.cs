using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Jede Prüfung nutzt ein eigenes Jahr, damit sich die Tests nicht gegenseitig Daten liefern.</summary>
[Collection(AnwendungCollection.Name)]
public partial class EntschaedigungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static DateTimeOffset Utc(int jahr, int monat, int tag, int stunde, int minute = 0) =>
        new(jahr, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    private static Mitglied NeuesMitglied(string nachname) => new()
    {
        Personalnummer = "EN-" + Guid.NewGuid().ToString("N")[..8],
        Vorname = "Test",
        Nachname = nachname,
        Geburtsdatum = new(1985, 5, 5),
    };

    /// <summary>Sätze ab Jahresbeginn bis Jahresende, damit Folgejahre wieder die Standardwerte sehen.</summary>
    private static async Task SaetzeSetzenAsync(AppDbContext db, int jahr, params (string Schluessel, string Wert)[] werte)
    {
        // Alle vorhandenen Pauschalen neutralisieren, damit fremde Funktionen in diesem Jahr nichts beitragen.
        var pauschalen = await db.Parameter
            .Where(p => p.Schluessel.StartsWith(Entschaedigungssaetze.PauschalePraefix))
            .Select(p => p.Schluessel).Distinct().ToListAsync();
        var alle = pauschalen.Select(s => (s, "0")).Concat(werte).GroupBy(w => w.Item1).Select(g => g.Last());
        foreach (var (schluessel, wert) in alle)
        {
            db.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = wert, GueltigAb = new(jahr, 1, 1), GueltigBis = new(jahr, 12, 31), Quelle = "Test" });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Abrechnung_rechnet_Einsatz_Sonderdienst_Lehrgang_und_Pauschale_und_zahlt_nichts_doppelt()
    {
        const int jahr = 2037;
        Mitglied anna, ben;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SaetzeSetzenAsync(db, jahr,
                ("Entschaedigung.Einsatz.SatzStunde", "20"),
                ("Entschaedigung.Einsatz.RundungMinuten", "30"),
                ("Entschaedigung.Sonderdienst.SatzStunde", "10"),
                ("Entschaedigung.Sonderdienst.RundungMinuten", "60"),
                ("Entschaedigung.Lehrgang.SatzTag", "50"),
                ("Entschaedigung.Lehrgang.SatzHalberTag", "25"),
                ("Entschaedigung.Lehrgang.HalberTagUnterStunden", "5"),
                ("Entschaedigung.Pauschale.KDT", "365"));

            anna = NeuesMitglied("Abrechnung");
            ben = NeuesMitglied("Bezahlt");
            db.Mitglieder.AddRange(anna, ben);
            var kategorie = await db.StatistikKategorien.SingleAsync(k => k.Code == "BR");
            var kommandant = await db.Funktionen.SingleAsync(f => f.Code == "KDT");

            // 70 Minuten ohne eigene Zeiten → 1,5 h; Ben 25 Minuten → 0,5 h.
            var einsatz = new Einsatz
            {
                Einsatznummer = $"T{jahr}-{Guid.NewGuid().ToString("N")[..6]}",
                Stichwort = "B 2",
                Alarmiert = Utc(jahr, 3, 10, 10),
                Einsatzende = Utc(jahr, 3, 10, 11, 10),
                StatistikKategorieId = kategorie.Id,
                Status = EinsatzStatus.Freigegeben,
            };
            einsatz.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = anna.Id });
            einsatz.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = ben.Id, Von = Utc(jahr, 3, 10, 10, 15), Bis = Utc(jahr, 3, 10, 10, 40) });
            var offen = new Einsatz { Einsatznummer = $"T{jahr}-{Guid.NewGuid().ToString("N")[..6]}", Alarmiert = Utc(jahr, 4, 1, 8), Status = EinsatzStatus.InBearbeitung };
            offen.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = anna.Id });

            // Sicherheitswache 5:20 h → 6 h; Ben entschuldigt.
            var wache = new Dienst { Art = DienstArt.Sonderdienst, Titel = "Sicherheitswache Sonnwendfeuer", Beginn = Utc(jahr, 6, 21, 18), Ende = Utc(jahr, 6, 21, 23, 20), Status = DienstStatus.Durchgefuehrt };
            wache.Anwesenheiten.Add(new Anwesenheit { MitgliedId = anna.Id, Status = AnwesenheitStatus.Anwesend });
            wache.Anwesenheiten.Add(new Anwesenheit { MitgliedId = ben.Id, Status = AnwesenheitStatus.Entschuldigt });
            var uebung = new Dienst { Art = DienstArt.Uebung, Titel = "Übung", Beginn = Utc(jahr, 6, 24, 17, 30), Ende = Utc(jahr, 6, 24, 19, 30), Status = DienstStatus.Durchgefuehrt };
            uebung.Anwesenheiten.Add(new Anwesenheit { MitgliedId = anna.Id, Status = AnwesenheitStatus.Anwesend });
            // Lehrgang über zwei Kalendertage → 2 × 50 €.
            var lehrgang = new Dienst { Art = DienstArt.Lehrgang, Titel = "Maschinist", Beginn = Utc(jahr, 5, 3, 6), Ende = Utc(jahr, 5, 4, 15), Status = DienstStatus.Durchgefuehrt };
            lehrgang.Anwesenheiten.Add(new Anwesenheit { MitgliedId = anna.Id, Status = AnwesenheitStatus.Anwesend });
            db.Einsaetze.AddRange(einsatz, offen);
            db.Dienste.AddRange(wache, uebung, lehrgang);

            // Februar 2037: 28 Tage zu 1 €.
            db.FunktionZuordnungen.Add(new FunktionZuordnung { MitgliedId = anna.Id, FunktionId = kommandant.Id, GueltigAb = new(jahr, 2, 1), GueltigBis = new(jahr, 2, 28) });
            await db.SaveChangesAsync();
        }

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var abrechnungen = scope.ServiceProvider.GetRequiredService<Abrechnungen>();
            var abrechnung = await abrechnungen.AnlegenAsync($"Jahr {jahr}", new(jahr, 1, 1), new(jahr, 12, 31));
            id = abrechnung.Id;

            var annas = abrechnung.Posten.Where(p => p.MitgliedId == anna.Id).OrderBy(p => p.Datum).ToList();
            Assert.Equal(
                [(PostenArt.Funktionspauschale, 28m, 28m), (PostenArt.Einsatz, 1.5m, 30m), (PostenArt.Dienst, 2m, 100m), (PostenArt.Dienst, 6m, 60m)],
                annas.Select(p => (p.Art, p.Menge, p.Betrag)));
            Assert.Equal(10m, abrechnung.Posten.Where(p => p.MitgliedId == ben.Id).Sum(p => p.Betrag));
            Assert.Contains("noch nicht freigegeben", abrechnung.Hinweise);
            Assert.DoesNotContain("Platzhalter", abrechnung.Hinweise ?? "");

            await abrechnungen.AbschliessenAsync(id, Guid.CreateVersion7());
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var abrechnungen = scope.ServiceProvider.GetRequiredService<Abrechnungen>();
            var nachtrag = await abrechnungen.BerechnenAsync(new(jahr, 1, 1), new(jahr, 12, 31));
            Assert.DoesNotContain(nachtrag.Posten, p => p.MitgliedId == anna.Id || p.MitgliedId == ben.Id);

            var daten = await scope.ServiceProvider.GetRequiredService<AbrechnungExport>().LadenAsync(id);
            var csv = Encoding.UTF8.GetString(AbrechnungExport.SummenCsv(daten));
            Assert.StartsWith("﻿Personalnummer;Nachname;Vorname;Stunden;Betrag", csv);
            Assert.Contains($"{anna.Personalnummer};Abrechnung;Test;7,50;218,00", csv);
            Assert.Contains($"{ben.Personalnummer};Bezahlt;Test;0,50;10,00", csv);
        }
    }

    [Fact]
    public async Task Abschliessen_mit_Beispielsaetzen_oder_veraltetem_Entwurf_wird_verweigert()
    {
        const int jahr = 2038;
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = NeuesMitglied("Platzhalter");
            db.Mitglieder.Add(mitglied);
            var einsatz = new Einsatz
            {
                Einsatznummer = $"T{jahr}-{Guid.NewGuid().ToString("N")[..6]}",
                Alarmiert = Utc(jahr, 2, 1, 12),
                Einsatzende = Utc(jahr, 2, 1, 13),
                Status = EinsatzStatus.Freigegeben,
            };
            einsatz.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = mitglied.Id });
            db.Einsaetze.Add(einsatz);
            await db.SaveChangesAsync();

            var abrechnungen = scope.ServiceProvider.GetRequiredService<Abrechnungen>();
            var abrechnung = await abrechnungen.AnlegenAsync("Beispiel", new(jahr, 1, 1), new(jahr, 12, 31));
            id = abrechnung.Id;
            Assert.Contains("Platzhalter", abrechnung.Hinweise);

            var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => abrechnungen.AbschliessenAsync(id, Guid.CreateVersion7()));
            Assert.Contains("Beispielsätze", fehler.Message);

            // Mit echten Sätzen ist der Entwurf veraltet, bis er neu berechnet wird.
            await SaetzeSetzenAsync(db, jahr, ("Entschaedigung.Einsatz.SatzStunde", "18"), ("Entschaedigung.Einsatz.RundungMinuten", "60"));
            fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => abrechnungen.AbschliessenAsync(id, Guid.CreateVersion7()));
            Assert.Contains("neu berechnen", fehler.Message);

            await abrechnungen.NeuBerechnenAsync(id);
            await abrechnungen.AbschliessenAsync(id, Guid.CreateVersion7());
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var abrechnung = await db.Abrechnungen.Include(a => a.Posten).SingleAsync(a => a.Id == id);
            Assert.Equal(AbrechnungStatus.Abgeschlossen, abrechnung.Status);
            Assert.Contains(abrechnung.Posten, p => p.Betrag == 18m);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<Abrechnungen>().LoeschenAsync(id));
        }
    }

    [Fact]
    public async Task Jahresstatistik_zaehlt_freigegebene_Einsaetze_nach_Kategorie()
    {
        const int jahr = 2039;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var th = await db.StatistikKategorien.SingleAsync(k => k.Code == "TH");
            var mitglied = NeuesMitglied("Statistik");
            db.Mitglieder.Add(mitglied);
            var einsatz = new Einsatz
            {
                Einsatznummer = $"T{jahr}-{Guid.NewGuid().ToString("N")[..6]}",
                Alarmiert = Utc(jahr, 7, 6, 8),           // Mittwoch 10:00 Ortszeit
                Ausgerueckt = Utc(jahr, 7, 6, 8, 6),
                Einsatzende = Utc(jahr, 7, 6, 10),
                StatistikKategorieId = th.Id,
                Gerettete = 2,
                Status = EinsatzStatus.Freigegeben,
            };
            einsatz.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = mitglied.Id });
            var entwurf = new Einsatz { Einsatznummer = $"T{jahr}-{Guid.NewGuid().ToString("N")[..6]}", Alarmiert = Utc(jahr, 8, 1, 8) };
            db.Einsaetze.AddRange(einsatz, entwurf);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var bericht = await scope.ServiceProvider.GetRequiredService<Jahresstatistik>().ErstellenAsync(jahr);

            Assert.Equal(2, bericht.Einsaetze.Gesamt);
            Assert.Equal(1, bericht.Einsaetze.Freigegeben);
            Assert.Equal(1, bericht.Einsaetze.NachKategorie.Single(k => k.Code == "TH").Anzahl);
            Assert.Equal(5, bericht.Einsaetze.NachKategorie.Count);
            Assert.Equal(2m, bericht.Einsaetze.Personenstunden);
            Assert.Equal(2, bericht.Einsaetze.Gerettete);
            Assert.Equal(6.0, bericht.Einsaetze.AusrueckzeitMedianMinuten);
            Assert.Equal(1, bericht.Einsaetze.WerktagsTagsueber);
            Assert.Equal(1, bericht.Einsaetze.NachMonat[6]);
            Assert.Contains(bericht.Hinweise, h => h.Contains("nicht freigegeben"));

            var pdf = JahresberichtPdf.Erzeugen(bericht, "Freiwillige Feuerwehr Adelberg", DateTimeOffset.UtcNow);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
            Assert.Contains("Technische Hilfe;1", Encoding.UTF8.GetString(JahresmeldungCsv.Erzeugen(bericht)));
        }
    }

    private WebApplicationFactory<Program> Factory => TestAnmeldung.Factory(fixture);

    private HttpClient Client(params string[] rollen)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    [Theory]
    [InlineData("/Auswertungen", Rollen.Kassier, HttpStatusCode.OK)]
    [InlineData("/Auswertungen", Rollen.Geraetewart, HttpStatusCode.Forbidden)]
    [InlineData("/Auswertungen/Entschaedigung", Rollen.GemeindeLesen, HttpStatusCode.OK)]
    [InlineData("/Auswertungen/Entschaedigung", Rollen.Fuehrungskraft, HttpStatusCode.Forbidden)]
    [InlineData("/Auswertungen/Entschaedigung/Saetze", Rollen.Kassier, HttpStatusCode.OK)]
    [InlineData("/Auswertungen/Jahresstatistik?jahr=2039", Rollen.Schriftfuehrer, HttpStatusCode.OK)]
    [InlineData("/Auswertungen/Jahresstatistik?jahr=2039", Rollen.Kassier, HttpStatusCode.Forbidden)]
    public async Task Seiten_pruefen_Berechtigungen(string pfad, string rolle, HttpStatusCode erwartet)
    {
        var antwort = await Client(rolle).GetAsync(pfad);
        Assert.Equal(erwartet, antwort.StatusCode);
    }

    [Fact]
    public async Task Jahresbericht_als_PDF_und_Abrechnung_als_Excel_herunterladen()
    {
        var client = Client(Rollen.Kommandant);

        var pdf = await client.GetAsync("/Auswertungen/Jahresstatistik?handler=Pdf&jahr=2039");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<Abrechnungen>().AnlegenAsync("Excel", new(2040, 1, 1), new(2040, 12, 31))).Id;
        }

        var excel = await client.GetAsync($"/Auswertungen/Entschaedigung/Abrechnung/{id}?handler=Export&format=excel");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        var bytes = await excel.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Auswertungen/Entschaedigung/Abrechnung/{id}")).StatusCode);
    }

    [Fact]
    public async Task Satz_aendern_legt_neue_Version_an_und_begrenzt_die_alte()
    {
        const string schluessel = "Entschaedigung.Veranstaltung.SatzStunde";
        var kommandant = Client(Rollen.Kommandant);

        async Task<HttpResponseMessage> SpeichernAsync(HttpClient client, string wert, string ab)
        {
            var seite = await client.GetStringAsync("/Auswertungen/Entschaedigung/Saetze");
            var token = TokenFeld().Match(seite).Groups[1].Value;
            return await client.PostAsync("/Auswertungen/Entschaedigung/Saetze", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Neu.Schluessel"] = schluessel,
                ["Neu.Wert"] = wert,
                ["Neu.GueltigAb"] = ab,
                ["Neu.Quelle"] = "Test",
            }));
        }

        Assert.Equal(HttpStatusCode.Redirect, (await SpeichernAsync(kommandant, "8", "2041-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SpeichernAsync(kommandant, "9,50", "2042-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SpeichernAsync(kommandant, "8,75", "2041-07-01")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SpeichernAsync(kommandant, "acht", "2043-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SpeichernAsync(Client(Rollen.Kassier), "1", "2043-01-01")).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versionen = await db.Parameter.Where(p => p.Schluessel == schluessel).OrderBy(p => p.GueltigAb)
            .Select(p => new { p.Wert, p.GueltigAb, p.GueltigBis }).ToListAsync();
        Assert.Equal(
            [("8", new DateOnly(2041, 1, 1), (DateOnly?)new DateOnly(2041, 6, 30)), ("8.75", new DateOnly(2041, 7, 1), new DateOnly(2041, 12, 31)), ("9.50", new DateOnly(2042, 1, 1), null)],
            versionen.Select(v => (v.Wert, v.GueltigAb, v.GueltigBis)));
    }
}
