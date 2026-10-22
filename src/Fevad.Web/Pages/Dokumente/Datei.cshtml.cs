using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Dokumente;

/// <summary>Liefert eine Version aus. Inhaltstyp nach Dateiendung, Office-Dateien nur als Download.</summary>
public class DateiModel(Dokumentablage ablage, IAuthorizationService autorisierung) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var vertraulich = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVertraulichLesen)).Succeeded;
        if (await ablage.HerunterladenAsync(id, vertraulich, ct) is not var (version, inhalt))
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        // Im Browser angezeigte Dateien dürfen nichts nachladen oder ausführen.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";

        return Dateitypen.Anzeigbar(version.Inhaltstyp)
            ? File(inhalt, version.Inhaltstyp)
            : File(inhalt, version.Inhaltstyp, version.Dateiname);
    }
}
