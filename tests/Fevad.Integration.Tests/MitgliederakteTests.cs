using System.Net;
using System.Text.RegularExpressions;
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
public partial class MitgliederakteTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Angemeldet(params string[] rollen)
    {
        var client = TestAnmeldung.Factory(fixture).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    /// <summary>Mitglied mit Jugendfeuerwehr (Übertritt) und Einsatzabteilung, Leistungsspange und Funktion.</summary>
    private async Task<(Guid Id, string Name)> UebergetretenAnlegenAsync()
    {
        var name = "Akte" + Guid.NewGuid().ToString("N")[..6];
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jugend = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Jugend);
        var einsatz = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var spange = await db.Qualifikationen.SingleAsync(q => q.Code == Qualifikation.CodeLeistungsspange);
        var funktion = await db.Funktionen.OrderBy(f => f.Sortierung).FirstAsync();
        var m = new Mitglied { Personalnummer = "A" + Guid.NewGuid().ToString("N")[..8], Vorname = "Test", Nachname = name, Geburtsdatum = new(2000, 5, 1) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jugend.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2011, 3, 1), GueltigBis = new(2017, 4, 30), Austrittsgrund = Austrittsgrund.Uebertritt });
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = einsatz.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2017, 5, 1) });
        m.Funktionen.Add(new FunktionZuordnung { FunktionId = funktion.Id, GueltigAb = new(2022, 1, 1) });
        db.Mitglieder.Add(m);
        db.QualifikationNachweise.Add(new QualifikationNachweis { MitgliedId = m.Id, QualifikationId = spange.Id, ErworbenAm = new(2016, 7, 9) });
        await db.SaveChangesAsync();
        return (m.Id, name);
    }

    [Fact]
    public async Task Akte_zeigt_Jugendzeit_und_Reiter_nach_Recht()
    {
        var (id, name) = await UebergetretenAnlegenAsync();

        var kommandant = await SeiteAsync(Angemeldet(Rollen.Kommandant), $"/Mitglieder/Akte/{id}");
        Assert.Contains(name, kommandant);
        Assert.Contains("01.03.2011", kommandant);
        Assert.Contains("30.04.2017 (Übertritt)", kommandant);
        Assert.Contains("09.07.2016", kommandant);
        Assert.Contains($"/Laufbahn/Mitglied/{id}", kommandant);
        Assert.Contains($"/Personal/Akte/{id}", kommandant);
        Assert.Contains($"/Mitglieder/Bearbeiten/{id}", kommandant);

        // Der Kassier liest Mitglieder, aber weder Laufbahn noch Ausbildung.
        var kassier = await SeiteAsync(Angemeldet(Rollen.Kassier), $"/Mitglieder/Akte/{id}");
        Assert.Contains("Jugendfeuerwehr", kassier);
        Assert.DoesNotContain($"/Laufbahn/Mitglied/{id}", kassier);
        Assert.DoesNotContain("Ehrungen", kassier);

        // Der Jugendwart sieht Mitglieder der Einsatzabteilung nicht.
        Assert.Equal(HttpStatusCode.NotFound, (await Angemeldet(Rollen.Jugendwart).GetAsync($"/Mitglieder/Akte/{id}")).StatusCode);

        // Die Mitgliederliste führt zur Akte.
        Assert.Contains($"/Mitglieder/Akte/{id}", await SeiteAsync(Angemeldet(Rollen.Kommandant), $"/Mitglieder?Suche={name}"));
    }

    [Fact]
    public async Task Erziehungsberechtigte_werden_gespeichert_und_geleert()
    {
        var name = "Eb" + Guid.NewGuid().ToString("N")[..6];
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jugend = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Jugend);
            var m = new Mitglied { Personalnummer = "E" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kind", Nachname = name, Geburtsdatum = Heute.AddYears(-12) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = jugend.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            id = m.Id;
        }

        var client = Angemeldet(Rollen.Jugendwart);
        async Task<HttpResponseMessage> SpeichernAsync(Dictionary<string, string> eltern)
        {
            var seite = await client.GetStringAsync($"/Mitglieder/Bearbeiten/{id}");
            var felder = new Dictionary<string, string>(eltern)
            {
                ["Formular.Id"] = id.ToString(), ["Formular.Personalnummer"] = "P" + name, ["Formular.Vorname"] = "Kind",
                ["Formular.Nachname"] = name, ["Formular.Geburtsdatum"] = Heute.AddYears(-12).ToString("yyyy-MM-dd"),
                ["Formular.Version"] = Regex.Match(seite, "name=\"Formular.Version\" value=\"(\\d+)\"").Groups[1].Value,
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value),
            };
            return await client.PostAsync($"/Mitglieder/Bearbeiten/{id}", new FormUrlEncodedContent(felder));
        }

        var ohneName = await SpeichernAsync(new() { ["Formular.Erziehungsberechtigte[0].Mobil"] = "+491701234567" });
        Assert.Equal(HttpStatusCode.OK, ohneName.StatusCode);
        Assert.Contains("Vor- und Nachname angeben", WebUtility.HtmlDecode(await ohneName.Content.ReadAsStringAsync()));

        var gespeichert = await SpeichernAsync(new()
        {
            ["Formular.Erziehungsberechtigte[0].Verhaeltnis"] = nameof(Sorgeverhaeltnis.Mutter),
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Anna", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
            ["Formular.Erziehungsberechtigte[0].Mobil"] = "+491701234567",
            ["Formular.Erziehungsberechtigte[1].Vorname"] = "Bernd", ["Formular.Erziehungsberechtigte[1].Nachname"] = name,
        });
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);
        Assert.Contains($"/Mitglieder/Akte/{id}", gespeichert.Headers.Location!.ToString());
        Assert.Contains("Anna " + name + " (Mutter)", await SeiteAsync(client, $"/Mitglieder/Akte/{id}"));

        // Leere Felder entfernen den zweiten Eintrag.
        await SpeichernAsync(new()
        {
            ["Formular.Erziehungsberechtigte[0].Vorname"] = "Anna", ["Formular.Erziehungsberechtigte[0].Nachname"] = name,
        });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eltern = await db.Erziehungsberechtigte.Where(e => e.MitgliedId == id).ToListAsync();
            Assert.Equal(("Anna", 1, (string?)null), (Assert.Single(eltern).Vorname, eltern[0].Nummer, eltern[0].Mobil));
        }
    }
}
