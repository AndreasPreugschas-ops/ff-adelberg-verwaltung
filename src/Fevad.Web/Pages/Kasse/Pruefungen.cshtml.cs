using System.ComponentModel.DataAnnotations;
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
/// Kassenprüfungen einer Kasse (V05): Liste mit Stand und, für die amtierenden Rechnungsprüfer, eine neue Prüfung mit Zeitraum und zweitem
/// Prüfer anlegen. Lesen mit Kassen-Leserecht.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class PruefungenModel(AppDbContext db, Kassenzugriff zugriff, Kassenabschluss abschluss, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Prüfungen, neueste zuerst.</summary>
    public IReadOnlyList<Kassenpruefung> Pruefungen { get; private set; } = [];

    /// <summary>Der Benutzer ist amtierender Prüfer und darf eine Prüfung anlegen.</summary>
    public bool DarfAnlegen { get; private set; }

    /// <summary>Die anderen amtierenden Prüfer zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Mitpruefer { get; private set; } = [];

    /// <summary>Eingaben für eine neue Prüfung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Felder einer neuen Prüfung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Erster Tag des Zeitraums.</summary>
        [Required(ErrorMessage = "Bitte den Beginn angeben."), Display(Name = "Zeitraum von")] public DateOnly? Von { get; set; }

        /// <summary>Letzter Tag des Zeitraums.</summary>
        [Required(ErrorMessage = "Bitte das Ende angeben."), Display(Name = "bis")] public DateOnly? Bis { get; set; }

        /// <summary>Zweiter Prüfer.</summary>
        [Required(ErrorMessage = "Bitte den zweiten Prüfer wählen."), Display(Name = "Zweiter Prüfer")] public Guid? Pruefer2Id { get; set; }
    }

    /// <summary>Zeigt die Prüfungen.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        var vorjahr = Anzeige.Heute(zeit).Year - 1;
        Formular = new Eingabe { Von = new DateOnly(vorjahr, 1, 1), Bis = new DateOnly(vorjahr, 12, 31) };
        return Page();
    }

    /// <summary>Legt eine Prüfung an; der angemeldete Prüfer ist der erste.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Prüfung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid kasse, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfAnlegen)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var heute = Anzeige.Heute(zeit);
        var ich = (await MitgliedIdAsync(ct))!.Value;
        var amtierend = (await abschluss.AmtierendePrueferAsync(heute, ct)).Select(p => p.Id).ToHashSet();
        foreach (var meldung in Pruefregeln.Fehler(Formular.Von!.Value, Formular.Bis!.Value, ich, Formular.Pruefer2Id!.Value, amtierend))
        {
            ModelState.AddModelError(string.Empty, meldung);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var pruefung = new Kassenpruefung
        {
            KasseId = Kasse.Id, Von = Formular.Von!.Value, Bis = Formular.Bis!.Value, Pruefer1Id = ich, Pruefer2Id = Formular.Pruefer2Id!.Value,
        };
        db.Kassenpruefungen.Add(pruefung);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Prüfung angelegt.";
        return RedirectToPage("Pruefung", new { id = pruefung.Id });
    }

    private async Task<Guid?> MitgliedIdAsync(CancellationToken ct) =>
        benutzer.Id is { } id ? await db.Users.Where(u => u.Id == id).Select(u => u.MitgliedId).FirstOrDefaultAsync(ct) : null;

    private async Task<IActionResult?> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return NotFound();
        }

        Kasse = kasse;
        Pruefungen = await db.Kassenpruefungen.AsNoTracking().Include(p => p.Pruefer1).Include(p => p.Pruefer2).Include(p => p.Beanstandungen)
            .Where(p => p.KasseId == kasseId).OrderByDescending(p => p.Bis).ThenByDescending(p => p.ErstelltAm).ToListAsync(ct);
        if (await zugriff.DarfAsync(User, Berechtigungen.KassePruefen, kasse) && await MitgliedIdAsync(ct) is { } ich)
        {
            var amtierend = await abschluss.AmtierendePrueferAsync(Anzeige.Heute(zeit), ct);
            DarfAnlegen = amtierend.Any(p => p.Id == ich);
            Mitpruefer = amtierend.Where(p => p.Id != ich).Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();
        }

        return null;
    }
}
