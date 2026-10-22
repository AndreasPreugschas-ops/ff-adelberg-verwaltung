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

    /// <summary>Vom Administrator abgeschaltet. Konten werden nicht gelöscht, damit das Protokoll lesbar bleibt.</summary>
    public DateTimeOffset? DeaktiviertAm { get; set; }

    public bool IstDeaktiviert => DeaktiviertAm is not null;
}
