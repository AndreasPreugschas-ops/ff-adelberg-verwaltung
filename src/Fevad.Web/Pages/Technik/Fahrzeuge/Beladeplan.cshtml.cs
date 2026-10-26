using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>
/// Soll-Beladung eines Fahrzeugs je Fach (V59): lesen mit Soll/Ist, pflegen mit dem Recht Technik bearbeiten.
/// Normbeladungen werden als eigene Liste erfasst; DIN-Texte übernimmt FEVAD nicht.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikLesen)]
public class BeladeplanModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>So viele leere Zeilen bietet das Formular für neue Positionen an.</summary>
    public const int NeueZeilen = 5;

    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug Fahrzeug { get; private set; } = null!;

    /// <summary>Positionen zum Bearbeiten.</summary>
    [BindProperty]
    public List<Zeile> Zeilen { get; set; } = [];

    /// <summary>Soll/Ist je Position und Geräte ohne Soll.</summary>
    public IReadOnlyList<Beladeabgleich.Zeile> SollIst { get; private set; } = [];

    /// <summary>Darf der Benutzer den Plan ändern.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Auswahl Materialstamm.</summary>
    public IReadOnlyList<SelectListItem> Materialien { get; private set; } = [];

    /// <summary>Auswahl Geräteart.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];

    /// <summary>Eine Position im Formular.</summary>
    public sealed class Zeile
    {
        /// <summary>Kennung; leer für eine neue Position.</summary>
        public Guid? Id { get; set; }
        /// <summary>Fach.</summary>
        [StringLength(50)] public string? Fach { get; set; }
        /// <summary>Bezeichnung; leer heißt bei neuen Zeilen „nicht anlegen“.</summary>
        [StringLength(200)] public string? Bezeichnung { get; set; }
        /// <summary>Materialstamm.</summary>
        public Guid? MaterialId { get; set; }
        /// <summary>Geräteart, wenn kein Materialstamm.</summary>
        public Guid? GeraeteartId { get; set; }
        /// <summary>Soll-Menge.</summary>
        [Range(1, 999, ErrorMessage = "Menge 1 bis 999.")] public int Menge { get; set; } = 1;
        /// <summary>Bemerkung.</summary>
        [StringLength(500)] public string? Bemerkung { get; set; }
        /// <summary>Position entfernen.</summary>
        public bool Entfernen { get; set; }
    }

    /// <summary>Zeigt den Beladeplan.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Zeilen = (await db.Beladepositionen.AsNoTracking().Where(p => p.FahrzeugId == id).ToListAsync(ct))
            .OrderBy(p => Beladeabgleich.Fachschluessel(p.Fach), StringComparer.Ordinal).ThenBy(p => p.Sortierung)
            .Select(p => new Zeile { Id = p.Id, Fach = p.Fach, Bezeichnung = p.Bezeichnung, MaterialId = p.MaterialId, GeraeteartId = p.GeraeteartId, Menge = p.Menge, Bemerkung = p.Bemerkung })
            .Concat(Enumerable.Range(0, NeueZeilen).Select(_ => new Zeile()))
            .ToList();
        return Page();
    }

    /// <summary>Speichert die Positionen: ändert vorhandene, legt neue an, entfernt markierte.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Seite oder 403/404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        for (var i = 0; i < Zeilen.Count; i++)
        {
            var z = Zeilen[i];
            if (z.Entfernen || (z.Id is null && string.IsNullOrWhiteSpace(z.Bezeichnung)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(z.Bezeichnung))
            {
                ModelState.AddModelError($"Zeilen[{i}].Bezeichnung", "Bezeichnung fehlt.");
            }

            if (string.IsNullOrWhiteSpace(z.Fach))
            {
                ModelState.AddModelError($"Zeilen[{i}].Fach", "Fach fehlt.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var vorhanden = await db.Beladepositionen.Where(p => p.FahrzeugId == id).ToDictionaryAsync(p => p.Id, ct);
        var sortierung = 0;
        foreach (var z in Zeilen)
        {
            sortierung++;
            if (z.Id is { } posId && vorhanden.TryGetValue(posId, out var position))
            {
                if (z.Entfernen)
                {
                    db.Beladepositionen.Remove(position);
                    continue;
                }

                Uebernehmen(position, z, sortierung);
            }
            else if (z.Id is null && !z.Entfernen && !string.IsNullOrWhiteSpace(z.Bezeichnung))
            {
                var neu = new Beladeposition { FahrzeugId = id, Fach = "", Bezeichnung = "" };
                Uebernehmen(neu, z, sortierung);
                db.Beladepositionen.Add(neu);
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Beladeplan gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Legt für jede Kombination aus Fach und Material der zugeordneten Geräte eine Position an, die es noch nicht gibt.
    /// Hilft beim ersten Erfassen; danach wird der Plan von Hand gepflegt.
    /// </summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Seite oder 403/404.</returns>
    public async Task<IActionResult> OnPostAusIstAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        var vorhanden = await db.Beladepositionen.Where(p => p.FahrzeugId == id)
            .Select(p => new { p.Fach, p.MaterialId }).ToListAsync(ct);
        var bekannt = vorhanden.Select(p => (Beladeabgleich.Fachschluessel(p.Fach), p.MaterialId)).ToHashSet();
        var gruppen = (await db.Geraete.AsNoTracking()
                .Where(g => g.FahrzeugId == id && g.AusgesondertAm == null)
                .Select(g => new { g.Lagerort, g.MaterialId, Material = g.Material!.Bezeichnung })
                .ToListAsync(ct))
            .GroupBy(g => (Fach: string.IsNullOrWhiteSpace(g.Lagerort) ? "ohne Fach" : g.Lagerort.Trim(), g.MaterialId, g.Material))
            .Where(g => !bekannt.Contains((Beladeabgleich.Fachschluessel(g.Key.Fach), g.Key.MaterialId)))
            .OrderBy(g => g.Key.Fach, StringComparer.CurrentCulture).ThenBy(g => g.Key.Material, StringComparer.CurrentCulture)
            .ToList();
        var sortierung = vorhanden.Count;
        foreach (var g in gruppen)
        {
            db.Beladepositionen.Add(new Beladeposition
            {
                FahrzeugId = id,
                Fach = g.Key.Fach,
                Bezeichnung = g.Key.Material,
                MaterialId = g.Key.MaterialId,
                Menge = g.Count(),
                Sortierung = ++sortierung,
                Bemerkung = "aus den zugeordneten Geräten übernommen",
            });
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = gruppen.Count == 0 ? "Alle zugeordneten Geräte haben schon eine Soll-Position." : $"{gruppen.Count} Positionen aus den zugeordneten Geräten übernommen.";
        return RedirectToPage(new { id });
    }

    private static void Uebernehmen(Beladeposition p, Zeile z, int sortierung)
    {
        p.Fach = z.Fach!.Trim();
        p.Bezeichnung = z.Bezeichnung!.Trim();
        p.MaterialId = z.MaterialId;
        p.GeraeteartId = z.MaterialId is null ? z.GeraeteartId : null;
        p.Menge = z.Menge;
        p.Bemerkung = string.IsNullOrWhiteSpace(z.Bemerkung) ? null : z.Bemerkung.Trim();
        p.Sortierung = sortierung;
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var fahrzeug = await db.Fahrzeuge.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fahrzeug is null)
        {
            return false;
        }

        Fahrzeug = fahrzeug;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
        var positionen = await db.Beladepositionen.AsNoTracking().Where(p => p.FahrzeugId == id).ToListAsync(ct);
        SollIst = Beladeabgleich.SollIst(positionen, await DetailsModel.BeladungAsync(db, id, ct));
        Materialien = await db.Materialien.AsNoTracking()
            .Where(m => m.Aktiv || m.Geraete.Any(g => g.FahrzeugId == id))
            .OrderBy(m => m.Bezeichnung).ThenBy(m => m.Hersteller)
            .Select(m => new SelectListItem(m.Bezeichnung + (m.Hersteller == null ? "" : " · " + m.Hersteller) + (m.Typ == null ? "" : " · " + m.Typ), m.Id.ToString()))
            .ToListAsync(ct);
        Geraetearten = await db.Geraetearten.AsNoTracking()
            .Where(g => g.Aktiv && !g.FuerFahrzeuge)
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        return true;
    }
}
