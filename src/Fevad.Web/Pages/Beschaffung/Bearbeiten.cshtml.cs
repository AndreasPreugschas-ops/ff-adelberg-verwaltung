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

/// <summary>Beschaffung anlegen, bearbeiten und im Status weiterschalten.</summary>
public class BearbeitenModel(AppDbContext db, IAuthorizationService autorisierung, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird eine neue Beschaffung angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Aktueller Stand der Beschaffung.</summary>
    public BeschaffungStatus Status { get; private set; } = BeschaffungStatus.Bedarf;
    /// <summary>Zulässige nächste Schritte.</summary>
    public IReadOnlyList<BeschaffungStatus> Folgestatus { get; private set; } = [];
    /// <summary>Darf Beschaffungen bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Darf Beschaffungen genehmigen.</summary>
    public bool DarfFreigeben { get; private set; }
    /// <summary>Beschreibung des Mangels, der den Bedarf ausgelöst hat.</summary>
    public string? Mangel { get; private set; }
    /// <summary>Geräte, die aus dieser Beschaffung angelegt wurden.</summary>
    public IReadOnlyList<(Guid Id, string Nummer, string Bezeichnung)> Geraete { get; private set; } = [];
    /// <summary>Auswahl der Gerätearten.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    /// <summary>Auswahl der Fahrzeuge.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Stand des Z-Feu-Antrags, falls einer angelegt ist.</summary>
    public Foerderstatus? Foerderstatus { get; private set; }

    /// <summary>Formular einer Beschaffung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuer Beschaffung.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Auslösender Mangel.</summary>
        public Guid? MangelId { get; set; }

        /// <summary>Was gebraucht wird.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Was wird gebraucht?")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Stückzahl.</summary>
        [Range(1, 10000, ErrorMessage = "Menge ungültig."), Display(Name = "Menge")] public int Menge { get; set; } = 1;
        /// <summary>Begründung.</summary>
        [StringLength(2000), Display(Name = "Begründung")] public string? Begruendung { get; set; }
        /// <summary>Dringlichkeit.</summary>
        [Display(Name = "Priorität")] public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;
        /// <summary>Geräteart.</summary>
        [Display(Name = "Geräteart")] public Guid? GeraeteartId { get; set; }
        /// <summary>Fahrzeug, für das beschafft wird.</summary>
        [Display(Name = "Für Fahrzeug")] public Guid? FahrzeugId { get; set; }
        /// <summary>Haushaltsjahr.</summary>
        [Range(2000, 2100, ErrorMessage = "Haushaltsjahr ungültig."), Display(Name = "Haushaltsjahr")] public int? Haushaltsjahr { get; set; }
        /// <summary>Geschätzte Gesamtkosten in Euro.</summary>
        [Range(0, 9999999), Display(Name = "Kostenschätzung gesamt (€)")] public decimal? Kostenschaetzung { get; set; }
        /// <summary>Förderfähig nach Z-Feu.</summary>
        [Display(Name = "förderfähig (Z-Feu)")] public bool Foerderfaehig { get; set; }
        /// <summary>Lieferant.</summary>
        [StringLength(200), Display(Name = "Lieferant")] public string? Lieferant { get; set; }
        /// <summary>Bestellnummer.</summary>
        [StringLength(50), Display(Name = "Bestellnummer")] public string? Bestellnummer { get; set; }
        /// <summary>Tag der Bestellung.</summary>
        [Display(Name = "Bestellt am")] public DateOnly? BestelltAm { get; set; }
        /// <summary>Tag der Lieferung.</summary>
        [Display(Name = "Geliefert am")] public DateOnly? GeliefertAm { get; set; }
        /// <summary>Tatsächliche Gesamtkosten in Euro.</summary>
        [Range(0, 9999999), Display(Name = "Istkosten gesamt (€)")] public decimal? Istkosten { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(2000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt eine Beschaffung oder ein leeres Formular, auf Wunsch zu einem Mangel.</summary>
    /// <param name="id">Kennung der Beschaffung; leer für eine neue.</param>
    /// <param name="mangelId">Mangel, aus dem der Bedarf entsteht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
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
            else if (ziel == BeschaffungStatus.Bestellt && await db.Foerderantraege.FirstOrDefaultAsync(a => a.BeschaffungId == beschaffung.Id, ct) is { BeginnGesperrt: true } antrag)
            {
                // VwV Z-Feu: Mit dem Vorhaben darf erst nach dem Zuwendungsbescheid begonnen werden, sonst entfällt die Förderung.
                ModelState.AddModelError(string.Empty, $"Z-Feu-Antrag ist „{Anzeige.Text(antrag.Status)}“: Bestellen erst nach dem Zuwendungsbescheid, sonst entfällt die Förderung.");
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
            Foerderstatus = await db.Foerderantraege.Where(a => a.BeschaffungId == Formular.Id).Select(a => (Foerderstatus?)a.Status).FirstOrDefaultAsync(ct);
            Geraete = (await db.Geraete.Where(g => g.BeschaffungId == Formular.Id).OrderBy(g => g.Inventarnummer)
                    .Select(g => new { g.Id, g.Inventarnummer, g.Bezeichnung }).ToListAsync(ct))
                .Select(g => (g.Id, g.Inventarnummer, g.Bezeichnung))
                .ToList();
        }
    }
}
