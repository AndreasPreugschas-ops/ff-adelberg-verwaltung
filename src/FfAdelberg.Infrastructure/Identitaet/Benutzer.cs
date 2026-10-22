using Microsoft.AspNetCore.Identity;

namespace FfAdelberg.Infrastructure.Identitaet;

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

    public Guid? MitgliedId { get; set; }
}
