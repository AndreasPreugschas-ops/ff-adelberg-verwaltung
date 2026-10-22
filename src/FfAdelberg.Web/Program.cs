using System.Globalization;
using FfAdelberg.Infrastructure;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Infrastructure.Persistenz.Seed;
using FfAdelberg.Web.Api;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAktuellerBenutzer, AktuellerBenutzer>();
builder.Services.AddInfrastruktur(builder.Configuration);
builder.Services.Configure<BetriebOptionen>(builder.Configuration.GetSection(BetriebOptionen.Abschnitt));
builder.Services.Configure<Fe2Optionen>(builder.Configuration.GetSection(Fe2Optionen.Abschnitt));

builder.Services
    .AddDefaultIdentity<Benutzer>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ffadelberg.anmeldung";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    // Alles ist geschützt, außer was ausdrücklich [AllowAnonymous] trägt (Login-Seiten, Webhook mit eigenem Token).
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    Berechtigungen.Registrieren(options);
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Mitglieder", Berechtigungen.MitgliederLesen);
    options.Conventions.AuthorizeFolder("/Einsaetze", Berechtigungen.EinsaetzeLesen);
    options.Conventions.AuthorizeFolder("/Fahrzeuge", Berechtigungen.TechnikLesen);
});

// HTMX schickt das Antiforgery-Token als Header (siehe _Layout.cshtml).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("datenbank");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Hinter Caddy im selben Compose-Netz.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

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

// Keine Selbstregistrierung und keine externen Logins: Konten legt nur der Administrator an.
// Die Identity-Seiten tragen [AllowAnonymous], deshalb hier vor dem Routing gesperrt.
string[] gesperrteIdentitySeiten = ["/Identity/Account/Register", "/Identity/Account/RegisterConfirmation", "/Identity/Account/ExternalLogin"];
app.Use(async (context, next) =>
{
    if (gesperrteIdentitySeiten.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapFe2Endpunkte();

if (!app.Environment.IsEnvironment("Test"))
{
    await DatenbankInitialisierung.InitialisierenAsync(app.Services);
}

app.Run();

public partial class Program;
