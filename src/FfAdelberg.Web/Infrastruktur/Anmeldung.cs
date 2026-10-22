using System.Security.Claims;
using System.Text;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FfAdelberg.Web.Infrastruktur;

public sealed class AnmeldungOptionen
{
    public const string Abschnitt = "Anmeldung";

    /// <summary>Gültigkeit von Einrichtungs- und Passwortlinks.</summary>
    public int LinkGueltigStunden { get; set; } = 72;

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

/// <summary>Deaktivierte Konten dürfen sich nicht anmelden, auch nicht mit gültigem Passwort.</summary>
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
    TimeProvider zeit)
{
    public sealed record Link(string Adresse, DateTimeOffset GueltigBis);

    public bool MailAktiv => mail.Aktiv;

    public async Task<Link> PasswortLinkAsync(HttpContext context, Benutzer konto)
    {
        var token = await benutzer.GeneratePasswordResetTokenAsync(konto);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var adresse = links.GetUriByPage(context, "/Konto/PasswortSetzen", values: new { id = konto.Id, code })
            ?? throw new InvalidOperationException("Seite /Konto/PasswortSetzen nicht gefunden.");
        return new Link(adresse, zeit.GetUtcNow().AddHours(optionen.Value.LinkGueltigStunden));
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
        await mail.SendenAsync(konto.Email!, "Dein Zugang zur Verwaltung der FF Adelberg", $"""
            Hallo,

            für dich wurde ein Zugang zur Verwaltung der Freiwilligen Feuerwehr Adelberg angelegt.
            Benutzername ist deine E-Mail-Adresse: {konto.Email}

            Über diesen Link legst du dein Passwort fest:
            {link.Adresse}

            Der Link gilt bis {Anzeige.DatumZeit(link.GueltigBis)} Uhr und nur einmal.

            Freiwillige Feuerwehr Adelberg
            """, ct);

    public async Task ZuruecksetzenSendenAsync(Benutzer konto, Link link, CancellationToken ct) =>
        await mail.SendenAsync(konto.Email!, "Passwort zurücksetzen – FF Adelberg Verwaltung", $"""
            Hallo,

            für deinen Zugang wurde ein neues Passwort angefordert. Über diesen Link legst du es fest:
            {link.Adresse}

            Der Link gilt bis {Anzeige.DatumZeit(link.GueltigBis)} Uhr und nur einmal.
            Wenn du das nicht warst, kannst du diese E-Mail ignorieren.

            Freiwillige Feuerwehr Adelberg
            """, ct);
}
