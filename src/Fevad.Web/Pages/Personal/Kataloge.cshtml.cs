using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Kataloge der Personalverwaltung: Lehrgänge/Qualifikationen und wiederkehrende Nachweise mit Intervall.</summary>
public class KatalogeModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    public IReadOnlyList<Qualifikation> Qualifikationen { get; private set; } = [];
    public IReadOnlyList<Nachweisart> Nachweisarten { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungKataloge)).Succeeded;
        Qualifikationen = await db.Qualifikationen.AsNoTracking().OrderBy(q => q.Art).ThenBy(q => q.Sortierung).ToListAsync(ct);
        Nachweisarten = await db.Nachweisarten.AsNoTracking().Include(a => a.Qualifikation).OrderBy(a => a.Sortierung).ToListAsync(ct);
    }
}
