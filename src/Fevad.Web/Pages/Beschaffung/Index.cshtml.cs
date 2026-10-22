using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool Alle { get; set; }
    [BindProperty(SupportsGet = true)] public int? Haushaltsjahr { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Bezeichnung, int Menge, Prioritaet Prioritaet, BeschaffungStatus Status, int? Haushaltsjahr,
        decimal? Kostenschaetzung, decimal? Istkosten, string? Fahrzeug, DateOnly? BestelltAm, DateOnly? GeliefertAm);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var abfrage = db.Beschaffungen.AsNoTracking();
        if (!Alle)
        {
            abfrage = abfrage.Where(b => b.Status != BeschaffungStatus.Geliefert && b.Status != BeschaffungStatus.Abgelehnt);
        }

        if (Haushaltsjahr is not null)
        {
            abfrage = abfrage.Where(b => b.Haushaltsjahr == Haushaltsjahr);
        }

        Zeilen = await abfrage
            .OrderByDescending(b => b.Prioritaet).ThenBy(b => b.Status).ThenByDescending(b => b.ErstelltAm)
            .Select(b => new Zeile(b.Id, b.Bezeichnung, b.Menge, b.Prioritaet, b.Status, b.Haushaltsjahr,
                b.Kostenschaetzung, b.Istkosten, b.Fahrzeug!.Funkrufname, b.BestelltAm, b.GeliefertAm))
            .ToListAsync(ct);
        Jahre = await db.Beschaffungen.Where(b => b.Haushaltsjahr != null).Select(b => b.Haushaltsjahr!.Value).Distinct().OrderByDescending(j => j).ToListAsync(ct);
    }
}
