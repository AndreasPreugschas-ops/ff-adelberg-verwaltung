using System.Globalization;
using Fevad.Infrastructure;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Seed;
using Fevad.Web.Api;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAktuellerBenutzer, AktuellerBenutzer>();
builder.Services.AddInfrastruktur(builder.Configuration);
builder.Services.Configure<BetriebOptionen>(builder.Configuration.GetSection(BetriebOptionen.Abschnitt));
builder.Services.Configure<Fe2Optionen>(builder.Configuration.GetSection(Fe2Optionen.Abschnitt));

builder.Services.Configure<AnmeldungOptionen>(builder.Configuration.GetSection(AnmeldungOptionen.Abschnitt));
var anmeldung = builder.Configuration.GetSection(AnmeldungOptionen.Abschnitt).Get<AnmeldungOptionen>() ?? new();

// Eigene deutsche Kontoseiten unter /Konto statt der englischen Identity-Standardoberfläche.
// Keine Selbstregistrierung und keine externen Logins: Konten legt nur der Administrator an.
builder.Services
    .AddIdentity<Benutzer, IdentityRole<Guid>>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        // Wie bisher mit der Identity-Standardoberfläche, damit das Schema gleich bleibt.
        options.Stores.MaxLengthForKeys = 128;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager<FfSignInManager>()
    .AddClaimsPrincipalFactory<BenutzerClaimsFactory>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<DeutscheIdentityFehler>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(anmeldung.LinkGueltigStunden));

// Rollenänderung, Sperre und „überall abmelden“ greifen spätestens nach dieser Zeit.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(anmeldung.SitzungPruefenMinuten));

builder.Services.AddDataProtection()
    .SetApplicationName("fevad")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "fevad.anmeldung";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Konto/Anmelden";
    options.LogoutPath = "/Konto/Abmelden";
    options.AccessDeniedPath = "/Konto/KeinZugriff";
    options.Events.OnSignedIn = async context =>
    {
        var benutzer = context.HttpContext.RequestServices.GetRequiredService<UserManager<Benutzer>>();
        if (await benutzer.GetUserAsync(context.Principal!) is { } konto)
        {
            konto.LetzteAnmeldungAm = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            await benutzer.UpdateAsync(konto);
        }
    };
});

builder.Services.AddScoped<Kontolinks>();
builder.Services.AddScoped<Datenbereich>();
builder.Services.AddScoped<Hauptmenue>();
builder.Services.AddScoped<WasLiegtAn>();
builder.Services.AddScoped<Parametereinstellungen>();
builder.Services.AddScoped<Uebertritt>();
builder.Services.AddScoped<Antragsannahme>();
builder.Services.AddScoped<Antragsbereich>();
builder.Services.AddScoped<Fevad.Web.Pages.Termine.Terminlader>();
builder.Services.AddScoped<Geraetecode>();

builder.Services.AddAuthorization(options =>
{
    // Alles ist geschützt, außer was ausdrücklich [AllowAnonymous] trägt (Login-Seiten, Webhook mit eigenem Token).
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    Berechtigungen.Registrieren(options);
});

// Datumsfelder nehmen "24.10.2026", "24.10.26", "24102026" und ISO an (siehe DatumEingabe).
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.ModelBinderProviders.Insert(0, new DatumBinderProvider()));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Mitglieder", Berechtigungen.MitgliederSehen);
    options.Conventions.AuthorizeFolder("/Abteilungen", Berechtigungen.MitgliederSehen);
    options.Conventions.AuthorizeFolder("/Laufbahn", Berechtigungen.LaufbahnLesen);
    options.Conventions.AuthorizeFolder("/Personal", Berechtigungen.AusbildungLesen);
    options.Conventions.AuthorizeFolder("/Einsaetze", Berechtigungen.EinsaetzeLesen);
    options.Conventions.AuthorizeFolder("/Dokumente", Berechtigungen.DokumenteLesen);
    options.Conventions.AuthorizeFolder("/Gremien", Berechtigungen.GremienLesen);
    options.Conventions.AuthorizeFolder("/Technik", Berechtigungen.TechnikLesen);
    options.Conventions.AuthorizeFolder("/Beschaffung", Berechtigungen.BeschaffungLesen);
    options.Conventions.AuthorizeFolder("/Melden", Berechtigungen.MaengelMelden);
    options.Conventions.AuthorizeFolder("/Verwaltung/Benutzer", Berechtigungen.BenutzerVerwalten);
    options.Conventions.AuthorizeFolder("/Verwaltung/Rechte", Berechtigungen.BenutzerVerwalten);
    options.Conventions.AuthorizeFolder("/Verwaltung/Datenschutz", Berechtigungen.DatenschutzLoeschlauf);
    options.Conventions.AuthorizeFolder("/Auswertungen/Entschaedigung", Berechtigungen.EntschaedigungLesen);
});

