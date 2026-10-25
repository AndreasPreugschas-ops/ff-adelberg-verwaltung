using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kassen als Sondervermögen (P2: V01, V02, V06): Kassenbuch, Belege, Festschreiben, Storno, Plan, Rechte je Kasse.</summary>
[Collection(AnwendungCollection.Name)]
public partial class KasseTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(params string[] rollen)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string ziel, Dictionary<string, string> felder,
        params (string Name, byte[] Inhalt)[] dateien)
    {
        var html = await client.GetStringAsync(seite);
        var inhalt = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)), "__RequestVerificationToken" },
        };
        foreach (var (name, wert) in felder)
        {
            inhalt.Add(new StringContent(wert), name);
        }

        foreach (var (name, bytes) in dateien)
        {
            inhalt.Add(new ByteArrayContent(bytes), "Dateien", name);
        }

        return await client.PostAsync(ziel, inhalt);
    }

    private async Task<(Kasse Kasse, Guid Giro, Guid Bar, Guid Position61, Guid Position11)> KasseAsync(string name)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kasse = await db.Kassen.Include(k => k.Konten).Include(k => k.Positionen).SingleAsync(k => k.Name == name);
        return (kasse, kasse.Konten.Single(k => k.Art == KontoArt.Girokonto).Id, kasse.Konten.Single(k => k.Art == KontoArt.Barkasse).Id,
            kasse.Positionen.Single(p => p.Nummer == "6.1").Id, kasse.Positionen.Single(p => p.Nummer == "1.1").Id);
    }

    [Fact]
    public async Task Beim_Start_gibt_es_Kameradschaftskasse_und_Jugendkasse_mit_LFV_Gliederung()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kassen = await db.Kassen.Include(k => k.Abteilung).Include(k => k.Positionen).ToListAsync();

        Assert.Contains(kassen, k => k.Name == Kassenfuehrung.NameKameradschaftskasse && k.AbteilungId == null);
        var jugend = Assert.Single(kassen, k => k.Name == Kassenfuehrung.NameJugendkasse);
        Assert.Equal("JF", jugend.Abteilung!.Code);
        Assert.All(kassen, k => Assert.Equal(KassenRechtsform.SondervermoegenFwG, k.Rechtsform));
        Assert.Contains(jugend.Positionen, p => p.Nummer == "1.2" && p.Bezeichnung.Contains("Kreisjugendring"));
    }

    [Fact]
    public async Task Kassier_bucht_mit_Beleg_schreibt_fest_und_storniert()
    {
        var (kasse, giro, _, position, _) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var kassier = Client(Rollen.Kassier);
        var text = "Jahresausflug " + Guid.NewGuid().ToString("N")[..6];

        var antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung?kasse={kasse.Id}", $"/Kasse/Buchung?kasse={kasse.Id}", new()
        {
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "250,50",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = position.ToString(), ["Formular.Partner"] = "Busunternehmen Muster",
            ["Formular.Text"] = text, ["Formular.Belegart"] = nameof(Belegart.Rechnung),
        }, ("Rechnung.pdf", Pdf));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Buchung buchung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            buchung = await db.Buchungen.Include(b => b.Belege).SingleAsync(b => b.Text == text);
        }

        Assert.Equal(250.50m, buchung.Betrag);
        Assert.Equal(Heute.Year, buchung.Jahr);
        var beleg = Assert.Single(buchung.Belege);
        Assert.Equal(64, beleg.Pruefsumme.Length);

        // Über 100 € weist die Seite auf den Ausschussbeschluss hin, gesperrt wird nichts.
        var buch = await kassier.GetStringAsync($"/Kasse/Buch?kasse={kasse.Id}");
        Assert.Contains(buchung.Kennung, buch);
        Assert.Contains("Beschluss?", buch);

        antwort = await AbschickenAsync(kassier, $"/Kasse/Buch?kasse={kasse.Id}", $"/Kasse/Buch?kasse={kasse.Id}&handler=Festschreiben",
            new() { ["FestschreibenBis"] = Heute.ToString("dd.MM.yyyy") });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        // Festgeschrieben: Ändern ist verboten, nur Storno.
        antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung/{buchung.Id}", $"/Kasse/Buchung/{buchung.Id}", new()
        {
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "1",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = position.ToString(), ["Formular.Text"] = "geändert",
        });
        Assert.Equal(HttpStatusCode.Forbidden, antwort.StatusCode);

        antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung/{buchung.Id}", $"/Kasse/Buchung/{buchung.Id}?handler=Storno",
            new() { ["StornoGrund"] = "falscher Betrag" });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var original = await db.Buchungen.SingleAsync(b => b.Id == buchung.Id);
            Assert.NotNull(original.FestgeschriebenAm);
            Assert.Equal(text, original.Text);
            var storno = await db.Buchungen.SingleAsync(b => b.StornoVonId == buchung.Id);
            Assert.True(storno.Belegnummer > buchung.Belegnummer || storno.Jahr > buchung.Jahr);
            Assert.NotNull(storno.FestgeschriebenAm);
            var ist = Kassenregeln.IstJePosition(await db.Buchungen.Where(b => b.Id == buchung.Id || b.Id == storno.Id).ToListAsync());
            Assert.Equal(0m, ist[position]);
        }
    }

    [Fact]
    public async Task Jugendwart_sieht_nur_die_Jugendkasse_und_bereitet_Ausgaben_vor_die_der_Kassier_bucht()
    {
        var haupt = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var (jugend, giro, _, _, _) = await KasseAsync(Kassenfuehrung.NameJugendkasse);
        Guid zeltlager;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            zeltlager = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Planpositionen
                .Where(p => p.KasseId == jugend.Id && p.Nummer == "5").Select(p => p.Id).SingleAsync();
        }

        var jugendwart = Client(Rollen.Jugendwart);
        var uebersicht = await jugendwart.GetStringAsync("/Kasse");
        Assert.Contains(Kassenfuehrung.NameJugendkasse, uebersicht);
        Assert.DoesNotContain($"/Kasse/Buch?kasse={haupt.Kasse.Id}", uebersicht);
        Assert.Equal(HttpStatusCode.NotFound, (await jugendwart.GetAsync($"/Kasse/Buch?kasse={haupt.Kasse.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jugendwart.GetAsync($"/Kasse/Buchung?kasse={jugend.Id}")).StatusCode);

        var zweck = "Zeltplatzmiete " + Guid.NewGuid().ToString("N")[..6];
        var antwort = await AbschickenAsync(jugendwart, $"/Kasse/Anordnungen?kasse={jugend.Id}", $"/Kasse/Anordnungen?kasse={jugend.Id}", new()
        {
            ["Formular.Betrag"] = "180", ["Formular.Empfaenger"] = "Zeltplatz Schurwald", ["Formular.Zweck"] = zweck,
            ["Formular.PlanpositionId"] = zeltlager.ToString(),
        }, ("Angebot.pdf", Pdf));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid anordnungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            anordnungId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Zahlungsanordnungen
                .Where(a => a.Zweck == zweck).Select(a => a.Id).SingleAsync();
        }

        // Das Hauptamt sieht die Jugendkasse, aber keine Namen (keine Jugenddaten).
        var gemeinde = await Client(Rollen.GemeindeLesen).GetStringAsync($"/Kasse/Anordnungen?kasse={jugend.Id}");
        Assert.Contains(zweck, gemeinde);
        Assert.DoesNotContain("Zeltplatz Schurwald", gemeinde);

        var kassier = Client(Rollen.Kassier);
        antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung?anordnung={anordnungId}", $"/Kasse/Buchung?anordnung={anordnungId}", new()
        {
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "180",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = zeltlager.ToString(), ["Formular.Partner"] = "Zeltplatz Schurwald",
            ["Formular.Text"] = zweck, ["Formular.Belegart"] = nameof(Belegart.Rechnung),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var anordnung = await db.Zahlungsanordnungen.SingleAsync(a => a.Id == anordnungId);
            Assert.Equal(AnordnungStatus.Gezahlt, anordnung.Status);
            var buchung = await db.Buchungen.Include(b => b.Belege).SingleAsync(b => b.Id == anordnung.BuchungId);
            Assert.Equal(jugend.Id, buchung.KasseId);
            Assert.Single(buchung.Belege);
        }
    }

    [Fact]
    public async Task Kassier_stellt_Wirtschaftsplan_auf_und_der_Ausschuss_beschliesst()
    {
        var (kasse, _, _, position, zuwendung) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var jahr = Heute.Year + 3;
        var kassier = Client(Rollen.Kassier);
        var seite = $"/Kasse/Plan?kasse={kasse.Id}&jahr={jahr}";

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, seite, $"/Kasse/Plan?kasse={kasse.Id}&jahr={jahr}&handler=Anlegen", [])).StatusCode);
        var antwort = await AbschickenAsync(kassier, seite, $"/Kasse/Plan?kasse={kasse.Id}&jahr={jahr}&handler=Ansaetze", new()
        {
            [$"Betraege[{zuwendung}]"] = "1.500,00", [$"Betraege[{position}]"] = "800", [$"Erlaeuterungen[{position}]"] = "Jahresausflug",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        antwort = await AbschickenAsync(kassier, seite, $"/Kasse/Plan?kasse={kasse.Id}&jahr={jahr}&handler=Status", new()
        {
            ["NeuerStatus"] = nameof(WirtschaftsplanStatus.Beschlossen), ["StatusDatum"] = Heute.ToString("dd.MM.yyyy"),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var plan = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Wirtschaftsplaene.Include(w => w.Ansaetze)
            .SingleAsync(w => w.KasseId == kasse.Id && w.Jahr == jahr);
        Assert.Equal(WirtschaftsplanStatus.Beschlossen, plan.Status);
        Assert.Equal(1500m, plan.Ansaetze.Single(a => a.PlanpositionId == zuwendung).Betrag);
        Assert.Equal("Jahresausflug", plan.Ansaetze.Single(a => a.PlanpositionId == position).Erlaeuterung);

        // Beschlossen: Ansätze sind fest, bis ein Nachtrag den Plan wieder öffnet.
        Assert.DoesNotContain($"name=\"Betraege[{position}]\"", await kassier.GetStringAsync(seite));
    }

    [Fact]
    public async Task Kassier_rechnet_eine_Veranstaltung_mit_Barkassenbericht_ab_und_die_Steuerampel_zaehlt_den_Umsatz()
    {
        var (kasse, giro, bar, _, _) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var position2 = kasse.Positionen.Single(p => p.Nummer == "2");
        var position5 = kasse.Positionen.Single(p => p.Nummer == "5");
        Assert.Equal(Steuerbereich.Wirtschaftlich, position2.Steuerbereich);
        var kassier = Client(Rollen.Kassier);
        var name = $"Hocketse {Guid.NewGuid():N}"[..20];

        var antwort = await AbschickenAsync(kassier, $"/Kasse/Veranstaltungen?kasse={kasse.Id}", $"/Kasse/Veranstaltungen?kasse={kasse.Id}",
            new() { ["Neu.Name"] = name, ["Neu.Beginn"] = Heute.ToString("dd.MM.yyyy"), ["Neu.AnteilProzent"] = "100" });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Guid veranstaltungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            veranstaltungId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Veranstaltungen.Where(v => v.Name == name).Select(v => v.Id).SingleAsync();
        }

        // 10 × 50 € und 7 × 10 € gezählt, 100 € Wechselgeld: 470 € Einnahme.
        antwort = await AbschickenAsync(kassier, $"/Kasse/Barkassenbericht?veranstaltung={veranstaltungId}", $"/Kasse/Barkassenbericht?veranstaltung={veranstaltungId}",
            new()
            {
                ["Formular.Tag"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Stelle"] = "Getränke", ["Formular.Wechselgeld"] = "100",
                ["Formular.GezaehltVon"] = "Anna Zählt", ["Formular.ZweitePerson"] = "Bernd Zählt", ["Formular.Steuersatz"] = nameof(Umsatzsteuersatz.Regel),
                ["Formular.Anzahl[5000]"] = "10", ["Formular.Anzahl[1000]"] = "7",
            });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Guid berichtId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var bericht = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Barkassenberichte.Include(b => b.Zaehlung)
                .SingleAsync(b => b.VeranstaltungId == veranstaltungId);
            Assert.Equal(470m, bericht.Einnahme);
            berichtId = bericht.Id;
        }

        var pdf = await kassier.GetAsync($"/Kasse/Barkassenbericht/{berichtId}?handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        antwort = await AbschickenAsync(kassier, $"/Kasse/Barkassenbericht/{berichtId}", $"/Kasse/Barkassenbericht/{berichtId}?handler=Buchen",
            new() { ["KontoId"] = bar.ToString(), ["PlanpositionId"] = position2.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        // Ausgabe für die Veranstaltung über das Buchungsformular mit Steuerangaben.
        antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung?kasse={kasse.Id}", $"/Kasse/Buchung?kasse={kasse.Id}",
            new()
            {
                ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "120",
                ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = position5.Id.ToString(), ["Formular.Text"] = $"Getränkeeinkauf {name}",
                ["Formular.Belegart"] = nameof(Belegart.Rechnung), ["Formular.Steuerbereich"] = nameof(Steuerbereich.Wirtschaftlich),
                ["Formular.Steuersatz"] = nameof(Umsatzsteuersatz.Regel), ["Formular.VeranstaltungId"] = veranstaltungId.ToString(),
            }, ("rechnung.pdf", Pdf));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bericht = await db.Barkassenberichte.SingleAsync(b => b.Id == berichtId);
            var einnahme = await db.Buchungen.Include(b => b.Belege).SingleAsync(b => b.Id == bericht.BuchungId);
            Assert.Equal(470m, einnahme.Betrag);
            Assert.Equal(BuchungArt.Einnahme, einnahme.Art);
            Assert.Equal(Steuerbereich.Wirtschaftlich, einnahme.Steuerbereich);
            Assert.Equal(Belegart.Kassenbericht, einnahme.Belegart);
            Assert.Equal(veranstaltungId, einnahme.VeranstaltungId);
            var beleg = Assert.Single(einnahme.Belege);
            Assert.True(beleg.Inhalt.AsSpan().StartsWith("%PDF"u8));
            var ausgabe = await db.Buchungen.SingleAsync(b => b.Text == $"Getränkeeinkauf {name}");
            Assert.Equal(veranstaltungId, ausgabe.VeranstaltungId);
            Assert.Equal(Umsatzsteuersatz.Regel, ausgabe.Steuersatz);
        }

        // Nach dem Buchen ist der Bericht nicht mehr änderbar.
        antwort = await AbschickenAsync(kassier, $"/Kasse/Barkassenbericht/{berichtId}", $"/Kasse/Barkassenbericht/{berichtId}",
            new() { ["Formular.Tag"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Stelle"] = "Getränke", ["Formular.GezaehltVon"] = "A", ["Formular.ZweitePerson"] = "B" });
        Assert.Equal(HttpStatusCode.Forbidden, antwort.StatusCode);

        var seite = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Veranstaltung/{veranstaltungId}"));
        Assert.Contains("350,00 €", seite);

        var steuer = await kassier.GetStringAsync("/Kasse/Steuer");
        Assert.Contains("Steuerampel", steuer);
        var csv = await kassier.GetStringAsync("/Kasse/Steuer?handler=Csv");
        Assert.Contains($"{name}: Getränke am", csv);
        Assert.Contains($"Getränkeeinkauf {name}", csv);

        // Die Steuerübersicht über alle Kassen braucht das Leserecht für alle; der Jugendwart sieht nur die Jugendkasse.
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync("/Kasse/Steuer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(Rollen.GemeindeLesen).GetAsync("/Kasse/Steuer")).StatusCode);
    }
}
