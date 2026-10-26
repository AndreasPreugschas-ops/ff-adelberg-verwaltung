using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Kataloge der Personalverwaltung: Lehrgänge/Qualifikationen und wiederkehrende Nachweise mit Intervall.</summary>
public class KatalogeModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Alle Lehrgänge und Qualifikationen.</summary>
    public IReadOnlyList<Qualifikation> Qualifikationen { get; private set; } = [];
    /// <summary>Alle Nachweisarten.</summary>
    public IReadOnlyList<Nachweisart> Nachweisarten { get; private set; } = [];
    /// <summary>Themen der Unterweisung (V53).</summary>
    public IReadOnlyList<Unterweisungsthema> Unterweisungsthemen { get; private set; } = [];
    /// <summary>Darf Kataloge pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Lädt die Kataloge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungKataloge)).Succeeded;
        Qualifikationen = await db.Qualifikationen.AsNoTracking().OrderBy(q => q.Art).ThenBy(q => q.Sortierung).ToListAsync(ct);
        Nachweisarten = await db.Nachweisarten.AsNoTracking().Include(a => a.Qualifikation).OrderBy(a => a.Sortierung).ToListAsync(ct);
        Unterweisungsthemen = await db.Unterweisungsthemen.AsNoTracking().Include(t => t.Qualifikation).Include(t => t.Nachweisart)
            .OrderBy(t => t.Sortierung).ThenBy(t => t.Bezeichnung).ToListAsync(ct);
    }
}
