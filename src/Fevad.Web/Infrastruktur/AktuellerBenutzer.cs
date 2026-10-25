using System.Security.Claims;
using Fevad.Infrastructure.Persistenz;

namespace Fevad.Web.Infrastruktur;

/// <summary>Liefert das Konto der laufenden Anfrage aus dem Anmelde-Cookie, z. B. für das Änderungsprotokoll.</summary>
public sealed class AktuellerBenutzer(IHttpContextAccessor httpContextAccessor) : IAktuellerBenutzer
{
    /// <summary>Kennung des angemeldeten Kontos; <c>null</c> ohne Anmeldung oder außerhalb einer Anfrage.</summary>
    public Guid? Id =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
