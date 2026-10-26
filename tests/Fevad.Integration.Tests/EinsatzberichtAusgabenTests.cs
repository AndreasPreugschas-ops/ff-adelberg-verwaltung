using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Ausgaben des Einsatzberichts: PDF, Anwesenheitsliste, Pressefassung, Papierformular und Mailversand (V18).</summary>
[Collection(AnwendungCollection.Name)]
public partial class EinsatzberichtAusgabenTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private readonly MailSammler _mail = new();
    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle)
    {
        _factory ??= TestAnmeldung.Factory(fixture).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMailVersand>(_mail)));
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, Guid.NewGuid().ToString());
        return client;
    }

    private async Task<Guid> EinsatzAsync(string nachname, EinsatzStatus status = EinsatzStatus.Freigegeben)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = new Mitglied { Personalnummer = "E" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kim", Nachname = nachname, Geburtsdatum = new DateOnly(1990, 1, 1) };
        db.Mitglieder.Add(m);
        var e = new Einsatz
        {
            Einsatznummer = $"A-{Guid.NewGuid().ToString("N")[..8]}", Stichwort = "B 2", StichwortBeschreibung = "Brand Gebäude",
            Strasse = "Hauptstraße", Hausnummer = "17", Plz = "73099", Ort = "Adelberg", Ortsteil = "Hundsholz",
            Alarmiert = DateTimeOffset.UtcNow.AddHours(-5), Einsatzende = DateTimeOffset.UtcNow.AddHours(-3), Status = status,
            FreigegebenAm = status == EinsatzStatus.Freigegeben ? DateTimeOffset.UtcNow.AddHours(-1) : null,
            StatistikKategorieId = await db.StatistikKategorien.Where(k => k.Code == "BR").Select(k => k.Id).SingleAsync(),
        };
        e.Teilnahmen.Add(new EinsatzTeilnahme { MitgliedId = m.Id, Funktion = "Angriffstrupp", Atemschutz = true });
        e.Beteiligte.Add(new EinsatzBeteiligter { Rolle = BeteiligtenRolle.Verursacher, Name = "Max Verursacher", Kennzeichen = "GP-XY 123" });
        db.Einsaetze.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string handler, IEnumerable<KeyValuePair<string, string>> felder)
    {
        var html = await client.GetStringAsync(seite);
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)) };
        liste.AddRange(felder);
        return await client.PostAsync($"{seite}?handler={handler}", new FormUrlEncodedContent(liste));
    }

    [Fact]
    public void Pressevorschlag_ohne_Hausnummer_und_Namen()
    {
        var e = new Einsatz
        {
            Einsatznummer = "2026-001", Stichwort = "TH 1", StichwortBeschreibung = "Verkehrsunfall", Strasse = "Hauptstraße", Hausnummer = "17",
            Ort = "Adelberg", Alarmiert = new DateTimeOffset(2026, 11, 3, 17, 5, 0, TimeSpan.Zero), Verletzte = 1,
            Einsatzleiter = new Mitglied { Personalnummer = "1", Vorname = "Kim", Nachname = "Geheim", Geburtsdatum = new DateOnly(1980, 1, 1) },
        };
        e.Teilnahmen.Add(new EinsatzTeilnahme());
        e.Teilnahmen.Add(new EinsatzTeilnahme());
        var text = EinsatzberichtPdf.Pressevorschlag(e, "Freiwillige Feuerwehr Adelberg");
        Assert.Contains("18:05 Uhr", text);
        Assert.Contains("„Verkehrsunfall“", text);
        Assert.Contains("(Adelberg)", text);
        Assert.Contains("2 Einsatzkräfte", text);
        Assert.Contains("Eine Person wurde verletzt.", text);
        Assert.DoesNotContain("17", text.Replace("17:", ""));
        Assert.DoesNotContain("Hauptstraße", text);
        Assert.DoesNotContain("Geheim", text);
    }

    [Fact]
    public async Task Pdf_Anwesenheit_und_Papierformular_mit_Rechten()
    {
        var id = await EinsatzAsync("Pdf" + Guid.NewGuid().ToString("N")[..6]);
        var kommandant = Client(Rollen.Kommandant);
        foreach (var url in new[] { $"/Einsaetze/Ausgaben/{id}?handler=Pdf", $"/Einsaetze/Ausgaben/{id}?handler=Pdf&teilnehmer=true&beteiligte=true",
                     $"/Einsaetze/Ausgaben/{id}?handler=Anwesenheit", "/Einsaetze/Papierformular" })
        {
            var antwort = await kommandant.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
            Assert.Equal("application/pdf", antwort.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await antwort.Content.ReadAsByteArrayAsync())[..4]));
        }

        // Führungskräfte lesen Berichte, sehen aber keine Beteiligten und geben nichts frei.
        var fuehrung = Client(Rollen.Fuehrungskraft);
        Assert.Equal(HttpStatusCode.Forbidden, (await fuehrung.GetAsync($"/Einsaetze/Ausgaben/{id}?handler=Pdf&beteiligte=true")).StatusCode);
        var seite = WebUtility.HtmlDecode(await fuehrung.GetStringAsync($"/Einsaetze/Ausgaben/{id}"));
        Assert.DoesNotContain("Speichern und freigeben", seite);
        Assert.DoesNotContain("Per Mail versenden", seite);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync($"/Einsaetze/Ausgaben/{id}")).StatusCode);
    }

    [Fact]
    public async Task Pressefassung_freigeben_und_Aenderung_hebt_Freigabe_auf()
    {
        var id = await EinsatzAsync("Pr" + Guid.NewGuid().ToString("N")[..6]);
        var kommandant = Client(Rollen.Kommandant);
        var seite = $"/Einsaetze/Ausgaben/{id}";
        Assert.Contains("Brand Gebäude", WebUtility.HtmlDecode(await kommandant.GetStringAsync(seite)));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Presse", [new("Pressetext", "Brand in Adelberg."), new("freigeben", "true")])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var e = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Einsaetze.SingleAsync(x => x.Id == id);
            Assert.Equal("Brand in Adelberg.", e.Pressetext);
            Assert.NotNull(e.PresseFreigegebenAm);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Presse", [new("Pressetext", "Brand in Adelberg, geändert.")])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var e = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Einsaetze.SingleAsync(x => x.Id == id);
            Assert.Null(e.PresseFreigegebenAm);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Fuehrungskraft), seite, "Presse", [new("Pressetext", "x")])).StatusCode);
    }

    [Fact]
    public async Task Versand_nur_freigegeben_mit_Anhang_und_Vermerk()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var o = await db.Organisationen.OrderBy(x => x.ErstelltAm).FirstAsync();
            o.EinsatzberichtEmpfaenger = "hauptamt@gemeinde.test; kbm@kreis.test";
            await db.SaveChangesAsync();
        }

        var entwurf = await EinsatzAsync("Ve" + Guid.NewGuid().ToString("N")[..6], EinsatzStatus.InBearbeitung);
        var kommandant = Client(Rollen.Kommandant);
        Assert.Contains("erst der freigegebene Bericht", WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Einsaetze/Ausgaben/{entwurf}")));

        var id = await EinsatzAsync("Vf" + Guid.NewGuid().ToString("N")[..6]);
        var seite = $"/Einsaetze/Ausgaben/{id}";
        Assert.Contains("kbm@kreis.test", WebUtility.HtmlDecode(await kommandant.GetStringAsync(seite)));
        var versand = await AbschickenAsync(kommandant, seite, "Versenden",
            [new("an", "hauptamt@gemeinde.test"), new("an", "fremd@nicht-in-stammdaten.test"), new("weitere", "polizei@revier.test")]);
        Assert.Equal(HttpStatusCode.Redirect, versand.StatusCode);
        Assert.Equal(["hauptamt@gemeinde.test", "polizei@revier.test"], _mail.Gesendet.Where(m => m.Betreff.Contains("Einsatzbericht")).Select(m => m.Empfaenger).Order());
        Assert.All(_mail.Gesendet, m => Assert.Equal("application/pdf", Assert.Single(m.Anhaenge).Inhaltstyp));
        Assert.DoesNotContain("personenbezogene", _mail.Gesendet[0].Text);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var v = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EinsatzberichtVersaende.SingleAsync(x => x.EinsatzId == id);
            Assert.False(v.MitTeilnehmern);
            Assert.False(v.MitBeteiligten);
            Assert.Contains("polizei@revier.test", v.Empfaenger);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(Client(Rollen.Fuehrungskraft), seite, "Versenden", [new("weitere", "x@y.test")])).StatusCode);

        // Mit Beteiligten nur an Standardempfänger: freie Adressen werden abgewiesen, nichts geht raus.
        var vorher = _mail.Gesendet.Count;
        await AbschickenAsync(kommandant, seite, "Versenden",
            [new("an", "kbm@kreis.test"), new("weitere", "versicherung@frei.test"), new("beteiligte", "true")]);
        Assert.Equal(vorher, _mail.Gesendet.Count);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "Versenden",
            [new("an", "kbm@kreis.test"), new("beteiligte", "true")])).StatusCode);
        Assert.Equal("kbm@kreis.test", _mail.Gesendet[^1].Empfaenger);
        Assert.Contains("personenbezogene", _mail.Gesendet[^1].Text);
    }

    private sealed class MailSammler : IMailVersand
    {
        public List<(string Empfaenger, string Betreff, string Text, IReadOnlyList<MailAnhang> Anhaenge)> Gesendet { get; } = [];

        public bool Aktiv => true;

        public Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default) => SendenAsync(empfaenger, betreff, text, [], ct);

        public Task SendenAsync(string empfaenger, string betreff, string text, IReadOnlyList<MailAnhang> anhaenge, CancellationToken ct = default)
        {
            lock (Gesendet)
            {
                Gesendet.Add((empfaenger, betreff, text, anhaenge));
            }

            return Task.CompletedTask;
        }
    }
}
