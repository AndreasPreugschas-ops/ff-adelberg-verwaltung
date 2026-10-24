using Fevad.Web.Infrastruktur;
using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class LehrgangTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private HttpClient Angemeldet(params string[] rollen)
    {
        var client = TestAnmeldung.Factory(fixture).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    private static async Task<HttpResponseMessage> AbsendenAsync(HttpClient client, string seite, Dictionary<string, string> felder)
    {
        felder["__RequestVerificationToken"] = TokenFeld().Match(await SeiteAsync(client, seite)).Groups[1].Value;
        return await client.PostAsync(seite, new FormUrlEncodedContent(felder));
    }

    [Fact]
    public async Task Lehrgang_von_der_Anmeldung_bis_zum_Nachweis()
    {
        Guid mitgliedId, sprf;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = new Mitglied { Personalnummer = "L" + Guid.NewGuid().ToString("N")[..8], Vorname = "Lena", Nachname = "Lehrgang", Geburtsdatum = new(1998, 2, 3) };
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            (mitgliedId, sprf) = (m.Id, (await db.Qualifikationen.SingleAsync(q => q.Code == "SPRF")).Id);
        }

        var client = Angemeldet(Rollen.Kommandant);
        var neu = $"/Personal/Lehrgang/{mitgliedId}";
        var angemeldet = await AbsendenAsync(client, neu, new()
        {
            ["Formular.QualifikationId"] = sprf.ToString(), ["Formular.AngemeldetAm"] = Heute.AddDays(-20).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, angemeldet.StatusCode);
        var teilnahme = await MitDbAsync(db => db.Lehrgangsteilnahmen.AsNoTracking().SingleAsync(t => t.MitgliedId == mitgliedId));

        // Oben in der Mitgliederakte als offener Lehrgang.
        var akte = await SeiteAsync(client, $"/Mitglieder/Akte/{mitgliedId}");
        Assert.Contains("Sprechfunker: angemeldet", akte);

        // Zuweisung vor der Anmeldung geht nicht.
        var bearbeiten = $"/Personal/Lehrgang/{mitgliedId}/{teilnahme.Id}";
        Dictionary<string, string> Felder(bool bestanden, bool abgeschlossen, DateOnly zugewiesen) => new()
        {
            ["Formular.Id"] = teilnahme.Id.ToString(), ["Formular.QualifikationId"] = sprf.ToString(),
            ["Formular.AngemeldetAm"] = Heute.AddDays(-20).ToString("yyyy-MM-dd"), ["Formular.ZugewiesenAm"] = zugewiesen.ToString("yyyy-MM-dd"),
            ["Formular.Von"] = Heute.AddDays(-6).ToString("yyyy-MM-dd"), ["Formular.Bis"] = Heute.AddDays(-2).ToString("yyyy-MM-dd"),
            ["Formular.Bestanden"] = bestanden ? "true" : "false", ["Formular.Abgeschlossen"] = abgeschlossen ? "true" : "false",
        };
        var falsch = await AbsendenAsync(client, bearbeiten, Felder(false, false, Heute.AddDays(-30)));
        Assert.Contains("Zugewiesen kann nicht vor der Anmeldung liegen.", WebUtility.HtmlDecode(await falsch.Content.ReadAsStringAsync()));

        // Vorbei ohne Ergebnis, dann nicht bestanden: kein Nachweis.
        await AbsendenAsync(client, bearbeiten, Felder(false, false, Heute.AddDays(-15)));
        Assert.Contains("Sprechfunker: vorbei, Ergebnis fehlt", await SeiteAsync(client, $"/Mitglieder/Akte/{mitgliedId}"));
        await AbsendenAsync(client, bearbeiten, Felder(false, true, Heute.AddDays(-15)));
        Assert.False(await MitDbAsync(db => db.QualifikationNachweise.AnyAsync(n => n.MitgliedId == mitgliedId)));
        Assert.DoesNotContain("Sprechfunker: ", await SeiteAsync(client, $"/Mitglieder/Akte/{mitgliedId}"));

        // Bestanden und abgeschlossen: Nachweis zum letzten Lehrgangstag; zurückgenommen fällt er weg.
        await AbsendenAsync(client, bearbeiten, Felder(true, true, Heute.AddDays(-15)));
        var nachweis = await MitDbAsync(db => db.QualifikationNachweise.AsNoTracking().SingleAsync(n => n.MitgliedId == mitgliedId));
        Assert.Equal((sprf, Heute.AddDays(-2)), (nachweis.QualifikationId, nachweis.ErworbenAm));
        var personal = await SeiteAsync(client, $"/Personal/Akte/{mitgliedId}");
        Assert.Contains("bestanden", personal);
        Assert.Contains("Lehrgänge und Qualifikationen", personal);
        Assert.Contains("Abzeichen", personal);

        await AbsendenAsync(client, bearbeiten, Felder(true, false, Heute.AddDays(-15)));
        Assert.False(await MitDbAsync(db => db.QualifikationNachweise.AnyAsync(n => n.MitgliedId == mitgliedId)));

        // Ohne Recht auf Ausbildung keine Lehrgangsseite.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Mitglied).GetAsync(neu)).StatusCode);
    }

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
