using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung;

public class BearbeitenModel(AppDbContext db, IAuthorizationService autorisierung, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public BeschaffungStatus Status { get; private set; } = BeschaffungStatus.Bedarf;
    public IReadOnlyList<BeschaffungStatus> Folgestatus { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }
    public bool DarfFreigeben { get; private set; }
    public string? Mangel { get; private set; }
    public IReadOnlyList<(Guid Id, string Nummer, string Bezeichnung)> Geraete { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }
        public Guid? MangelId { get; set; }

        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Was wird gebraucht?")]
        public string Bezeichnung { get; set; } = "";

        [Range(1, 10000, ErrorMessage = "Menge ungültig."), Display(Name = "Menge")] public int Menge { get; set; } = 1;
        [StringLength(2000), Display(Name = "Begründung")] public string? Begruendung { get; set; }
        [Display(Name = "Priorität")] public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;
        [Display(Name = "Geräteart")] public Guid? GeraeteartId { get; set; }
        [Display(Name = "Für Fahrzeug")] public Guid? FahrzeugId { get; set; }
        [Range(2000, 2100, ErrorMessage = "Haushaltsjahr ungültig."), Display(Name = "Haushaltsjahr")] public int? Haushaltsjahr { get; set; }
        [Range(0, 9999999), Display(Name = "Kostenschätzung gesamt (€)")] public decimal? Kostenschaetzung { get; set; }
        [Display(Name = "förderfähig (Z-Feu)")] public bool Foerderfaehig { get; set; }
        [StringLength(200), Display(Name = "Lieferant")] public string? Lieferant { get; set; }
        [StringLength(50), Display(Name = "Bestellnummer")] public string? Bestellnummer { get; set; }
        [Display(Name = "Bestellt am")] public DateOnly? BestelltAm { get; set; }
        [Display(Name = "Geliefert am")] public DateOnly? GeliefertAm { get; set; }
        [Range(0, 9999999), Display(Name = "Istkosten gesamt (€)")] public decimal? Istkosten { get; set; }
        [StringLength(2000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? mangelId, CancellationToken ct)
    {
        if (id is not null)
        {
            var b = await db.Beschaffungen.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (b is null)
            {
                return NotFound();
            }

            Status = b.Status;
            Formular = new Eingabe
            {
                Id = b.Id,
                Version = db.Entry(b).Property<uint>("Version").CurrentValue,
                MangelId = b.MangelId,
                Bezeichnung = b.Bezeichnung,
                Menge = b.Menge,
                Begruendung = b.Begruendung,
                Prioritaet = b.Prioritaet,
                GeraeteartId = b.GeraeteartId,
                FahrzeugId = b.FahrzeugId,
                Haushaltsjahr = b.Haushaltsjahr,
                Kostenschaetzung = b.Kostenschaetzung,
                Foerderfaehig = b.Foerderfaehig,
                Lieferant = b.Lieferant,
                Bestellnummer = b.Bestellnummer,
                BestelltAm = b.BestelltAm,
                GeliefertAm = b.GeliefertAm,
                Istkosten = b.Istkosten,
                Bemerkung = b.Bemerkung,
            };
        }
        else
        {
            if (!await DarfAsync(Berechtigungen.BeschaffungBearbeiten))
            {
                return Forbid();
            }

            Formular.Haushaltsjahr = Anzeige.Heute(zeit).Year + 1;
            if (mangelId is not null && await db.Maengel.AsNoTracking().Include(m => m.Geraet).FirstOrDefaultAsync(m => m.Id == mangelId, ct) is { } mangel)
            {
                // Ersatzbeschaffung aus einem Mangel.
                Formular.MangelId = mangel.Id;
                Formular.Bezeichnung = mangel.Geraet is null ? "" : $"Ersatz für {mangel.Geraet.Bezeichnung}";
                Formular.GeraeteartId = mangel.Geraet?.GeraeteartId;
                Formular.FahrzeugId = mangel.Geraet?.FahrzeugId ?? mangel.FahrzeugId;
                Formular.Begruendung = mangel.Beschreibung;
                Formular.Prioritaet = mangel.Schwere == MangelSchwere.AusserBetrieb ? Prioritaet.Einsatzrelevant : Prioritaet.Hoch;
            }
        }

        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert die Angaben; mit <paramref name="ziel"/> geht der Vorgang einen Schritt weiter.</summary>
    public async Task<IActionResult> OnPostAsync(BeschaffungStatus? ziel, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungBearbeiten))
        {
            return Forbid();
        }

        Domain.Technik.Beschaffung beschaffung;
        if (IstNeu)
        {
            beschaffung = new Domain.Technik.Beschaffung { Bezeichnung = Formular.Bezeichnung, Status = BeschaffungStatus.Bedarf };
            db.Beschaffungen.Add(beschaffung);
        }
        else
        {
            var vorhanden = await db.Beschaffungen.FirstOrDefaultAsync(b => b.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            beschaffung = vorhanden;
            db.Entry(beschaffung).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        Status = beschaffung.Status;
        if (ziel is not null)
        {
            if (!Domain.Technik.Beschaffung.Folgestatus(beschaffung.Status).Contains(ziel.Value))
            {
                ModelState.AddModelError(string.Empty, $"Von „{Anzeige.Text(beschaffung.Status)}“ geht es nicht nach „{Anzeige.Text(ziel)}“.");
            }
            else if (ziel == BeschaffungStatus.Genehmigt && !await DarfAsync(Berechtigungen.BeschaffungFreigeben))
            {
                return Forbid();
            }
        }

        if (Formular.GeraeteartId is not null && !await db.Geraetearten.AnyAsync(g => g.Id == Formular.GeraeteartId, ct))
        {
            ModelState.AddModelError("Formular.GeraeteartId", "Unbekannte Geräteart.");
        }

        if (Formular.FahrzeugId is not null && !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
        {
            ModelState.AddModelError("Formular.FahrzeugId", "Unbekanntes Fahrzeug.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        beschaffung.Bezeichnung = Formular.Bezeichnung.Trim();
        beschaffung.Menge = Formular.Menge;
        beschaffung.Begruendung = Formular.Begruendung;
        beschaffung.Prioritaet = Formular.Prioritaet;
        beschaffung.GeraeteartId = Formular.GeraeteartId;
        beschaffung.FahrzeugId = Formular.FahrzeugId;
        beschaffung.Haushaltsjahr = Formular.Haushaltsjahr;
        beschaffung.Kostenschaetzung = Formular.Kostenschaetzung;
        beschaffung.Foerderfaehig = Formular.Foerderfaehig;
        beschaffung.Lieferant = Formular.Lieferant;
        beschaffung.Bestellnummer = Formular.Bestellnummer;
        beschaffung.BestelltAm = Formular.BestelltAm;
        beschaffung.GeliefertAm = Formular.GeliefertAm;
        beschaffung.Istkosten = Formular.Istkosten;
        beschaffung.Bemerkung = Formular.Bemerkung;
        if (IstNeu && Formular.MangelId is not null && await db.Maengel.AnyAsync(m => m.Id == Formular.MangelId, ct))
        {
            beschaffung.MangelId = Formular.MangelId;
        }

        if (ziel is not null)
        {
            var heute = Anzeige.Heute(zeit);
            beschaffung.Status = ziel.Value;
            switch (ziel)
            {
                case BeschaffungStatus.Genehmigt:
                    beschaffung.GenehmigtAm = heute;
                    beschaffung.GenehmigtVon = (await benutzer.GetUserAsync(User))?.Id;
                    break;
                case BeschaffungStatus.Bestellt:
                    beschaffung.BestelltAm ??= heute;
                    break;
                case BeschaffungStatus.Geliefert:
                    beschaffung.GeliefertAm ??= heute;
                    break;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Vorgang wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            await LadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = ziel is null ? $"{beschaffung.Bezeichnung} gespeichert." : $"{beschaffung.Bezeichnung}: {Anzeige.Text(beschaffung.Status)}.";
        return RedirectToPage(new { id = beschaffung.Id });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync(Berechtigungen.BeschaffungBearbeiten);
        DarfFreigeben = await DarfAsync(Berechtigungen.BeschaffungFreigeben);
        Folgestatus = IstNeu ? [] : Domain.Technik.Beschaffung.Folgestatus(Status);
        Geraetearten = await db.Geraetearten.Where(g => g.Aktiv || g.Id == Formular.GeraeteartId).OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.Where(f => f.AusserDienstAm == null || f.Id == Formular.FahrzeugId).OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString())).ToListAsync(ct);
        if (Formular.MangelId is not null)
        {
            Mangel = await db.Maengel.Where(m => m.Id == Formular.MangelId).Select(m => m.Beschreibung).FirstOrDefaultAsync(ct);
        }

        if (Formular.Id is not null)
        {
            Geraete = (await db.Geraete.Where(g => g.BeschaffungId == Formular.Id).OrderBy(g => g.Inventarnummer)
                    .Select(g => new { g.Id, g.Inventarnummer, g.Bezeichnung }).ToListAsync(ct))
                .Select(g => (g.Id, g.Inventarnummer, g.Bezeichnung))
                .ToList();
        }
    }
}
