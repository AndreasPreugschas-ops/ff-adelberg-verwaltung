using System.Net;
using System.Text.RegularExpressions;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FfAdelberg.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public class KontoTests(AnwendungFixture fixture)
{
    private const string Passwort = "Feuerwehr-Test-2026";

    [Fact]
    public async Task Anmeldeseite_ist_deutsch()
    {
        var html = await new Browser(fixture).SeiteAsync("/Konto/Anmelden");
        Assert.Contains("Passwort vergessen?", html);
        Assert.Contains("Angemeldet bleiben", html);
    }

    [Fact]
    public async Task Falsches_Passwort_wird_abgelehnt()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Mitglied);
        var antwort = await new Browser(fixture).AnmeldenAsync(konto.Email!, "falsch-falsch-falsch");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("E-Mail-Adresse oder Passwort ist falsch.", await antwort.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mitglied_ohne_Pflichtrolle_kommt_ohne_Zwei_Faktor_rein_aber_nicht_in_die_Benutzerverwaltung()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Mitglied);
        var browser = new Browser(fixture);
        await browser.VollstaendigAnmeldenAsync(konto.Email!, Passwort);

        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/")).StatusCode);

        // Die englische Identity-Standardoberfläche samt Selbstregistrierung gibt es nicht mehr.
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Identity/Account/Register")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Identity/Account/Manage")).StatusCode);

        var verwaltung = await browser.Client.GetAsync("/Verwaltung/Benutzer");
        Assert.Equal(HttpStatusCode.Redirect, verwaltung.StatusCode);
        Assert.Contains("/Konto/KeinZugriff", verwaltung.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Pflichtrolle_muss_erst_Authenticator_einrichten()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Kommandant);
        var browser = new Browser(fixture);
        await browser.VollstaendigAnmeldenAsync(konto.Email!, Passwort);

        var gesperrt = await browser.Client.GetAsync("/Mitglieder");
        Assert.Equal(HttpStatusCode.Redirect, gesperrt.StatusCode);
        Assert.Equal("/Konto/Authenticator", gesperrt.Headers.Location!.ToString());

        var html = await browser.SeiteAsync("/Konto/Authenticator");
        Assert.Contains("<svg", html);
        var schluessel = Regex.Match(html, "<code[^>]*>([a-z2-7 ]+)</code>").Groups[1].Value;

        var bestaetigt = await browser.AbschickenAsync("/Konto/Authenticator", new Dictionary<string, string> { ["Code"] = Browser.Totp(schluessel) });
        Assert.Equal(HttpStatusCode.Redirect, bestaetigt.StatusCode);
        Assert.Equal("/Konto/Wiederherstellungscodes", bestaetigt.Headers.Location!.ToString());
        Assert.Equal(10, Regex.Matches(await browser.SeiteAsync("/Konto/Wiederherstellungscodes"), "<li class=\"rounded bg-gray-100").Count);

        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/Mitglieder")).StatusCode);

        // Nächste Anmeldung verlangt den Code.
        var neu = new Browser(fixture);
        var antwort = await neu.AnmeldenAsync(konto.Email!, Passwort);
        Assert.Contains("/Konto/AnmeldenCode", antwort.Headers.Location!.ToString());
        await neu.VollstaendigAnmeldenAsync(konto.Email!, Passwort, schluessel);
        Assert.Equal(HttpStatusCode.OK, (await neu.Client.GetAsync("/Mitglieder")).StatusCode);
    }

    [Fact]
    public async Task Administrator_legt_Konto_an_und_Link_setzt_das_Passwort()
    {
        var (admin, schluessel) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: true, Rollen.Administrator);
        var browser = new Browser(fixture);
        await browser.VollstaendigAnmeldenAsync(admin.Email!, Passwort, schluessel);

        var email = $"neu-{Guid.NewGuid():N}"[..12] + "@ff-adelberg.test";
        var angelegt = await browser.AbschickenAsync("/Verwaltung/Benutzer/Bearbeiten", new Dictionary<string, string>
        {
            ["Formular.Email"] = email,
            ["Formular.Rollen"] = Rollen.Mitglied,
        });
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);

        var seite = await browser.SeiteAsync(angelegt.Headers.Location!.ToString());
        var link = Regex.Match(seite, "value=\"(http[^\"]+/Konto/PasswortSetzen[^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(link);
        var pfad = new Uri(WebUtility.HtmlDecode(link)).PathAndQuery;

        var neu = new Browser(fixture);
        var gesetzt = await neu.AbschickenAsync(pfad, new Dictionary<string, string> { ["Passwort"] = Passwort, ["Wiederholung"] = Passwort });
        Assert.Equal(HttpStatusCode.Redirect, gesetzt.StatusCode);
        await neu.VollstaendigAnmeldenAsync(email, Passwort);

        // Der Link gilt nur einmal.
        Assert.Contains("ungültig", await neu.SeiteAsync(pfad));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var konto = await db.Users.SingleAsync(b => b.Email == email);
        Assert.NotNull(konto.LetzteAnmeldungAm);
        var protokoll = await db.AuditEintraege.Where(a => a.DatensatzId == konto.Id).ToListAsync();
        Assert.Contains(protokoll, a => a.Aktion == AuditAktion.Angelegt && a.BenutzerId == admin.Id && a.Aenderungen!.Contains(email));
        Assert.Contains(protokoll, a => a.Tabelle == "AspNetUserRoles" && a.BenutzerId == admin.Id && a.Aenderungen!.Contains(Rollen.Mitglied));

        // Passwort setzen geschieht ohne Anmeldung; der Hash steht nicht im Protokoll.
        var passwort = Assert.Single(protokoll, a => a.Aktion == AuditAktion.Geaendert && a.Aenderungen!.Contains("PasswordHash"));
        Assert.Null(passwort.BenutzerId);
        Assert.DoesNotContain("AQAAAA", passwort.Aenderungen);
    }

    [Fact]
    public async Task Deaktiviertes_Konto_kann_sich_nicht_anmelden()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Mitglied);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var geladen = (await benutzer.FindByIdAsync(konto.Id.ToString()))!;
            geladen.DeaktiviertAm = DateTimeOffset.UtcNow;
            await benutzer.UpdateAsync(geladen);
        }

        var antwort = await new Browser(fixture).AnmeldenAsync(konto.Email!, Passwort);
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("deaktiviert", await antwort.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Eigene_Administratorrolle_kann_nicht_entfernt_werden()
    {
        var (admin, schluessel) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: true, Rollen.Administrator);
        var browser = new Browser(fixture);
        await browser.VollstaendigAnmeldenAsync(admin.Email!, Passwort, schluessel);

        var antwort = await browser.AbschickenAsync($"/Verwaltung/Benutzer/Bearbeiten/{admin.Id}", new Dictionary<string, string>
        {
            ["Formular.Id"] = admin.Id.ToString(),
            ["Formular.Email"] = admin.Email!,
            ["Formular.Rollen"] = Rollen.Mitglied,
        });

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("Die eigene Rolle Administrator kann nicht entfernt werden.", await antwort.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Laufende_Sitzung_endet_nach_Deaktivierung()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Mitglied);
        var browser = new Browser(fixture);
        await browser.VollstaendigAnmeldenAsync(konto.Email!, Passwort);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/Konto")).StatusCode);

        // Nur das Feld setzen, ohne neuen Sicherheitsstempel: die Sitzung muss trotzdem enden.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var geladen = (await benutzer.FindByIdAsync(konto.Id.ToString()))!;
            geladen.DeaktiviertAm = DateTimeOffset.UtcNow;
            await benutzer.UpdateAsync(geladen);
        }

        var antwort = await browser.Client.GetAsync("/Konto");
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("/Konto/Anmelden", antwort.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Passwort_zuruecksetzen_hebt_vorlaeufige_Sperre_auf()
    {
        var (konto, _) = await Browser.KontoAnlegenAsync(fixture, Passwort, zweiFaktor: false, Rollen.Mitglied);
        string code;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var geladen = (await benutzer.FindByIdAsync(konto.Id.ToString()))!;
            await benutzer.SetLockoutEndDateAsync(geladen, DateTimeOffset.UtcNow.AddMinutes(15));
            code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
                System.Text.Encoding.UTF8.GetBytes(await benutzer.GeneratePasswordResetTokenAsync(geladen)));
        }

        var browser = new Browser(fixture);
        const string neu = "Neues-Passwort-2026";
        var gesetzt = await browser.AbschickenAsync($"/Konto/PasswortSetzen?id={konto.Id}&code={code}",
            new Dictionary<string, string> { ["Passwort"] = neu, ["Wiederholung"] = neu });
        Assert.Equal(HttpStatusCode.Redirect, gesetzt.StatusCode);

        await browser.VollstaendigAnmeldenAsync(konto.Email!, neu);
    }
}
