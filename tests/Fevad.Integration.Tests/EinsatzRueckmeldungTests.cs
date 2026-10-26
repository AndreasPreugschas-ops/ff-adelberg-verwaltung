using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Zeitbestätigung durch die Teilnehmer und Anhänge am Einsatzbericht (V18).</summary>
[Collection(AnwendungCollection.Name)]
public partial class EinsatzRueckmeldungTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle, Guid? konto = null)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, (konto ?? Guid.NewGuid()).ToString());
        return client;
    }

    private async Task<(Guid EinsatzId, Guid TeilnahmeId, Guid KontoId)> EinsatzAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = new Mitglied { Personalnummer = "Q" + Guid.NewGuid().ToString("N")[..8], Vorname = "Kim", Nachname = "Rueck", Geburtsdatum = new DateOnly(1990, 1, 1) };
        db.Mitglieder.Add(m);
        var e = new Einsatz
        {
            Einsatznummer = $"R-{Guid.NewGuid().ToString("N")[..8]}", Stichwort = "TH 1", Ort = "Adelberg",
            Alarmiert = DateTimeOffset.UtcNow.AddHours(-5), Einsatzende = DateTimeOffset.UtcNow.AddHours(-3), Status = EinsatzStatus.InBearbeitung,
        };
        var t = new EinsatzTeilnahme { MitgliedId = m.Id, Funktion = "Maschinist" };
        e.Teilnahmen.Add(t);
        db.Einsaetze.Add(e);
        await db.SaveChangesAsync();
        var email = $"q-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var k = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(k)).Succeeded);
        return (e.Id, t.Id, k.Id);
    }

    private static async Task<HttpResponseMessage> AbschickenAsync(HttpClient client, string seite, string? handler, IEnumerable<KeyValuePair<string, string>> felder)
    {
        var html = await client.GetStringAsync(seite);
        var liste = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)) };
        liste.AddRange(felder);
        return await client.PostAsync(handler is null ? seite : $"{seite}?handler={handler}", new FormUrlEncodedContent(liste));
    }

    private static async Task<HttpResponseMessage> HochladenAsync(HttpClient client, string seite, string dateiname, byte[] inhalt)
    {
        var html = await client.GetStringAsync(seite);
        var form = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value)), "__RequestVerificationToken" },
            { new StringContent(nameof(EinsatzAnhangArt.Foto)), "art" },
            { new StringContent("Lage bei Eintreffen"), "beschreibung" },
        };
        var datei = new ByteArrayContent(inhalt);
        datei.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(datei, "datei", dateiname);
        return await client.PostAsync($"{seite}?handler=Anhang", form);
    }

    private static byte[] Segment(byte marker, byte[] daten)
    {
        var s = new byte[4 + daten.Length];
        s[0] = 0xFF;
        s[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(2), (ushort)(daten.Length + 2));
        daten.CopyTo(s, 4);
        return s;
    }

    [Fact]
    public async Task Teilnehmer_bestaetigt_oder_meldet_Korrektur_Zeitaenderung_hebt_Bestaetigung_auf()
    {
        var (einsatzId, teilnahmeId, konto) = await EinsatzAsync();
        var kommandant = Client(Rollen.Kommandant);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, $"/Einsaetze/Ausgaben/{einsatzId}", "BestaetigungAnfordern", [])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var b = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Benachrichtigungen.SingleAsync(x => x.EmpfaengerId == konto);
            Assert.Equal("/Konto/Einsaetze", b.Link);
        }

        var selbst = Client(Rollen.Mitglied, konto);
        var seite = WebUtility.HtmlDecode(await selbst.GetStringAsync("/Konto/Einsaetze"));
        Assert.Contains("Maschinist", seite);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Einsaetze", null, [new("teilnahmeId", teilnahmeId.ToString())])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.EinsatzTeilnahmen.SingleAsync(x => x.Id == teilnahmeId);
            Assert.True(t.Bestaetigt);
            Assert.NotNull(t.RueckmeldungAm);
            Assert.NotNull((await db.Benachrichtigungen.SingleAsync(x => x.EmpfaengerId == konto)).GelesenAm);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(selbst, "/Konto/Einsaetze", null,
            [new("teilnahmeId", teilnahmeId.ToString()), new("korrektur", "Ich war erst ab 17:30 Uhr dabei.")])).StatusCode);
        Assert.Contains("Ich war erst ab 17:30 Uhr dabei.", WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Einsaetze/Bericht/{einsatzId}")));

        // Fremde Teilnahmen kann niemand bestätigen.
        Assert.Equal(HttpStatusCode.NotFound, (await AbschickenAsync(Client(Rollen.Mitglied, (await EinsatzAsync()).KontoId), "/Konto/Einsaetze", null,
            [new("teilnahmeId", teilnahmeId.ToString())])).StatusCode);
    }

    [Fact]
    public async Task Fotos_ohne_Metadaten_nur_erlaubte_Typen_und_nur_mit_Recht()
    {
        var (einsatzId, _, _) = await EinsatzAsync();
        var seite = $"/Einsaetze/Ausgaben/{einsatzId}";
        var kommandant = Client(Rollen.Kommandant);
        var jfif = Segment(0xE0, Encoding.ASCII.GetBytes("JFIF\0\x01\x01"));
        var exif = Segment(0xE1, Encoding.ASCII.GetBytes("Exif\0\0GPS 48.76 9.59"));
        var scan = Segment(0xDA, [1, 2, 3]).Concat(new byte[] { 0x11, 0x22, 0xFF, 0xD9 }).ToArray();
        var foto = new byte[] { 0xFF, 0xD8 }.Concat(jfif).Concat(exif).Concat(scan).ToArray();

        Assert.Equal(HttpStatusCode.Redirect, (await HochladenAsync(kommandant, seite, "lage.jpg", foto)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await HochladenAsync(kommandant, seite, "makro.docm", [1, 2, 3])).StatusCode);
        Guid anhangId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EinsatzAnhaenge.SingleAsync(x => x.EinsatzId == einsatzId);
            Assert.Equal("lage.jpg", a.Dateiname);
            Assert.DoesNotContain("GPS", Encoding.ASCII.GetString(a.Inhalt));
            anhangId = a.Id;
        }

        var abruf = await kommandant.GetAsync($"{seite}?handler=Anhang&anhangId={anhangId}");
        Assert.Equal("image/jpeg", abruf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await HochladenAsync(Client(Rollen.GemeindeLesen), seite, "lage.jpg", foto)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Rollen.Mitglied).GetAsync($"{seite}?handler=Anhang&anhangId={anhangId}")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, seite, "AnhangLoeschen", [new("anhangId", anhangId.ToString())])).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().EinsatzAnhaenge.AnyAsync(x => x.EinsatzId == einsatzId));
        }
    }
}
