using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Fevad.Integration.Tests;

/// <summary>
/// Oberflächentests im echten Browser (V71): je Rolle die Startseite und alle Einträge des Hauptmenüs aufrufen, ohne Serverfehler,
/// ohne Skriptfehler in der Konsole und ohne waagrechtes Scrollen am Handy. Dazu die Suche im Kopf und die Betriebsseite.
/// Braucht Chromium für Playwright (lokal PLAYWRIGHT_BROWSERS_PATH, in der CI über playwright.ps1 installiert).
/// </summary>
[Collection(AnwendungCollection.Name)]
[Trait("Art", "Oberflaeche")]
public sealed class OberflaechenTests(AnwendungFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private string _adresse = null!;

    public async Task InitializeAsync()
    {
        // Ohne Development-Umgebung findet MapStaticAssets die Dateien aus wwwroot und obj nur über das Manifest der Static Web Assets.
        _factory = TestAnmeldung.Factory(fixture).WithWebHostBuilder(b => b.UseStaticWebAssets());
        _factory.UseKestrel(0);
        _factory.StartServer();
        _adresse = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Aktives Mitglied der Einsatzabteilung mit eigenem Konto, damit auch „Mein Konto“ und die Kontaktliste laden.</summary>
    private async Task<Guid> KontoAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilung = await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz);
        var m = new Mitglied { Personalnummer = "O" + Guid.NewGuid().ToString("N")[..8], Vorname = "Olga", Nachname = "Oberfläche", Geburtsdatum = new DateOnly(1985, 3, 1) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new DateOnly(2010, 1, 1) });
        db.Mitglieder.Add(m);
        await db.SaveChangesAsync();
        var email = $"o-{Guid.NewGuid():N}"[..14] + "@ff-adelberg.test";
        var konto = new Benutzer { UserName = email, Email = email, MitgliedId = m.Id };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().CreateAsync(konto)).Succeeded);
        return konto.Id;
    }

    private async Task<(IPage Seite, List<string> Fehler)> SeiteAsync(string rollen, bool handy = false)
    {
        var konto = await KontoAsync();
        var kontext = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _adresse,
            ExtraHTTPHeaders = new Dictionary<string, string> { [TestAnmeldung.RollenHeader] = rollen, [TestAnmeldung.BenutzerHeader] = konto.ToString() },
            ViewportSize = handy ? new ViewportSize { Width = 320, Height = 640 } : new ViewportSize { Width = 1280, Height = 900 },
            Locale = "de-DE",
            TimezoneId = "Europe/Berlin",
        });
        var seite = await kontext.NewPageAsync();
        var fehler = new List<string>();
        seite.PageError += (_, text) => fehler.Add($"{seite.Url}: {text}");
        seite.Console += (_, meldung) =>
        {
            if (meldung.Type == "error")
            {
                fehler.Add($"{seite.Url}: {meldung.Text}");
            }
        };
        return (seite, fehler);
    }

    /// <summary>Kernpfade je Rolle: alles, was das Menü der Rolle anbietet, lädt fehlerfrei.</summary>
    [Theory]
    [InlineData(Rollen.Mitglied)]
    [InlineData(Rollen.Kommandant)]
    [InlineData(Rollen.Geraetewart)]
    [InlineData(Rollen.Kassier)]
    [InlineData(Rollen.Jugendwart)]
    [InlineData(Rollen.Hauptamt)]
    [InlineData(Rollen.Administrator)]
    public async Task Menue_der_Rolle_laedt_ohne_Fehler(string rolle)
    {
        var (seite, fehler) = await SeiteAsync(rolle);
        var antwort = await seite.GotoAsync("/");
        Assert.Equal(200, antwort!.Status);

        var ziele = (await seite.Locator("nav a[href^='/']").EvaluateAllAsync<string[]>("links => links.map(a => a.getAttribute('href'))"))
            .Where(z => !z.StartsWith("/Konto/Abmelden", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        Assert.NotEmpty(ziele);

        var kaputt = new List<string>();
        foreach (var ziel in ziele)
        {
            var a = await seite.GotoAsync(ziel);
            if (a is null || a.Status >= 400)
            {
                kaputt.Add($"{ziel}: {a?.Status}");
            }
        }

        Assert.Empty(kaputt);
        Assert.Empty(fehler);
    }

    /// <summary>Einsatzkraft am Handy (320 px, entspricht 1280 px bei 400 % Zoom, WCAG 1.4.10): Seiten passen in die Breite.</summary>
    [Fact]
    public async Task Einsatzkraft_am_Handy_ohne_waagrechtes_Scrollen()
    {
        var (seite, fehler) = await SeiteAsync(Rollen.Mitglied, handy: true);
        foreach (var pfad in new[] { "/", "/Konto/Index", "/Termine/Index" })
        {
            var antwort = await seite.GotoAsync(pfad);
            Assert.True(antwort!.Status < 400, $"{pfad}: {antwort.Status}");
            var breite = await seite.EvaluateAsync<int>("document.documentElement.scrollWidth");
            Assert.True(breite <= 320 + 1, $"{pfad} ist {breite} px breit.");
        }

        Assert.Empty(fehler);
    }

    /// <summary>Suche im Seitenkopf führt auf die Ergebnisseite.</summary>
    [Fact]
    public async Task Suche_im_Kopf_fuehrt_zur_Ergebnisseite()
    {
        var (seite, fehler) = await SeiteAsync(Rollen.Kommandant);
        await seite.GotoAsync("/");
        await seite.Locator("#kopfsuche").FillAsync("HLF");
        await seite.Locator("#kopfsuche").PressAsync("Enter");
        await seite.WaitForURLAsync("**/Suche?q=HLF*");
        Assert.Contains("HLF", await seite.Locator("main").InnerTextAsync());
        Assert.Empty(fehler);
    }

    /// <summary>Betriebsseite zeigt Antwortzeiten, nachdem Seiten aufgerufen wurden.</summary>
    [Fact]
    public async Task Betriebsseite_zeigt_Antwortzeiten()
    {
        var (seite, fehler) = await SeiteAsync(Rollen.Administrator);
        await seite.GotoAsync("/");
        var antwort = await seite.GotoAsync("/Verwaltung/Betrieb");
        Assert.Equal(200, antwort!.Status);
        var text = await seite.Locator("main").InnerTextAsync();
        Assert.Contains("Median", text);
        Assert.Contains("Letzte Sicherung", text);
        Assert.Empty(fehler);
    }

    /// <summary>
    /// Barrierefreiheit (V66): axe-core prüft jede Menüseite der Rolle gegen WCAG 2.1 A und AA (Grundlage von EN 301 549 Kapitel 9).
    /// Automatisch prüfbar ist nur ein Teil der Kriterien; der Rest steht im Prüfbericht docs/Barrierefreiheit.md.
    /// </summary>
    [Theory]
    [InlineData(Rollen.Mitglied, false)]
    [InlineData(Rollen.Kommandant, false)]
    [InlineData(Rollen.Administrator, false)]
    [InlineData(Rollen.Kassier + "," + Rollen.Geraetewart + "," + Rollen.Jugendwart, false)]
    [InlineData(Rollen.Kommandant, true)]
    public async Task Seiten_ohne_Verstoesse_gegen_WCAG(string rolle, bool dunkel)
    {
        var (seite, _) = await SeiteAsync(rolle);
        if (dunkel)
        {
            await seite.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        }

        await seite.GotoAsync("/");
        var menue = (await LinksAsync(seite, "nav")).Prepend("/").Distinct().ToList();

        var optionen = new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] } };
        var verstoesse = new List<string>();
        var geprueft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unterseiten = new List<string>();

        async Task PruefenAsync(string ziel)
        {
            if (!geprueft.Add(ziel.Split('?')[0]))
            {
                return;
            }

            try
            {
                // Verlinkte, aber für die Rolle gesperrte Seiten (403) liefern im Test keine Seite, nur den Status.
                if (await seite.GotoAsync(ziel) is not { Ok: true })
                {
                    return;
                }
            }
            catch (PlaywrightException e) when (e.Message.Contains("Download is starting"))
            {
                return;
            }

            var ergebnis = await seite.RunAxe(optionen);
            verstoesse.AddRange(ergebnis.Violations.Select(v =>
                $"{ziel}: {v.Id} ({v.Nodes.Length}×) {string.Join(" | ", v.Nodes.Take(3).Select(n => $"{n.Target} {n.Any.FirstOrDefault()?.Message}"))}"));
        }

        // Menüseiten und von dort eine Ebene tiefer (Formulare, Akten, Details), je Pfad einmal.
        foreach (var ziel in menue)
        {
            await PruefenAsync(ziel);
            unterseiten.AddRange(await LinksAsync(seite, "main"));
        }

        foreach (var ziel in unterseiten.Distinct().Take(120))
        {
            await PruefenAsync(ziel);
        }

        Assert.True(verstoesse.Count == 0, string.Join(Environment.NewLine, verstoesse));
    }

    /// <summary>Interne Seitenlinks im Bereich, ohne Abmelden und ohne Downloads.</summary>
    private static async Task<IEnumerable<string>> LinksAsync(IPage seite, string bereich) =>
        (await seite.Locator($"{bereich} a[href^='/']").EvaluateAllAsync<string[]>("links => links.map(a => a.getAttribute('href'))"))
            .Where(z => !z.StartsWith("/Konto/Abmelden", StringComparison.OrdinalIgnoreCase)
                && !z.Contains("handler=", StringComparison.OrdinalIgnoreCase)
                && !z.Contains(".pdf", StringComparison.OrdinalIgnoreCase)
                && !z.Contains(".ics", StringComparison.OrdinalIgnoreCase)
                && !z.Contains(".csv", StringComparison.OrdinalIgnoreCase));

    /// <summary>Dunkelmodus folgt dem Gerät und lässt sich im Fuß umstellen; Sprunglink führt zum Inhalt.</summary>
    [Fact]
    public async Task Farbschema_und_Sprunglink()
    {
        var (seite, fehler) = await SeiteAsync(Rollen.Mitglied);
        await seite.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        await seite.GotoAsync("/");
        Assert.True(await HelligkeitAsync(seite) < 0.3, "Seite ist im Dunkelmodus nicht dunkel.");

        await seite.Locator("#fevad-farbschema").SelectOptionAsync("hell");
        Assert.True(await HelligkeitAsync(seite) > 0.7, "Auswahl „hell“ greift nicht.");
        await seite.ReloadAsync();
        Assert.True(await HelligkeitAsync(seite) > 0.7, "Auswahl „hell“ bleibt nicht erhalten.");

        await seite.Keyboard.PressAsync("Tab");
        Assert.Equal("Zum Inhalt springen", await seite.EvaluateAsync<string>("document.activeElement.textContent.trim()"));
        await seite.Keyboard.PressAsync("Enter");
        Assert.Equal("inhalt", await seite.EvaluateAsync<string>("document.activeElement.id"));
        Assert.Empty(fehler);
    }

    /// <summary>Helligkeit des Seitenhintergrunds zwischen 0 und 1 (Canvas rechnet oklch in RGB um).</summary>
    private static Task<double> HelligkeitAsync(IPage seite) => seite.EvaluateAsync<double>("""
        () => {
            const c = document.createElement("canvas").getContext("2d");
            c.fillStyle = getComputedStyle(document.body).backgroundColor;
            c.fillRect(0, 0, 1, 1);
            const [r, g, b] = c.getImageData(0, 0, 1, 1).data;
            return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
        }
        """);
}
