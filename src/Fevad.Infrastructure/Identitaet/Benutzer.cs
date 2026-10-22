using Fevad.Domain.Gemeinsam;
using Microsoft.AspNetCore.Identity;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>
/// Login-Konto. Getrennt vom Mitglied, weil nicht jedes Mitglied einen Login hat
/// und nicht jeder Login ein Mitglied ist (z. B. Lesezugang der Gemeinde).
/// </summary>
public class Benutzer : IdentityUser<Guid>
{
    public Benutzer()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>Verknüpftes Mitglied (höchstens ein Konto je Mitglied).</summary>
    public Guid? MitgliedId { get; set; }

    public DateTimeOffset? LetzteAnmeldungAm { get; set; }

    /// <summary>Vom Administrator abgeschaltet. Nach der Frist anonymisiert der Löschlauf das Konto.</summary>
    public DateTimeOffset? DeaktiviertAm { get; set; }

    /// <summary>
    /// Vom Löschlauf geleert: Name, Adresse, Passwort und Rollen sind entfernt, nur die ID bleibt,
    /// damit Protokoll und Lesebestätigungen ihren Verweis behalten.
    /// </summary>
    public DateTimeOffset? AnonymisiertAm { get; set; }

    /// <summary>
    /// Geheimer Teil des Kalender-Abo-Links (iCal). Gibt nur die eigenen Termine heraus und lässt sich
    /// jederzeit neu erzeugen, womit der alte Link ungültig wird.
    /// </summary>
    [NichtProtokollieren]
    public string? KalenderToken { get; set; }

    public bool IstDeaktiviert => DeaktiviertAm is not null;

    public bool IstAnonymisiert => AnonymisiertAm is not null;
}
