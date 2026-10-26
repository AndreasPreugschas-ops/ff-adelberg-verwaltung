using System.Net;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Arbeitgeberbescheinigung mit Prüfcode und Erstattung an den Arbeitgeber (P3: V33).</summary>
public partial class EntschaedigungTests
{
    [Fact]
    public async Task Mitglied_stellt_Arbeitgeberbescheinigung_aus_Arbeitgeber_prueft_Erstattung_sperrt_Verdienstausfall()
    {
        const int jahr = 2048;
        var name = "Arbeit" + Guid.NewGuid().ToString("N")[..6];
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync(name);
        var heute = Anzeige.Heute(TimeProvider.System);
        var tag = heute.AddDays(-10);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Mitglieder.SingleAsync(x => x.Id == mitgliedId);
            m.Arbeitgeber = "Muster Metallbau GmbH";
            // Einsatz 09:10 bis 11:40 Uhr Ortszeit, Rückkehr 12:00 Uhr; das Stichwort erscheint nirgends.
            var einsatz = new Einsatz
            {
                Einsatznummer = $"A{Guid.NewGuid().ToString("N")[..8]}", Stichwort = "Geheimstichwort", Status = EinsatzStatus.Freigegeben,
                Alarmiert = Anzeige.AlsUtc(tag, new(9, 10)), Einsatzende = Anzeige.AlsUtc(tag, new(11, 40)), EinsatzbereitschaftHergestellt = Anzeige.AlsUtc(tag, new(12, 0)),
            };
            einsatz.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = mitgliedId });
            var uebung = new Dienst { Art = DienstArt.Uebung, Titel = "Übung", Beginn = Anzeige.AlsUtc(tag, new(19, 0)), Ende = Anzeige.AlsUtc(tag, new(21, 0)), Status = DienstStatus.Durchgefuehrt };
            uebung.Anwesenheiten.Add(new Anwesenheit { MitgliedId = mitgliedId, Status = AnwesenheitStatus.Anwesend });
            db.Einsaetze.Add(einsatz);
            db.Dienste.Add(uebung);
            await db.SaveChangesAsync();
        }

        var seite = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Arbeitgeber"));
        Assert.Contains("Muster Metallbau GmbH", seite);

        var felder = new Dictionary<string, string>
        {
            ["Formular.Von"] = tag.AddDays(-1).ToString("dd.MM.yyyy"), ["Formular.Bis"] = tag.ToString("dd.MM.yyyy"),
            ["Formular.Arbeitgeber"] = "Muster Metallbau GmbH", ["Formular.Einsaetze"] = "true", ["Formular.Lehrgaenge"] = "true",
        };
        var pdf = await PostAsync(mitglied, "/Konto/Arbeitgeber", "/Konto/Arbeitgeber?handler=Ausstellen", felder);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        string code;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var b = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Arbeitgeberbescheinigungen.SingleAsync(x => x.MitgliedId == mitgliedId);
            // Nur der Einsatz bis zur Einsatzbereitschaft, die Übung ist nicht gewählt.
            Assert.Equal((2.83m, BescheinigungArt.Teilnahme), (b.Stunden, b.Art));
            Assert.DoesNotContain("Geheimstichwort", b.Zeilen);
            Assert.Contains("|Einsatz|09:10|12:00|", b.Zeilen);
            code = b.Pruefcode;
        }

        // Der Arbeitgeber prüft ohne Anmeldung, auch mit Kleinbuchstaben und ohne Bindestriche.
        var anonym = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antwort = await PostAsync(anonym, "/Pruefen", "/Pruefen", new() { ["code"] = code.Replace("-", "").ToLowerInvariant() });
        Assert.True(antwort.Headers.CacheControl?.NoStore);
        Assert.Contains("noindex", string.Join(",", antwort.Headers.GetValues("X-Robots-Tag")));
        var pruefung = WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync());
        Assert.Contains("name=\"robots\"", pruefung);
        Assert.Contains("so von der", pruefung);
        Assert.Contains("Test " + name[0] + ".", pruefung);
        Assert.Contains("09:10", pruefung);
        Assert.DoesNotContain("Geheimstichwort", pruefung);
        Assert.DoesNotContain(name, pruefung);
        Assert.Contains("keine Bescheinigung", await PruefenAsync(anonym, "2222-3333-4444"));
        // Ein Code in der Adresse wird nicht ausgewertet.
        Assert.DoesNotContain("so von der", WebUtility.HtmlDecode(await anonym.GetStringAsync($"/Pruefen?code={code}")));

        // Die Gemeinde sieht die Arbeitgeberseite, darf aber nichts erfassen.
        Assert.Equal(HttpStatusCode.OK, (await Client(Rollen.GemeindeLesen).GetAsync("/Auswertungen/Entschaedigung/Arbeitgeber")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(Client(Rollen.GemeindeLesen), "/Auswertungen/Entschaedigung/Arbeitgeber",
            "/Auswertungen/Entschaedigung/Arbeitgeber?handler=Erstattung", new())).StatusCode);

        // Der Arbeitgeber beantragt Erstattung für den Lehrgangstag; der Verdienstausfall des Mitglieds entfällt.
        var lehrgangstag = new DateOnly(jahr, 3, 6);
        Guid antragId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var antrag = new Entschaedigungsantrag
            {
                MitgliedId = mitgliedId, Anlass = AntragAnlass.Lehrgang, Bezeichnung = "Atemschutz", Erwerbslage = Erwerbslage.Arbeitnehmer, Verdienstausfall = true,
                Stand = AntragStand.Genehmigt, EingereichtAm = lehrgangstag,
                Tage = [new AntragTag { Von = Ortszeit.TagesbeginnUtc(lehrgangstag).AddHours(7), Bis = Ortszeit.TagesbeginnUtc(lehrgangstag).AddHours(13) }],
            };
            db.Entschaedigungsantraege.Add(antrag);
            await db.SaveChangesAsync();
            antragId = antrag.Id;
        }

        var kassier = Client(Rollen.Kassier);
        var erfasst = await PostAsync(kassier, "/Auswertungen/Entschaedigung/Arbeitgeber", "/Auswertungen/Entschaedigung/Arbeitgeber?handler=Erstattung", new()
        {
            ["Erstattung.MitgliedId"] = mitgliedId.ToString(), ["Erstattung.Arbeitgeber"] = "Muster Metallbau GmbH",
            ["Erstattung.Von"] = lehrgangstag.ToString("dd.MM.yyyy"), ["Erstattung.Bis"] = lehrgangstag.ToString("dd.MM.yyyy"),
            ["Erstattung.Entgelt"] = "180", ["Erstattung.Sozialversicherung"] = "37,50", ["Erstattung.Pruefcode"] = code,
        });
        Assert.Equal(HttpStatusCode.Redirect, erfasst.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var abrechnungen = scope.ServiceProvider.GetRequiredService<Abrechnungen>();
            var abrechnung = await abrechnungen.AnlegenAsync($"Jahr {jahr}", new(jahr, 1, 1), new(jahr, 12, 31));
            var posten = abrechnung.Posten.Where(p => p.EntschaedigungsantragId == antragId).ToList();
            Assert.Equal(Steuerart.Aufwandsentschaedigung, Assert.Single(posten).Steuerart);
            Assert.Contains("§ 15 Abs. 2 FwG", abrechnung.Hinweise);
            await abrechnungen.AbschliessenAsync(abrechnung.Id, Guid.CreateVersion7());
        }

        // Widerruf: die Prüfseite warnt.
        Guid bescheinigungId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            bescheinigungId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Arbeitgeberbescheinigungen.SingleAsync(x => x.Pruefcode == code)).Id;
        }

        await PostAsync(Client(Rollen.Kommandant), "/Auswertungen/Entschaedigung/Arbeitgeber", $"/Auswertungen/Entschaedigung/Arbeitgeber?handler=Widerrufen&id={bescheinigungId}", new());
        Assert.Contains("widerrufen", await PruefenAsync(anonym, code));
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.GetAsync($"/Konto/Arbeitgeber?handler=Pdf&id={bescheinigungId}")).StatusCode);
    }

    [Fact]
    public async Task Lehrgangsmitteilung_fuer_kommenden_Lehrgang_als_PDF()
    {
        var (mitgliedId, mitglied) = await MitgliedMitKontoAsync("Mitteil" + Guid.NewGuid().ToString("N")[..6]);
        var heute = Anzeige.Heute(TimeProvider.System);
        Guid teilnahmeId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var qualifikation = await db.Qualifikationen.OrderBy(q => q.Sortierung).FirstAsync();
            var teilnahme = new Domain.Ausbildung.Lehrgangsteilnahme
            {
                MitgliedId = mitgliedId, QualifikationId = qualifikation.Id, Von = heute.AddDays(30), Bis = heute.AddDays(34), Lehrgangsort = "Bruchsal",
            };
            db.Lehrgangsteilnahmen.Add(teilnahme);
            await db.SaveChangesAsync();
            teilnahmeId = teilnahme.Id;
        }

        Assert.Contains("Mitteilung als PDF", WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Konto/Arbeitgeber")));
        var pdf = await PostAsync(mitglied, "/Konto/Arbeitgeber", "/Konto/Arbeitgeber?handler=Mitteilung",
            new() { ["teilnahmeId"] = teilnahmeId.ToString(), ["arbeitgeber"] = "Muster GmbH" });
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var b = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Arbeitgeberbescheinigungen.SingleAsync(x => x.MitgliedId == mitgliedId);
            Assert.Equal((BescheinigungArt.Lehrgangsmitteilung, heute.AddDays(30), 5), (b.Art, b.Von, Arbeitgeberbescheinigungen.ZeilenLesen(b.Zeilen).Count));
        }
    }

    [Fact]
    public async Task Loeschlauf_L45_und_L46_loeschen_alte_Bescheinigungen_und_entschiedene_Erstattungen()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var jetzt = DateTimeOffset.UtcNow;
        Guid altB, frischB, altE, offenE, frischE;
        string altCode;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = NeuesMitglied("AgLoesch" + Guid.NewGuid().ToString("N")[..6]);
            db.Mitglieder.Add(m);
            Arbeitgeberbescheinigung Bescheinigung(DateOnly tag) => new()
            {
                MitgliedId = m.Id, Art = BescheinigungArt.Teilnahme, Von = tag, Bis = tag, Pruefcode = Arbeitgeberbescheinigung.NeuerPruefcode(),
                Arbeitgeber = "Muster GmbH", Zeilen = "", AusgestelltAm = Ortszeit.TagesbeginnUtc(tag).AddHours(10),
            };
            Arbeitgebererstattung Erstattung(DateOnly? entschieden) => new()
            {
                MitgliedId = m.Id, Arbeitgeber = "Muster GmbH", Von = heute.AddYears(-12), Bis = heute.AddYears(-12), Entgelt = 100,
                Stand = entschieden is null ? ErstattungStand.Eingegangen : ErstattungStand.Erstattet, EingegangenAm = heute.AddYears(-12), EntschiedenAm = entschieden,
            };

            // Ausgestellt am 31.12. vor vier Jahren: Frist (3 Jahre ab Jahresende) abgelaufen; am 01.01. vor drei Jahren: läuft noch.
            var alt = Bescheinigung(new(heute.Year - 4, 12, 31));
            var frisch = Bescheinigung(new(heute.Year - 3, 1, 1));
            var eAlt = Erstattung(new(heute.Year - 11, 12, 31));
            var eOffen = Erstattung(null);
            var eFrisch = Erstattung(new(heute.Year - 10, 1, 1));
            db.AddRange(alt, frisch, eAlt, eOffen, eFrisch);
            await db.SaveChangesAsync();
            (altB, frischB, altE, offenE, frischE, altCode) = (alt.Id, frisch.Id, eAlt.Id, eOffen.Id, eFrisch.Id, alt.Pruefcode);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelArbeitgeberbescheinigungen).Anzahl >= 1);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelArbeitgebererstattungen).Anzahl >= 1);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Arbeitgeberbescheinigungen.AnyAsync(b => b.Id == altB));
            Assert.True(await db.Arbeitgeberbescheinigungen.AnyAsync(b => b.Id == frischB));
            Assert.False(await db.Arbeitgebererstattungen.AnyAsync(e => e.Id == altE));
            Assert.True(await db.Arbeitgebererstattungen.AnyAsync(e => e.Id == offenE));
            Assert.True(await db.Arbeitgebererstattungen.AnyAsync(e => e.Id == frischE));
        }

        // Die Prüfseite kennt den gelöschten Code nicht mehr.
        Assert.Contains("keine Bescheinigung bekannt", await PruefenAsync(Factory.CreateClient(), altCode));
    }

    private static async Task<string> PruefenAsync(HttpClient client, string code) =>
        WebUtility.HtmlDecode(await (await PostAsync(client, "/Pruefen", "/Pruefen", new() { ["code"] = code })).Content.ReadAsStringAsync());
}
