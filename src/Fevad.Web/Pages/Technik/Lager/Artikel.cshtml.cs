using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Lager;

/// <summary>Ein Lagerartikel (V16): Chargen mit Verfall, Eingang, Entnahme (auch für einen Einsatz), Verfall entsorgen, Bewegungen.</summary>
public class ArtikelModel(AppDbContext db, Lagerverwaltung lager, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Der Artikel mit Chargen.</summary>
    public Lagerartikel Artikel { get; private set; } = null!;

    /// <summary>Letzte Bewegungen, neueste zuerst.</summary>
    public IReadOnlyList<(Lagerbewegung Bewegung, string? Einsatznummer)> Bewegungen { get; private set; } = [];

    /// <summary>Einsätze der letzten 60 Tage für die Entnahme.</summary>
    public IReadOnlyList<SelectListItem> Einsaetze { get; private set; } = [];

    /// <summary>Stichtag.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Darf Eingang, Entnahme und Verfall buchen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Darf Stammdaten ändern.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Stammdaten.</summary>
    [BindProperty]
    public IndexModel.Eingabe Formular { get; set; } = new();

    /// <summary>Eingang oder Entnahme.</summary>
    [BindProperty]
    public BuchungEingabe Buchung { get; set; } = new();

    /// <summary>Formular für Eingang und Entnahme.</summary>
    public sealed class BuchungEingabe
    {
        /// <summary>Menge als Text (Komma erlaubt).</summary>
        [Display(Name = "Menge")] public string? Menge { get; set; }

        /// <summary>Tag; leer heißt heute.</summary>
        [Display(Name = "Datum")] public DateOnly? Datum { get; set; }

        /// <summary>Verfall der gelieferten Charge.</summary>
        [Display(Name = "Verfällt am")] public DateOnly? VerfallAm { get; set; }

        /// <summary>Einsatz der Entnahme.</summary>
        [Display(Name = "Im Einsatz")] public Guid? EinsatzId { get; set; }

        /// <summary>Bemerkung (Sachstand).</summary>
        [StringLength(200), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt den Artikel.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular = new IndexModel.Eingabe
        {
            Bezeichnung = Artikel.Bezeichnung,
            Kategorie = Artikel.Kategorie,
            Einheit = Artikel.Einheit,
            Mindestmenge = Artikel.Mindestmenge.ToString("0.##"),
            Stueckpreis = Artikel.Stueckpreis?.ToString("0.00"),
            Lagerort = Artikel.Lagerort,
        };
        return Page();
    }

    /// <summary>Bucht einen Eingang als neue Charge.</summary>
    public async Task<IActionResult> OnPostEingangAsync(Guid id, CancellationToken ct)
    {
        if (await PruefenAsync(id, Berechtigungen.TechnikPruefen, ct) is { } fehler)
        {
            return fehler;
        }

        var (menge, datum) = MengeUndDatum();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var charge = new Lagercharge { LagerartikelId = Artikel.Id, Menge = menge, VerfallAm = Buchung.VerfallAm, EingangAm = datum };
        db.Lagerchargen.Add(charge);
        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            LagerartikelId = Artikel.Id, LagerchargeId = charge.Id, Art = LagerbewegungArt.Eingang, Datum = datum, Menge = menge,
            Stueckpreis = Artikel.Stueckpreis, Bemerkung = Kurz(Buchung.Bemerkung),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Eingang {menge:0.##} {Artikel.Einheit} gebucht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Bucht eine Entnahme nach FEFO; mit Einsatz wird sie Kostenersatz-Posten.</summary>
    public async Task<IActionResult> OnPostEntnahmeAsync(Guid id, CancellationToken ct)
    {
        if (await PruefenAsync(id, Berechtigungen.TechnikPruefen, ct) is { } fehler)
        {
            return fehler;
        }

        var (menge, datum) = MengeUndDatum();
        if (Buchung.EinsatzId is { } einsatzId && !await db.Einsaetze.AnyAsync(e => e.Id == einsatzId, ct))
        {
            ModelState.AddModelError("Buchung.EinsatzId", "Einsatz nicht gefunden.");
        }

        if (ModelState.IsValid && lager.Entnehmen(Artikel, menge, datum, Buchung.EinsatzId, Kurz(Buchung.Bemerkung)) is { } meldung)
        {
            ModelState.AddModelError("Buchung.Menge", meldung);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Entnahme {menge:0.##} {Artikel.Einheit} gebucht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Entsorgt den Rest einer verfallenen Charge.</summary>
    public async Task<IActionResult> OnPostVerfallAsync(Guid id, Guid chargeId, CancellationToken ct)
    {
        if (await PruefenAsync(id, Berechtigungen.TechnikPruefen, ct) is { } fehler)
        {
            return fehler;
        }

        var charge = Artikel.Chargen.FirstOrDefault(c => c.Id == chargeId && c.Menge > 0);
        if (charge is null)
        {
            return NotFound();
        }

        db.Lagerbewegungen.Add(new Lagerbewegung
        {
            LagerartikelId = Artikel.Id, LagerchargeId = charge.Id, Art = LagerbewegungArt.Verfall, Datum = Heute, Menge = charge.Menge,
            Stueckpreis = Artikel.Stueckpreis, Bemerkung = charge.VerfallAm is { } v ? $"verfallen am {Anzeige.Datum(v)}" : "entsorgt",
        });
        charge.Menge = 0;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Charge entsorgt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Ändert die Stammdaten des Artikels oder schaltet ihn inaktiv.</summary>
    public async Task<IActionResult> OnPostStammdatenAsync(Guid id, bool aktiv, CancellationToken ct)
    {
        if (await PruefenAsync(id, Berechtigungen.TechnikBearbeiten, ct) is { } fehler)
        {
            return fehler;
        }

        foreach (var schluessel in ModelState.Keys.Where(k => k.StartsWith("Buchung.", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(schluessel);
        }

        var (mindestmenge, preis) = IndexModel.Zahlen(Formular, ModelState);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Artikel.Bezeichnung = Formular.Bezeichnung!.Trim();
        Artikel.Kategorie = Formular.Kategorie;
        Artikel.Einheit = Formular.Einheit!.Trim();
        Artikel.Mindestmenge = mindestmenge ?? 0;
        Artikel.Stueckpreis = preis;
        Artikel.Lagerort = string.IsNullOrWhiteSpace(Formular.Lagerort) ? null : Formular.Lagerort.Trim();
        Artikel.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Gespeichert.";
        return RedirectToPage(new { id });
    }

    private (decimal Menge, DateOnly Datum) MengeUndDatum()
    {
        // Ohne Stammdatenfelder bindet MVC „Formular“ ohne Präfix; dessen Fehler gehören nicht zur Buchung.
        foreach (var schluessel in ModelState.Keys.Where(k => !k.StartsWith("Buchung.", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(schluessel);
        }

        var menge = Entschaedigungssaetze.Lesen(Buchung.Menge);
        if (menge is null or <= 0 or > 100_000)
        {
            ModelState.AddModelError("Buchung.Menge", "Bitte eine Menge über 0 angeben, z. B. 2 oder 0,5.");
        }

        var datum = Buchung.Datum ?? Heute;
        if (datum > Heute)
        {
            ModelState.AddModelError("Buchung.Datum", "Das Datum liegt in der Zukunft.");
        }

        return (menge ?? 0, datum);
    }

    private async Task<IActionResult?> PruefenAsync(Guid id, string policy, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, policy)).Succeeded)
        {
            return Forbid();
        }

        return await LadenAsync(id, ct, verfolgen: true) ? null : NotFound();
    }

    private static string? Kurz(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 200)];

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct, bool verfolgen = false)
    {
        var abfrage = db.Lagerartikel.Include(a => a.Chargen).AsQueryable();
        var artikel = await (verfolgen ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (artikel is null)
        {
            return false;
        }

        Artikel = artikel;
        Heute = Anzeige.Heute(zeit);
        DarfBuchen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
        Bewegungen = (await db.Lagerbewegungen.AsNoTracking().Where(b => b.LagerartikelId == id)
                .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ErstelltAm).Take(100)
                .Select(b => new { Bewegung = b, Einsatznummer = b.Einsatz != null ? b.Einsatz.Einsatznummer : null })
                .ToListAsync(ct))
            .Select(b => (b.Bewegung, b.Einsatznummer)).ToList();
        var seit = zeit.GetUtcNow().AddDays(-60);
        Einsaetze = (await db.Einsaetze.AsNoTracking().Where(e => e.Alarmiert >= seit).OrderByDescending(e => e.Alarmiert)
                .Select(e => new { e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort }).ToListAsync(ct))
            .Select(e => new SelectListItem($"{e.Einsatznummer} vom {Anzeige.Datum(Anzeige.LokalesDatum(e.Alarmiert))} {e.Stichwort}", e.Id.ToString()))
            .ToList();
        return true;
    }
}
