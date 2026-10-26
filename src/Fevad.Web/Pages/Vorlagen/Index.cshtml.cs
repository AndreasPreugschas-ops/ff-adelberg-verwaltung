using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Vorlagen;

/// <summary>Übersicht der Briefvorlagen (V56).</summary>
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Die Vorlagen; inaktive nur für die Schriftführung.</summary>
    public IReadOnlyList<Briefvorlage> Vorlagen { get; private set; } = [];

    /// <summary>Der Benutzer darf Vorlagen ändern.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Zeigt die Vorlagen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Eine Aufgabe.</returns>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.VorlagenBearbeiten)).Succeeded;
        Vorlagen = await db.Briefvorlagen.AsNoTracking().Where(v => v.Aktiv || DarfBearbeiten).OrderBy(v => v.Sortierung).ThenBy(v => v.Bezeichnung).ToListAsync(ct);
    }
}
