using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class ArtikelModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Code, string Bezeichnung, ArtikelKategorie Kategorie, Groessenart Groessenart, string? Geraeteart, string? Grundausstattung, string? Norm, bool Aktiv);

    public async Task OnGetAsync(CancellationToken ct) =>
        Zeilen = await db.Artikel.AsNoTracking()
            .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung)
            .Select(a => new Zeile(a.Id, a.Code, a.Bezeichnung, a.Kategorie, a.Groessenart, a.Geraeteart!.Bezeichnung, a.GrundausstattungAbteilung!.Bezeichnung, a.Norm, a.Aktiv))
            .ToListAsync(ct);
}
