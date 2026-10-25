using System.Net;
using Fevad.Domain.Gremien;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Spenden- und Sponsoringregister, Hinweise der Kassenübersicht und Mitgliedssicht (P2: V08, V09).</summary>
public partial class KasseTests
{
    [Fact]
    public async Task Kassier_fuehrt_das_Spendenregister_mit_Annahmebeschluss_und_Dankschreiben()
    {
        var (jugend, giro, _, _, einnahme) = await KasseAsync(Kassenfuehrung.NameJugendkasse);
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var buchung = await BuchenAsync(jugend.Id, giro, einnahme, BuchungArt.Einnahme, 250m, Heute, "Spende Zeltlager " + kennung);
        var kassier = Client(Rollen.Kassier);
        var neu = $"/Kasse/Spende?kasse={jugend.Id}&buchung={buchung.Id}";
        Assert.Contains("Spende Zeltlager " + kennung, WebUtility.HtmlDecode(await kassier.GetStringAsync(neu)));

        // Sache ohne Beschreibung geht nicht; mit Buchung und Zweckbindung schon.
        var felder = new Dictionary<string, string>
        {
            ["Formular.Eingang"] = Heute.ToString("dd.MM.yyyy"), ["Formular.Geber"] = "Autohaus " + kennung, ["Formular.Anschrift"] = "Hauptstraße 1\n73099 Adelberg",
            ["Formular.Art"] = nameof(ZuwendungArt.Sache), ["Formular.Betrag"] = "250,00", ["Formular.Zweck"] = "Zeltlager", ["Formular.Zweckgebunden"] = "true",
            ["Formular.BuchungId"] = buchung.Id.ToString(),
        };
        var antwort = await AbschickenAsync(kassier, neu, neu, felder);
        Assert.Contains("beschreiben, was gegeben wurde", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        felder["Formular.Art"] = nameof(ZuwendungArt.Geld);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kassier, neu, neu, felder)).StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var z = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Zuwendungen.SingleAsync(x => x.Geber == "Autohaus " + kennung);
            Assert.Equal((250m, true, buchung.Id, (DateOnly?)null), (z.Betrag, z.Zweckgebunden, z.BuchungId, z.AnnahmeAm));
            id = z.Id;
        }

        // Ohne Annahmebeschluss: Hinweis im Register und in der Übersicht.
        var register = $"/Kasse/Spenden?kasse={jugend.Id}";
        Assert.Contains("Beschluss fehlt", WebUtility.HtmlDecode(await kassier.GetStringAsync(register)));
        Assert.Contains("ohne Annahmebeschluss des Gemeinderats", WebUtility.HtmlDecode(await kassier.GetStringAsync("/Kasse/Index")));

        // Dankschreiben als PDF vermerkt das Versanddatum.
        var seite = $"/Kasse/Spende?id={id}";
        var pdf = await AbschickenAsync(kassier, seite, seite + "&handler=Dankschreiben", new());
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var z = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Zuwendungen.SingleAsync(x => x.Id == id);
            Assert.Equal(Heute, z.DankschreibenAm);
            var hinweis = DankschreibenPdf.Datenschutzhinweis(new(new("FF Adelberg", null, null, null, null), "Jugendkasse", z, Heute, null, "Gemeinde Adelberg", "dsb@example.org", 10));
            Assert.Contains("Gemeinde Adelberg", hinweis);
            Assert.Contains("10 Jahre", hinweis);
            Assert.Contains("dsb@example.org", hinweis);
        }

        // Liste für die Rechtsaufsicht; das Hauptamt sieht bei der Jugendkasse keine Geber, der Jugendwart erfasst nichts.
        var csv = await kassier.GetStringAsync(register + "&handler=Csv");
        Assert.Contains("Autohaus " + kennung, csv);
        Assert.Contains("250,00", csv);
        var gemeinde = WebUtility.HtmlDecode(await Client(Rollen.GemeindeLesen).GetStringAsync(register));
        Assert.Contains("250,00", gemeinde);
        Assert.DoesNotContain("Autohaus " + kennung, gemeinde);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Jugendwart).GetAsync(seite)).StatusCode);
    }

    [Fact]
    public async Task Uebersicht_mahnt_Abschluss_und_Pruefung_des_Vorjahres_an()
    {
        var vorjahr = Heute.Year - 1;
        var (kasse, giro, einnahme, _) = await EigeneKasseAsync(new DateOnly(vorjahr, 1, 1));
        await BuchenAsync(kasse.Id, giro, einnahme, BuchungArt.Einnahme, 100m, new DateOnly(vorjahr, 5, 1), "Zuwendung");
        var html = WebUtility.HtmlDecode(await Client(Rollen.Kassier).GetStringAsync("/Kasse/Index"));
        var abschnitt = html[html.IndexOf(kasse.Name, StringComparison.Ordinal)..];
        abschnitt = abschnitt[..abschnitt.IndexOf("</section>", StringComparison.Ordinal)];
        Assert.Contains("Jahresabschluss</a> steht aus", abschnitt);
        Assert.Contains("Kassenprüfung</a> fällig", abschnitt);
        Assert.Contains("noch nicht beschlossen", abschnitt);
    }

    [Fact]
    public async Task Mitglieder_lesen_die_Niederschrift_und_freigegebene_Kassenberichte()
    {
        var jahr = Heute.Year - 2;
        var (kasse, giro, einnahme, _) = await EigeneKasseAsync(new DateOnly(jahr, 1, 1));
        await BuchenAsync(kasse.Id, giro, einnahme, BuchungArt.Einnahme, 300m, new DateOnly(jahr, 4, 1), "Zuwendung");
        var kennung = Guid.NewGuid().ToString("N")[..6];
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await scope.ServiceProvider.GetRequiredService<Kassenfuehrung>().FestschreibenAsync(kasse.Id, new DateOnly(jahr, 12, 31), null);
            await scope.ServiceProvider.GetRequiredService<Kassenabschluss>().AbschliessenAsync(kasse, jahr, null, Heute);
            db.Sitzungen.Add(new Sitzung
            {
                Art = SitzungArt.Hauptversammlung, Titel = "Hauptversammlung " + kennung, Beginn = DateTimeOffset.UtcNow.AddDays(-30),
                Status = SitzungStatus.Durchgefuehrt, Protokoll = "TOP 1 Bericht des Kassenwarts " + kennung,
            });
            await db.SaveChangesAsync();
        }

        var mitglied = Client(Rollen.Mitglied);
        var html = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Hauptversammlung/Index"));
        Assert.Contains("Bericht des Kassenwarts " + kennung, html);
        Assert.Contains("noch nicht genehmigt", html);
        Assert.DoesNotContain(kasse.Name, html);
        var pdf = $"/Hauptversammlung/Index?handler=Abschluss&kasse={kasse.Id}&jahr={jahr}";
        Assert.Equal(HttpStatusCode.NotFound, (await mitglied.GetAsync(pdf)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Erziehungsberechtigt).GetAsync("/Hauptversammlung/Index")).StatusCode);

        // Der Kassenwart schaltet die Mitgliedssicht ein.
        var einrichten = "/Kasse/Einrichten";
        var antwort = await AbschickenAsync(Client(Rollen.Kassier), einrichten, $"{einrichten}?handler=Kasse&id={kasse.Id}",
            new() { ["Name"] = kasse.Name, ["Aktiv"] = "true", ["BerichteFuerMitglieder"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        html = WebUtility.HtmlDecode(await mitglied.GetStringAsync("/Hauptversammlung/Index"));
        Assert.Contains(kasse.Name, html);
        Assert.Contains($"Jahresabschluss {jahr} (PDF)", html);
        Assert.Equal("application/pdf", (await mitglied.GetAsync(pdf)).Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Loeschlauf_L43_loescht_alte_Eintraege_im_Spendenregister()
    {
        var (kasse, _, _, _) = await EigeneKasseAsync(new DateOnly(Heute.Year - 12, 1, 1));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alt = new Zuwendung { KasseId = kasse.Id, Geber = "Alter Geber", Eingang = new DateOnly(Heute.Year - 11, 6, 1), Betrag = 50m };
        var neu = new Zuwendung { KasseId = kasse.Id, Geber = "Neuer Geber", Eingang = new DateOnly(Heute.Year - 9, 6, 1), Betrag = 50m };
        db.Zuwendungen.AddRange(alt, neu);
        await db.SaveChangesAsync();

        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelZuwendungen).Anzahl >= 1);
        var uebrig = await db.Zuwendungen.IgnoreQueryFilters().AsNoTracking().Where(z => z.KasseId == kasse.Id).Select(z => z.Geber).ToListAsync();
        Assert.Equal(["Neuer Geber"], uebrig);
    }
}
