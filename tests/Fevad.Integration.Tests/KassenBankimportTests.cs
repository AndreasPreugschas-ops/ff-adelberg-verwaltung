using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kontoauszug einlesen und abgleichen (P2: V07 Teil 2).</summary>
public partial class KasseTests
{
    private async Task<HttpResponseMessage> AuszugHochladenAsync(HttpClient client, Guid kasse, Guid konto, string dateiname, string inhalt, Guid? format = null)
    {
        var seite = $"/Kasse/Kontoauszug?kasse={kasse}";
        var token = WebUtility.HtmlDecode(TokenFeld().Match(await client.GetStringAsync(seite)).Groups[1].Value);
        var daten = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(konto.ToString()), "KontoId" },
            { new StringContent(format?.ToString() ?? ""), "BankformatId" },
        };
        var datei = new ByteArrayContent(Encoding.UTF8.GetBytes(inhalt));
        datei.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        daten.Add(datei, "Datei", dateiname);
        return await client.PostAsync(seite, daten);
    }

    [Fact]
    public async Task Kassier_liest_camt_ein_und_gleicht_Umsaetze_mit_Buchungen_Erstattungen_und_Zuwendung_ab()
    {
        var (kasse, giro, _, position, einnahme) = await KasseAsync(Kassenfuehrung.NameJugendkasse);
        var (mitgliedId, personalnummer, _) = await MitgliedMitKontoAsync("Bank" + Guid.NewGuid().ToString("N")[..6]);
        const string iban = "DE89370400440532013000";
        var tag = Heute.AddDays(-2);
        var kennung = Guid.NewGuid().ToString("N")[..6];
        Guid anordnungId, buchungId;
        string referenz;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Zahlungsmittelkonten.SingleAsync(k => k.Id == giro)).Iban = iban;
            var anordnung = new Zahlungsanordnung
            {
                KasseId = kasse.Id, Art = AnordnungArt.Auslagenerstattung, MitgliedId = mitgliedId, Betrag = 45.50m, Empfaenger = "Kai Muster",
                Zweck = "Bastelmaterial " + kennung, Status = AnordnungStatus.Vorbereitet,
            };
            db.Zahlungsanordnungen.Add(anordnung);
            await db.SaveChangesAsync();
            (anordnungId, referenz) = (anordnung.Id, SepaUeberweisung.Referenz(personalnummer, anordnung.Id));

            var buchung = new Buchung
            {
                KasseId = kasse.Id, Art = BuchungArt.Ausgabe, Betrag = 19.99m, KontoId = giro, PlanpositionId = position, Datum = tag.AddDays(-1),
                Text = "Material " + kennung, Belegart = Belegart.Rechnung,
            };
            await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().BuchenAsync(buchung);
            buchungId = buchung.Id;
        }

        var xml = KontoauszugleserTests.Camt(iban, tag, 777m,
            KontoauszugleserTests.Eintrag(tag, 600m, "Gemeinde Adelberg", "Zuwendung Jugendfeuerwehr " + kennung),
            KontoauszugleserTests.Eintrag(tag, -45.50m, "Kai Muster", "Auslagenerstattung " + kennung, referenz),
            KontoauszugleserTests.Eintrag(tag, -19.99m, "Bastelladen", "Rechnung " + kennung),
            KontoauszugleserTests.Eintrag(tag, 12.34m, "Unbekannt", "Spende " + kennung));

        // Der Jugendwart sieht den Abgleich nur, einlesen darf er nicht; Datei eines fremden Kontos wird abgelehnt.
        var jugendwart = Client(Rollen.Jugendwart);
        Assert.DoesNotContain("Einlesen", await jugendwart.GetStringAsync($"/Kasse/Kontoauszug?kasse={kasse.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await AuszugHochladenAsync(jugendwart, kasse.Id, giro, "a.xml", KontoauszugleserTests.Camt(iban, tag, 1m))).StatusCode);
        var kassier = Client(Rollen.Kassier);
        var fremd = await AuszugHochladenAsync(kassier, kasse.Id, giro, "fremd.xml", KontoauszugleserTests.Camt("DE02120300000000202051", tag, 1m));
        Assert.Contains("gehört zum Konto", WebUtility.HtmlDecode(await fremd.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await AuszugHochladenAsync(kassier, kasse.Id, giro, "auszug.xml", xml)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AuszugHochladenAsync(kassier, kasse.Id, giro, "auszug.xml", xml)).StatusCode);
        Dictionary<string, Kontoumsatz> umsaetze;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            umsaetze = (await db.Kontoumsaetze.Where(u => u.Verwendungszweck!.EndsWith(kennung)).ToListAsync())
                .ToDictionary(u => u.Verwendungszweck!.Split(' ')[0]);
            Assert.Equal(4, umsaetze.Count); // zweites Einlesen hat nichts verdoppelt
            Assert.Equal(0, (await db.Kontoauszugimporte.OrderByDescending(i => i.ErstelltAm).FirstAsync(i => i.KontoId == giro)).Neu);
        }

        var seite = WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Kontoauszug?kasse={kasse.Id}"));
        Assert.Contains($"/Kasse/Buchung?vorlage=", seite);
        Assert.Contains($"umsatz={umsaetze["Zuwendung"].Id}", seite);
        Assert.Contains($"/Kasse/Buchung?anordnung={anordnungId}&umsatz={umsaetze["Auslagenerstattung"].Id}", seite);
        Assert.Contains($"buchungId={buchungId}", seite);
        Assert.Contains("Saldenabgleich", seite);

        // Das Hauptamt sieht bei der Jugendkasse nur Tag und Betrag, keine Namen und keinen Verwendungszweck.
        var gemeinde = WebUtility.HtmlDecode(await Client(Rollen.GemeindeLesen).GetStringAsync($"/Kasse/Kontoauszug?kasse={kasse.Id}"));
        Assert.Contains("45,50", gemeinde);
        Assert.DoesNotContain("Kai Muster", gemeinde);
        Assert.DoesNotContain("Auslagenerstattung " + kennung, gemeinde);
        Assert.Contains("Kai Muster", seite);

        // Vorgeschlagene Buchung zuordnen.
        var token = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        var antwort = await kassier.PostAsync($"/Kasse/Kontoauszug?kasse={kasse.Id}&id={umsaetze["Rechnung"].Id}&buchungId={buchungId}&handler=Zuordnen",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        // Erstattung aus der Referenz zahlen: Tag und Konto kommen von der Bank.
        var pfad = $"/Kasse/Buchung?anordnung={anordnungId}&umsatz={umsaetze["Auslagenerstattung"].Id}";
        Assert.Contains($"value=\"{tag:dd.MM.yyyy}\"", await kassier.GetStringAsync(pfad));
        antwort = await AbschickenAsync(kassier, pfad, pfad, new()
        {
            ["Formular.Datum"] = tag.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Ausgabe), ["Formular.Betrag"] = "45,50",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = position.ToString(), ["Formular.Text"] = "Erstattung " + kennung,
            ["Formular.Belegart"] = nameof(Belegart.Kontoauszug),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("/Kasse/Kontoauszug", antwort.Headers.Location?.ToString());

        // Unbekannten Eingang neu buchen.
        pfad = $"/Kasse/Buchung?kasse={kasse.Id}&umsatz={umsaetze["Spende"].Id}";
        Assert.Contains("Unbekannt", await kassier.GetStringAsync(pfad));
        antwort = await AbschickenAsync(kassier, pfad, pfad, new()
        {
            ["Formular.Datum"] = tag.ToString("dd.MM.yyyy"), ["Formular.Art"] = nameof(BuchungArt.Einnahme), ["Formular.Betrag"] = "12,34",
            ["Formular.KontoId"] = giro.ToString(), ["Formular.PlanpositionId"] = einnahme.ToString(), ["Formular.Text"] = "Spende " + kennung,
            ["Formular.Belegart"] = nameof(Belegart.Kontoauszug),
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stand = await db.Kontoumsaetze.Where(u => u.Verwendungszweck!.EndsWith(kennung)).ToDictionaryAsync(u => u.Verwendungszweck!.Split(' ')[0]);
            Assert.Equal(buchungId, stand["Rechnung"].BuchungId);
            Assert.Equal(UmsatzStatus.Zugeordnet, stand["Auslagenerstattung"].Status);
            var anordnung = await db.Zahlungsanordnungen.SingleAsync(a => a.Id == anordnungId);
            Assert.Equal(AnordnungStatus.Gezahlt, anordnung.Status);
            Assert.Equal(anordnung.BuchungId, stand["Auslagenerstattung"].BuchungId);
            Assert.Equal(UmsatzStatus.Zugeordnet, stand["Spende"].Status);
            Assert.Equal(UmsatzStatus.Offen, stand["Zuwendung"].Status);
        }
    }

    [Fact]
    public async Task Bankformat_in_den_Stammdaten_pflegen_und_CSV_einlesen()
    {
        var (kasse, giro, _, _, _) = await KasseAsync(Kassenfuehrung.NameKameradschaftskasse);
        var name = "Sparkasse " + Guid.NewGuid().ToString("N")[..6];
        var einrichten = Client(Rollen.Kommandant);
        Assert.Contains("/Kasse/Bankformate", await einrichten.GetStringAsync("/Stammdaten"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync("/Kasse/Bankformate")).StatusCode);

        var ohneBetrag = await AbschickenAsync(einrichten, "/Kasse/Bankformate", "/Kasse/Bankformate", new()
        {
            ["Formular.Name"] = name, ["Formular.SpalteBuchungstag"] = "Buchungstag", ["Formular.Datumsformat"] = "dd.MM.yy",
        });
        Assert.Contains("Bitte die Spalte Betrag angeben", WebUtility.HtmlDecode(await ohneBetrag.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(einrichten, "/Kasse/Bankformate", "/Kasse/Bankformate", new()
        {
            ["Formular.Name"] = name, ["Formular.SpalteBuchungstag"] = "Buchungstag", ["Formular.SpalteBetrag"] = "Betrag",
            ["Formular.SpalteGegenpartei"] = "Beguenstigter/Zahlungspflichtiger", ["Formular.SpalteVerwendungszweck"] = "Verwendungszweck",
            ["Formular.Datumsformat"] = "dd.MM.yy", ["Formular.Dezimalkomma"] = "true",
        })).StatusCode);
        Guid formatId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            formatId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bankformate.Where(f => f.Name == name).Select(f => f.Id).SingleAsync();
        }

        var zweck = "Getränkeverkauf " + Guid.NewGuid().ToString("N")[..6];
        var csv = $"\"Auftragskonto\";\"Buchungstag\";\"Verwendungszweck\";\"Beguenstigter/Zahlungspflichtiger\";\"Betrag\"\n"
            + $"\"DE..\";\"{Heute:dd.MM.yy}\";\"{zweck}\";\"Bar-Einzahlung\";\"-1.050,00\"\n";
        var kassier = Client(Rollen.Kassier);
        var ohneFormat = await AuszugHochladenAsync(kassier, kasse.Id, giro, "umsaetze.csv", csv);
        Assert.Contains("Format der Bank", WebUtility.HtmlDecode(await ohneFormat.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await AuszugHochladenAsync(kassier, kasse.Id, giro, "umsaetze.csv", csv, formatId)).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var u = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Kontoumsaetze.SingleAsync(x => x.Verwendungszweck == zweck);
            Assert.Equal((-1050m, "Bar-Einzahlung", Heute), (u.Betrag, u.Gegenpartei, u.Buchungstag));
        }
    }
}
