using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraetearten;

/// <summary>Katalog der Bauteilarten mit Einbau- und Gesamtfrist (V61); lesen alle mit Technik-Zugang, pflegen mit TechnikBearbeiten.</summary>
public class BauteilartenModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>So viele leere Zeilen für neue Bauteilarten.</summary>
    public const int NeueZeilen = 2;

    /// <summary>Zeilen des Formulars.</summary>
    [BindProperty]
    public List<Eingabe> Zeilen { get; set; } = [];

    /// <summary>Gerätearten zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];

    /// <summary>Darf pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Eine Bauteilart im Formular.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuen Zeilen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Kurzschlüssel.</summary>
        [StringLength(20)] public string? Code { get; set; }
        /// <summary>Bezeichnung.</summary>
        [StringLength(100)] public string? Bezeichnung { get; set; }
        /// <summary>Geräteart.</summary>
        public Guid? GeraeteartId { get; set; }
        /// <summary>Höchstdauer eingebaut in Monaten.</summary>
        [Range(1, 600)] public int? EingebautMonate { get; set; }
        /// <summary>Höchstalter ab Herstellung in Monaten.</summary>
        [Range(1, 600)] public int? GesamtMonate { get; set; }
        /// <summary>Herkunft der Fristen.</summary>
        [StringLength(200)] public string? Grundlage { get; set; }
        /// <summary>Aktiv.</summary>
        public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt den Katalog.</summary>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(true, ct);

    /// <summary>Speichert Änderungen und neue Zeilen.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var arten = await db.Geraetearten.Select(g => g.Id).ToListAsync(ct);
        var vorhanden = await db.Bauteilarten.IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, ct);
        var codes = vorhanden.Values.ToDictionary(b => b.Code, b => b.Id, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Zeilen.Count; i++)
        {
            var z = Zeilen[i];
            if (z.Id is null && string.IsNullOrWhiteSpace(z.Code) && string.IsNullOrWhiteSpace(z.Bezeichnung))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(z.Code) || string.IsNullOrWhiteSpace(z.Bezeichnung) || z.GeraeteartId is not { } art || !arten.Contains(art))
            {
                ModelState.AddModelError($"Zeilen[{i}].Code", $"Zeile {i + 1}: Code, Bezeichnung und Geräteart sind Pflicht.");
                continue;
            }

            if (z.EingebautMonate is null && z.GesamtMonate is null)
            {
                ModelState.AddModelError($"Zeilen[{i}].EingebautMonate", $"Zeile {i + 1}: mindestens eine Frist angeben.");
                continue;
            }

            var code = z.Code.Trim().ToUpperInvariant();
            if (codes.TryGetValue(code, out var andere) && andere != z.Id)
            {
                ModelState.AddModelError($"Zeilen[{i}].Code", $"Zeile {i + 1}: Code {code} gibt es schon.");
                continue;
            }

            Bauteilart eintrag;
            if (z.Id is { } id && vorhanden.TryGetValue(id, out var gefunden))
            {
                eintrag = gefunden;
                eintrag.Code = code;
                eintrag.Bezeichnung = z.Bezeichnung.Trim();
            }
            else
            {
                eintrag = new Bauteilart { Code = code, Bezeichnung = z.Bezeichnung.Trim(), Sortierung = (vorhanden.Count + i + 1) * 10 };
                db.Bauteilarten.Add(eintrag);
            }

            eintrag.GeraeteartId = art;
            eintrag.EingebautMonate = z.EingebautMonate;
            eintrag.GesamtMonate = z.GesamtMonate;
            eintrag.Grundlage = string.IsNullOrWhiteSpace(z.Grundlage) ? null : z.Grundlage.Trim();
            eintrag.Aktiv = z.Aktiv;
            codes[code] = eintrag.Id;
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(false, ct);
            return Page();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Bauteilarten gespeichert.";
        return RedirectToPage();
    }

    private async Task LadenAsync(bool zeilen, CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
        Geraetearten = await db.Geraetearten.AsNoTracking().Where(g => !g.FuerFahrzeuge).OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
        if (zeilen)
        {
            Zeilen = await db.Bauteilarten.AsNoTracking().OrderBy(b => b.Sortierung)
                .Select(b => new Eingabe { Id = b.Id, Code = b.Code, Bezeichnung = b.Bezeichnung, GeraeteartId = b.GeraeteartId, EingebautMonate = b.EingebautMonate, GesamtMonate = b.GesamtMonate, Grundlage = b.Grundlage, Aktiv = b.Aktiv })
                .ToListAsync(ct);
            if (DarfBearbeiten)
            {
                Zeilen.AddRange(Enumerable.Range(0, NeueZeilen).Select(_ => new Eingabe()));
            }
        }
    }
}
