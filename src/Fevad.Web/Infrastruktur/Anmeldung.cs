using System.Security.Claims;
using System.Text;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Infrastruktur;

/// <summary>Einstellungen der Anmeldung aus dem Konfigurationsabschnitt <c>Anmeldung</c>.</summary>
public sealed class AnmeldungOptionen
{
    /// <summary>Name des Konfigurationsabschnitts.</summary>
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

    /// <summary>
    /// Wer Gesundheitsdaten sehen darf (Atemschutz, Befreiungsgründe, Unfälle), wird nach so vielen Stunden ohne Nutzung abgemeldet, statt
    /// nach 14 Tagen (V69, Art. 32 DSGVO).
    /// </summary>
    public int SitzungGesundheitsdatenStunden { get; set; } = 12;
}

/// <summary>
/// Nimmt den 2FA-Status ins Anmelde-Cookie auf, damit die Pflichtprüfung ohne Datenbankzugriff auskommt. Rollen der Gemeindeverwaltung
/// fallen weg, solange keine Verpflichtung auf Vertraulichkeit eingetragen ist. Dazu kommt die Rolle
/// Kassenprüfer, solange das verknüpfte Mitglied eine laufende Funktion mit dieser Rolle hat (Amtszeit aus der Wahl); die Sitzungsprüfung
/// erneuert das alle paar Minuten, so endet der Zugang mit der Amtszeit.
/// </summary>
/// <param name="benutzer">Benutzerverwaltung.</param>
/// <param name="rollen">Rollenverwaltung.</param>
/// <param name="optionen">Identity-Einstellungen.</param>
/// <param name="db">Datenbank für die Funktionen des Mitglieds.</param>
/// <param name="zeit">Uhr für den heutigen Tag.</param>
public sealed class BenutzerClaimsFactory(
    UserManager<Benutzer> benutzer,
    RoleManager<IdentityRole<Guid>> rollen,
    IOptions<IdentityOptions> optionen,
    AppDbContext db,
    TimeProvider zeit)
    : UserClaimsPrincipalFactory<Benutzer, IdentityRole<Guid>>(benutzer, rollen, optionen)
{
    /// <inheritdoc/>
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Benutzer user)
    {
        var identitaet = await base.GenerateClaimsAsync(user);
        if (user.TwoFactorEnabled)
        {
            identitaet.AddClaim(new Claim(ZweiFaktorPflicht.ClaimTyp, "an"));
        }

        // Rollen der Gemeindeverwaltung wirken erst mit dokumentierter Verpflichtung auf Vertraulichkeit (V26).
        if (user.VerpflichtetAm is null)
        {
            foreach (var claim in identitaet.FindAll(identitaet.RoleClaimType).Where(c => Rollen.VerpflichtungPflicht.Contains(c.Value)).ToList())
            {
                identitaet.RemoveClaim(claim);
            }
        }

        if (user.MitgliedId is { } mitgliedId && !identitaet.HasClaim(identitaet.RoleClaimType, Rollen.Kassenpruefer))
        {
            var heute = Anzeige.Heute(zeit);
            if (await db.FunktionZuordnungen.AnyAsync(z => z.MitgliedId == mitgliedId && z.Funktion!.VorgeschlageneRolle == Rollen.Kassenpruefer
                    && z.GueltigAb <= heute && (z.GueltigBis == null || z.GueltigBis >= heute)))
            {
                identitaet.AddClaim(new Claim(identitaet.RoleClaimType, Rollen.Kassenpruefer));
            }
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
    /// <summary>Deaktivierte Konten dürfen sich nicht anmelden.</summary>
    /// <param name="user">Das Konto.</param>
    /// <returns><c>true</c>, wenn die Anmeldung erlaubt ist.</returns>
    public override async Task<bool> CanSignInAsync(Benutzer user) =>
        !user.IstDeaktiviert && await base.CanSignInAsync(user);

    /// <summary>Prüft den Sicherheitsstempel; ein deaktiviertes Konto gilt dabei als ungültig.</summary>
    /// <param name="principal">Der Benutzer aus dem Cookie.</param>
    /// <returns>Das Konto oder <c>null</c>, wenn die Sitzung nicht mehr gilt.</returns>
    public override async Task<Benutzer?> ValidateSecurityStampAsync(ClaimsPrincipal? principal) =>
        await base.ValidateSecurityStampAsync(principal) is { IstDeaktiviert: false } user ? user : null;

    /// <summary>Erneuert das Anmelde-Cookie; ein deaktiviertes Konto wird stattdessen abgemeldet.</summary>
    /// <param name="user">Das Konto.</param>
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
/// Wer eine Rolle hat, für die <see cref="Rollen.ZweiFaktorNoetig"/> gilt, muss zuerst die Authenticator-App einrichten.
/// Bis dahin sind nur die Kontoseiten erreichbar.
/// </summary>
public static class ZweiFaktorPflicht
{
    /// <summary>Claim, der „an“ enthält, wenn das Konto die Zwei-Faktor-Anmeldung eingerichtet hat.</summary>
    public const string ClaimTyp = "ff:zweifaktor";
    /// <summary>Seite zum Einrichten der Authenticator-App.</summary>
    public const string EinrichtungsSeite = "/Konto/Authenticator";

    /// <summary>Eine der Rollen des Benutzers verlangt die Zwei-Faktor-Anmeldung.</summary>
    /// <param name="user">Der angemeldete Benutzer.</param>
    /// <returns><c>true</c>, wenn die Pflicht gilt.</returns>
    public static bool Erforderlich(ClaimsPrincipal user) => user.FindAll(ClaimTypes.Role).Any(c => Rollen.ZweiFaktorNoetig(c.Value));

    /// <summary>Der Benutzer ist angemeldet, braucht die Zwei-Faktor-Anmeldung und hat sie noch nicht eingerichtet.</summary>
    /// <param name="user">Der angemeldete Benutzer.</param>
    /// <returns><c>true</c>, wenn die Einrichtung fehlt.</returns>
    public static bool Fehlt(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && Erforderlich(user) && !user.HasClaim(ClaimTyp, "an");

    /// <summary>Middleware, die Benutzer ohne eingerichtete Zwei-Faktor-Anmeldung auf die Einrichtungsseite umleitet.</summary>
    /// <param name="app">Die Anwendung.</param>
    /// <returns>Die Anwendung zum Verketten.</returns>
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
    /// <summary>Ein Link zum Setzen des Passworts.</summary>
    /// <param name="Adresse">Vollständige Adresse mit Token.</param>
    /// <param name="GueltigBis">Ablauf des Links (UTC).</param>
    public sealed record Link(string Adresse, DateTimeOffset GueltigBis);

    /// <summary>Der E-Mail-Versand ist eingerichtet.</summary>
    public bool MailAktiv => mail.Aktiv;

    /// <summary>Erzeugt einen Link mit Token zum Setzen des Passworts; die Adresse kommt aus <see cref="AnmeldungOptionen.OeffentlicheAdresse"/>.</summary>
    /// <param name="context">Die laufende Anfrage (nur in Entwicklung und Test als Adressquelle).</param>
    /// <param name="konto">Das Konto.</param>
    /// <returns>Der Link mit Ablaufzeit.</returns>
    /// <exception cref="InvalidOperationException">Die öffentliche Adresse fehlt außerhalb von Entwicklung und Test.</exception>
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

    /// <summary>Wandelt den Code aus dem Link zurück in das Token.</summary>
    /// <param name="code">Base64-URL-kodierter Code.</param>
    /// <returns>Das Token oder <c>null</c>, wenn der Code fehlt oder ungültig ist.</returns>
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

    /// <summary>Schickt den Einrichtungslink für ein neues Konto per E-Mail.</summary>
    /// <param name="konto">Das Konto.</param>
    /// <param name="link">Der Link.</param>
    /// <param name="ct">Abbruchsignal.</param>
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

    /// <summary>Schickt den Link zum Zurücksetzen des Passworts per E-Mail.</summary>
    /// <param name="konto">Das Konto.</param>
    /// <param name="link">Der Link.</param>
    /// <param name="ct">Abbruchsignal.</param>
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
