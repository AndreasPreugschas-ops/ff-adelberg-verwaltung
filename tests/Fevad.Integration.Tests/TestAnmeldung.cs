using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fevad.Integration.Tests;

/// <summary>
/// Meldet Testanfragen ohne Login-Seite an: Header X-Test-Rollen enthält die Rollen, kommagetrennt.
/// Unabhängig vom Aufbau der Anmeldeseiten; trägt den Claim, den die 2FA-Pflicht erwartet.
/// </summary>
public sealed class TestAnmeldung(IOptionsMonitor<AuthenticationSchemeOptions> optionen, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionen, logger, encoder)
{
    public const string Schema = "Test";
    public const string RollenHeader = "X-Test-Rollen";
    public static readonly Guid BenutzerId = Guid.CreateVersion7();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RollenHeader, out var rollen))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, BenutzerId.ToString()),
            new(ClaimTypes.Name, "test@ff-adelberg.test"),
            new("ff:zweifaktor", "an"),
        };
        claims.AddRange(rollen.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => new Claim(ClaimTypes.Role, r.Trim())));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Schema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Schema)));
    }

    public static WebApplicationFactory<Program> Factory(AnwendungFixture fixture) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAnmeldung>(Schema, _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = Schema;
                o.DefaultAuthenticateScheme = Schema;
                o.DefaultChallengeScheme = Schema;
                o.DefaultForbidScheme = Schema;
            });
        }));
}
