using System.Net;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Abwesenheit melden und nachträglich entschuldigen (V37).</summary>
public partial class DienstplanungTests
{
    private static DateOnly HeuteLokal => Dienstplanung.LokalesDatum(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Abwesenheit_sagt_Dienste_ab_der_Kommandant_sieht_sie_und_Ruecknahme_hebt_das_auf()
    {
        var (mitgliedId, benutzerId) = await MitgliedMitKontoAsync("EA", "Urlauber");
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        var von = HeuteLokal.AddDays(400);
        var vorher = await DienstAnlegenAsync("Vorher " + Guid.NewGuid().ToString("N")[..6], "EA", Dienstplanung.AlsUtc(von.AddDays(2), new(19, 30)));
        await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{vorher.Id}?handler=Rueckmelden", new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) });

        var antwort = await AbschickenAsync(mitglied, "/Termine/Abwesenheit", "/Termine/Abwesenheit", new()
        {
            ["Formular.Von"] = von.ToString("yyyy-MM-dd"),
            ["Formular.Bis"] = von.AddDays(20).ToString("yyyy-MM-dd"),
            ["Formular.Art"] = nameof(AbwesenheitArt.Beruf),
            ["Formular.Bemerkung"] = "Montage",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        // Später geplanter Dienst im Zeitraum bekommt die Absage gleich mit.
        var titel = "Nachher " + Guid.NewGuid().ToString("N")[..6];
        await AbschickenAsync(Angemeldet(null, Rollen.Fuehrungskraft), "/Dienste", "/Dienste", new()
        {
            ["Neu.Titel"] = titel, ["Neu.Art"] = nameof(DienstArt.Uebung), ["Neu.Datum"] = von.AddDays(10).ToString("yyyy-MM-dd"),
            ["Neu.Beginn"] = "19:30", ["Neu.Ende"] = "21:30", ["Neu.RueckmeldungErbeten"] = "true", ["Neu.ZaehltFuerFortbildung"] = "true",
        });

        Guid abwesenheitId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nachher = await db.Dienste.SingleAsync(d => d.Titel == titel);
            var absagen = await db.Rueckmeldungen.Where(r => r.MitgliedId == mitgliedId).ToListAsync();
            Assert.Equal(2, absagen.Count);
            Assert.All(absagen, r =>
            {
                Assert.Equal(RueckmeldungAntwort.Absage, r.Antwort);
                Assert.Equal(Dienstplanung.AbwesenheitBegruendung, r.Begruendung);
                Assert.NotNull(r.AbwesenheitId);
            });
            Assert.Contains(absagen, r => r.DienstId == nachher.Id);
            Assert.Equal(AnwesenheitStatus.Entschuldigt, (await db.Anwesenheiten.SingleAsync(a => a.DienstId == vorher.Id && a.MitgliedId == mitgliedId)).Status);
            abwesenheitId = absagen[0].AbwesenheitId!.Value;
        }

        // Drei Wochen: anzeigepflichtig. Der Kommandant sieht Grund und Bemerkung, die Führungskraft die Seite nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Fuehrungskraft).GetAsync("/Dienste/Abwesenheiten")).StatusCode);
        var kommandant = Angemeldet(null, Rollen.Kommandant);
        var liste = await kommandant.GetStringAsync("/Dienste/Abwesenheiten");
        Assert.Contains("Urlauber", liste);
        Assert.Contains("Montage", liste);
        Assert.Contains("Zur Kenntnis genommen", liste);
        await AbschickenAsync(kommandant, "/Dienste/Abwesenheiten", $"/Dienste/Abwesenheiten?handler=Kenntnis&abwesenheitId={abwesenheitId}", []);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await db.Abwesenheiten.SingleAsync(a => a.Id == abwesenheitId)).KenntnisGenommenAm);
        }

