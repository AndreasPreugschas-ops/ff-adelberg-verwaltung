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

[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Vorlagen { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }

        [Required(ErrorMessage = "Funkrufname fehlt."), StringLength(50), Display(Name = "Funkrufname (wie in alamos)")]
        public string Funkrufname { get; set; } = "";

        [Required(ErrorMessage = "Typ fehlt."), StringLength(50), Display(Name = "Typ, z. B. HLF 10")]
        public string Typ { get; set; } = "";

        [StringLength(20), Display(Name = "Kennzeichen")] public string? Kennzeichen { get; set; }
        [Display(Name = "Fahrgestell")] public string? Hersteller { get; set; }
        [Display(Name = "Aufbau")] public string? Aufbau { get; set; }
        [Range(1900, 2100, ErrorMessage = "Baujahr ungültig."), Display(Name = "Baujahr")] public int? Baujahr { get; set; }
        [Display(Name = "In Dienst seit")] public DateOnly? IndienstAm { get; set; }
        [Display(Name = "Außer Dienst seit")] public DateOnly? AusserDienstAm { get; set; }
        [Range(1, 99), Display(Name = "Sitzplätze")] public int? Sitzplaetze { get; set; }
        [Display(Name = "ISSI")] public string? Issi { get; set; }
        [Display(Name = "Prüfplan")] public Guid? PruefvorlageId { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var f = await db.Fahrzeuge.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (f is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = f.Id,
                Version = db.Entry(f).Property<uint>("Version").CurrentValue,
                Funkrufname = f.Funkrufname,
                Typ = f.Typ,
                Kennzeichen = f.Kennzeichen,
                Hersteller = f.Hersteller,
                Aufbau = f.Aufbau,
                Baujahr = f.Baujahr,
                IndienstAm = f.IndienstAm,
                AusserDienstAm = f.AusserDienstAm,
                Sitzplaetze = f.Sitzplaetze,
                Issi = f.Issi,
                PruefvorlageId = f.PruefvorlageId,
                Bemerkung = f.Bemerkung,
            };
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var funkrufname = (Formular.Funkrufname ?? "").Trim();
        if (await db.Fahrzeuge.IgnoreQueryFilters().AnyAsync(f => f.Funkrufname == funkrufname && f.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Funkrufname", "Diesen Funkrufnamen gibt es schon.");
        }

        if (Formular.PruefvorlageId is not null && !await db.Geraetearten.AnyAsync(g => g.Id == Formular.PruefvorlageId, ct))
        {
            ModelState.AddModelError("Formular.PruefvorlageId", "Unbekannter Prüfplan.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Fahrzeug fahrzeug;
        if (IstNeu)
        {
            fahrzeug = new Fahrzeug { Funkrufname = funkrufname, Typ = Formular.Typ };
            db.Fahrzeuge.Add(fahrzeug);
        }
        else
        {
            var vorhanden = await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            fahrzeug = vorhanden;
            db.Entry(fahrzeug).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        fahrzeug.Funkrufname = funkrufname;
        fahrzeug.Typ = Formular.Typ.Trim();
        fahrzeug.Kennzeichen = Formular.Kennzeichen;
        fahrzeug.Hersteller = Formular.Hersteller;
        fahrzeug.Aufbau = Formular.Aufbau;
        fahrzeug.Baujahr = Formular.Baujahr;
        fahrzeug.IndienstAm = Formular.IndienstAm;
        fahrzeug.AusserDienstAm = Formular.AusserDienstAm;
        fahrzeug.Sitzplaetze = Formular.Sitzplaetze;
        fahrzeug.Issi = Formular.Issi;
        fahrzeug.PruefvorlageId = Formular.PruefvorlageId;
        fahrzeug.Bemerkung = Formular.Bemerkung;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Das Fahrzeug wurde inzwischen von jemand anderem geändert. Bitte neu laden und die Änderungen erneut eintragen.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{fahrzeug.Funkrufname} gespeichert.";
        return RedirectToPage("Details", new { id = fahrzeug.Id });
    }

    private async Task AuswahlLadenAsync(CancellationToken ct) =>
        Vorlagen = await db.Geraetearten
            .Where(g => g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.PruefvorlageId))
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
}
