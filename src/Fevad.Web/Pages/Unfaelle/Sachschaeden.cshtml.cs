using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Unfaelle;

/// <summary>Gemeldete Sachschäden an privatem Eigentum (§ 17 FwG): offene zuerst, erledigte darunter.</summary>
[Authorize(Policy = Berechtigungen.SachschaedenLesen)]
public class SachschaedenModel(AppDbContext db) : PageModel
{
    /// <summary>Noch nicht erledigte Sachschäden, älteste zuerst.</summary>
    public IReadOnlyList<Sachschaden> Offen { get; private set; } = [];
    /// <summary>Erstattete oder abgelehnte Sachschäden, neueste zuerst.</summary>
    public IReadOnlyList<Sachschaden> Erledigt { get; private set; } = [];

    /// <summary>Lädt die Listen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var alle = await db.Sachschaeden.AsNoTracking().Include(s => s.Mitglied).OrderBy(s => s.Zeitpunkt).ToListAsync(ct);
        Offen = alle.Where(s => !s.Abgeschlossen).ToList();
        Erledigt = alle.Where(s => s.Abgeschlossen).OrderByDescending(s => s.Zeitpunkt).ToList();
    }
}
