using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Technik;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung;

/// <summary>
/// Lieferung wird Gerät (V60): Aus einer gelieferten Beschaffung entstehen die noch fehlenden Geräte in einem Schritt,
/// mit fortlaufenden Inventarnummern, Preis, Standort und Anlagendaten; jedes bleibt bis zur Eingangsprüfung gesperrt.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class EingangModel(AppDbContext db, ParameterLeser parameter, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Höchstzahl der Geräte je Buchung.</summary>
    public const int HoechstZahl = 200;

    /// <summary>Wert der Materialauswahl für „neuen Materialstamm anlegen“.</summary>
    public const string NeuesMaterial = "neu";

    /// <summary>Die Beschaffung.</summary>
    public Domain.Technik.Beschaffung Beschaffung { get; private set; } = null!;

    /// <summary>Schon inventarisierte Geräte dieser Beschaffung.</summary>
    public int Vorhanden { get; private set; }

    /// <summary>Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Auswahl Materialstamm.</summary>
    public IReadOnlyList<SelectListItem> Materialien { get; private set; } = [];

    /// <summary>Auswahl Geräteart für einen neuen Materialstamm.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];

    /// <summary>Auswahl Fahrzeug.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Auswahl Inventurgruppe.</summary>
    public IReadOnlyList<SelectListItem> Inventurgruppen { get; private set; } = [];

    /// <summary>Angaben für alle Geräte der Lieferung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Id eines Materialstamms oder <see cref="NeuesMaterial"/>.</summary>
        [Required(ErrorMessage = "Bitte einen Materialstamm wählen oder neu anlegen."), Display(Name = "Materialstamm")]
        public string? Material { get; set; }

        /// <summary>Bezeichnung eines neuen Materialstamms.</summary>
        [StringLength(200), Display(Name = "Bezeichnung")] public string? NeuBezeichnung { get; set; }

        /// <summary>Hersteller eines neuen Materialstamms.</summary>
        [StringLength(100), Display(Name = "Hersteller")] public string? NeuHersteller { get; set; }

        /// <summary>Typ eines neuen Materialstamms.</summary>
        [StringLength(100), Display(Name = "Typ / Modell")] public string? NeuTyp { get; set; }

        /// <summary>Geräteart eines neuen Materialstamms.</summary>
        [Display(Name = "Geräteart (bestimmt den Prüfplan)")] public Guid? NeuGeraeteartId { get; set; }

        /// <summary>Anzahl der anzulegenden Geräte.</summary>
        [Range(1, HoechstZahl, ErrorMessage = "Anzahl ungültig."), Display(Name = "Anzahl")] public int Anzahl { get; set; } = 1;

        /// <summary>Erste Inventarnummer; die weiteren zählen hoch.</summary>
        [Required(ErrorMessage = "Erste Inventarnummer fehlt."), StringLength(30), Display(Name = "Erste Inventarnummer")]
        public string ErsteNummer { get; set; } = "";

        /// <summary>Seriennummern, eine je Zeile in der Reihenfolge der Inventarnummern.</summary>
        [StringLength(10000), Display(Name = "Seriennummern (eine je Zeile, optional)")] public string? Seriennummern { get; set; }

        /// <summary>Preis je Stück.</summary>
        [Range(0, 9999999), Display(Name = "Anschaffungspreis brutto je Stück (€)")] public decimal? Stueckpreis { get; set; }

        /// <summary>Lieferdatum.</summary>
        [Display(Name = "Beschafft am")] public DateOnly? BeschafftAm { get; set; }

        /// <summary>Fahrzeug.</summary>
        [Display(Name = "Verlastet auf")] public Guid? FahrzeugId { get; set; }

        /// <summary>Lagerort bzw. Fach.</summary>
        [StringLength(50), Display(Name = "Lagerort / Fach")] public string? Lagerort { get; set; }

        /// <summary>Inventurgruppe.</summary>
        [Display(Name = "Inventurgruppe")] public Guid? InventurgruppeId { get; set; }

        /// <summary>Anlagenbuchhaltung und Förderung, für jedes Gerät gleich.</summary>
        public AnlageEingabe Anlage { get; set; } = new();

        /// <summary>Geräte bis zur Eingangsprüfung sperren.</summary>
        [Display(Name = "Eingangsprüfung vor der ersten Nutzung fordern (Geräte bis dahin gesperrt)")]
        public bool Eingangspruefung { get; set; } = true;
    }

    /// <summary>Formular mit den Angaben der Beschaffung vorbelegen.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Beschaffung.Status != BeschaffungStatus.Geliefert)
        {
            TempData["Fehler"] = "Geräte entstehen erst, wenn der Eingang gebucht ist.";
            return RedirectToPage("Bearbeiten", new { id });
        }

        var stueckpreis = Beschaffung.Istkosten ?? Beschaffung.Kostenschaetzung;
        Formular = new Eingabe
        {
            Material = NeuesMaterial,
            NeuBezeichnung = Beschaffung.Bezeichnung,
            NeuGeraeteartId = Beschaffung.GeraeteartId,
            Anzahl = Math.Clamp(Beschaffung.Menge - Vorhanden, 1, HoechstZahl),
            Stueckpreis = stueckpreis is null ? null : Math.Round(stueckpreis.Value / Math.Max(Beschaffung.Menge, 1), 2),
            BeschafftAm = Beschaffung.GeliefertAm,
            FahrzeugId = Beschaffung.FahrzeugId,
            Anlage = new AnlageEingabe { Foerdergeber = Beschaffung.Foerderfaehig ? "Land BW (Z-Feu)" : null },
        };
        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Legt die Geräte an; bei einem Fehler entsteht keines.</summary>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Beschaffung.Status != BeschaffungStatus.Geliefert)
        {
            return RedirectToPage("Bearbeiten", new { id });
        }

        Material? material = null;
        if (Formular.Material == NeuesMaterial)
        {
            if (string.IsNullOrWhiteSpace(Formular.NeuBezeichnung))
            {
                ModelState.AddModelError("Formular.NeuBezeichnung", "Bezeichnung des Materialstamms fehlt.");
            }

            if (Formular.NeuGeraeteartId is null || !await db.Geraetearten.AnyAsync(g => g.Id == Formular.NeuGeraeteartId && !g.FuerFahrzeuge, ct))
            {
                ModelState.AddModelError("Formular.NeuGeraeteartId", "Bitte eine Geräteart wählen.");
            }
        }
        else if (Guid.TryParse(Formular.Material, out var materialId))
        {
            material = await db.Materialien.FirstOrDefaultAsync(m => m.Id == materialId, ct);
            if (material is null)
            {
                ModelState.AddModelError("Formular.Material", "Unbekannter Materialstamm.");
            }
        }

        var nummern = ModelState.IsValid ? Inventarnummern.Folge(Formular.ErsteNummer, Formular.Anzahl) : [];
        if (nummern.Any(n => n.Length > 30))
        {
            ModelState.AddModelError("Formular.ErsteNummer", "Die Inventarnummern werden zu lang (höchstens 30 Zeichen).");
        }
        else if (nummern.Count > 0)
        {
            // Der eindeutige Index gilt auch für gelöschte Geräte.
            var belegt = await db.Geraete.IgnoreQueryFilters().Where(g => nummern.Contains(g.Inventarnummer)).Select(g => g.Inventarnummer).OrderBy(n => n).ToListAsync(ct);
            if (belegt.Count > 0)
            {
                ModelState.AddModelError("Formular.ErsteNummer", $"Schon vergeben: {string.Join(", ", belegt.Take(5))}{(belegt.Count > 5 ? " …" : "")}.");
            }
        }

        var seriennummern = (Formular.Seriennummern ?? "").Split('\n').Select(z => z.Trim()).Where(z => z.Length > 0).ToList();
        if (seriennummern.Count > Formular.Anzahl)
        {
            ModelState.AddModelError("Formular.Seriennummern", $"{seriennummern.Count} Seriennummern für {Formular.Anzahl} Geräte.");
        }
        else if (seriennummern.Any(z => z.Length > 100))
        {
            ModelState.AddModelError("Formular.Seriennummern", "Eine Seriennummer ist länger als 100 Zeichen.");
        }

        if (Formular.FahrzeugId is not null && !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
        {
            ModelState.AddModelError("Formular.FahrzeugId", "Unbekanntes Fahrzeug.");
        }

        if (Formular.InventurgruppeId is not null && !await db.Inventurgruppen.AnyAsync(g => g.Id == Formular.InventurgruppeId, ct))
        {
            ModelState.AddModelError("Formular.InventurgruppeId", "Unbekannte Inventurgruppe.");
        }

        var inventurpflichtig = material?.Inventurpflichtig ?? true;
        if (ModelState.IsValid && inventurpflichtig && Formular.InventurgruppeId is null && Formular.FahrzeugId is null)
        {
            ModelState.AddModelError("Formular.InventurgruppeId", "Das Material ist inventurpflichtig. Bitte eine Inventurgruppe wählen oder auf ein Fahrzeug verlasten.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        if (material is null)
        {
            material = await Materialstamm.FindenOderAnlegenAsync(db, Formular.NeuBezeichnung!, Formular.NeuGeraeteartId!.Value, Formular.NeuHersteller, Formular.NeuTyp, inventurpflichtig, ct);
            if (db.Entry(material).State == EntityState.Added)
            {
                var abWert = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, Anzeige.Heute(zeit), 150, ct);
                material.Stueckwert = Formular.Stueckpreis;
                material.Inventarisierungspflichtig = Domain.Technik.Material.InventarisierungVorschlagen(Formular.Stueckpreis, abWert) || inventurpflichtig;
            }
        }

        var gruppeId = Formular.InventurgruppeId
            ?? (Formular.FahrzeugId is { } fahrzeugId ? (await Materialstamm.FahrzeuggruppeAsync(db, fahrzeugId, ct)).Id : null);
        var jetzt = zeit.GetUtcNow();
        var mitgliedId = (await benutzer.GetUserAsync(User))?.MitgliedId;
        for (var i = 0; i < nummern.Count; i++)
        {
            var geraet = new Geraet
            {
                Inventarnummer = nummern[i],
                Bezeichnung = material.Bezeichnung,
                BeschaffungId = Beschaffung.Id,
                Seriennummer = i < seriennummern.Count ? seriennummern[i] : null,
                BeschafftAm = Formular.BeschafftAm,
                IndienstAm = Formular.BeschafftAm,
                Anschaffungspreis = Formular.Stueckpreis ?? material.Stueckwert,
                FahrzeugId = Formular.FahrzeugId,
                Lagerort = string.IsNullOrWhiteSpace(Formular.Lagerort) ? null : Formular.Lagerort.Trim(),
                InventurgruppeId = gruppeId,
            };
            Materialstamm.Zuordnen(geraet, material);
            Formular.Anlage.Uebernehmen(geraet);
            db.Geraete.Add(geraet);
            if (Formular.Eingangspruefung)
            {
                db.Maengel.Add(Mangel.Eingangssperre(geraet.Id, jetzt, mitgliedId));
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = nummern.Count == 1
            ? $"Gerät {nummern[0]} angelegt."
            : $"{nummern.Count} Geräte angelegt ({nummern[0]} bis {nummern[^1]})." + (Formular.Eingangspruefung ? " Sie sind bis zur Eingangsprüfung gesperrt." : "");
        return RedirectToPage("Bearbeiten", new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var beschaffung = await db.Beschaffungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beschaffung is null)
        {
            return false;
        }

        Beschaffung = beschaffung;
        Vorhanden = await db.Geraete.CountAsync(g => g.BeschaffungId == id, ct);
        return true;
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Materialien = await db.Materialien.Where(m => m.Aktiv)
            .OrderBy(m => m.Bezeichnung).ThenBy(m => m.Hersteller)
            .Select(m => new SelectListItem(m.Bezeichnung + (m.Hersteller == null ? "" : " · " + m.Hersteller) + (m.Typ == null ? "" : " · " + m.Typ), m.Id.ToString()))
            .ToListAsync(ct);
        Geraetearten = await db.Geraetearten.Where(g => !g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.NeuGeraeteartId)).OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.Where(f => f.AusserDienstAm == null || f.Id == Formular.FahrzeugId).OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString())).ToListAsync(ct);
        Inventurgruppen = await db.Inventurgruppen.Where(g => g.Aktiv || g.Id == Formular.InventurgruppeId).OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
    }
}
