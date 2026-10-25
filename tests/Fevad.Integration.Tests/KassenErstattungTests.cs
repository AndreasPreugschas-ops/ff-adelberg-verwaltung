using System.Net;
using Fevad.Domain.Kassen;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Auslagenerstattung mit Überweisungsdatei und Jahresliste, wiederkehrende Buchungen (P2: V07).</summary>
public partial class KasseTests
{
    private async Task<(Guid MitgliedId, string Personalnummer, HttpClient Client)> MitgliedMitKontoAsync(string nachname)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied { Personalnummer = "K" + Guid.NewGuid().ToString("N")[..7], Vorname = "Kai", Nachname = nachname, Geburtsdatum = Heute.AddYears(-30) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();

        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var email = $"k-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        var client = Client(Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, konto.Id.ToString());
        return (m.Id, m.Personalnummer, client);
    }

    [Fact]
    public async Task Mitglied_reicht_Auslagen_ein_und_der_Kassier_ueberweist_und_bucht()
    {
        var name = "Auslage" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, personalnummer, mitglied) = await MitgliedMitKontoAsync(name);
        var (kasse, giro, _, position, _) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var zweck = "Getränke Übung " + Guid.NewGuid().ToString("N")[..6];

        Assert.Contains("noch keine Bankverbindung", WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Auslagen")));
        var felder = new Dictionary<string, string>
        {
            ["Formular.KasseId"] = kasse.Id.ToString(), ["Formular.Betrag"] = "23,80", ["Formular.Zweck"] = zweck,
            ["Formular.Iban"] = "DE89 3704 0044 0532 0130 00",
        };

        // Ohne Beleg geht es nicht.
        var ohne = await AbschickenAsync(mitglied, "/Konto/Auslagen", "/Konto/Auslagen", felder);
        Assert.Equal(HttpStatusCode.OK, ohne.StatusCode);
        Assert.Contains("Bitte den Beleg hochladen", WebUtility.HtmlDecode(await ohne.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Konto/Auslagen", "/Konto/Auslagen", felder, ("Kassenzettel.pdf", Pdf))).StatusCode);
        Guid anordnungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = await db.Zahlungsanordnungen.Include(x => x.Belege).SingleAsync(x => x.Zweck == zweck);
            Assert.Equal(AnordnungArt.Auslagenerstattung, a.Art);
            Assert.Equal(mitgliedId, a.MitgliedId);
            Assert.Equal(23.80m, a.Betrag);
            Assert.Single(a.Belege);
            Assert.Equal("DE89370400440532013000", (await db.Bankverbindungen.SingleAsync(b => b.MitgliedId == mitgliedId)).Iban);
            anordnungId = a.Id;

            // Für die Überweisungsdatei braucht das Girokonto eine IBAN.
            var konto = await db.Zahlungsmittelkonten.SingleAsync(k => k.Id == giro);
            konto.Iban = "DE02120300000000202051";
            await db.SaveChangesAsync();
        }

        // Das Mitglied sieht seine Erstattung und nur die IBAN maskiert.
        var eigene = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Auslagen"));
        Assert.Contains(zweck, eigene);
        Assert.Contains("eingereicht", eigene);
        Assert.Contains("DE•• •••• •••• •••• ••30 00", eigene);
        Assert.DoesNotContain("0532 0130", eigene);

        // Ein anderes Mitglied sieht sie nicht.
        var (_, _, anderes) = await MitgliedMitKontoAsync("Anders" + Guid.NewGuid().ToString("N")[..6]);
        Assert.DoesNotContain(zweck, WebUtility.HtmlDecode(await anderes.GetStringAsync("/Konto/Auslagen")));

        var kassier = Client(Rollen.Kassier);
        var liste = WebUtility.HtmlDecode(await kassier.GetStringAsync("/Kasse/Erstattungen"));
        Assert.Contains(zweck, liste);
        Assert.Contains(personalnummer, liste);
        Assert.Contains("IBAN hinterlegt", liste);

        // Überweisungsdatei: nur mit Recht auf Bankdaten; Kennung und Zweck tragen die Personalnummer.
        async Task<HttpResponseMessage> UeberweisenAsync(HttpClient client)
        {
            var token = TokenFeld().Match(await client.GetStringAsync("/Kasse/Erstattungen")).Groups[1].Value;
            return await client.PostAsync("/Kasse/Erstattungen?handler=Ueberweisung", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token), ["auswahl"] = anordnungId.ToString(), ["kontoId"] = giro.ToString(),
                ["auftraggeber"] = "Kameradschaftskasse FF Adelberg", ["ausfuehrung"] = Heute.ToString("yyyy-MM-dd"),
            }));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await UeberweisenAsync(Client(Rollen.GemeindeLesen))).StatusCode);
        var sepa = await UeberweisenAsync(kassier);
        Assert.Equal("application/xml", sepa.Content.Headers.ContentType?.MediaType);
        var xml = await sepa.Content.ReadAsStringAsync();
        Assert.Contains("<IBAN>DE89370400440532013000</IBAN>", xml);
        Assert.Contains("<IBAN>DE02120300000000202051</IBAN>", xml);
        Assert.Contains("<InstdAmt Ccy=\"EUR\">23.80</InstdAmt>", xml);
        Assert.Contains($"<EndToEndId>PN{personalnummer}-", xml);
        Assert.Contains($"PNr {personalnummer}", xml);

        // Zahlen und buchen wie jede vorbereitete Ausgabe.
        var antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung?anordnung={anordnungId}", $"/Kasse/Buchung?anordnung={anordnungId}", new()
        {
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "23,80",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = position.ToString(), ["Formular.Partner"] = name,
            ["Formular.Text"] = zweck, ["Formular.Belegart"] = nameof(Belegart.Rechnung),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Assert.Contains("gezahlt", WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Auslagen")));
        var jahresliste = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Erstattungen?jahr={Heute.Year}&handler=Csv"));
        Assert.Contains($"{personalnummer};", jahresliste);
        Assert.Contains("23,80", jahresliste);
    }

    [Fact]
    public async Task Mitglied_zieht_eine_Erstattung_zurueck()
    {
        var (_, _, mitglied) = await MitgliedMitKontoAsync("Zurueck" + Guid.NewGuid().ToString("N")[..6]);
        var (kasse, _, _, _, _) = await KasseAsync(Kassenfuehrung.NameJugendkasse);
        var zweck = "Bastelmaterial " + Guid.NewGuid().ToString("N")[..6];
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Konto/Auslagen", "/Konto/Auslagen", new()
        {
            ["Formular.KasseId"] = kasse.Id.ToString(), ["Formular.Betrag"] = "12", ["Formular.Zweck"] = zweck,
        }, ("Bon.pdf", Pdf))).StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Zahlungsanordnungen.Where(a => a.Zweck == zweck).Select(a => a.Id).SingleAsync();
        }

        var token = TokenFeld().Match(await mitglied.GetStringAsync("/Konto/Auslagen")).Groups[1].Value;
        var antwort = await mitglied.PostAsync($"/Konto/Auslagen?handler=Zurueckziehen&id={id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Zahlungsanordnungen.SingleAsync(x => x.Id == id);
            Assert.Equal(AnordnungStatus.Abgelehnt, a.Status);
            Assert.Equal("vom Mitglied zurückgezogen", a.Bemerkung);
        }
    }

    [Fact]
    public async Task Zuwendung_der_Gemeinde_ist_als_wiederkehrende_Einnahme_angelegt_und_wird_vorbefuellt_gebucht()
    {
        var (kasse, giro, _, _, zuwendung) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        Guid vorlageId;
        DateOnly faellig;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var alle = await db.WiederkehrendeBuchungen.ToListAsync();
            Assert.Equal(2, alle.Count(w => w.Bezeichnung == "Zuwendung der Gemeinde"));
            var w = alle.Single(x => x.KasseId == kasse.Id);
            Assert.Equal(Domain.Gemeinsam.ParameterSchluessel.KameradschaftskasseZuschussEuro, w.BetragParameter);
            Assert.Equal(zuwendung, w.PlanpositionId);
            Assert.Equal(giro, w.KontoId);
            (vorlageId, faellig) = (w.Id, w.NaechsteFaelligkeit);
        }

        var kassier = Client(Rollen.Kassier);
        Assert.Contains($"/Kasse/Buchung?vorlage={vorlageId}", await kassier.GetStringAsync("/Kasse"));
        var formular = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Buchung?vorlage={vorlageId}"));
        Assert.Contains($"Zuwendung der Gemeinde {faellig.Year} (Entschädigungssatzung § 5 Abs. 1)", formular);
        Assert.Matches("name=\"Formular.Betrag\" value=\"1\\.?500", formular);

        var antwort = await AbschickenAsync(kassier, $"/Kasse/Buchung?vorlage={vorlageId}", $"/Kasse/Buchung?vorlage={vorlageId}", new()
        {
            ["Formular.Datum"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Einnahme), ["Formular.Betrag"] = "1500",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = zuwendung.ToString(), ["Formular.Partner"] = "Gemeinde",
            ["Formular.Text"] = $"Zuwendung der Gemeinde {faellig.Year}", ["Formular.Belegart"] = nameof(Belegart.Kontoauszug),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var w = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WiederkehrendeBuchungen.SingleAsync(x => x.Id == vorlageId);
            Assert.Equal(faellig.AddYears(1), w.NaechsteFaelligkeit);
        }

        // Pflegen darf nur, wer bucht; der Jugendwart nicht.
        var (jugend, _, _, _, _) = await KasseAsync(Kassenfuehrung.NameJugendkasse);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync($"/Kasse/Wiederkehrend?kasse={jugend.Id}")).StatusCode);

        // Neue monatliche Ausgabe am 31.: im kurzen Monat am letzten Tag, danach wieder am 31.
        var gebuehr = "Kontoführung " + Guid.NewGuid().ToString("N")[..6];
        antwort = await AbschickenAsync(kassier, $"/Kasse/Wiederkehrend?kasse={kasse.Id}", $"/Kasse/Wiederkehrend?kasse={kasse.Id}", new()
        {
            ["Formular.Bezeichnung"] = gebuehr, ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "4,90",
            ["Formular.Text"] = gebuehr, ["Formular.Rhythmus"] = nameof(Buchungsrhythmus.Monatlich), ["Formular.NaechsteFaelligkeit"] = "31.01.2030",
            ["Formular.Aktiv"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Guid gebuehrId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var w = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WiederkehrendeBuchungen.SingleAsync(x => x.Bezeichnung == gebuehr);
            Assert.Equal(31, w.Tag);
            gebuehrId = w.Id;
        }

        var seite = $"/Kasse/Wiederkehrend?kasse={kasse.Id}";
        var token = TokenFeld().Match(await kassier.GetStringAsync(seite)).Groups[1].Value;
        antwort = await kassier.PostAsync($"/Kasse/Wiederkehrend?kasse={kasse.Id}&id={gebuehrId}&handler=Ueberspringen",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Equal(new DateOnly(2030, 2, 28),
                (await scope.ServiceProvider.GetRequiredService<AppDbContext>().WiederkehrendeBuchungen.SingleAsync(x => x.Id == gebuehrId)).NaechsteFaelligkeit);
        }
    }
}
