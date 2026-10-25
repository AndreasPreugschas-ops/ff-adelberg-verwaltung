using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Liste aller Kleiderkammer-Artikel.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class ArtikelModel(AppDbContext db) : PageModel
{
    /// <summary>Die Artikel nach Sortierung.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Ein Artikel in der Liste.</summary>
    /// <param name="Id">Kennung des Artikels.</param>
    /// <param name="Code">Kurzcode des Artikels.</param>
    /// <param name="Bezeichnung">Bezeichnung des Artikels.</param>
    /// <param name="Kategorie">Kategorie des Artikels.</param>
    /// <param name="Groessenart">Nach welcher Größe der Artikel ausgegeben wird.</param>
    /// <param name="Geraeteart">Geräteart, wenn der Artikel als Einzelstück geführt wird.</param>
    /// <param name="Grundausstattung">Abteilungsart, zu deren Grundausstattung der Artikel gehört.</param>
    /// <param name="Norm">Norm, z. B. „DIN EN 469“.</param>
    /// <param name="Aktiv">Der Artikel wird noch ausgegeben.</param>
    public sealed record Zeile(Guid Id, string Code, string Bezeichnung, ArtikelKategorie Kategorie, Groessenart Groessenart, string? Geraeteart, AbteilungArt? Grundausstattung, string? Norm, bool Aktiv);

    /// <summary>Lädt die Artikel.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) =>
        Zeilen = await db.Artikel.AsNoTracking()
            .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung)
            .Select(a => new Zeile(a.Id, a.Code, a.Bezeichnung, a.Kategorie, a.Groessenart, a.Geraeteart!.Bezeichnung, a.GrundausstattungArt, a.Norm, a.Aktiv))
            .ToListAsync(ct);
}
