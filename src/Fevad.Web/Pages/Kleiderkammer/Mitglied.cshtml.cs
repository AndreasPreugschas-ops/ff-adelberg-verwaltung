using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Größenprofil, aktuelle Ausstattung und Quittungen eines Mitglieds; Rücknahme.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class MitgliedModel(AppDbContext db, Kleiderkammerbuchungen buchungen, Kleiderkammerstand stand, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Personalnummer { get; private set; } = "";
    public bool DarfBearbeiten { get; private set; }

    [BindProperty]
    public Dictionary<Groessenart, string?> Groessen { get; set; } = [];

    [BindProperty]
    public List<RuecknahmeEingabe> Ruecknahme { get; set; } = [];

    [BindProperty]
    public string? Bemerkung { get; set; }

    public IReadOnlyList<AusstattungZeile> Ausstattung { get; private set; } = [];
    public IReadOnlyList<Kleiderkammervorgang> Vorgaenge { get; private set; } = [];

    public sealed class RuecknahmeEingabe
    {
        public Guid AusstattungId { get; set; }
        public bool Gewaehlt { get; set; }
        public int Menge { get; set; }
        public RuecknahmeZustand Zustand { get; set; }
    }

    public sealed record AusstattungZeile(
        Guid Id, string Artikel, string Groesse, int Menge, string? Inventarnummer, DateOnly AusgegebenAm,
        DateOnly? NaechstePruefung, bool NieGeprueft, DateOnly? Aussonderung, Fristampel? Ampel);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostGroessenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await buchungen.GroessenSetzenAsync(id, Groessen, ct);
        TempData["Meldung"] = "Größen gespeichert.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRuecknahmeAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var positionen = Ruecknahme
            .Where(r => r.Gewaehlt)
            .Select(r => new RuecknahmePosition(r.AusstattungId, r.Menge, r.Zustand))
            .ToList();
        try
        {
            var vorgang = await buchungen.ZuruecknehmenAsync(id, positionen, Bemerkung, ct);
            TempData["Meldung"] = $"Rücknahme {vorgang.Nummer} gebucht.";
            return RedirectToPage("Quittung", new { id = vorgang.Id });
        }
        catch (KleiderkammerFehler fehler)
        {
            TempData["Fehler"] = fehler.Message;
            return RedirectToPage(new { id });
        }
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var mitglied = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Personalnummer })
            .FirstOrDefaultAsync(ct);
        if (mitglied is null)
        {
            return false;
        }

        Id = mitglied.Id;
        Name = $"{mitglied.Nachname}, {mitglied.Vorname}";
        Personalnummer = mitglied.Personalnummer;
        DarfBearbeiten = await DarfAsync();

        Groessen = await db.Groessenangaben.AsNoTracking()
            .Where(g => g.MitgliedId == id)
            .ToDictionaryAsync(g => g.Groessenart, g => (string?)g.Wert, ct);

        var heute = Anzeige.Heute(zeit);
        var psa = (await stand.PsaAsync(heute, ct)).Where(p => p.MitgliedId == id).ToDictionary(p => p.Stand.Id);
        var zeilen = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.MitgliedId == id && a.ZurueckAm == null)
            .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.AusgegebenAm)
            .Select(a => new { a.Id, Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.GeraetId, Inventarnummer = a.Geraet!.Inventarnummer, a.AusgegebenAm })
            .ToListAsync(ct);
        Ausstattung = zeilen
            .Select(a =>
            {
                var p = a.GeraetId is { } g ? psa.GetValueOrDefault(g) : null;
                return new AusstattungZeile(a.Id, a.Artikel, a.Groesse, a.Menge, a.Inventarnummer, a.AusgegebenAm,
                    p?.Stand.NaechsterTermin, p?.Stand.NieGeprueft == true, p?.Stand.AussonderungFaellig, p?.Stand.Ampel);
            })
            .ToList();
        Ruecknahme = Ausstattung.Select(a => new RuecknahmeEingabe { AusstattungId = a.Id, Menge = a.Menge }).ToList();

        Vorgaenge = await db.Kleiderkammervorgaenge.AsNoTracking()
            .Where(v => v.MitgliedId == id)
            .OrderByDescending(v => v.Zeitpunkt)
            .ToListAsync(ct);
        return true;
    }
}
