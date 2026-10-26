using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Beschlussregister (V55): alle Beschlüsse eines Jahres mit Nummer, Sitzung, Ergebnis und Stand gefasst, umgesetzt oder aufgehoben.</summary>
public class BeschluesseModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Gezeigtes Jahr.</summary>
    public int Jahr { get; private set; }
    /// <summary>Jahre, in denen es Beschlüsse gibt, neueste zuerst.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];
    /// <summary>Beschlüsse des Jahres mit Sitzung, nach Nummer.</summary>
    public IReadOnlyList<Beschluss> Beschluesse { get; private set; } = [];
    /// <summary>Nur dieser Stand; leer für alle.</summary>
    [BindProperty(SupportsGet = true)]
    public BeschlussStand? Stand { get; set; }

    /// <summary>Lädt das Register eines Jahres.</summary>
    /// <param name="jahr">Jahr; leer = laufendes Jahr oder das letzte mit Beschlüssen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        Jahre = await db.Beschluesse.AsNoTracking().Select(b => b.Jahr).Distinct().OrderByDescending(j => j).ToListAsync(ct);
        var heute = Anzeige.Heute(zeit).Year;
        Jahr = jahr ?? (Jahre.Contains(heute) || Jahre.Count == 0 ? heute : Jahre[0]);
        var liste = await db.Beschluesse.AsNoTracking().Include(b => b.Sitzung)
            .Where(b => b.Jahr == Jahr)
            .OrderBy(b => b.Nummer)
            .ToListAsync(ct);
        Beschluesse = Stand is { } s ? liste.Where(b => b.Stand == s).ToList() : liste;
    }
}
