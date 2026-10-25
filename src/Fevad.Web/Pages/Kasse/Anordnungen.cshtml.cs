using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Vorbereitete Ausgaben einer Kasse (V02, V06): Jugendwart, Kommandant oder Kassenwart halten Betrag, Empfänger und Beleg fest; der
/// Kassenwart zahlt und bucht. Kommandant oder Stellvertreter können eine Anordnung vermerken, nötig ist sie nicht.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class AnordnungenModel(AppDbContext db, Kassenzugriff zugriff, ParameterLeser parameter, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Offene vorbereitete Ausgaben, älteste zuerst.</summary>
    public IReadOnlyList<Zahlungsanordnung> Offen { get; private set; } = [];

    /// <summary>Erledigte (gezahlt oder abgelehnt), die letzten 50.</summary>
    public IReadOnlyList<Zahlungsanordnung> Erledigt { get; private set; } = [];

    /// <summary>Der Benutzer darf vorbereiten.</summary>
    public bool DarfVorbereiten { get; private set; }

    /// <summary>Der Benutzer darf zahlen und ablehnen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Der Benutzer darf eine Anordnung vermerken.</summary>
    public bool DarfAnordnen { get; private set; }

    /// <summary>Empfänger sichtbar (Jugendkasse nur mit Zugang).</summary>
    public bool PartnerSichtbar { get; private set; }

    /// <summary>Beschlussgrenze in Euro.</summary>
    public int Beschlussgrenze { get; private set; }

    /// <summary>Ausgabepositionen der Kasse.</summary>
    public IReadOnlyList<SelectListItem> Positionen { get; private set; } = [];

    /// <summary>Beschlüsse für den Verweis.</summary>
    public IReadOnlyList<SelectListItem> Beschluesse { get; private set; } = [];

    /// <summary>Eingabe einer neuen Ausgabe.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Belege zur neuen Ausgabe.</summary>
    [BindProperty] public List<IFormFile> Dateien { get; set; } = [];

    /// <summary>Grund einer Ablehnung.</summary>
    [BindProperty, StringLength(500)] public string? Grund { get; set; }

    /// <summary>Felder einer vorbereiteten Ausgabe.</summary>
    public sealed class Eingabe
    {
        /// <summary>Betrag in Euro.</summary>
        [Required(ErrorMessage = "Bitte den Betrag angeben."), Range(0.01, 1_000_000, ErrorMessage = "Der Betrag muss größer als 0 sein."), Display(Name = "Betrag (Euro)")]
        public decimal? Betrag { get; set; }

        /// <summary>Empfänger.</summary>
        [Required(ErrorMessage = "Bitte den Empfänger angeben."), StringLength(200), Display(Name = "Empfänger")] public string Empfaenger { get; set; } = "";

        /// <summary>Zweck.</summary>
        [Required(ErrorMessage = "Bitte den Zweck angeben."), StringLength(500), Display(Name = "Zweck")] public string Zweck { get; set; } = "";

        /// <summary>Vorgeschlagene Position.</summary>
        [Display(Name = "Position")] public Guid? PlanpositionId { get; set; }

        /// <summary>Zahlen bis.</summary>
        [Display(Name = "Zahlen bis")] public DateOnly? FaelligAm { get; set; }

        /// <summary>Ausschussbeschluss.</summary>
        [Display(Name = "Ausschussbeschluss")] public Guid? AusschussbeschlussId { get; set; }
    }

    /// <summary>Zeigt die Liste.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) => await LadenAsync(kasse, ct) ? Page() : NotFound();

    /// <summary>Legt eine vorbereitete Ausgabe mit Belegen an.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        if (!DarfVorbereiten)
        {
            return Forbid();
        }

        if (Formular.PlanpositionId is { } p && !Positionen.Any(x => x.Value == p.ToString()))
        {
            ModelState.AddModelError("Formular.PlanpositionId", "Diese Position steht nicht zur Wahl.");
        }

        var belege = await Kassenauswahl.BelegeLesenAsync(Dateien, parameter, Anzeige.Heute(zeit), ModelState, nameof(Dateien), ct);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.Zahlungsanordnungen.Add(new Zahlungsanordnung
        {
            KasseId = kasse, Betrag = Formular.Betrag!.Value, Empfaenger = Formular.Empfaenger.Trim(), Zweck = Formular.Zweck.Trim(),
            PlanpositionId = Formular.PlanpositionId, FaelligAm = Formular.FaelligAm, Status = AnordnungStatus.Vorbereitet,
            AusschussbeschlussId = Beschluesse.Any(b => b.Value == Formular.AusschussbeschlussId?.ToString()) ? Formular.AusschussbeschlussId : null,
            Belege = belege,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Ausgabe vorbereitet; der Kassenwart sieht sie in seiner Liste.";
        return RedirectToPage(new { kasse });
    }

    /// <summary>Vermerkt die Anordnung durch Kommandant oder Stellvertreter (nur Dokumentation).</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Kennung der vorbereiteten Ausgabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAnordnenAsync(Guid kasse, Guid id, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseAnordnen, kasse, ct) is null)
        {
            return Forbid();
        }

        var anordnung = await db.Zahlungsanordnungen.FirstOrDefaultAsync(a => a.Id == id && a.KasseId == kasse && a.Status == AnordnungStatus.Vorbereitet, ct);
        if (anordnung is null)
        {
            return NotFound();
        }

        anordnung.AngeordnetAm = zeit.GetUtcNow();
        anordnung.AngeordnetVon = benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Anordnung vermerkt.";
        return RedirectToPage(new { kasse });
    }

    /// <summary>Lehnt eine vorbereitete Ausgabe ab.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Kennung der vorbereiteten Ausgabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAblehnenAsync(Guid kasse, Guid id, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseBuchen, kasse, ct) is null)
        {
            return Forbid();
        }

        var anordnung = await db.Zahlungsanordnungen.FirstOrDefaultAsync(a => a.Id == id && a.KasseId == kasse && a.Status == AnordnungStatus.Vorbereitet, ct);
        if (anordnung is null)
        {
            return NotFound();
        }

        anordnung.Status = AnordnungStatus.Abgelehnt;
        anordnung.Bemerkung = string.IsNullOrWhiteSpace(Grund) ? null : Grund.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Ausgabe abgelehnt.";
        return RedirectToPage(new { kasse });
    }

    /// <summary>Liefert einen Beleg einer vorbereiteten Ausgabe aus.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetBelegAsync(Guid kasse, Guid belegId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasse, ct) is null)
        {
            return NotFound();
        }

        var beleg = await db.Kassenbelege.AsNoTracking()
            .Where(b => b.Id == belegId && db.Zahlungsanordnungen.Any(a => a.Id == b.ZahlungsanordnungId && a.KasseId == kasse))
            .FirstOrDefaultAsync(ct);
        return beleg is null ? NotFound() : File(beleg.Inhalt, beleg.Inhaltstyp, beleg.Dateiname);
    }

    private async Task<bool> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return false;
        }

        Kasse = kasse;
        DarfVorbereiten = await zugriff.DarfAsync(User, Berechtigungen.KasseVorbereiten, kasse) || await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        DarfAnordnen = await zugriff.DarfAsync(User, Berechtigungen.KasseAnordnen, kasse);
        PartnerSichtbar = await zugriff.PartnerSichtbarAsync(User, kasse);
        var heute = Anzeige.Heute(zeit);
        Beschlussgrenze = await parameter.ZahlAsync(Domain.Gemeinsam.ParameterSchluessel.KasseAusschussbeschlussUeberEuro, heute, 100, ct);
        var alle = db.Zahlungsanordnungen.AsNoTracking().Include(a => a.Planposition).Include(a => a.Ausschussbeschluss).Include(a => a.Belege)
            .Where(a => a.KasseId == kasseId);
        Offen = await alle.Where(a => a.Status == AnordnungStatus.Vorbereitet).OrderBy(a => a.ErstelltAm).ToListAsync(ct);
        Erledigt = await alle.Where(a => a.Status != AnordnungStatus.Vorbereitet).OrderByDescending(a => a.GeaendertAm ?? a.ErstelltAm).Take(50).ToListAsync(ct);
        Positionen = (await Kassenauswahl.PositionenAsync(db, kasseId, null, ct)).Where(p => p.Group?.Name == "Ausgaben").ToList();
        Beschluesse = await Kassenauswahl.BeschluesseAsync(db, heute, null, ct);
        return true;
    }
}
