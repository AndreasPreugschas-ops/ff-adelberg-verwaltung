using System.Security.Claims;
using FfAdelberg.Infrastructure.Persistenz;

namespace FfAdelberg.Web.Infrastruktur;

public sealed class AktuellerBenutzer(IHttpContextAccessor httpContextAccessor) : IAktuellerBenutzer
{
    public Guid? Id =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
