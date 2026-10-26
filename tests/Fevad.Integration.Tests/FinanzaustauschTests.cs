using System.Net;
using System.Security.Claims;
using System.Text;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Rathaus-Rollen und Datenaustausch mit der Kämmerei (P3: V26, V23).</summary>
public partial class EinsatzberichtTests
{
    [Fact]
    public async Task Gemeinderollen_wirken_erst_mit_Verpflichtung()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var fabrik = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<Benutzer>>();
        var email = $"r-{Guid.NewGuid():N}"[..14] + "@gemeinde.test";
        var konto = new Benutzer { UserName = email, Email = email };
        Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
        Assert.True((await benutzer.AddToRolesAsync(konto, [Rollen.Hauptamt, Rollen.Kaemmerei])).Succeeded);

        Assert.DoesNotContain((await fabrik.CreateAsync(konto)).FindAll(ClaimTypes.Role), c => c.Value is Rollen.Hauptamt or Rollen.Kaemmerei);
        konto.VerpflichtetAm = new DateOnly(2026, 10, 26);
        await benutzer.UpdateAsync(konto);
        var rollen = (await fabrik.CreateAsync(konto)).FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains(Rollen.Hauptamt, rollen);
        Assert.Contains(Rollen.Kaemmerei, rollen);
    }

    [Fact]
    public async Task Finanzaustausch_Sollstellung_Spaltenzuordnung_und_Rueckmeldung()
    {
        Guid einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var einsatz = await scope.ServiceProvider.GetRequiredService<Einsatzberichte>()
                .NeuAnlegenAsync(new DateTimeOffset(2055, 5, 6, 8, 0, 0, TimeSpan.Zero));
            einsatz = await db.Einsaetze.SingleAsync(e => e.Id == einsatz.Id);
            einsatz.KostenersatzOffen = true;
            var vorgang = new Kostenersatzvorgang
            {
                EinsatzId = einsatz.Id, Tatbestand = Kostentatbestand.Abs1Nr2Fahrzeug, Entscheidung = KostenersatzErgebnis.Kostenpflichtig, Empfehlung = KostenersatzErgebnis.Kostenpflichtig,
            };
            db.Add(vorgang);
            var schuldner = new Kostenschuldner { KostenersatzvorgangId = vorgang.Id, Name = "Doris Debitor", Art = SchuldnerArt.Halter };
            db.Add(schuldner);
            var bescheid = new Kostenbescheid { KostenersatzvorgangId = vorgang.Id, KostenschuldnerId = schuldner.Id, Kassenzeichen = "KZ-2055-1", FaelligAm = new DateOnly(2055, 7, 1) };
            db.Add(bescheid);
            db.Add(new BescheidEreignis { KostenbescheidId = bescheid.Id, Schritt = BescheidSchritt.Festsetzung, Datum = new DateOnly(2055, 5, 20), Betrag = 200, ErfasstAm = DateTimeOffset.UtcNow });
            db.Add(new BescheidEreignis { KostenbescheidId = bescheid.Id, Schritt = BescheidSchritt.Bekanntgabe, Datum = new DateOnly(2055, 5, 27), ErfasstAm = DateTimeOffset.UtcNow.AddSeconds(1) });
            await db.SaveChangesAsync();
            einsatzId = einsatz.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var abgaben = Angemeldet(factory, Rollen.Abgaben);
        var kaemmerei = Angemeldet(factory, Rollen.Kaemmerei);

        // Abgaben sieht den Zahlungsstatus ohne Namen und richtet keine Formate ein.
        var seite = WebUtility.HtmlDecode(await abgaben.GetStringAsync("/Finanzen?jahr=2055"));
        Assert.Contains("KZ-2055-1", seite);
        Assert.DoesNotContain("Doris Debitor", seite);
        Assert.Equal(HttpStatusCode.Forbidden, (await abgaben.GetAsync("/Finanzen/Format?art=KostenersatzSollstellung")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Mitglied).GetAsync("/Finanzen")).StatusCode);

        // Die Kämmerei stellt die Spalten auf ihr Importformat um; unbekannte Felder werden abgewiesen.
        var format = await kaemmerei.GetStringAsync("/Finanzen/Format?art=KostenersatzSollstellung");
        Dictionary<string, string> Format(string spalten) => new()
        {
            ["__RequestVerificationToken"] = Token(format), ["Formular.Trennzeichen"] = ";", ["Formular.Kodierung"] = "Utf8", ["Formular.Kopfzeile"] = "true",
            ["Formular.DezimalKomma"] = "true", ["Formular.Datumsformat"] = "yyyyMMdd", ["Formular.Spalten"] = spalten, ["Formular.Buchungsstelle"] = "12.60.01",
        };
        var falsch = WebUtility.HtmlDecode(await (await kaemmerei.PostAsync("/Finanzen/Format?art=KostenersatzSollstellung", new FormUrlEncodedContent(Format("Kassenzeichen\nIban")))).Content.ReadAsStringAsync());
        Assert.Contains("Unbekanntes Feld „Iban“", falsch);
        Assert.Equal(HttpStatusCode.Redirect, (await kaemmerei.PostAsync("/Finanzen/Format?art=KostenersatzSollstellung",
            new FormUrlEncodedContent(Format("KZ=Kassenzeichen\nDebitor=Schuldner\nBetrag\nFaelligAm\nBuchungsstelle\nArt=\"SOLL\"")))).StatusCode);

        var csv = Encoding.UTF8.GetString(await abgaben.GetByteArrayAsync("/Finanzen?handler=Export&art=KostenersatzSollstellung&jahr=2055"));
        Assert.Contains("KZ;Debitor;Betrag;FaelligAm;Buchungsstelle;Art", csv);
        Assert.Contains("KZ-2055-1;Doris Debitor;200,00;20550701;12.60.01;SOLL", csv);

        // Rückmeldung der Gemeindekasse: Zahlung über den Rest, unbekanntes Kassenzeichen wird gemeldet.
        var token = Token(await abgaben.GetStringAsync("/Finanzen?jahr=2055"));
        // Die Rückmeldung liest Datumsangaben im Format ihrer eigenen Spaltenzuordnung (Standard dd.MM.yyyy).
        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("Kassenzeichen;Status;Bezahlt;Datum\nKZ-2055-1;bezahlt;200,00;01.07.2055\nKZ-UNBEKANNT;offen;;\n")), "Datei", "rueckmeldung.csv" },
        };
        Assert.Equal(HttpStatusCode.Redirect, (await abgaben.PostAsync("/Finanzen?handler=Rueckmeldung", form)).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bescheid = await db.Kostenbescheide.Include(b => b.Ereignisse).SingleAsync(b => b.Kassenzeichen == "KZ-2055-1");
            Assert.Equal(BescheidStand.Bezahlt, bescheid.Stand);
            Assert.Contains(bescheid.Ereignisse, e => e.Schritt == BescheidSchritt.Zahlung && e.Betrag == 200 && e.Vermerk == "Rückmeldung Gemeindekasse");
            Assert.False((await db.Einsaetze.SingleAsync(e => e.Id == einsatzId)).KostenersatzOffen);
        }
    }
}
