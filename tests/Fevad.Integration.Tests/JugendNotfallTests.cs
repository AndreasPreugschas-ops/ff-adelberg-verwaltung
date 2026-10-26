using System.Net;
using static System.Net.WebUtility;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Seed;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class JugendNotfallTests(AnwendungFixture fixture)
{
    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _factory = TestAnmeldung.Factory(fixture);

    private HttpClient Angemeldet(Guid? benutzerId, params string[] rollen)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        if (benutzerId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, benutzerId.ToString());
        }

        return client;
    }

    /// <summary>Kind in der Jugendfeuerwehr mit Heimweg, Notfallmappe und Elternkonto; bis ist das Ende der Mitgliedschaft.</summary>
    private async Task<(Guid Kind, Guid Eltern)> KindAsync(string name, DateOnly? bis = null, DateOnly? mappe = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var jf = await db.Abteilungen.SingleAsync(a => a.Code == "JF");
        var kind = new Mitglied
        {
            Personalnummer = "N-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Lea", Nachname = name, Geburtsdatum = Heute.AddYears(-11),
            Abholregelung = Abholregelung.WirdAbgeholt, Abholberechtigte = "Oma Inge 0171 555", NotfallmappeAm = mappe ?? Heute.AddMonths(-2),
            NotfallkontaktName = "Onkel Tom", NotfallkontaktTelefon = "0171 777",
        };
        kind.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jf.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1), GueltigBis = bis });
        db.Mitglieder.Add(kind);
        var email = $"n-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        db.Erziehungsberechtigte.Add(new Erziehungsberechtigter
        {
            MitgliedId = kind.Id, Nummer = 1, Vorname = "Petra", Nachname = name, Mobil = "0171 999", KontoId = konto.Id, KontoZugeordnetAm = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (kind.Id, konto.Id);
    }

    [Fact]
    public async Task Notfallliste_fuer_Jugendwart_und_Betreuer_aber_nicht_fuer_den_Geraetewart()
    {
        var name = "Notf" + Guid.NewGuid().ToString("N")[..6];
        var (kind, _) = await KindAsync(name);
        await KindAsync(name + "alt", mappe: Heute.AddMonths(-13));

        foreach (var rolle in new[] { Rollen.Jugendwart, Rollen.BetreuerJugend, Rollen.Kommandant })
        {
            var seite = HtmlDecode(await Angemeldet(null, rolle).GetStringAsync("/Jugend/Notfallliste"));
            Assert.Contains($"{name}, Lea", seite);
            Assert.Contains("wird abgeholt", seite);
            Assert.Contains("Oma Inge 0171 555", seite);
            Assert.Contains("Petra " + name + ": 0171 999", seite);
            Assert.Contains("Onkel Tom 0171 777", seite);
            Assert.Contains($"liegt vor ({Anzeige.Datum(Heute.AddMonths(-2))})", seite);
            Assert.Contains("erneuern (vom", seite);
        }

        var pdf = await Angemeldet(null, Rollen.BetreuerJugend).GetAsync("/Jugend/Notfallliste?handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(name, pdf.Content.Headers.ContentDisposition?.FileName ?? "");

        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Geraetewart).GetAsync("/Jugend/Notfallliste")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.Mitglied).GetAsync("/Jugend/Notfallliste")).StatusCode);

        // Der Betreuer sieht keine Akten, der Jugendwart pflegt die Angaben dort.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(null, Rollen.BetreuerJugend).GetAsync($"/Mitglieder/Akte/{kind}")).StatusCode);
        Assert.Contains("Heimweg nach dem Dienst", HtmlDecode(await Angemeldet(null, Rollen.Jugendwart).GetStringAsync($"/Mitglieder/Bearbeiten/{kind}")));
    }

    [Fact]
    public async Task Eltern_sehen_die_Notfallangaben_nur_ihres_Kindes()
    {
        var name = "Elt" + Guid.NewGuid().ToString("N")[..6];
        var (_, eltern) = await KindAsync(name);
        var (_, andere) = await KindAsync(name + "x");

        var client = Angemeldet(eltern, Rollen.Erziehungsberechtigt);
        Assert.Contains("Heimweg und Notfallangaben meiner Kinder", HtmlDecode(await client.GetStringAsync("/Konto")));
        var seite = HtmlDecode(await client.GetStringAsync("/Konto/Notfallangaben"));
        Assert.Contains($"Lea {name}", seite);
        Assert.Contains("Oma Inge 0171 555", seite);
        Assert.DoesNotContain($"Lea {name}x", seite);
        var fremd = HtmlDecode(await Angemeldet(andere, Rollen.Erziehungsberechtigt).GetStringAsync("/Konto/Notfallangaben"));
        Assert.Contains($"Lea {name}x", fremd);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(fremd, "<section"));
    }

    [Fact]
    public async Task L54_leert_die_Angaben_nach_dem_Ende_in_der_Jugendfeuerwehr()
    {
        var name = "L54" + Guid.NewGuid().ToString("N")[..6];
        var (aktiv, _) = await KindAsync(name);
        var (ausgetreten, _) = await KindAsync(name + "a", bis: Heute.AddDays(-1));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelNotfallangaben).Anzahl >= 1);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alt = await db.Mitglieder.AsNoTracking().IgnoreQueryFilters().SingleAsync(m => m.Id == ausgetreten);
        Assert.Equal((null, null, null), (alt.Abholregelung, alt.Abholberechtigte, alt.NotfallmappeAm));
        Assert.Equal(Abholregelung.WirdAbgeholt, (await db.Mitglieder.AsNoTracking().SingleAsync(m => m.Id == aktiv)).Abholregelung);
    }

    [Fact]
    public async Task Neue_Rolle_bekommt_auch_die_Vorbelegung_mit_Abteilungsbezug()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.RollenRechte.IgnoreQueryFilters().Where(r => r.Rolle == Rollen.BetreuerJugend).ExecuteDeleteAsync();
            await db.Rollenprofile.IgnoreQueryFilters().Where(r => r.Name == Rollen.BetreuerJugend).ExecuteDeleteAsync();
        }

        // Wie ein Update, das die Rolle neu mitbringt: die Rechte sind schon eingeführt.
        await DatenbankInitialisierung.InitialisierenAsync(fixture.Factory.Services);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<Rechteverzeichnis>().Verwerfen();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jf = await db.Abteilungen.Where(a => a.Code == "JF").Select(a => a.Id).SingleAsync();
            var rechte = await db.RollenRechte.AsNoTracking().Where(r => r.Rolle == Rollen.BetreuerJugend).ToListAsync();
            Assert.Equal([Berechtigungen.AnwesenheitLesen, Berechtigungen.JugendNotfallliste], rechte.Select(r => r.Recht).Order());
            Assert.All(rechte, r => Assert.Equal([jf], r.Abteilungen));
        }
    }
}
