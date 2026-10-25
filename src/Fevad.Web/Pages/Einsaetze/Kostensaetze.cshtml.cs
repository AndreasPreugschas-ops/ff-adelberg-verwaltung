using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Sätze für den Kostenersatz nach § 34 FwG und der Feuerwehrkostenersatzsatzung der Gemeinde:
/// je Fahrzeug, für Einsatzkräfte sowie für Geräte und Verbrauchsmaterial. Eine neue Fassung ist eine neue
/// Version ab ihrem Stichtag; die Vorgängerversion endet am Vortag.
/// </summary>
public class KostensaetzeModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Alle Sätze mit allen Versionen.</summary>
    public IReadOnlyList<Kostensatz> Saetze { get; private set; } = [];
    /// <summary>Auswahl der Fahrzeuge für einen Fahrzeugsatz.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeugauswahl { get; private set; } = [];
    /// <summary>Anzeigenamen der Fahrzeuge nach Kennung.</summary>
    public IReadOnlyDictionary<Guid, string> Fahrzeugnamen { get; private set; } = new Dictionary<Guid, string>();
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Darf Kostensätze pflegen.</summary>
    public bool DarfPflegen { get; private set; }

    /// <summary>Auswahl der Satzarten.</summary>
    public static IReadOnlyList<SelectListItem> Arten { get; } =
        Enum.GetValues<KostensatzArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Auswahl der Einheiten.</summary>
    public static IReadOnlyList<SelectListItem> Einheiten { get; } =
        Enum.GetValues<Kosteneinheit>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Eingabe für einen neuen Satz oder eine neue Fassung.</summary>
    [BindProperty]
    public Formular Neu { get; set; } = new();

    /// <summary>Formular für einen Kostensatz.</summary>
    public sealed class Formular
    {
        /// <summary>Art des Satzes.</summary>
        [Display(Name = "Art")] public KostensatzArt Art { get; set; }
        /// <summary>Fahrzeug bei einem Fahrzeugsatz.</summary>
        [Display(Name = "Fahrzeug")] public Guid? FahrzeugId { get; set; }
        /// <summary>Bezeichnung; beim Fahrzeug der Fahrzeugname, beim Personal ohne Angabe „Einsatzkräfte“.</summary>
        [StringLength(200), Display(Name = "Bezeichnung")] public string? Bezeichnung { get; set; }
        /// <summary>Einheit des Satzes.</summary>
        [Display(Name = "Einheit")] public Kosteneinheit Einheit { get; set; }
        /// <summary>Betrag in Euro als Text, z. B. „12,50“.</summary>
        [Required(ErrorMessage = "Betrag fehlt."), Display(Name = "Betrag (€)")] public string Betrag { get; set; } = "";
        /// <summary>Ab diesem Tag gilt die Fassung.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Gültig ab")] public DateOnly? GueltigAb { get; set; }
        /// <summary>Quelle des Satzes, z. B. die Satzung.</summary>
        [StringLength(200), Display(Name = "Quelle")] public string? Quelle { get; set; }
    }

    /// <summary>Heute gültige Sätze.</summary>
    public IEnumerable<Kostensatz> Aktuelle => Saetze.Where(s => s.IstGueltigAm(Heute));

    /// <summary>Anzeigename eines Satzes; beim Fahrzeug der aktuelle Fahrzeugname.</summary>
    /// <param name="s">Der Satz.</param>
    /// <returns>Der Name.</returns>
    public string Name(Kostensatz s) => s.Art == KostensatzArt.Fahrzeug && s.FahrzeugId is { } id
        ? Fahrzeugnamen.GetValueOrDefault(id, s.Bezeichnung)
        : s.Bezeichnung;

    /// <summary>Lädt Sätze und Fahrzeuge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        Neu = new Formular { GueltigAb = new DateOnly(Heute.Year, 1, 1), Quelle = "Feuerwehrkostenersatzsatzung Adelberg" };
    }

    /// <summary>Speichert einen Satz; eine Fassung mit neuem Stichtag beendet die vorige am Vortag.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, bei Fehlern die Seite oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeKostensaetze)).Succeeded)
        {
            return Forbid();
        }

        await LadenAsync(ct);
        var betrag = Entschaedigungssaetze.Lesen(Neu.Betrag);
        if (!string.IsNullOrWhiteSpace(Neu.Betrag) && (betrag is null || betrag < 0))
        {
            ModelState.AddModelError("Neu.Betrag", "Bitte eine Zahl ab 0 eingeben, z. B. 120 oder 12,50.");
        }

        string bezeichnung;
        if (Neu.Art == KostensatzArt.Fahrzeug)
        {
            if (Neu.FahrzeugId is null || !Fahrzeugnamen.TryGetValue(Neu.FahrzeugId.Value, out var name))
            {
                ModelState.AddModelError("Neu.FahrzeugId", "Bitte ein Fahrzeug wählen.");
                name = "";
            }

            bezeichnung = name;
        }
        else
        {
            Neu.FahrzeugId = null;
            bezeichnung = Neu.Art == KostensatzArt.Personal && string.IsNullOrWhiteSpace(Neu.Bezeichnung)
                ? Kostenersatz.PersonalBezeichnung
                : (Neu.Bezeichnung ?? "").Trim();
            if (bezeichnung.Length == 0)
            {
                ModelState.AddModelError("Neu.Bezeichnung", "Bezeichnung fehlt.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ab = Neu.GueltigAb!.Value;
        var quelle = string.IsNullOrWhiteSpace(Neu.Quelle) ? null : Neu.Quelle.Trim();
        // Gleicher Satz: gleiche Art, gleiches Fahrzeug bzw. gleiche Bezeichnung; beim Personal gibt es nur einen.
        var vorhanden = await db.Kostensaetze
            .Where(k => k.Art == Neu.Art && (Neu.Art == KostensatzArt.Personal
                || (Neu.Art == KostensatzArt.Fahrzeug ? k.FahrzeugId == Neu.FahrzeugId : k.Bezeichnung == bezeichnung)))
            .ToListAsync(ct);

        if (vorhanden.FirstOrDefault(k => k.GueltigAb == ab) is { } gleich)
        {
            gleich.Bezeichnung = bezeichnung;
            gleich.Einheit = Neu.Einheit;
            gleich.Betrag = betrag!.Value;
            gleich.Quelle = quelle;
        }
        else
        {
            foreach (var vorher in vorhanden.Where(k => k.GueltigAb < ab && (k.GueltigBis is null || k.GueltigBis >= ab)))
            {
                vorher.GueltigBis = ab.AddDays(-1);
            }

            var nachfolger = vorhanden.Where(k => k.GueltigAb > ab).MinBy(k => k.GueltigAb);
            db.Kostensaetze.Add(new Kostensatz
            {
                Art = Neu.Art,
                FahrzeugId = Neu.FahrzeugId,
                Bezeichnung = bezeichnung,
                Einheit = Neu.Einheit,
                Betrag = betrag!.Value,
                GueltigAb = ab,
                GueltigBis = nachfolger?.GueltigAb.AddDays(-1),
                Quelle = quelle,
            });
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{bezeichnung}: {Anzeige.Euro(betrag!.Value)} je {Anzeige.Text(Neu.Einheit)} ab {Anzeige.Datum(ab)} gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Ein Satz, den es nicht mehr gibt (z. B. ausgemustertes Gerät), gilt ab heute nicht mehr.</summary>
    public async Task<IActionResult> OnPostBeendenAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeKostensaetze)).Succeeded)
        {
            return Forbid();
        }

        var satz = await db.Kostensaetze.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (satz is null)
        {
            return NotFound();
        }

        var gestern = Anzeige.Heute(zeit).AddDays(-1);
        if (satz.GueltigAb > gestern)
        {
            // Noch nie gültig gewesen: ganz entfernen, sofern kein Bericht ihn verwendet.
            if (await db.EinsatzAusruestungen.AnyAsync(a => a.KostensatzId == id, ct))
            {
                TempData["Meldung"] = "Der Satz wird schon in einem Einsatzbericht verwendet.";
                return RedirectToPage();
            }

            db.Kostensaetze.Remove(satz);
        }
        else
        {
            satz.GueltigBis = gestern;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{satz.Bezeichnung} gilt nicht mehr.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfPflegen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeKostensaetze)).Succeeded;
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().OrderBy(f => f.Funkrufname)
            .Select(f => new { f.Id, Name = f.Funkrufname + " (" + f.Typ + ")" }).ToListAsync(ct);
        Fahrzeugnamen = fahrzeuge.ToDictionary(f => f.Id, f => f.Name);
        Fahrzeugauswahl = fahrzeuge.Select(f => new SelectListItem(f.Name, f.Id.ToString())).ToList();
        Saetze = await db.Kostensaetze.AsNoTracking()
            .OrderBy(k => k.Art).ThenBy(k => k.Bezeichnung).ThenByDescending(k => k.GueltigAb)
            .ToListAsync(ct);
    }
}
