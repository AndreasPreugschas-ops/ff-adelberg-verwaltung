using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Materialien;

[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    public int InventarisierungAbWert { get; private set; }
    public int DirektabschreibungBisWert { get; private set; }
    public int Bestand { get; private set; }

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }

        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")]
        public string? Bezeichnung { get; set; }

        [StringLength(100), Display(Name = "Hersteller")] public string? Hersteller { get; set; }
        [StringLength(100), Display(Name = "Typ / Modell")] public string? Typ { get; set; }
        [StringLength(100), Display(Name = "Herstellerteilenummer")] public string? Herstellerteilenummer { get; set; }

        [Required(ErrorMessage = "Geräteart fehlt."), Display(Name = "Geräteart (bestimmt den Prüfplan)")]
        public Guid? GeraeteartId { get; set; }

        [Range(0, 9999999), Display(Name = "Stückwert netto (€)")] public decimal? Stueckwert { get; set; }

        /// <summary>Leer beim neuen Materialstamm: aus dem Stückwert vorschlagen.</summary>
        [Display(Name = "inventarisierungspflichtig (Inventarnummer je Stück)")] public bool? Inventarisierungspflichtig { get; set; }
        [Display(Name = "inventurpflichtig (bei Inventuren zählen)")] public bool Inventurpflichtig { get; set; } = true;
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var m = await db.Materialien.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = m.Id,
                Version = db.Entry(m).Property<uint>("Version").CurrentValue,
                Bezeichnung = m.Bezeichnung,
                Hersteller = m.Hersteller,
                Typ = m.Typ,
                Herstellerteilenummer = m.Herstellerteilenummer,
                GeraeteartId = m.GeraeteartId,
                Stueckwert = m.Stueckwert,
                Inventarisierungspflichtig = m.Inventarisierungspflichtig,
                Inventurpflichtig = m.Inventurpflichtig,
                Bemerkung = m.Bemerkung,
                Aktiv = m.Aktiv,
            };
        }

        await LadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Formular.GeraeteartId is not null && !await db.Geraetearten.AnyAsync(g => g.Id == Formular.GeraeteartId && !g.FuerFahrzeuge, ct))
        {
            ModelState.AddModelError("Formular.GeraeteartId", "Unbekannte Geräteart.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var abWert = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, Anzeige.Heute(zeit), 150, ct);
        var inventarisieren = Formular.Inventarisierungspflichtig ?? Domain.Technik.Material.InventarisierungVorschlagen(Formular.Stueckwert, abWert);

        Domain.Technik.Material material;
        if (IstNeu)
        {
            material = new Domain.Technik.Material { Bezeichnung = "" };
            db.Materialien.Add(material);
        }
        else
        {
            var vorhanden = await db.Materialien.FirstOrDefaultAsync(m => m.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            material = vorhanden;
            db.Entry(material).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        var neueArt = material.GeraeteartId != Formular.GeraeteartId!.Value;
        material.Bezeichnung = Formular.Bezeichnung!.Trim();
        material.Hersteller = Leer(Formular.Hersteller);
        material.Typ = Leer(Formular.Typ);
        material.Herstellerteilenummer = Leer(Formular.Herstellerteilenummer);
        material.GeraeteartId = Formular.GeraeteartId.Value;
        material.Stueckwert = Formular.Stueckwert;

        // Inventur setzt Inventarisierung voraus.
        material.Inventurpflichtig = Formular.Inventurpflichtig && inventarisieren;
        material.Inventarisierungspflichtig = inventarisieren;
        material.Bemerkung = Leer(Formular.Bemerkung);
        material.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Materialstamm wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            await LadenAsync(ct);
            return Page();
        }

        // Prüfpflicht folgt dem Materialstamm: alle Geräte bekommen die neue Geräteart.
        if (!IstNeu && neueArt)
        {
            await db.Geraete.Where(g => g.MaterialId == material.Id)
                .ExecuteUpdateAsync(g => g.SetProperty(x => x.GeraeteartId, material.GeraeteartId), ct);
        }

        TempData["Meldung"] = $"{material.Bezeichnung} gespeichert.";
        return RedirectToPage("Index");
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        InventarisierungAbWert = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, heute, 150, ct);
        DirektabschreibungBisWert = await parameter.ZahlAsync(ParameterSchluessel.DirektabschreibungBisWertEuro, heute, 1000, ct);
        Geraetearten = await db.Geraetearten
            .Where(g => !g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.GeraeteartId))
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        Bestand = Formular.Id is null ? 0 : await db.Geraete.CountAsync(g => g.MaterialId == Formular.Id && g.AusgesondertAm == null, ct);
    }
}
