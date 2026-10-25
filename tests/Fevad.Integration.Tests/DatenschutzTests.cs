using Fevad.Web.Infrastruktur;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class DatenschutzTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, Guid? benutzer, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        if (benutzer is { } id)
        {
            client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, id.ToString());
        }

        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, string handler, Dictionary<string, string> felder)
    {
        var seite = await client.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = Token(seite);
        return await client.PostAsync($"{pfad}?handler={handler}", new FormUrlEncodedContent(felder));
    }

    private async Task<Mitglied> MitgliedAnlegenAsync(AppDbContext db, string nachname, DateOnly? ausgetretenAm)
    {
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var mitglied = new Mitglied
        {
            Personalnummer = "D" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = nachname, Geburtsdatum = Heute.AddYears(-40),
            Strasse = "Hauptstraße 1", Plz = "73099", Ort = "Adelberg", Email = nachname + "@example.test", TelefonMobil = "+4917000000",
            Arbeitgeber = "Firma", Beruf = "Schreiner", ArbeitgeberTelefon = "+497161000000", ArbeitgeberEmail = "chef@example.test",
            Geschlecht = Geschlecht.Weiblich, Notizen = "Notiz", Fe2SyncAktiv = true,
        };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft
        {
            AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-10), GueltigBis = ausgetretenAm,
            Austrittsgrund = ausgetretenAm is null ? null : Austrittsgrund.Wohnortwechsel,
        });
        db.Mitglieder.Add(mitglied);
        db.Groessenangaben.Add(new Groessenangabe { MitgliedId = mitglied.Id, Groessenart = Groessenart.Jacke, Wert = "52", ErfasstAm = Heute.AddYears(-2) });
        await db.SaveChangesAsync();
        return mitglied;
    }

    private static async Task<Benutzer> KontoAnlegenAsync(IServiceProvider services, string name, DateTimeOffset? deaktiviertAm, Guid? mitgliedId = null)
    {
        var benutzer = services.GetRequiredService<UserManager<Benutzer>>();
        var konto = new Benutzer { UserName = name, Email = name, MitgliedId = mitgliedId, DeaktiviertAm = deaktiviertAm, KalenderToken = "geheim" + name };
        Assert.True((await benutzer.CreateAsync(konto, "Ein-langes-Passwort-2026")).Succeeded);
        Assert.True((await benutzer.AddToRoleAsync(konto, Rollen.Mitglied)).Succeeded);
        return konto;
    }

    [Fact]
    public async Task Loeschlauf_wendet_die_Fristen_an_und_schwaerzt_das_Protokoll()
    {
        var name = "Ll" + Guid.NewGuid().ToString("N")[..6];
        var jetzt = DateTimeOffset.UtcNow;
        Guid ausgetreten, aktiv, minderjaehrig, altKonto, neuKonto, alteRueckmeldung, neueRueckmeldung, alarm;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ausgetreten = (await MitgliedAnlegenAsync(db, name + "Weg", Heute.AddMonths(-4))).Id;
            aktiv = (await MitgliedAnlegenAsync(db, name + "Da", null)).Id;
            var jung = await MitgliedAnlegenAsync(db, name + "Jung", null);
            jung.Geburtsdatum = Heute.AddYears(-15);
            minderjaehrig = jung.Id;
            foreach (var (id, nummer) in new[] { (ausgetreten, 1), (aktiv, 1), (minderjaehrig, 1) })
            {
                db.Erziehungsberechtigte.Add(new Erziehungsberechtigter { MitgliedId = id, Nummer = nummer, Vorname = "Eva", Nachname = name, Mobil = "+4917011111" });
            }

            Pruefhinweis Hinweis(Guid mitglied, string schluessel, PruefhinweisStatus status, int monateAbgeschlossen) => new()
            {
                MitgliedId = mitglied, Art = PruefhinweisArt.Ehrung, Schluessel = schluessel, Text = name, Status = status, ErkanntAm = jetzt.AddMonths(-24),
                AbgeschlossenAm = status == PruefhinweisStatus.Offen ? null : jetzt.AddMonths(-monateAbgeschlossen),
            };
            db.Pruefhinweise.AddRange(
                Hinweis(ausgetreten, "weg", PruefhinweisStatus.Offen, 0),
                Hinweis(aktiv, "alt", PruefhinweisStatus.Entfallen, 13),
                Hinweis(aktiv, "zurueck", PruefhinweisStatus.Zurueckgestellt, 1),
                Hinweis(aktiv, "offen", PruefhinweisStatus.Offen, 0));

            await db.SaveChangesAsync();
            altKonto = (await KontoAnlegenAsync(scope.ServiceProvider, name + "alt@example.test", jetzt.AddMonths(-7))).Id;
            neuKonto = (await KontoAnlegenAsync(scope.ServiceProvider, name + "neu@example.test", jetzt.AddMonths(-1))).Id;

            var alterDienst = new Dienst { Titel = name + " alt", Beginn = jetzt.AddMonths(-13), Ende = jetzt.AddMonths(-13).AddHours(2), Status = DienstStatus.Durchgefuehrt };
            var neuerDienst = new Dienst { Titel = name + " neu", Beginn = jetzt.AddMonths(-4), Ende = jetzt.AddMonths(-4).AddHours(2), Status = DienstStatus.Durchgefuehrt };
            db.Dienste.AddRange(alterDienst, neuerDienst);
            var r1 = new Rueckmeldung { DienstId = alterDienst.Id, MitgliedId = aktiv, Antwort = RueckmeldungAntwort.Absage, Begruendung = "krank", GemeldetAm = alterDienst.Beginn.AddDays(-2) };
            var r2 = new Rueckmeldung { DienstId = neuerDienst.Id, MitgliedId = aktiv, Antwort = RueckmeldungAntwort.Absage, Begruendung = "krank", GemeldetAm = neuerDienst.Beginn.AddDays(-2) };
            db.Rueckmeldungen.AddRange(r1, r2);
            var import = AlarmImport.Empfangen(AlarmQuelle.Fe2Webhook, name, "{}", jetzt.AddMonths(-7), 6);
            db.AlarmImporte.Add(import);

            // L8: Atemschutznachweise bleiben 40 Jahre nach dem letzten Nachweis des Mitglieds, auch über den Austritt hinaus.
            db.AtemschutzNachweise.AddRange(
                new AtemschutzNachweis { MitgliedId = ausgetreten, Datum = Heute.AddYears(-41), Art = AtemschutzNachweisArt.Einsatz, Minuten = 30, Ort = name },
                new AtemschutzNachweis { MitgliedId = ausgetreten, Datum = Heute.AddYears(-42), Art = AtemschutzNachweisArt.Belastungsuebung, Minuten = 25, Ort = name },
                new AtemschutzNachweis { MitgliedId = aktiv, Datum = Heute.AddYears(-41), Art = AtemschutzNachweisArt.Einsatz, Minuten = 30, Ort = name },
                new AtemschutzNachweis { MitgliedId = aktiv, Datum = Heute.AddMonths(-1), Art = AtemschutzNachweisArt.Einsatzuebung, Minuten = 20, Ort = name });
            await db.SaveChangesAsync();
            (alteRueckmeldung, neueRueckmeldung, alarm) = (r1.Id, r2.Id, import.Id);

            // Die Begründung landet seit dem Löschlauf nicht mehr im Protokoll.
            var protokoll = await db.AuditEintraege.SingleAsync(a => a.DatensatzId == r2.Id);
            Assert.DoesNotContain("krank", protokoll.Aenderungen);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelErziehungsberechtigte).Anzahl >= 2);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelPruefhinweise).Anzahl >= 2);
            Assert.All(new[] { Loeschlauf.RegelKontaktdaten, Loeschlauf.RegelGroessen, Loeschlauf.RegelKonten, Loeschlauf.RegelBegruendungen,
                Loeschlauf.RegelRueckmeldungen, Loeschlauf.RegelAlarmdaten }, r => Assert.True(ergebnisse.Single(e => e.Regel == r).Anzahl >= 1, r));
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelAtemschutznachweise).Anzahl >= 2);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var weg = await db.Mitglieder.Include(m => m.Mitgliedschaften).SingleAsync(m => m.Id == ausgetreten);
            Assert.Equal(name + "Weg", weg.Nachname);
            Assert.Single(weg.Mitgliedschaften);
            Assert.Null(weg.Email);
            Assert.Null(weg.Strasse);
            Assert.Null(weg.Notizen);
            Assert.Equal((null, null, null, null), (weg.Arbeitgeber, weg.Beruf, weg.ArbeitgeberTelefon, weg.ArbeitgeberEmail));
            Assert.Equal(Geschlecht.Weiblich, weg.Geschlecht);
            Assert.False(weg.Fe2SyncAktiv);
            Assert.False(await db.Groessenangaben.AnyAsync(g => g.MitgliedId == ausgetreten));

            // L8: beim Ausgetretenen liegt der letzte Nachweis über 40 Jahre zurück, beim Aktiven hält der junge Eintrag auch den alten.
            Assert.False(await db.AtemschutzNachweise.IgnoreQueryFilters().AnyAsync(n => n.MitgliedId == ausgetreten));
            Assert.Equal(2, await db.AtemschutzNachweise.IgnoreQueryFilters().CountAsync(n => n.MitgliedId == aktiv));
            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == ausgetreten).Select(a => a.Aenderungen).ToListAsync();
            Assert.NotEmpty(protokoll);
            Assert.All(protokoll, p => Assert.DoesNotContain("Hauptstraße", p));

            var da = await db.Mitglieder.SingleAsync(m => m.Id == aktiv);
            Assert.Equal("Hauptstraße 1", da.Strasse);
            Assert.True(await db.Groessenangaben.AnyAsync(g => g.MitgliedId == aktiv));

            // L31: abgeschlossene Prüfhinweise nach 12 Monaten, die von Ausgetretenen mit L6.
            var hinweise = await db.Pruefhinweise.IgnoreQueryFilters().Where(p => p.Text == name).Select(p => p.Schluessel).OrderBy(x => x).ToListAsync();
            Assert.Equal(["offen", "zurueck"], hinweise);

            // Erziehungsberechtigte bleiben nur bei Minderjährigen, die noch dabei sind (L15).
            var eltern = await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(e => e.Nachname == name).Select(e => e.MitgliedId).ToListAsync();
            Assert.Equal([minderjaehrig], eltern);

            var alt = await db.Users.SingleAsync(u => u.Id == altKonto);
            Assert.NotNull(alt.AnonymisiertAm);
            Assert.Null(alt.Email);
            Assert.Null(alt.PasswordHash);
            Assert.Null(alt.KalenderToken);
            Assert.StartsWith("geloescht-", alt.UserName);
            Assert.False(await db.UserRoles.AnyAsync(r => r.UserId == altKonto));
            var neu = await db.Users.SingleAsync(u => u.Id == neuKonto);
            Assert.Null(neu.AnonymisiertAm);
            Assert.NotNull(neu.Email);

            Assert.False(await db.Rueckmeldungen.IgnoreQueryFilters().AnyAsync(r => r.Id == alteRueckmeldung));
            var behalten = await db.Rueckmeldungen.SingleAsync(r => r.Id == neueRueckmeldung);
            Assert.Null(behalten.Begruendung);
            Assert.Equal(RueckmeldungAntwort.Absage, behalten.Antwort);
            Assert.False(await db.AlarmImporte.IgnoreQueryFilters().AnyAsync(a => a.Id == alarm));

            Assert.True(await db.Loeschnachweise.AnyAsync(l => l.Regel == Loeschlauf.RegelLauf && l.Zeitpunkt == jetzt));
        }

        // Ein zweiter Lauf findet nichts mehr von diesen Daten.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            Assert.Equal(0, ergebnisse.Single(e => e.Regel == Loeschlauf.RegelKonten).Anzahl);
            Assert.Equal(0, ergebnisse.Single(e => e.Regel == Loeschlauf.RegelKontaktdaten).Anzahl);
        }
    }

    [Fact]
    public void Naechtlicher_Lauf_laeuft_hoechstens_einmal_am_Tag_ab_der_Startstunde()
    {
        // 15.01.: Ortszeit = UTC + 1 Stunde, Startstunde 3 Uhr Ortszeit = 02:00 UTC.
        var nacht = new DateTimeOffset(2027, 1, 15, 2, 0, 0, TimeSpan.Zero);
        Assert.True(Loeschlauf.IstFaellig(nacht, 3, null));
        Assert.False(Loeschlauf.IstFaellig(nacht.AddHours(-1), 3, null));
        Assert.False(Loeschlauf.IstFaellig(nacht.AddHours(2), 3, nacht));
        // Ein Lauf von Hand am Vorabend verhindert den nächtlichen Lauf nicht.
        Assert.True(Loeschlauf.IstFaellig(nacht, 3, nacht.AddHours(-4)));
        // Ein Lauf von Hand kurz nach Mitternacht zählt für den ganzen Tag.
        Assert.False(Loeschlauf.IstFaellig(nacht.AddHours(20), 3, nacht.AddHours(-1.5)));
        Assert.True(Loeschlauf.IstFaellig(nacht.AddDays(1), 3, nacht));
    }

    [Fact]
    public async Task Datenschutzseite_nur_fuer_Administrator_und_Kommando()
    {
        var factory = TestAnmeldung.Factory(fixture);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, null, Rollen.Fuehrungskraft).GetAsync("/Verwaltung/Datenschutz")).StatusCode);

        var admin = Angemeldet(factory, null, Rollen.Administrator);
        var seite = WebUtility.HtmlDecode(await admin.GetStringAsync("/Verwaltung/Datenschutz"));
        Assert.Contains(ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, seite);
        var antwort = await AbschickenAsync(admin, "/Verwaltung/Datenschutz", "Ausfuehren", []);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("von Hand", await admin.GetStringAsync("/Verwaltung/Datenschutz"));
    }

    [Fact]
    public async Task Dokument_endgueltig_loeschen_und_alte_Versionen_entfernen()
    {
        var titel = "Löschtest " + Guid.NewGuid().ToString("N")[..6];
        Guid dokumentId, leser;
        List<Guid> dateien;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ablage = scope.ServiceProvider.GetRequiredService<Dokumentablage>();
            var dokument = new Dokument
            {
                Titel = titel, KategorieId = await db.Dokumentkategorien.Where(k => k.Code == "DA").Select(k => k.Id).SingleAsync(),
                LesebestaetigungErforderlich = true,
            };
            db.Dokumente.Add(dokument);
            foreach (var n in new[] { "eins", "zwei", "drei" })
            {
                await ablage.VersionHinzufuegenAsync(dokument, n + ".txt", new MemoryStream(Encoding.UTF8.GetBytes(n)), null);
            }

            await db.SaveChangesAsync();
            leser = (await KontoAnlegenAsync(scope.ServiceProvider, "leser" + Guid.NewGuid().ToString("N")[..8] + "@example.test", null)).Id;
            foreach (var v in dokument.Versionen)
            {
                await ablage.BestaetigenAsync(v.Id, leser);
            }

            dokumentId = dokument.Id;
            dateien = dokument.Versionen.Select(v => v.DateiId).ToList();
        }

        var factory = TestAnmeldung.Factory(fixture);
        var pfad = $"/Dokumente/Detail/{dokumentId}";
        var schriftfuehrer = Angemeldet(factory, null, Rollen.Schriftfuehrer);
        Assert.DoesNotContain("Endgültig löschen", WebUtility.HtmlDecode(await schriftfuehrer.GetStringAsync(pfad)));
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(schriftfuehrer, pfad, "EndgueltigLoeschen", new() { ["bestaetigt"] = "true" })).StatusCode);

        var kommandant = Angemeldet(factory, null, Rollen.Kommandant);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "AlteVersionenLoeschen", new() { ["bestaetigt"] = "true" })).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var versionen = await db.DokumentVersionen.IgnoreQueryFilters().Where(v => v.DokumentId == dokumentId).ToListAsync();
            Assert.Equal(3, Assert.Single(versionen).Nummer);
            Assert.Equal(1, await db.DokumentDateien.IgnoreQueryFilters().CountAsync(d => dateien.Contains(d.Id)));
            Assert.Equal(1, await db.Lesebestaetigungen.IgnoreQueryFilters().CountAsync(l => l.BenutzerId == leser));
        }

        // Ohne Bestätigung passiert nichts.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, pfad, "EndgueltigLoeschen", [])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await kommandant.GetAsync(pfad)).StatusCode);

        var geloescht = await AbschickenAsync(kommandant, pfad, "EndgueltigLoeschen", new() { ["bestaetigt"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, geloescht.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await kommandant.GetAsync(pfad)).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Dokumente.IgnoreQueryFilters().AnyAsync(d => d.Id == dokumentId));
            Assert.False(await db.DokumentDateien.IgnoreQueryFilters().AnyAsync(d => dateien.Contains(d.Id)));
            Assert.False(await db.Lesebestaetigungen.IgnoreQueryFilters().AnyAsync(l => l.BenutzerId == leser));
            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == dokumentId).Select(a => a.Aenderungen).ToListAsync();
            Assert.NotEmpty(protokoll);
            Assert.All(protokoll, p => Assert.DoesNotContain("Löschtest", p));
            Assert.True(await db.Loeschnachweise.AnyAsync(l => l.Regel == Loeschlauf.RegelDokument && l.Anzahl == 1));
        }
    }

    [Fact]
    public async Task Mitglied_sieht_und_exportiert_die_eigenen_Daten()
    {
        var name = "Sa" + Guid.NewGuid().ToString("N")[..6];
        Guid konto;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mitglied = await MitgliedAnlegenAsync(db, name, null);
            konto = (await KontoAnlegenAsync(scope.ServiceProvider, name + "@example.test", null, mitglied.Id)).Id;
            await MitgliedAnlegenAsync(db, name + "Fremd", null);
            // Als Verantwortlicher beteiligt: nur Bezeichnung und Datum, nicht der Bericht.
            db.Dienste.Add(new Dienst { Titel = name + " Übung", Beginn = DateTimeOffset.UtcNow, Ende = DateTimeOffset.UtcNow.AddHours(2),
                VerantwortlicherId = mitglied.Id, Bericht = "Interner Bericht " + name });
            await db.SaveChangesAsync();
        }

        var client = Angemeldet(TestAnmeldung.Factory(fixture), konto, Rollen.Mitglied);
        var seite = WebUtility.HtmlDecode(await client.GetStringAsync("/Konto/MeineDaten"));
        Assert.Contains(name + "@example.test", seite);
        Assert.Contains("Hauptstraße 1", seite);
        Assert.Contains("Mitgliedschaften", seite);
        Assert.Contains("Gültig ab", seite);
        Assert.Contains("Einsatzabteilung", seite);
        Assert.Contains("Größen", seite);
        Assert.DoesNotContain(name + "Fremd", seite);
        Assert.DoesNotContain("geheim", seite);
        Assert.Contains(name + " Übung", seite);
        Assert.Contains("(als Verantwortlicher)", seite);
        Assert.DoesNotContain("Interner Bericht", seite);

        var export = await client.GetAsync("/Konto/MeineDaten?handler=Export");
        Assert.Equal("application/json", export.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await export.Content.ReadAsStringAsync());
        var titel = json.RootElement.GetProperty("Abschnitte").EnumerateArray().Select(a => a.GetProperty("Titel").GetString()).ToList();
        Assert.Contains("Stammdaten", titel);
        Assert.Contains("Benutzerkonto", titel);
        Assert.Contains(name, json.RootElement.ToString());
    }
}
