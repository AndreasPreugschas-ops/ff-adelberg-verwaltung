using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Dienstpflicht in der Mitgliederakte (V57): Abwesenheitsanzeigen, Beurlaubungen und Ordnungsmaßnahmen mit Anhörung sowie das Kennzeichen
/// Fachberater (§ 11 Abs. 4 FwG). Nur Kommandant und Stellvertreter.
/// </summary>
[Authorize(Policy = Berechtigungen.Dienstpflicht)]
public class DienstpflichtModel(AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe eines Vorgangs.</summary>
    public sealed class Eingabe
    {
        /// <summary>Art.</summary>
        [Display(Name = "Art")] public DienstpflichtArt Art { get; set; }
        /// <summary>Datum.</summary>
        [Required(ErrorMessage = "Bitte das Datum angeben."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        /// <summary>Beginn.</summary>
        [Display(Name = "von")] public DateOnly? Von { get; set; }
        /// <summary>Ende.</summary>
        [Display(Name = "bis")] public DateOnly? Bis { get; set; }
        /// <summary>Sachstand.</summary>
        [StringLength(2000), Display(Name = "Sachstand")] public string? Sachverhalt { get; set; }
        /// <summary>Dem Bürgermeister mitgeteilt am.</summary>
        [Display(Name = "dem Bürgermeister mitgeteilt am")] public DateOnly? BuergermeisterMitgeteiltAm { get; set; }
    }

    /// <summary>Das Mitglied.</summary>
    public Mitglied Mitglied { get; private set; } = null!;

    /// <summary>Die Vorgänge, neueste zuerst.</summary>
    public IReadOnlyList<Dienstpflichtvorgang> Vorgaenge { get; private set; } = [];

    /// <summary>Heutiger Tag in Ortszeit.</summary>
    public DateOnly Heute => Anzeige.Heute(zeit);

    /// <summary>Formular für einen neuen Vorgang.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Fachberater-Kennzeichen.</summary>
    [BindProperty] public bool Fachberater { get; set; }

    /// <summary>Fachgebiet.</summary>
    [BindProperty, StringLength(100)] public string? Fachgebiet { get; set; }

    /// <summary>Zeigt die Vorgänge.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular.Datum = Heute;
        Fachberater = Mitglied.Fachberater;
        Fachgebiet = Mitglied.Fachgebiet;
        return Page();
    }

    /// <summary>Legt einen Vorgang an.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostVorgangAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        ModelState.Remove(nameof(Fachgebiet));
        if (Formular.Von is { } von && Formular.Bis is { } bis && bis < von)
        {
            ModelState.AddModelError("Formular.Bis", "Das Ende liegt vor dem Beginn.");
        }

        if (Formular.Art is DienstpflichtArt.Abwesenheitsanzeige or DienstpflichtArt.Beurlaubung && Formular.Von is null)
        {
            ModelState.AddModelError("Formular.Von", "Bitte den Beginn angeben.");
        }

        if (!ModelState.IsValid)
        {
            Fachberater = Mitglied.Fachberater;
            Fachgebiet = Mitglied.Fachgebiet;
            return Page();
        }

        db.Dienstpflichtvorgaenge.Add(new Dienstpflichtvorgang
        {
            MitgliedId = id, Art = Formular.Art, Datum = Formular.Datum!.Value, Von = Formular.Von, Bis = Formular.Bis,
            Sachverhalt = string.IsNullOrWhiteSpace(Formular.Sachverhalt) ? null : Formular.Sachverhalt.Trim(),
            BuergermeisterMitgeteiltAm = Formular.BuergermeisterMitgeteiltAm, ErfasstVon = User.Identity?.Name,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Vorgang eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Vermerkt die Mitteilung an den Bürgermeister mit dem heutigen Tag.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="vorgangId">Kennung des Vorgangs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostMitgeteiltAsync(Guid id, Guid vorgangId, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || await db.Dienstpflichtvorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId && v.MitgliedId == id, ct) is not { } v)
        {
            return NotFound();
        }

        v.BuergermeisterMitgeteiltAm ??= Heute;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht einen irrtümlich erfassten Vorgang.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="vorgangId">Kennung des Vorgangs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, Guid vorgangId, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || await db.Dienstpflichtvorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId && v.MitgliedId == id, ct) is not { } v)
        {
            return NotFound();
        }

        db.Dienstpflichtvorgaenge.Remove(v);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Vorgang gelöscht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Setzt das Fachberater-Kennzeichen.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostFachberaterAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        var m = await db.Mitglieder.FirstAsync(x => x.Id == id, ct);
        m.Fachberater = Fachberater;
        m.Fachgebiet = Fachberater && !string.IsNullOrWhiteSpace(Fachgebiet) ? Fachgebiet.Trim() : null;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = Fachberater ? "Als Fachberater gekennzeichnet." : "Kennzeichen Fachberater entfernt.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var sichtbar = await datenbereich.Filtern(db.Mitglieder, User, Heute);
        if (await sichtbar.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct) is not { } mitglied)
        {
            return false;
        }

        Mitglied = mitglied;
        Vorgaenge = await db.Dienstpflichtvorgaenge.AsNoTracking().Where(v => v.MitgliedId == id).OrderByDescending(v => v.Datum).ToListAsync(ct);
        return true;
    }
}