// HTMX schickt das Antiforgery-Token als Header (siehe _Layout.cshtml).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("datenbank");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Nur Proxys aus Betrieb:VertrauenswuerdigeProxyNetze (CIDR) dürfen Client-IP und Schema setzen,
    // z. B. das Compose-Netz von Caddy. Ohne Eintrag gilt nur Loopback.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var netze = builder.Configuration.GetSection("Betrieb:VertrauenswuerdigeProxyNetze").Get<string[]>() ?? [];
    if (netze.Length > 0)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var netz in netze)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(netz));
        }
    }
});

// Wöchentliche Fristerinnerung per Mail. Nicht im Notbetrieb (sonst doppelte Mails vom Gerätehaus-PC) und nicht in Tests.
var betrieb = builder.Configuration.GetSection(BetriebOptionen.Abschnitt).Get<BetriebOptionen>() ?? new();
if (!betrieb.Notbetrieb && !builder.Environment.IsEnvironment("Test") && builder.Configuration.GetValue("Fristen:ErinnerungAktiv", true))
{
    builder.Services.AddHostedService<FristErinnerungsDienst>();
}

// Nächtlicher Löschlauf nach Löschkonzept. Nicht im Notbetrieb: die Kopie im Gerätehaus folgt dem Hauptsystem.
if (!betrieb.Notbetrieb && !builder.Environment.IsEnvironment("Test") && builder.Configuration.GetValue("Datenschutz:LoeschlaufAktiv", true))
{
    builder.Services.AddHostedService<LoeschlaufDienst>();
}

// Personalprüfung nach Änderungen, beim Start und täglich. Nicht im Notbetrieb und nicht in Tests (dort direkt aufgerufen).
if (!betrieb.Notbetrieb && !builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddHostedService<Fevad.Infrastructure.Laufbahn.PersonalpruefungDienst>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Test"))
{
    var pruefung = Startpruefung.Pruefen(app.Configuration);
    foreach (var warnung in pruefung.Warnungen)
    {
        app.Logger.LogWarning("Konfiguration: {Warnung}", warnung);
    }

    if (pruefung.Fehler.Count > 0)
    {
        throw new InvalidOperationException("Konfiguration unvollständig: " + string.Join(" ", pruefung.Fehler));
    }
}

var deutsch = new CultureInfo("de-DE");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(deutsch),
    SupportedCultures = [deutsch],
    SupportedUICultures = [deutsch],
});

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
if (anmeldung.ZweiFaktorPflichtAus && app.Environment.IsDevelopment())
{
    app.Logger.LogWarning("Zwei-Faktor-Pflicht ist abgeschaltet (Anmeldung:ZweiFaktorPflichtAus, nur Development).");
}
else
{
    app.UseZweiFaktorPflicht();
}

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapFe2Endpunkte();
app.MapKalenderEndpunkte();

if (!app.Environment.IsEnvironment("Test"))
{
    await DatenbankInitialisierung.InitialisierenAsync(app.Services);

    // Fiktive Testdaten nur in der Entwicklung und nur auf ausdrücklichen Wunsch.
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Testdaten:Anlegen", false))
    {
        await Testdaten.AnlegenAsync(app.Services);
        await Testbenutzer.AnlegenAsync(app.Services);
    }
}

app.Run();

/// <summary>Einstiegspunkt der Anwendung; öffentlich für die Integrationstests (<c>WebApplicationFactory</c>).</summary>
public partial class Program;
