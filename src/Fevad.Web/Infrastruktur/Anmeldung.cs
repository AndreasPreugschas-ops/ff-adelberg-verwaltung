using System.Security.Claims;
using System.Text;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Infrastruktur;

public sealed class AnmeldungOptionen
{
    public const string Abschnitt = "Anmeldung";

    /// <summary>Gültigkeit von Einrichtungs- und Passwortlinks.</summary>
    public int LinkGueltigStunden { get; set; } = 72;

    /// <summary>
    /// Öffentliche Adresse der Anwendung, z. B. https://verwaltung.ff-adelberg.de. Daraus entstehen die Links in E-Mails,
    /// nie aus dem Host-Header der Anfrage. Außerhalb der Entwicklung Pflicht.
    /// </summary>
    public string? OeffentlicheAdresse { get; set; }

    /// <summary>
    /// Schaltet die Zwei-Faktor-Pflicht zum lokalen Testen ab. Wirkt nur in der Umgebung Development,
    /// überall sonst wird der Wert ignoriert.
    /// </summary>
    public bool ZweiFaktorPflichtAus { get; set; }

    /// <summary>Wie oft eine laufende Sitzung gegen die Datenbank geprüft wird (Rollen, Sperre, Abmelden überall).</summary>
    public int SitzungPruefenMinuten { get; set; } = 5;
}

/// <summary>Nimmt den 2FA-Status ins Anmelde-Cookie auf, damit die Pflichtprüfung ohne Datenbankzugriff auskommt.</summary>
public sealed class BenutzerClaimsFactory(
    UserManager<Benutzer> benutzer,
    RoleManager<IdentityRole<Guid>> rollen,
    IOptions<IdentityOptions> optionen)
    : UserClaimsPrincipalFactory<Benutzer, IdentityRole<Guid>>(benutzer, rollen, optionen)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Benutzer user)
    {
        var identitaet = await base.GenerateClaimsAsync(user);
        if (user.TwoFactorEnabled)
        {
            identitaet.AddClaim(new Claim(ZweiFaktorPflicht.ClaimTyp, "an"));
        }

        return identitaet;
    }
}

/// <summary>
/// Deaktivierte Konten dürfen sich nicht anmelden, auch nicht mit gültigem Passwort. Laufende Sitzungen fallen bei der
/// nächsten Prüfung des Sicherheitsstempels heraus und können sich auch nicht selbst erneuern.
/// </summary>
public sealed class FfSignInManager(
    UserManager<Benutzer> benutzer,
    IHttpContextAccessor kontext,
    IUserClaimsPrincipalFactory<Benutzer> claims,
    IOptions<IdentityOptions> optionen,
    ILogger<SignInManager<Benutzer>> log,
    IAuthenticationSchemeProvider schemata,
    IUserConfirmation<Benutzer> bestaetigung)
    : SignInManager<Benutzer>(benutzer, kontext, claims, optionen, log, schemata, bestaetigung)
{
    public override async Task<bool> CanSignInAsync(Benutzer user) =>
        !user.IstDeaktiviert && await base.CanSignInAsync(user);

    public override async Task<Benutzer?> ValidateSecurityStampAsync(ClaimsPrincipal? principal) =>
        await base.ValidateSecurityStampAsync(principal) is { IstDeaktiviert: false } user ? user : null;

    public override async Task RefreshSignInAsync(Benutzer user)
    {
        if (user.IstDeaktiviert)
        {
            await SignOutAsync();
            return;
        }

        await base.RefreshSignInAsync(user);
    }
}

/// <summary>
/// Wer eine Rolle aus <see cref="Rollen.ZweiFaktorPflicht"/> hat, muss zuerst die Authenticator-App einrichten.
/// Bis dahin sind nur die Kontoseiten erreichbar.
/// </summary>
public static class ZweiFaktorPflicht
{
    public const string ClaimTyp = "ff:zweifaktor";
    public const string EinrichtungsSeite = "/Konto/Authenticator";

