using System.Net;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Lehrgangsantrag durch das Mitglied, Prüfung, Abrechnung und Jahresbescheinigung (P3: V32, V34).</summary>
public partial class EntschaedigungTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    private async Task<(Guid MitgliedId, HttpClient Client)> MitgliedMitKontoAsync(string nachname)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = NeuesMitglied(nachname);
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();

        var email = $"e-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(konto)).Succeeded);
        var client = Client(Rollen.Mitglied);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, konto.Id.ToString());
        return (m.Id, client);
    }

    private static async Task<HttpResponseMessage> MultipartAsync(HttpClient client, string seite, Dictionary<string, string> felder, params (string Name, byte[] Inhalt)[] dateien)
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

        return await client.PostAsync(seite, inhalt);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string seite, string ziel, Dictionary<string, string> felder)
    {
        var html = await client.GetStringAsync(seite);
        felder["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);
        return await client.PostAsync(ziel, new FormUrlEncodedContent(felder));
    }

    [Fact]
    public async Task Mitglied_beantragt_Lehrgang_Kassier_genehmigt_Abrechnung_zahlt_einmal_mit_Jahresbescheinigung()
    {
        const int jahr = 2047;
        var name = "Antrag" + Guid.NewGuid().ToString("N")[..6];
        var bezeichnung = "Maschinist " + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);
        var heute = Anzeige.Heute(TimeProvider.System);
        // Zwei Werktage in der Vergangenheit, 08:00 bis 16:30 Uhr.
        var montag = heute.AddDays(-14 - ((int)heute.DayOfWeek + 6) % 7);

        var felder = new Dictionary<string, string>
        {
            ["Formular.Anlass"] = nameof(AntragAnlass.Lehrgang), ["Formular.Bezeichnung"] = bezeichnung, ["Formular.Ort"] = "Geislingen",
            ["Formular.Tage[0].Datum"] = montag.ToString("dd.MM.yyyy"), ["Formular.Tage[0].Beginn"] = "08:00", ["Formular.Tage[0].Ende"] = "16:30",
            ["Formular.Tage[1].Datum"] = montag.AddDays(1).ToString("dd.MM.yyyy"), ["Formular.Tage[1].Beginn"] = "08:00", ["Formular.Tage[1].Ende"] = "16:30",
            ["Formular.Erwerbslage"] = nameof(Erwerbslage.Arbeitnehmer), ["Formular.Verdienstausfall"] = "true",
            ["Formular.Fahrtart"] = nameof(Fahrtart.Pkw),
        };

        // Ohne Kilometer bei eigenem Auto geht es nicht.
        var fehler = await MultipartAsync(mitglied, "/Konto/Entschaedigung", felder);
        Assert.Equal(HttpStatusCode.OK, fehler.StatusCode);
        Assert.Contains("Kilometer angeben", WebUtility.HtmlDecode(await fehler.Content.ReadAsStringAsync()));

        felder["Formular.Kilometer"] = "120";
        Assert.Equal(HttpStatusCode.Redirect, (await MultipartAsync(mitglied, "/Konto/Entschaedigung", felder, ("Teilnahme.pdf", Pdf))).StatusCode);

        Guid antragId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var antrag = await db.Entschaedigungsantraege.Include(a => a.Tage).Include(a => a.Belege).SingleAsync(a => a.Bezeichnung == bezeichnung);
            Assert.Equal((mitgliedId, AntragStand.Eingereicht, 2, 1), (antrag.MitgliedId, antrag.Stand, antrag.Tage.Count, antrag.Belege.Count));
            Assert.Equal("08:00", Anzeige.Lokal(antrag.Tage.Min(t => t.Von)).ToString("HH:mm"));
            antragId = antrag.Id;
        }

        // 2 × (20 € Staffel + 63 € Aufschlag) + 120 km × 0,35 € = 208 €.
        var eigene = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Entschaedigung"));
        Assert.Contains(bezeichnung, eigene);
        Assert.Contains("208,00 €", eigene);

        // Die Gemeinde sieht keine Anträge mit Verdienstangaben; ein anderes Mitglied sieht den Antrag nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.GemeindeLesen).GetAsync("/Auswertungen/Entschaedigung/Antraege")).StatusCode);
        var (_, anderes) = await MitgliedMitKontoAsync("Anders" + Guid.NewGuid().ToString("N")[..6]);
        Assert.DoesNotContain(bezeichnung, WebUtility.HtmlDecode(await anderes.GetStringAsync("/Konto/Entschaedigung")));

        var kassier = Client(Rollen.Kassier);
        var pruefung = WebUtility.HtmlDecode(await kassier.GetStringAsync("/Auswertungen/Entschaedigung/Antraege"));
        Assert.Contains(bezeichnung, pruefung);
        Assert.Contains("208,00 €", pruefung);

        // Ablehnen nur mit Begründung, dann genehmigen.
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kassier, "/Auswertungen/Entschaedigung/Antraege", "/Auswertungen/Entschaedigung/Antraege?handler=Ablehnen",
            new() { ["id"] = antragId.ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(kassier, "/Auswertungen/Entschaedigung/Antraege", "/Auswertungen/Entschaedigung/Antraege?handler=Genehmigen",
            new() { ["id"] = antragId.ToString(), ["vermerk"] = "Bescheinigung liegt vor" })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var antrag = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Entschaedigungsantraege.SingleAsync(a => a.Id == antragId);
            Assert.Equal((AntragStand.Genehmigt, "Bescheinigung liegt vor"), (antrag.Stand, antrag.Entscheidungsvermerk));

            // Ein später genehmigter Antrag kommt in die nächste Abrechnung, auch wenn sein Lehrgang davor lag.
            var abrechnungen = scope.ServiceProvider.GetRequiredService<Abrechnungen>();
            var abrechnung = await abrechnungen.AnlegenAsync($"Jahr {jahr}", new(jahr, 1, 1), new(jahr, 12, 31));
            var posten = abrechnung.Posten.Where(p => p.EntschaedigungsantragId == antragId).ToList();
            Assert.Equal(208m, posten.Sum(p => p.Betrag));
            Assert.Equal(126m, posten.Where(p => p.Steuerart == Steuerart.Verdienstausfall).Sum(p => p.Betrag));
            await abrechnungen.AbschliessenAsync(abrechnung.Id, Guid.CreateVersion7());

            var nachtrag = await abrechnungen.BerechnenAsync(new(jahr + 1, 1, 1), new(jahr + 1, 12, 31));
            Assert.DoesNotContain(nachtrag.Posten, p => p.EntschaedigungsantragId == antragId);
        }

        // Abgerechnet: Zurücksetzen geht nicht mehr; das Mitglied lädt die Jahresbescheinigung.
        await PostAsync(kassier, "/Auswertungen/Entschaedigung/Antraege", $"/Auswertungen/Entschaedigung/Antraege?handler=Zuruecksetzen&id={antragId}", new());
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Equal(AntragStand.Genehmigt, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Entschaedigungsantraege.SingleAsync(a => a.Id == antragId)).Stand);
        }

        var seite = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Entschaedigung"));
        Assert.Contains($"PDF {heute.Year}", seite);
        Assert.Contains("Verdienstausfall (§ 24 Nr. 1a EStG)", seite);
        var pdf = await mitglied.GetAsync($"/Konto/Entschaedigung?handler=Bescheinigung&jahr={heute.Year}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(name, pdf.Content.Headers.ContentDisposition?.FileNameStar ?? pdf.Content.Headers.ContentDisposition?.FileName ?? "");
        Assert.Equal(HttpStatusCode.NotFound, (await anderes.GetAsync($"/Konto/Entschaedigung?handler=Bescheinigung&jahr={heute.Year}")).StatusCode);
    }

    [Fact]
    public async Task Loeschlauf_L13_und_L44_loeschen_alte_Abrechnungen_Belege_und_abgelehnte_Antraege()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var jetzt = DateTimeOffset.UtcNow;
        Guid altId, altAntragId, belegAntragId, abgelehntId, frischId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = NeuesMitglied("Loesch" + Guid.NewGuid().ToString("N")[..6]);
            db.Mitglieder.Add(m);

            Entschaedigungsantrag Antrag(AntragStand stand, DateOnly tag, DateOnly? entschieden) => new()
            {
                MitgliedId = m.Id, Anlass = AntragAnlass.Lehrgang, Bezeichnung = "Lehrgang", Stand = stand, EingereichtAm = tag, EntschiedenAm = entschieden,
                VerdienstausfallBetrag = 123.45m,
                Tage = [new AntragTag { Von = Ortszeit.TagesbeginnUtc(tag).AddHours(8), Bis = Ortszeit.TagesbeginnUtc(tag).AddHours(16) }],
                Belege = [new AntragBeleg { Dateiname = "Nachweis.pdf", Inhaltstyp = "application/pdf", Inhalt = Pdf, Pruefsumme = "x" }],
            };
            Abrechnung Abrechnung(DateOnly bis, DateTimeOffset abgeschlossen, Entschaedigungsantrag antrag) => new()
            {
                Bezeichnung = "Alt", Von = bis.AddMonths(-6), Bis = bis, Status = AbrechnungStatus.Abgeschlossen, AbgeschlossenAm = abgeschlossen,
                Posten = [new AbrechnungsPosten { MitgliedId = m.Id, Art = PostenArt.Antrag, Vorgang = "Lehrgang", EntschaedigungsantragId = antrag.Id, Datum = bis, Bezeichnung = "Lehrgang", Menge = 1, Betrag = 10 }],
            };

            // Abrechnungsjahr vor elf Jahren: weg samt Antrag. Vor zwei Jahren abgeschlossen: nur die Belege weg.
            var altAntrag = Antrag(AntragStand.Genehmigt, new(heute.Year - 11, 3, 1), new(heute.Year - 11, 3, 5));
            var belegAntrag = Antrag(AntragStand.Genehmigt, heute.AddYears(-2), heute.AddYears(-2));
            var abgelehnt = Antrag(AntragStand.Abgelehnt, heute.AddMonths(-14), heute.AddMonths(-13));
            var frisch = Antrag(AntragStand.Zurueckgezogen, heute.AddMonths(-2), heute.AddMonths(-1));
            db.Entschaedigungsantraege.AddRange(altAntrag, belegAntrag, abgelehnt, frisch);
            var alt = Abrechnung(new(heute.Year - 11, 6, 30), jetzt.AddYears(-11), altAntrag);
            db.Abrechnungen.AddRange(alt, Abrechnung(heute.AddYears(-2), jetzt.AddYears(-2), belegAntrag));
            await db.SaveChangesAsync();
            (altId, altAntragId, belegAntragId, abgelehntId, frischId) = (alt.Id, altAntrag.Id, belegAntrag.Id, abgelehnt.Id, frisch.Id);

            // Verdienstbeträge stehen nicht im Änderungsprotokoll.
            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == altAntragId).Select(a => a.Aenderungen).ToListAsync();
            Assert.All(protokoll, p => Assert.DoesNotContain("123.45", p ?? ""));
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelAbrechnungen).Anzahl >= 1);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Abrechnungen.IgnoreQueryFilters().AnyAsync(a => a.Id == altId));
            Assert.False(await db.Entschaedigungsantraege.IgnoreQueryFilters().AnyAsync(a => a.Id == altAntragId));
            Assert.True(await db.Entschaedigungsantraege.AnyAsync(a => a.Id == belegAntragId));
            Assert.False(await db.AntragBelege.AnyAsync(b => b.EntschaedigungsantragId == belegAntragId));
            Assert.False(await db.Entschaedigungsantraege.IgnoreQueryFilters().AnyAsync(a => a.Id == abgelehntId));
            Assert.False(await db.AntragTage.AnyAsync(t => t.EntschaedigungsantragId == abgelehntId));
            Assert.True(await db.AntragBelege.AnyAsync(b => b.EntschaedigungsantragId == frischId));
        }
    }
}
