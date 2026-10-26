using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gemeinde;

/// <summary>Übersicht der Vorgänge „Entscheidung der Gemeinde nötig“ und der Bescheide des Bürgermeisters an Mitglieder (V27), je nach Recht.</summary>
[Authorize]
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Der Benutzer darf Gemeinde-Vorgänge sehen.</summary>
    public bool DarfVorgaenge { get; private set; }

    /// <summary>Der Benutzer darf Bescheide an Mitglieder sehen.</summary>
    public bool DarfBescheide { get; private set; }

    /// <summary>Offene Vorgänge zuerst, dann die entschiedenen der letzten Zeit.</summary>
    public IReadOnlyList<Gemeindevorgang> Vorgaenge { get; private set; } = [];

    /// <summary>Bescheide an Mitglieder, laufende zuerst.</summary>
    public IReadOnlyList<Mitgliederbescheid> Bescheide { get; private set; } = [];

    /// <summary>Lädt die Listen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        DarfVorgaenge = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GemeindeVorgaenge)).Succeeded;
        DarfBescheide = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GemeindeMitgliederbescheide)).Succeeded;
        if (!DarfVorgaenge && !DarfBescheide)
        {
            return Forbid();
        }

        if (DarfVorgaenge)
        {
            Vorgaenge = (await db.Gemeindevorgaenge.AsNoTracking().ToListAsync(ct))
                .OrderBy(v => v.Stand is GemeindevorgangStand.Entschieden or GemeindevorgangStand.Zurueckgezogen)
                .ThenByDescending(v => v.EntschiedenAm ?? v.VorgelegtAm ?? DateOnly.FromDateTime(v.ErstelltAm.UtcDateTime))
                .ToList();
        }

        if (DarfBescheide)
        {
            Bescheide = (await db.Mitgliederbescheide.AsNoTracking().ToListAsync(ct))
                .OrderBy(b => b.Stand is MitgliederbescheidStand.Bekanntgegeben or MitgliederbescheidStand.Eingestellt)
                .ThenByDescending(b => b.ErlassenAm ?? b.AnhoerungAm ?? DateOnly.FromDateTime(b.ErstelltAm.UtcDateTime))
                .ToList();
        }

        return Page();
    }
}
