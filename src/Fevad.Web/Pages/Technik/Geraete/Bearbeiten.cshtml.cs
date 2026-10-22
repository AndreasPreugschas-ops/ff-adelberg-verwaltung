using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }
        public Guid? BeschaffungId { get; set; }

        [Required(ErrorMessage = "Inventarnummer fehlt."), StringLength(30), Display(Name = "Inventarnummer")]
        public string Inventarnummer { get; set; } = "";

        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        [Required(ErrorMessage = "Geräteart fehlt."), Display(Name = "Geräteart (bestimmt den Prüfplan)")]
        public Guid? GeraeteartId { get; set; }

        [StringLength(100), Display(Name = "Hersteller")] public string? Hersteller { get; set; }
        [StringLength(100), Display(Name = "Typ")] public string? Typ { get; set; }
        [StringLength(100), Display(Name = "Seriennummer")] public string? Seriennummer { get; set; }
        [Range(1900, 2100, ErrorMessage = "Baujahr ungültig."), Display(Name = "Baujahr")] public int? Baujahr { get; set; }
        [Display(Name = "Herstelldatum")] public DateOnly? Herstelldatum { get; set; }
        [StringLength(20), Display(Name = "Größe")] public string? Groesse { get; set; }
        [Display(Name = "Beschafft am")] public DateOnly? BeschafftAm { get; set; }
        [Range(0, 9999999), Display(Name = "Anschaffungspreis (€)")] public decimal? Anschaffungspreis { get; set; }
        [Display(Name = "In Dienst seit")] public DateOnly? IndienstAm { get; set; }
        [Display(Name = "Verlastet auf")] public Guid? FahrzeugId { get; set; }
        [StringLength(50), Display(Name = "Lagerort / Fach")] public string? Lagerort { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? beschaffungId, CancellationToken ct)
    {
        if (id is not null)
        {
            var g = await db.Geraete.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (g is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = g.Id,
                Version = db.Entry(g).Property<uint>("Version").CurrentValue,
                BeschaffungId = g.BeschaffungId,
                Inventarnummer = g.Inventarnummer,
                Bezeichnung = g.Bezeichnung,
                GeraeteartId = g.GeraeteartId,
                Hersteller = g.Hersteller,
                Typ = g.Typ,
                Seriennummer = g.Seriennummer,
                Baujahr = g.Baujahr,
                Herstelldatum = g.Herstelldatum,
                Groesse = g.Groesse,
                BeschafftAm = g.BeschafftAm,
                Anschaffungspreis = g.Anschaffungspreis,
                IndienstAm = g.IndienstAm,
                FahrzeugId = g.FahrzeugId,
                Lagerort = g.Lagerort,
                Bemerkung = g.Bemerkung,
            };
        }
        else if (beschaffungId is not null && await db.Beschaffungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == beschaffungId, ct) is { } beschaffung)
        {
            // Eingang einer Beschaffung: Angaben übernehmen, Inventarnummer vergibt der Gerätewart.
            var stueckpreis = beschaffung.Istkosten ?? beschaffung.Kostenschaetzung;
            Formular = new Eingabe
            {
                BeschaffungId = beschaffung.Id,
                Bezeichnung = beschaffung.Bezeichnung,
                GeraeteartId = beschaffung.GeraeteartId,
                FahrzeugId = beschaffung.FahrzeugId,
                BeschafftAm = beschaffung.GeliefertAm,
                IndienstAm = beschaffung.GeliefertAm,
                Anschaffungspreis = stueckpreis is null ? null : Math.Round(stueckpreis.Value / Math.Max(beschaffung.Menge, 1), 2),
            };
        }
        else
        {
            Formular.IndienstAm = Anzeige.Heute(zeit);
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Der eindeutige Index gilt auch für gelöschte Geräte.
        var nummer = (Formular.Inventarnummer ?? "").Trim();
        if (await db.Geraete.IgnoreQueryFilters().AnyAsync(g => g.Inventarnummer == nummer && g.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Inventarnummer", "Diese Inventarnummer ist schon vergeben.");
        }

        if (Formular.GeraeteartId is not null && !await db.Geraetearten.AnyAsync(g => g.Id == Formular.GeraeteartId && !g.FuerFahrzeuge, ct))
        {
            ModelState.AddModelError("Formular.GeraeteartId", "Unbekannte Geräteart.");
        }

        if (Formular.FahrzeugId is not null && !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
        {
            ModelState.AddModelError("Formular.FahrzeugId", "Unbekanntes Fahrzeug.");
        }

        if (Formular.BeschaffungId is not null && !await db.Beschaffungen.AnyAsync(b => b.Id == Formular.BeschaffungId, ct))
        {
            Formular.BeschaffungId = null;
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Geraet geraet;
        if (IstNeu)
        {
            geraet = new Geraet { Inventarnummer = nummer, Bezeichnung = Formular.Bezeichnung, BeschaffungId = Formular.BeschaffungId };
            db.Geraete.Add(geraet);
        }
        else
        {
            var vorhanden = await db.Geraete.FirstOrDefaultAsync(g => g.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            geraet = vorhanden;
            db.Entry(geraet).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        geraet.Inventarnummer = nummer;
        geraet.Bezeichnung = Formular.Bezeichnung.Trim();
        geraet.GeraeteartId = Formular.GeraeteartId!.Value;
        geraet.Hersteller = Formular.Hersteller;
        geraet.Typ = Formular.Typ;
        geraet.Seriennummer = Formular.Seriennummer;
        geraet.Baujahr = Formular.Baujahr;
        geraet.Herstelldatum = Formular.Herstelldatum;
        geraet.Groesse = Formular.Groesse;
        geraet.BeschafftAm = Formular.BeschafftAm;
        geraet.Anschaffungspreis = Formular.Anschaffungspreis;
        geraet.IndienstAm = Formular.IndienstAm;
        geraet.FahrzeugId = Formular.FahrzeugId;
        geraet.Lagerort = Formular.Lagerort;
        geraet.Bemerkung = Formular.Bemerkung;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Das Gerät wurde inzwischen von jemand anderem geändert. Bitte neu laden und die Änderungen erneut eintragen.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{geraet.Inventarnummer} {geraet.Bezeichnung} gespeichert.";
        return RedirectToPage("Details", new { id = geraet.Id });
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Geraetearten = await db.Geraetearten
            .Where(g => !g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.GeraeteartId))
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge
            .Where(f => f.AusserDienstAm == null || f.Id == Formular.FahrzeugId)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString()))
            .ToListAsync(ct);
    }
}