    public static bool Erforderlich(ClaimsPrincipal user) => Rollen.ZweiFaktorPflicht.Any(user.IsInRole);

    public static bool Fehlt(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && Erforderlich(user) && !user.HasClaim(ClaimTyp, "an");

    public static IApplicationBuilder UseZweiFaktorPflicht(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var offen = context.Request.Path.StartsWithSegments("/Konto")
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (!offen && Fehlt(context.User))
        {
            if (context.Request.Headers.ContainsKey("HX-Request"))
            {
                context.Response.Headers["HX-Redirect"] = EinrichtungsSeite;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
            else
            {
                context.Response.Redirect(EinrichtungsSeite);
            }

            return;
        }

        await next();
    });
}

/// <summary>Erzeugt Links zum Setzen des Passworts (Ersteinrichtung und „Passwort vergessen“) und verschickt sie.</summary>
public sealed class Kontolinks(
    UserManager<Benutzer> benutzer,
    LinkGenerator links,
    IMailVersand mail,
    IOptions<AnmeldungOptionen> optionen,
    IHostEnvironment umgebung,
    TimeProvider zeit)
{
    public sealed record Link(string Adresse, DateTimeOffset GueltigBis);

    public bool MailAktiv => mail.Aktiv;

    public async Task<Link> PasswortLinkAsync(HttpContext context, Benutzer konto)
    {
        var token = await benutzer.GeneratePasswordResetTokenAsync(konto);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var basis = optionen.Value.OeffentlicheAdresse;
        string? adresse;
        if (!string.IsNullOrWhiteSpace(basis))
        {
            var uri = new Uri(basis);
            adresse = links.GetUriByPage("/Konto/PasswortSetzen", handler: null, values: new { id = konto.Id, code },
                uri.Scheme, new HostString(uri.Authority), new PathString(uri.AbsolutePath.TrimEnd('/')));
        }
        else if (umgebung.IsDevelopment() || umgebung.IsEnvironment("Test"))
        {
            adresse = links.GetUriByPage(context, "/Konto/PasswortSetzen", values: new { id = konto.Id, code });
        }
        else
        {
            // Sonst könnte ein gefälschter Host-Header den Link samt Token auf einen fremden Server lenken.
            throw new InvalidOperationException("Anmeldung:OeffentlicheAdresse ist nicht gesetzt.");
        }

        return new Link(adresse ?? throw new InvalidOperationException("Seite /Konto/PasswortSetzen nicht gefunden."), zeit.GetUtcNow().AddHours(optionen.Value.LinkGueltigStunden));
    }

    public static string? CodeLesen(string? code)
    {
        try
        {
            return code is null ? null : Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task EinrichtungSendenAsync(Benutzer konto, Link link, CancellationToken ct) =>
        await mail.SendenAsync(konto.Email!, "Dein Zugang zu FEVAD, der Verwaltung der FF Adelberg", $"""
            Hallo,

            für dich wurde ein Zugang zu FEVAD, der Verwaltung der Freiwilligen Feuerwehr Adelberg, angelegt.
            Benutzername ist deine E-Mail-Adresse: {konto.Email}

            Über diesen Link legst du dein Passwort fest:
            {link.Adresse}

            Der Link gilt bis {Anzeige.DatumZeit(link.GueltigBis)} Uhr und nur einmal.

            Freiwillige Feuerwehr Adelberg
            """, ct);

    public async Task ZuruecksetzenSendenAsync(Benutzer konto, Link link, CancellationToken ct) =>
        await mail.SendenAsync(konto.Email!, "Passwort zurücksetzen – FEVAD", $"""
            Hallo,

            für deinen Zugang wurde ein neues Passwort angefordert. Über diesen Link legst du es fest:
            {link.Adresse}

            Der Link gilt bis {Anzeige.DatumZeit(link.GueltigBis)} Uhr und nur einmal.
            Wenn du das nicht warst, kannst du diese E-Mail ignorieren.

            Freiwillige Feuerwehr Adelberg
            """, ct);
}
