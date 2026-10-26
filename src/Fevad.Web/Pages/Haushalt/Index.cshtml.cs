using Fevad.Domain.Haushalt;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Haushalt;

/// <summary>Haushaltsanmeldungen der Feuerwehr je Jahr (V29).</summary>
[Authorize(Policy = Berechtigungen.HaushaltLesen)]
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Anmeldungen mit Summen.</summary>
    public IReadOnlyList<Haushaltsanmeldung> Anmeldungen { get; private set; } = [];

    /// <summary>Jahr, für das als Nächstes angemeldet wird.</summary>
    public int NaechstesJahr { get; private set; }

    /// <summary>Ob der Nutzer eine Anmeldung anlegen darf.</summary>
    public bool DarfAnlegen { get; private set; }

    /// <summary>Zeigt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        NaechstesJahr = Anzeige.Heute(zeit).Year + 1;
        DarfAnlegen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.HaushaltZumelden)).Succeeded;
        Anmeldungen = await db.Haushaltsanmeldungen.AsNoTracking().Include(a => a.Positionen).ThenInclude(p => p.Haushaltsstelle)
            .OrderByDescending(a => a.Jahr).ToListAsync(ct);
    }

    /// <summary>Legt die Anmeldung für ein Jahr an, falls es sie noch nicht gibt.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(int jahr, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.HaushaltZumelden)).Succeeded)
        {
            return Forbid();
        }

        if (jahr is < 2020 or > 2100)
        {
            return BadRequest();
        }

        if (!await db.Haushaltsanmeldungen.AnyAsync(a => a.Jahr == jahr, ct))
        {
            db.Haushaltsanmeldungen.Add(new Haushaltsanmeldung { Jahr = jahr });
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage("Anmeldung", new { jahr });
    }
}
