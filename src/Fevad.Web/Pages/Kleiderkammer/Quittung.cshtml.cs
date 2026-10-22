using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Druckbare Quittung über Ausgabe oder Rücknahme, mit Feldern für beide Unterschriften.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class QuittungModel(AppDbContext db, Kleiderkammerbuchungen buchungen, IAuthorizationService autorisierung) : PageModel
{
    public Kleiderkammervorgang Vorgang { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public string Personalnummer { get; private set; } = "";
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }

    public sealed record Zeile(string Artikel, string Groesse, int Menge, string? Inventarnummer, string? Seriennummer, RuecknahmeZustand? Zustand);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var vorgang = await db.Kleiderkammervorgaenge.AsNoTracking().Include(v => v.Mitglied).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vorgang is null)
        {
            return NotFound();
        }

        Vorgang = vorgang;
        Name = $"{vorgang.Mitglied!.Vorname} {vorgang.Mitglied.Nachname}";
        Personalnummer = vorgang.Mitglied.Personalnummer;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

        var abfrage = vorgang.Art == VorgangArt.Ausgabe
            ? db.Ausstattungen.Where(a => a.AusgabeVorgangId == id)
            : db.Ausstattungen.Where(a => a.RuecknahmeVorgangId == id);
        var zeilen = await abfrage.AsNoTracking()
            .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.Groesse)
            .Select(a => new { Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.Geraet!.Inventarnummer, a.Geraet.Seriennummer, a.Zustand })
            .ToListAsync(ct);

        // Teilrückgaben teilen Zeilen; auf der Ausgabequittung erscheinen sie wieder zusammengefasst.
        Zeilen = zeilen
            .GroupBy(a => (a.Artikel, a.Groesse, a.Inventarnummer, a.Seriennummer, Zustand: vorgang.Art == VorgangArt.Ausgabe ? null : a.Zustand))
            .Select(g => new Zeile(g.Key.Artikel, g.Key.Groesse, g.Sum(a => a.Menge), g.Key.Inventarnummer, g.Key.Seriennummer, g.Key.Zustand))
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostQuittiertAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        await buchungen.QuittiertAsync(id, ct);
        TempData["Meldung"] = "Unterschriebene Quittung vermerkt.";
        return RedirectToPage(new { id });
    }
}
