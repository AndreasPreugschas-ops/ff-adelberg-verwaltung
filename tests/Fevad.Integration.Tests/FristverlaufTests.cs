using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Verlängern legt neue Datensätze an; ältere bleiben als Verlauf und sind standardmäßig ausgeblendet.</summary>
[Collection(AnwendungCollection.Name)]
public partial class FristverlaufTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static async Task<string> SeiteAsync(HttpClient client, string pfad) => WebUtility.HtmlDecode(await client.GetStringAsync(pfad));

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private sealed record Ausgangslage(Mitglied Mitglied, Guid ArtId, Guid QualifikationId, DateOnly AlterNachweisBis, DateOnly AlteQualifikationBis);

    /// <summary>Mitglied der Einsatzabteilung mit abgelaufener Führerscheinkontrolle und abgelaufener befristeter Qualifikation.</summary>
    private async Task<Ausgangslage> AnlegenAsync(DateOnly heute)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ea = await db.Abteilungen.SingleAsync(a => a.Code == "EA");
        var fsk = await db.Nachweisarten.SingleAsync(a => a.Code == "FSK");
        var nummer = "V" + Guid.NewGuid().ToString("N")[..8];
        var qualifikation = new Qualifikation { Code = "Q" + nummer, Bezeichnung = "Sanitätsdienst " + nummer, Art = QualifikationArt.Sonstiges, GueltigkeitMonate = 24 };
        var mitglied = new Mitglied { Personalnummer = nummer, Vorname = "Vera", Nachname = "Verlauf-" + nummer, Geburtsdatum = new(1988, 7, 3) };
        mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = ea.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1) });
        db.Qualifikationen.Add(qualifikation);
        db.Mitglieder.Add(mitglied);

        var alterNachweisBis = heute.AddMonths(-2).AddDays(-3);
        var alteQualifikationBis = heute.AddMonths(-13).AddDays(-5);
        db.Nachweise.Add(new Nachweis { Mitglied = mitglied, NachweisartId = fsk.Id, Datum = alterNachweisBis.AddMonths(-6), GueltigBis = alterNachweisBis });
        db.QualifikationNachweise.Add(new QualifikationNachweis
        {
            Mitglied = mitglied, Qualifikation = qualifikation, ErworbenAm = alteQualifikationBis.AddMonths(-24), GueltigBis = alteQualifikationBis,
        });
        await db.SaveChangesAsync();
        return new(mitglied, fsk.Id, qualifikation.Id, alterNachweisBis, alteQualifikationBis);
    }

    [Fact]
    public async Task Verlaengern_legt_neue_Datensaetze_an_und_der_alte_bleibt_unveraendert()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var lage = await AnlegenAsync(heute);
        var id = lage.Mitglied.Id;
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        // „verlängern“ aus der Fristenübersicht öffnet das vorbelegte Formular.
        var html = await client.GetStringAsync($"/Personal/Akte/{id}?verlaengern={Fristbereich.Nachweis}&schluessel={lage.ArtId}");
        var nachweis = await client.PostAsync($"/Personal/Akte/{id}?handler=Nachweis", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["NachweisartId"] = lage.ArtId.ToString(),
            ["Datum"] = heute.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        }));
        Assert.Equal(HttpStatusCode.Redirect, nachweis.StatusCode);

        var qualifikation = await client.PostAsync($"/Personal/Akte/{id}?handler=Qualifikation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["QualifikationId"] = lage.QualifikationId.ToString(),
            ["ErworbenAm"] = heute.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        }));
        Assert.Equal(HttpStatusCode.Redirect, qualifikation.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nachweise = await db.Nachweise.Where(n => n.MitgliedId == id).OrderBy(n => n.Datum).ToListAsync();
        Assert.Equal(2, nachweise.Count);
        Assert.Equal(lage.AlterNachweisBis, nachweise[0].GueltigBis);
        Assert.Equal(heute.AddMonths(6), nachweise[1].GueltigBis);

        var qualifikationen = await db.QualifikationNachweise.Where(q => q.MitgliedId == id).OrderBy(q => q.ErworbenAm).ToListAsync();
        Assert.Equal(2, qualifikationen.Count);
        Assert.Equal(lage.AlteQualifikationBis, qualifikationen[0].GueltigBis);
        Assert.Equal(heute.AddMonths(24), qualifikationen[1].GueltigBis);
    }

    [Fact]
    public async Task Verlaengerte_Eintraege_sind_in_der_Akte_ausgeblendet_und_per_Haken_sichtbar()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var lage = await AnlegenAsync(heute);
        await NeueEintraegeAsync(lage, heute);
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        var akte = await SeiteAsync(client, $"/Personal/Akte/{lage.Mitglied.Id}");
        Assert.DoesNotContain(Anzeige.Datum(lage.AlterNachweisBis), akte);
        Assert.DoesNotContain(Anzeige.Datum(lage.AlteQualifikationBis), akte);
        Assert.Contains(Anzeige.Datum(heute.AddMonths(6)), akte);
        Assert.Contains("verlängerte Fristen anzeigen (2)", akte);
        Assert.DoesNotContain("(verlängert)", akte);

        var mitVerlauf = await SeiteAsync(client, $"/Personal/Akte/{lage.Mitglied.Id}?Verlaengerte=true");
        Assert.Contains(Anzeige.Datum(lage.AlterNachweisBis), mitVerlauf);
        Assert.Contains(Anzeige.Datum(lage.AlteQualifikationBis), mitVerlauf);
        Assert.Contains("(verlängert)", mitVerlauf);
        Assert.Matches("<tr class=\"text-gray-400\">\\s*<td>[^<]*Führerscheinkontrolle", mitVerlauf);
    }

    [Fact]
    public async Task Fristenuebersicht_zeigt_verlaengerte_Fristen_nur_mit_Haken()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var lage = await AnlegenAsync(heute);
        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        var suche = $"/Personal?Alle=true&Suche={lage.Mitglied.Nachname}";

        // Vor dem Verlängern: die abgelaufene Kontrolle ist rot und wird angezeigt.
        Assert.Contains(Anzeige.Datum(lage.AlterNachweisBis), await SeiteAsync(client, suche));

        await NeueEintraegeAsync(lage, heute);
        var uebersicht = await SeiteAsync(client, suche);
        Assert.DoesNotContain(Anzeige.Datum(lage.AlterNachweisBis), uebersicht);
        Assert.DoesNotContain(Anzeige.Datum(lage.AlteQualifikationBis), uebersicht);
        Assert.Contains(Anzeige.Datum(heute.AddMonths(6)), uebersicht);

        var mitVerlauf = await SeiteAsync(client, suche + "&Verlaengerte=true");
        Assert.Contains(Anzeige.Datum(lage.AlterNachweisBis), mitVerlauf);
        Assert.Contains(Anzeige.Datum(lage.AlteQualifikationBis), mitVerlauf);
        Assert.Contains(">verlängert</span>", mitVerlauf);

        // Auch ohne „auch gültige Fristen zeigen“ erscheinen die verlängerten mit dem Haken.
        Assert.Contains(Anzeige.Datum(lage.AlterNachweisBis), await SeiteAsync(client, $"/Personal?Verlaengerte=true&Suche={lage.Mitglied.Nachname}"));
    }

    [Fact]
    public async Task Fristen_und_Erinnerungen_werten_nur_den_neuesten_Datensatz()
    {
        var heute = Anzeige.Heute(TimeProvider.System);
        var lage = await AnlegenAsync(heute);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var abfrage = scope.ServiceProvider.GetRequiredService<Personalfristenabfrage>();
        var name = $"{lage.Mitglied.Nachname}, {lage.Mitglied.Vorname}";

        // Vorher erinnert die abgelaufene Kontrolle.
        Assert.Contains(await abfrage.FaelligeAsync(heute, default), h => h.Gegenstand == name && h.Bezeichnung == "Führerscheinkontrolle");

        await NeueEintraegeAsync(lage, heute);
        var fristen = await abfrage.LadenAsync(heute, lage.Mitglied.Id);
        var kontrolle = Assert.Single(fristen, f => f.Bezeichnung == "Führerscheinkontrolle");
        Assert.Equal(Fristampel.Gruen, kontrolle.Ampel);
        Assert.Equal(heute.AddMonths(6), kontrolle.FaelligAm);
        Assert.DoesNotContain(fristen, f => f.Verlaengert);
        Assert.Equal(heute.AddMonths(24), Assert.Single(fristen, f => f.Bereich == Fristbereich.Qualifikation).FaelligAm);

        var faellig = await abfrage.FaelligeAsync(heute, default);
        Assert.DoesNotContain(faellig, h => h.Gegenstand == name && h.Bezeichnung == "Führerscheinkontrolle");
        Assert.DoesNotContain(faellig, h => h.Gegenstand == name && h.Bezeichnung.StartsWith("Sanitätsdienst", StringComparison.Ordinal));

        // Der Verlauf liefert die alten Fristen, immer grün und gekennzeichnet.
        var verlauf = await abfrage.LadenMitVerlaufAsync(heute, lage.Mitglied.Id);
        var alt = Assert.Single(verlauf, f => f.Bezeichnung == "Führerscheinkontrolle" && f.Verlaengert);
        Assert.Equal(lage.AlterNachweisBis, alt.FaelligAm);
        Assert.Equal(Fristampel.Gruen, alt.Ampel);
        Assert.Single(verlauf, f => f.Bereich == Fristbereich.Qualifikation && f.Verlaengert && f.FaelligAm == lage.AlteQualifikationBis);
    }

    /// <summary>Trägt die Verlängerungen direkt als neue Datensätze ein.</summary>
    private async Task NeueEintraegeAsync(Ausgangslage lage, DateOnly heute)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Nachweise.Add(new Nachweis { MitgliedId = lage.Mitglied.Id, NachweisartId = lage.ArtId, Datum = heute, GueltigBis = heute.AddMonths(6) });
        db.QualifikationNachweise.Add(new QualifikationNachweis
        {
            MitgliedId = lage.Mitglied.Id, QualifikationId = lage.QualifikationId, ErworbenAm = heute, GueltigBis = heute.AddMonths(24),
        });
        await db.SaveChangesAsync();
    }
}