        // Übungsleiter sehen nur „Abwesenheit gemeldet“, nie den Grund.
        var termin = await Angemeldet(null, Rollen.Fuehrungskraft).GetStringAsync($"/Termine/Termin/{vorher.Id}");
        Assert.Contains(Dienstplanung.AbwesenheitBegruendung, termin);
        Assert.DoesNotContain("Montage", termin);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(mitglied, "/Termine/Abwesenheit",
            $"/Termine/Abwesenheit?handler=Zuruecknehmen&abwesenheitId={abwesenheitId}", [])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Abwesenheiten.AnyAsync(a => a.Id == abwesenheitId));
            Assert.False(await db.Rueckmeldungen.AnyAsync(r => r.MitgliedId == mitgliedId));
            Assert.False(await db.Anwesenheiten.AnyAsync(a => a.MitgliedId == mitgliedId));
        }
    }

    [Fact]
    public async Task Abwesenheit_fremder_Mitglieder_laesst_sich_nicht_zuruecknehmen()
    {
        var (_, benutzerId) = await MitgliedMitKontoAsync("EA", "Eigen");
        var (fremdId, _) = await MitgliedMitKontoAsync("EA", "Fremd");
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var planung = scope.ServiceProvider.GetRequiredService<Dienstplanung>();
            var abwesenheit = new Abwesenheit { MitgliedId = fremdId, Von = HeuteLokal.AddDays(500), Bis = HeuteLokal.AddDays(501), Art = AbwesenheitArt.Urlaub };
            await planung.AbwesenheitMeldenAsync(abwesenheit);
            id = abwesenheit.Id;
        }

        var antwort = await AbschickenAsync(Angemeldet(benutzerId, Rollen.Mitglied), "/Termine/Abwesenheit",
            $"/Termine/Abwesenheit?handler=Zuruecknehmen&abwesenheitId={id}", []);
        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);
    }

    [Fact]
    public async Task Nach_dem_Dienst_ist_eine_Entschuldigung_bis_zum_Folgetag_moeglich()
    {
        var (mitgliedId, benutzerId) = await MitgliedMitKontoAsync("EA", "Nachzuegler");
        var mitglied = Angemeldet(benutzerId, Rollen.Mitglied);
        var gestern = await DienstAnlegenAsync("Gestern " + Guid.NewGuid().ToString("N")[..6], "EA", Dienstplanung.AlsUtc(HeuteLokal.AddDays(-1), new(19, 0)));
        var unentschuldigt = await DienstAnlegenAsync("Unentschuldigt " + Guid.NewGuid().ToString("N")[..6], "EA", Dienstplanung.AlsUtc(HeuteLokal.AddDays(-1), new(10, 0)));
        var alt = await DienstAnlegenAsync("Alt " + Guid.NewGuid().ToString("N")[..6], "EA", Dienstplanung.AlsUtc(HeuteLokal.AddDays(-3), new(19, 0)));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Anwesenheiten.Add(new Anwesenheit { DienstId = unentschuldigt.Id, MitgliedId = mitgliedId, Status = AnwesenheitStatus.Unentschuldigt, ErfasstUeber = ErfassungsWeg.Web });
            await db.SaveChangesAsync();
        }

        Assert.Contains("Nachträglich entschuldigen", await mitglied.GetStringAsync("/Termine"));

        // Zusagen geht nach dem Schluss nicht mehr, entschuldigen schon.
        await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{gestern.Id}?handler=Rueckmelden", new() { ["antwort"] = nameof(RueckmeldungAntwort.Zusage) });
        await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{gestern.Id}?handler=Rueckmelden", new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });
        await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{unentschuldigt.Id}?handler=Rueckmelden", new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });
        await AbschickenAsync(mitglied, "/Termine", $"/Termine/Termin/{alt.Id}?handler=Rueckmelden", new() { ["antwort"] = nameof(RueckmeldungAntwort.Absage) });

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(RueckmeldungAntwort.Absage, (await db.Rueckmeldungen.SingleAsync(r => r.DienstId == gestern.Id && r.MitgliedId == mitgliedId)).Antwort);
            Assert.Equal(AnwesenheitStatus.Entschuldigt, (await db.Anwesenheiten.SingleAsync(a => a.DienstId == gestern.Id && a.MitgliedId == mitgliedId)).Status);
            Assert.Equal(AnwesenheitStatus.Entschuldigt, (await db.Anwesenheiten.SingleAsync(a => a.DienstId == unentschuldigt.Id && a.MitgliedId == mitgliedId)).Status);
            Assert.False(await db.Rueckmeldungen.AnyAsync(r => r.DienstId == alt.Id));
        }
    }
}
