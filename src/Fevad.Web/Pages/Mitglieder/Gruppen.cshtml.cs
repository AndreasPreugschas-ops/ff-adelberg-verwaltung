using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>Gruppen (z. B. Gruppe 1, Führungsgruppe) anlegen und Mitglieder zuordnen; dienen als Zielgruppe im Dienstplan.</summary>
public class GruppenModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    public IReadOnlyList<Zeile> Gruppen { get; private set; } = [];
    public Gruppe? Auswahl { get; private set; }
    public IReadOnlyList<Kandidat> Kandidaten { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Bezeichnung fehlt.")]
    [StringLength(100)]
    public string? Bezeichnung { get; set; }

    public sealed record Zeile(Guid Id, string Bezeichnung, bool Aktiv, int Mitglieder);
    public sealed record Kandidat(Guid Id, string Name, string? Abteilung, bool Zugeordnet);

    public async Task OnGetAsync(Guid? id, CancellationToken ct) => await LadenAsync(id, ct);

    public async Task<IActionResult> OnPostAnlegenAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(null, ct);
            return Page();
        }

        var gruppe = new Gruppe
        {
            Code = "G" + Guid.CreateVersion7().ToString("N")[^8..].ToUpperInvariant(),
            Bezeichnung = Bezeichnung!.Trim(),
            Sortierung = await db.Gruppen.IgnoreQueryFilters().CountAsync(ct) * 10 + 10,
        };
        db.Gruppen.Add(gruppe);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Gruppe „{gruppe.Bezeichnung}“ angelegt. Jetzt Mitglieder zuordnen.";
        return RedirectToPage(new { id = gruppe.Id });
    }

    /// <summary>Speichert die angehakten Mitglieder als vollständige Besetzung der Gruppe.</summary>
    public async Task<IActionResult> OnPostZuordnenAsync(Guid id, List<Guid> mitglieder, bool aktiv, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var gruppe = await db.Gruppen.Include(g => g.Mitglieder).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gruppe is null)
        {
            return NotFound();
        }

        var gewaehlt = mitglieder.ToHashSet();
        db.GruppenMitgliedschaften.RemoveRange(gruppe.Mitglieder.Where(m => !gewaehlt.Contains(m.MitgliedId)));
        foreach (var mitgliedId in gewaehlt.Except(gruppe.Mitglieder.Select(m => m.MitgliedId)))
        {
            db.GruppenMitgliedschaften.Add(new GruppenMitgliedschaft { GruppeId = id, MitgliedId = mitgliedId });
        }

        gruppe.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Gruppe „{gruppe.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync() =>
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederBearbeiten)).Succeeded;

    private async Task LadenAsync(Guid? id, CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        Gruppen = await db.Gruppen.AsNoTracking()
            .OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new Zeile(g.Id, g.Bezeichnung, g.Aktiv, g.Mitglieder.Count))
            .ToListAsync(ct);

        if (id is null)
        {
            return;
        }

        Auswahl = await db.Gruppen.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (Auswahl is null)
        {
            return;
        }

        var heute = Anzeige.Heute(zeit);
        var zugeordnet = await db.GruppenMitgliedschaften.Where(g => g.GruppeId == id).Select(g => g.MitgliedId).ToListAsync(ct);
        Kandidaten = (await db.Mitglieder.AsNoTracking()
                .Where(m => zugeordnet.Contains(m.Id)
                    || m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
                .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
                .Select(m => new
                {
                    m.Id,
                    m.Nachname,
                    m.Vorname,
                    Abteilung = m.Mitgliedschaften
                        .Where(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
                        .Select(s => s.Abteilung!.Kurzbezeichnung ?? s.Abteilung.Code)
                        .FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(m => new Kandidat(m.Id, $"{m.Nachname}, {m.Vorname}", m.Abteilung, zugeordnet.Contains(m.Id)))
            .ToList();
    }
}
