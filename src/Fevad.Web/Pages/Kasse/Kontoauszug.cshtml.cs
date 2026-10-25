using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Kontoauszug einlesen und abgleichen (V07): camt.053 oder CSV hochladen, offene Umsätze mit Vorschlag zuordnen, neu buchen oder
/// ignorieren, Saldo der Bank mit dem Kassenbuch vergleichen. Nur für den Kassenwart der Kasse.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class KontoauszugModel(AppDbContext db, Kassenzugriff zugriff, Bankimport bankimport) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Offene Umsätze, älteste zuerst.</summary>
    public IReadOnlyList<Kontoumsatz> Offen { get; private set; } = [];

    /// <summary>Vorschlag je offenem Umsatz.</summary>
    public IReadOnlyDictionary<Guid, Abgleichvorschlag> Vorschlaege { get; private set; } = new Dictionary<Guid, Abgleichvorschlag>();

    /// <summary>Zuletzt zugeordnete oder ignorierte Umsätze.</summary>
    public IReadOnlyList<Kontoumsatz> Erledigt { get; private set; } = [];

    /// <summary>Saldenabgleich je Konto.</summary>
    public IReadOnlyList<Bankimport.Saldenabgleich> Salden { get; private set; } = [];

    /// <summary>Letzte Importe.</summary>
    public IReadOnlyList<Kontoauszugimport> Importe { get; private set; } = [];

    /// <summary>Konten der Kasse ohne Barkasse.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>CSV-Formate.</summary>
    public IReadOnlyList<SelectListItem> Formate { get; private set; } = [];

    /// <summary>Hochgeladene Datei.</summary>
    [BindProperty] public IFormFile? Datei { get; set; }

    /// <summary>Konto der Datei.</summary>
    [BindProperty] public Guid? KontoId { get; set; }

    /// <summary>Bankformat für CSV.</summary>
    [BindProperty] public Guid? BankformatId { get; set; }

    /// <summary>Zeigt den Abgleich.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) => await LadenAsync(kasse, ct) ?? Page();

    /// <summary>Liest eine Datei ein.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid kasse, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (KontoId is not { } kontoId || !Konten.Any(k => k.Value == kontoId.ToString()))
        {
            ModelState.AddModelError(nameof(KontoId), "Bitte das Konto wählen.");
        }

        if (Datei is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(Datei), "Bitte die Datei wählen.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var speicher = new MemoryStream();
        await Datei!.CopyToAsync(speicher, ct);
        try
        {
            var import = await bankimport.EinlesenAsync(KontoId!.Value, speicher.ToArray(), Path.GetFileName(Datei.FileName), BankformatId, ct);
            TempData["Meldung"] = $"{import.Anzahl} Umsätze gelesen, {import.Neu} neu"
                + (import.Anzahl > import.Neu ? $", {import.Anzahl - import.Neu} waren schon eingelesen." : ".");
            return RedirectToPage(new { kasse });
        }
        catch (FormatException ex)
        {
            ModelState.AddModelError(nameof(Datei), ex.Message);
            return Page();
        }
    }

    /// <summary>Ordnet einen Umsatz der vorgeschlagenen Buchung zu.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Der Umsatz.</param>
    /// <param name="buchungId">Die Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZuordnenAsync(Guid kasse, Guid id, Guid buchungId, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Offen.Any(u => u.Id == id) || !await db.Buchungen.AnyAsync(b => b.Id == buchungId && b.KasseId == kasse, ct))
        {
            return NotFound();
        }

        try
        {
            await bankimport.ZuordnenAsync(id, buchungId, ct);
            TempData["Meldung"] = "Umsatz zugeordnet.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { kasse });
    }

    /// <summary>Ignoriert einen offenen Umsatz oder öffnet einen erledigten wieder.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Der Umsatz.</param>
    /// <param name="ignorieren">true ignoriert, false öffnet wieder.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostStatusAsync(Guid kasse, Guid id, bool ignorieren, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (!await db.Kontoumsaetze.AnyAsync(u => u.Id == id && u.Konto!.KasseId == kasse, ct))
        {
            return NotFound();
        }

        await bankimport.StatusSetzenAsync(id, ignorieren, ct);
        TempData["Meldung"] = ignorieren ? "Umsatz ignoriert." : "Umsatz wieder offen.";
        return RedirectToPage(new { kasse });
    }

    private async Task<IActionResult?> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return NotFound();
        }

        if (!await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse))
        {
            return Forbid();
        }

        Kasse = kasse;
        var umsaetze = db.Kontoumsaetze.AsNoTracking().Include(u => u.Konto).Where(u => u.Konto!.KasseId == kasseId);
        Offen = await umsaetze.Where(u => u.Status == UmsatzStatus.Offen).OrderBy(u => u.Buchungstag).ThenBy(u => u.ErstelltAm).Take(500).ToListAsync(ct);
        Vorschlaege = await bankimport.VorschlaegeAsync(kasseId, Offen, ct);
        Erledigt = await umsaetze.Include(u => u.Buchung).Where(u => u.Status != UmsatzStatus.Offen)
            .OrderByDescending(u => u.Buchungstag).Take(30).ToListAsync(ct);
        Salden = await bankimport.SaldenAsync(kasseId, ct);
        Importe = await db.Kontoauszugimporte.AsNoTracking().Include(i => i.Konto).Where(i => i.Konto!.KasseId == kasseId)
            .OrderByDescending(i => i.ErstelltAm).Take(10).ToListAsync(ct);
        Konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == kasseId && k.Aktiv && k.Art != KontoArt.Barkasse)
            .OrderBy(k => k.Sortierung).Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString())).ToListAsync(ct);
        Formate = await db.Bankformate.AsNoTracking().OrderBy(f => f.Name).Select(f => new SelectListItem(f.Name, f.Id.ToString())).ToListAsync(ct);
        return null;
    }
}
