using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung;

/// <summary>Liste der Beschaffungen, wahlweise nur offene und je Haushaltsjahr.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    /// <summary>Auch gelieferte und abgelehnte zeigen.</summary>
    [BindProperty(SupportsGet = true)] public bool Alle { get; set; }
    /// <summary>Nur dieses Haushaltsjahr.</summary>
    [BindProperty(SupportsGet = true)] public int? Haushaltsjahr { get; set; }

    /// <summary>Die angezeigten Beschaffungen.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Haushaltsjahre mit Beschaffungen, jüngstes zuerst.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Bezeichnung">Was beschafft wird.</param>
    /// <param name="Menge">Stückzahl.</param>
    /// <param name="Prioritaet">Dringlichkeit.</param>
    /// <param name="Status">Stand.</param>
    /// <param name="Haushaltsjahr">Haushaltsjahr.</param>
    /// <param name="Kostenschaetzung">Geschätzte Kosten in Euro.</param>
    /// <param name="Istkosten">Tatsächliche Kosten in Euro.</param>
    /// <param name="Fahrzeug">Funkrufname des Fahrzeugs.</param>
    /// <param name="BestelltAm">Tag der Bestellung.</param>
    /// <param name="GeliefertAm">Tag der Lieferung.</param>
    public sealed record Zeile(Guid Id, string Bezeichnung, int Menge, Prioritaet Prioritaet, BeschaffungStatus Status, int? Haushaltsjahr,
        decimal? Kostenschaetzung, decimal? Istkosten, string? Fahrzeug, DateOnly? BestelltAm, DateOnly? GeliefertAm);

    /// <summary>Lädt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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
