using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

/// <summary>Ziel des QR-Codes am Gerät. Gerätewarte kommen zum Gerät, alle anderen zur Mängelmeldung.</summary>
public class GeraetecodeModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Leitet zum Gerät (Technik lesen), zur Mängelmeldung (Mängel melden) oder verweigert den Zugriff.</summary>
    /// <param name="nummer">Inventarnummer aus dem QR-Code.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(string nummer, CancellationToken ct)
    {
        if ((await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikLesen)).Succeeded)
        {
            var id = await db.Geraete.Where(g => g.Inventarnummer == nummer).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);
            return id is null
                ? RedirectToPage("/Technik/Geraete/Index", new { suche = nummer })
                : RedirectToPage("/Technik/Geraete/Details", new { id });
        }

        if ((await autorisierung.AuthorizeAsync(User, Berechtigungen.MaengelMelden)).Succeeded)
        {
            return RedirectToPage("/Melden/Index", new { nummer });
        }

        return Forbid();
    }
}
