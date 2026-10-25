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

/// <summary>Materialstamm anlegen oder bearbeiten.</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neuer Materialstamm angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Auswahl der Gerätearten.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    /// <summary>Ab diesem Stückwert in Euro wird inventarisiert (Parameter).</summary>
    public int InventarisierungAbWert { get; private set; }
    /// <summary>Bis zu diesem Stückwert in Euro wird direkt abgeschrieben (Parameter).</summary>
    public int DirektabschreibungBisWert { get; private set; }
    /// <summary>Zahl der Geräte dieses Materials im Bestand.</summary>
    public int Bestand { get; private set; }

    /// <summary>Formular eines Materialstamms.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuem Materialstamm.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")]
        public string? Bezeichnung { get; set; }

        /// <summary>Hersteller.</summary>
        [StringLength(100), Display(Name = "Hersteller")] public string? Hersteller { get; set; }
        /// <summary>Typ oder Modell.</summary>
        [StringLength(100), Display(Name = "Typ / Modell")] public string? Typ { get; set; }
        /// <summary>Teilenummer des Herstellers.</summary>
        [StringLength(100), Display(Name = "Herstellerteilenummer")] public string? Herstellerteilenummer { get; set; }

        /// <summary>Geräteart; bestimmt den Prüfplan.</summary>
        [Required(ErrorMessage = "Geräteart fehlt."), Display(Name = "Geräteart (bestimmt den Prüfplan)")]
        public Guid? GeraeteartId { get; set; }

        /// <summary>Stückwert netto in Euro.</summary>
        [Range(0, 9999999), Display(Name = "Stückwert netto (€)")] public decimal? Stueckwert { get; set; }

        /// <summary>Leer beim neuen Materialstamm: aus dem Stückwert vorschlagen.</summary>
        [Display(Name = "inventarisierungspflichtig (Inventarnummer je Stück)")] public bool? Inventarisierungspflichtig { get; set; }
        /// <summary>Stücke werden bei Inventuren gezählt.</summary>
        [Display(Name = "inventurpflichtig (bei Inventuren zählen)")] public bool Inventurpflichtig { get; set; } = true;
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
        /// <summary>In Auswahllisten angeboten.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt einen Materialstamm oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Materialstamms; leer für einen neuen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
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

    /// <summary>Speichert den Materialstamm.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, bei Fehlern die Seite oder 404.</returns>
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
