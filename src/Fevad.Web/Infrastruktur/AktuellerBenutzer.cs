using System.Security.Claims;
using Fevad.Infrastructure.Persistenz;

namespace Fevad.Web.Infrastruktur;

public sealed class AktuellerBenutzer(IHttpContextAccessor httpContextAccessor) : IAktuellerBenutzer
{
    public Guid? Id =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
