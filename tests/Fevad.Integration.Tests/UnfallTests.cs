using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class UnfallTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private static readonly DateOnly Heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(params string[] rollen)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string pfad, Dictionary<string, string> felder)
    {
        var seite = await client.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenFeld().Match(seite).Groups[1].Value);
        return await client.PostAsync(pfad, new FormUrlEncodedContent(felder));
    }

    private async Task<Guid> MitgliedAsync(string nachname)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied { Personalnummer = "U" + Guid.NewGuid().ToString("N")[..8], Vorname = "Uli", Nachname = nachname, Geburtsdatum = Heute.AddYears(-30) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact]
    public async Task Fuehrungskraft_erfasst_Kommandant_meldet_und_druckt_ohne_Namen_im_Dateinamen()
    {
        var name = "Unf" + Guid.NewGuid().ToString("N")[..6];
        var mitglied = await MitgliedAsync(name);

        var fuehrung = Client(Rollen.Fuehrungskraft);
        var antwort = await AbschickenAsync(fuehrung, "/Unfaelle/Bearbeiten", new()
        {
            ["Formular.MitgliedId"] = mitglied.ToString(), ["Formular.Datum"] = Heute.AddDays(-1).ToString("dd.MM.yyyy"), ["Formular.Uhrzeit"] = "19:45",
            ["Formular.Anlass"] = nameof(UnfallAnlass.Dienst), ["Formular.Ort"] = "Übungsgelände", ["Formular.Hergang"] = "Auf nasser Leiter abgerutscht",
            ["Formular.VerletzteKoerperteile"] = "rechter Knöchel", ["Formular.ArtDerVerletzung"] = "Verstauchung", ["Formular.MehrAlsDreiTage"] = "true",
            ["Formular.Zeugen[0].Name"] = "Erika Muster", ["Formular.Zeugen[0].Augenzeuge"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Unfaelle.Include(x => x.Zeugen).SingleAsync(x => x.MitgliedId == mitglied);
            id = u.Id;
            Assert.Equal(UnfallStatus.Erfasst, u.Status);
            Assert.Equal("19:45", Anzeige.Lokal(u.Zeitpunkt).ToString("HH:mm"));
            Assert.Equal("Erika Muster", Assert.Single(u.Zeugen).Name);

            // Verletzung und Hergang stehen nicht im Änderungsprotokoll.
            var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == id).Select(a => a.Aenderungen).ToListAsync();
            Assert.All(protokoll, p => Assert.DoesNotContain("Knöchel", p ?? ""));
            Assert.All(protokoll, p => Assert.DoesNotContain("Leiter", p ?? ""));
        }

        // Wer nur erfassen darf, sieht den Unfall danach nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await fuehrung.GetAsync($"/Unfaelle/Bearbeiten/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fuehrung.GetAsync("/Unfaelle")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync("/Unfaelle/Bearbeiten")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.GemeindeLesen).GetAsync("/Unfaelle")).StatusCode);

        var kommandant = Client(Rollen.Kommandant);
        var liste = await kommandant.GetStringAsync("/Unfaelle");
        Assert.Contains(name, liste);
        Assert.DoesNotContain("Knöchel", liste);

        var pdf = await kommandant.GetAsync($"/Unfaelle/Bearbeiten/{id}?handler=Pdf");
        Assert.True(pdf.Content.Headers.ContentType?.MediaType == "application/pdf", (int)pdf.StatusCode + await pdf.Content.ReadAsStringAsync());
        var datei = pdf.Content.Headers.ContentDisposition?.FileNameStar ?? pdf.Content.Headers.ContentDisposition?.FileName ?? "";
        Assert.StartsWith("Unfallanzeige-", datei.Trim('"'));
        Assert.DoesNotContain(name, datei);

        var seite = await kommandant.GetStringAsync($"/Unfaelle/Bearbeiten/{id}");
        var version = Regex.Match(seite, "name=\"Formular.Version\" value=\"(\\d+)\"").Groups[1].Value;
        antwort = await AbschickenAsync(kommandant, $"/Unfaelle/Bearbeiten/{id}", new()
        {
            ["Formular.Version"] = version, ["Formular.MitgliedId"] = mitglied.ToString(), ["Formular.Datum"] = Heute.AddDays(-1).ToString("dd.MM.yyyy"),
            ["Formular.Uhrzeit"] = "19:45", ["Formular.Anlass"] = nameof(UnfallAnlass.Dienst), ["Formular.Ort"] = "Übungsgelände",
            ["Formular.Hergang"] = "Auf nasser Leiter abgerutscht", ["Formular.MehrAlsDreiTage"] = "true",
            ["Formular.Status"] = nameof(UnfallStatus.Gemeldet), ["Formular.Aktenzeichen"] = "UK-123",
        });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Unfaelle.Include(x => x.Zeugen).SingleAsync(x => x.Id == id);
            Assert.Equal((UnfallStatus.Gemeldet, Heute, "UK-123"), (u.Status, u.GemeldetAm!.Value, u.Aktenzeichen));
            Assert.Empty(u.Zeugen);
        }
    }

    [Fact]
    public async Task Loeschlauf_loescht_abgeschlossene_Unfaelle_nach_der_Frist()
    {
        var mitglied = await MitgliedAsync("Ul" + Guid.NewGuid().ToString("N")[..6]);
        Guid alt, neu;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Unfall Neu(int jahreAbgeschlossen) => new()
            {
                MitgliedId = mitglied, Ort = "Halle", Hergang = "gestolpert", Zeitpunkt = DateTimeOffset.UtcNow.AddYears(-jahreAbgeschlossen - 1),
                Status = UnfallStatus.Abgeschlossen, AbgeschlossenAm = Heute.AddYears(-jahreAbgeschlossen).AddDays(-1),
                Zeugen = [new UnfallZeuge { Name = "Zeuge" }],
            };
            var a = Neu(5);
            var b = Neu(4);
            db.Unfaelle.AddRange(a, b);
            await db.SaveChangesAsync();
            (alt, neu) = (a.Id, b.Id);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var ergebnisse = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
            Assert.True(ergebnisse.Single(e => e.Regel == Loeschlauf.RegelUnfaelle).Anzahl >= 1);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Unfaelle.IgnoreQueryFilters().AnyAsync(u => u.Id == alt));
            Assert.False(await db.UnfallZeugen.IgnoreQueryFilters().AnyAsync(z => z.UnfallId == alt));
            Assert.True(await db.Unfaelle.AnyAsync(u => u.Id == neu));
        }
    }
}
