using System.Net;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Nacharbeit P6 (Andi, 2026-10-26): Ausschussmitglieder lesen den Haushaltsentwurf, der Gerätewart lädt Dokumente hoch.</summary>
[Collection(AnwendungCollection.Name)]
public class AusschussUndGeraetewartTests(AnwendungFixture fixture)
{
    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public async Task Funktion_Ausschuss_bringt_die_Rolle_und_damit_den_Haushalt()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var fabrik = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<Benutzer>>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var ausschuss = await db.Funktionen.SingleAsync(f => f.Code == "AUS");
        Assert.Equal(Rollen.Ausschussmitglied, ausschuss.VorgeschlageneRolle);

        async Task<Benutzer> KontoAsync(DateOnly? bis)
        {
            var m = new Mitglied { Personalnummer = "A" + Guid.NewGuid().ToString("N")[..7], Vorname = "Ada", Nachname = "Ausschuss", Geburtsdatum = Heute.AddYears(-40) };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-5) });
            db.Mitglieder.Add(m);
            db.FunktionZuordnungen.Add(new FunktionZuordnung
            {
                MitgliedId = m.Id, FunktionId = ausschuss.Id, Art = FunktionArt.Wahl, GueltigAb = Heute.AddYears(-1), GueltigBis = bis,
            });
            await db.SaveChangesAsync();
            var email = $"a-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
            var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
            Assert.True((await benutzer.CreateAsync(konto)).Succeeded);
            return konto;
        }

        Assert.True((await fabrik.CreateAsync(await KontoAsync(Heute.AddYears(4)))).IsInRole(Rollen.Ausschussmitglied));
        Assert.False((await fabrik.CreateAsync(await KontoAsync(Heute.AddDays(-1)))).IsInRole(Rollen.Ausschussmitglied));
        Assert.True(await db.RollenRechte.AnyAsync(r => r.Rolle == Rollen.Ausschussmitglied && r.Recht == Berechtigungen.HaushaltLesen));
        Assert.False(await db.RollenRechte.AnyAsync(r => r.Rolle == Rollen.Ausschussmitglied && r.Recht == Berechtigungen.HaushaltZumelden));

        var factory = TestAnmeldung.Factory(fixture);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, $"{Rollen.Mitglied},{Rollen.Ausschussmitglied}");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Haushalt")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Haushalt/Position?jahr={Heute.Year + 1}")).StatusCode);
    }

    [Fact]
    public async Task Geraetewart_darf_Dokumente_hochladen_aber_nicht_nur_fuer_die_Fuehrung()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RollenRechte.AnyAsync(r => r.Rolle == Rollen.Geraetewart && r.Recht == Berechtigungen.DokumenteVerwalten));

        var factory = TestAnmeldung.Factory(fixture);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, Rollen.Geraetewart);
        var seite = await client.GetStringAsync("/Dokumente/Neu");
        var token = WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(seite, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        var kategorie = await db.Dokumentkategorien.Select(k => k.Id).FirstAsync();

        async Task<HttpResponseMessage> HochladenAsync(string titel, string sichtbarkeit)
        {
            using var inhalt = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent(titel), "Formular.Titel" },
                { new StringContent(kategorie.ToString()), "Formular.KategorieId" },
                { new StringContent(sichtbarkeit), "Formular.Sichtbarkeit" },
            };
            inhalt.Add(new ByteArrayContent("Kurzanleitung Hebekissen"u8.ToArray()), "Datei", "bedienungsanleitung.txt");
            return await client.PostAsync("/Dokumente/Neu", inhalt);
        }

        var titel = "Bedienungsanleitung " + Guid.NewGuid().ToString("N")[..6];
        var abgelehnt = await HochladenAsync(titel, "Fuehrung");
        Assert.Equal(HttpStatusCode.OK, abgelehnt.StatusCode);
        Assert.Contains("nur für die Führung", WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await HochladenAsync(titel, "Alle")).StatusCode);
        Assert.True(await db.Dokumente.AnyAsync(d => d.Titel == titel));
    }
}
