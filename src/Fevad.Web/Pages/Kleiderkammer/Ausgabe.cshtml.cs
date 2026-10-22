using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Ausgabe an ein Mitglied. Größen kommen aus dem Größenprofil, Einzelstücke werden per Inventarnummer gewählt.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerBearbeiten)]
public class AusgabeModel(AppDbContext db, Kleiderkammerbuchungen buchungen) : PageModel
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";

    [BindProperty]
    public List<Eingabe> Positionen { get; set; } = [];

    [BindProperty]
    public string? Bemerkung { get; set; }

    public IReadOnlyList<ArtikelAuswahl> Artikel { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid ArtikelId { get; set; }
        public string? Groesse { get; set; }
        public int Menge { get; set; }
        public Guid? GeraetId { get; set; }
    }

    public sealed record Stueck(Guid Id, string Inventarnummer, string? Groesse, bool Passend);

    public sealed record ArtikelAuswahl(Guid Id, string Bezeichnung, Groessenart Groessenart, bool Einzelstueck, string? Vorschlag,
        IReadOnlyDictionary<string, int> Lager, IReadOnlyList<Stueck> Stuecke, int SchonAusgegeben);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var positionen = Positionen
            .Where(p => p.GeraetId is not null || p.Menge > 0)
            .Select(p => new AusgabePosition(p.ArtikelId, p.Groesse ?? "", p.GeraetId is null ? p.Menge : 1, p.GeraetId))
            .ToList();
        try
        {
            var vorgang = await buchungen.AusgebenAsync(id, positionen, Bemerkung, ct);
            TempData["Meldung"] = $"Ausgabe {vorgang.Nummer} gebucht. Bitte Quittung drucken und unterschreiben lassen.";
            return RedirectToPage("Quittung", new { id = vorgang.Id });
        }
        catch (KleiderkammerFehler fehler)
        {
            ModelState.AddModelError("", fehler.Message);
            return await LadenAsync(id, ct) ? Page() : NotFound();
        }
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var mitglied = await db.Mitglieder.AsNoTracking().Where(m => m.Id == id).Select(m => new { m.Nachname, m.Vorname }).FirstOrDefaultAsync(ct);
        if (mitglied is null)
        {
            return false;
        }

        Id = id;
        Name = $"{mitglied.Nachname}, {mitglied.Vorname}";

        var groessen = await db.Groessenangaben.AsNoTracking().Where(g => g.MitgliedId == id).ToDictionaryAsync(g => g.Groessenart, g => g.Wert, ct);
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.Aktiv)
            .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung)
            .Select(a => new { a.Id, a.Bezeichnung, a.Groessenart, a.GeraeteartId })
            .ToListAsync(ct);
        var lager = await db.Lagerbestaende.AsNoTracking().Where(l => l.Menge > 0).Select(l => new { l.ArtikelId, l.Groesse, l.Menge }).ToListAsync(ct);
        var artIds = artikel.Where(a => a.GeraeteartId != null).Select(a => a.GeraeteartId!.Value).ToList();
        var stuecke = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && artIds.Contains(g.GeraeteartId) && !db.Ausstattungen.Any(a => a.GeraetId == g.Id && a.ZurueckAm == null))
            .OrderBy(g => g.Inventarnummer)
            .Select(g => new { g.Id, g.GeraeteartId, g.Inventarnummer, g.Groesse })
            .ToListAsync(ct);
        var hat = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.MitgliedId == id && a.ZurueckAm == null)
            .GroupBy(a => a.ArtikelId)
            .Select(g => new { g.Key, Menge = g.Sum(a => a.Menge) })
            .ToDictionaryAsync(g => g.Key, g => g.Menge, ct);

        Artikel = artikel
            .Select(a =>
            {
                var vorschlag = a.Groessenart == Groessenart.Einheitsgroesse ? null : groessen.GetValueOrDefault(a.Groessenart);
                var passende = stuecke
                    .Where(s => s.GeraeteartId == a.GeraeteartId)
                    .Select(s => new Stueck(s.Id, s.Inventarnummer, s.Groesse, vorschlag is not null && s.Groesse == vorschlag))
                    .OrderByDescending(s => s.Passend).ThenBy(s => s.Inventarnummer)
                    .ToList();
                return new ArtikelAuswahl(a.Id, a.Bezeichnung, a.Groessenart, a.GeraeteartId is not null, vorschlag,
                    lager.Where(l => l.ArtikelId == a.Id).ToDictionary(l => l.Groesse, l => l.Menge), passende, hat.GetValueOrDefault(a.Id));
            })
            .ToList();

        if (Positionen.Count != Artikel.Count)
        {
            Positionen = Artikel.Select(a => new Eingabe { ArtikelId = a.Id, Groesse = a.Vorschlag }).ToList();
        }

        return true;
    }
}
