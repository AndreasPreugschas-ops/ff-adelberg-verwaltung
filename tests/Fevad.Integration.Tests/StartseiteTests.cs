using System.Net;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class StartseiteTests(AnwendungFixture fixture)
{
    private HttpClient Angemeldet(params string[] rollen)
    {
        var client = TestAnmeldung.Factory(fixture).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    [Fact]
    public async Task Einstieg_zeigt_nur_Bereiche_mit_Recht_und_keine_Fristen()
    {
        var kommandant = await SeiteAsync(Angemeldet(Rollen.Kommandant), "/");
        Assert.Contains("Bereich wählen", kommandant);
        foreach (var bereich in new[] { "Mannschaft", "Dienstbetrieb", "Ausrüstung", "Verwaltung" })
        {
            Assert.Contains($">{bereich}</div>", kommandant);
        }
        Assert.DoesNotContain("Fristen Personal", kommandant);

        var mannschaft = await SeiteAsync(Angemeldet(Rollen.Kommandant), "/?bereich=Mannschaft");
        Assert.Contains("href=\"/Mitglieder\"", mannschaft);
        Assert.Contains("← Bereiche", mannschaft);

        var mitglied = await SeiteAsync(Angemeldet(Rollen.Mitglied), "/");
        Assert.DoesNotContain("bereich=Mannschaft", mitglied);
        Assert.Contains("bereich=Dienstbetrieb", mitglied);
        Assert.DoesNotContain("href=\"/Personal\"", mitglied);
    }

    [Fact]
    public async Task Abgelaufene_Frist_laesst_sich_aus_der_Akte_verlaengern()
    {
        Guid mitgliedId, artId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
            var art = await db.Nachweisarten.SingleAsync(a => a.Code == "FSK");
            var m = new Mitglied { Personalnummer = "S" + Guid.NewGuid().ToString("N")[..8], Vorname = "Frist", Nachname = "Abgelaufen", Geburtsdatum = new(1990, 1, 1) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2015, 1, 1) });
            db.Mitglieder.Add(m);
            db.Nachweise.Add(new Nachweis { MitgliedId = m.Id, NachweisartId = art.Id, Datum = new(2020, 1, 10), GueltigBis = new(2021, 1, 10) });
            await db.SaveChangesAsync();
            (mitgliedId, artId) = (m.Id, art.Id);
        }

        var client = Angemeldet(Rollen.Kommandant);
        var akte = await SeiteAsync(client, $"/Mitglieder/Akte/{mitgliedId}");
        var link = $"/Personal/Akte/{mitgliedId}?verlaengern=Nachweis&schluessel={artId}#erfassen-nachweis";
        Assert.Contains(link, akte);

        var formular = await SeiteAsync(client, link.Split('#')[0]);
        Assert.Contains("id=\"erfassen-nachweis\" open=\"open\"", formular);
        Assert.Contains($"<option selected=\"selected\" value=\"{artId}\">", formular);


        // Andere Tests zählen die aktuellen Mitglieder der Einsatzabteilung.
        await using var aufraeumen = fixture.Factory.Services.CreateAsyncScope();
        var adb = aufraeumen.ServiceProvider.GetRequiredService<AppDbContext>();
        adb.Mitglieder.Remove(await adb.Mitglieder.SingleAsync(m => m.Id == mitgliedId));
        await adb.SaveChangesAsync();
    }
}
