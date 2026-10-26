using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Chronik;

/// <summary>Chronik-Eintrag anlegen, ändern, freigeben oder löschen (Schriftführung, F25).</summary>
[Authorize(Policy = Berechtigungen.ChronikBearbeiten)]
public class EintragModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Formularfelder.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag des Ereignisses.</summary>
        [Required(ErrorMessage = "Bitte das Datum angeben.")]
        [Display(Name = "Datum")]
        public DateOnly? Datum { get; set; }

        /// <summary>Titel.</summary>
        [Required(ErrorMessage = "Bitte einen Titel angeben.")]
        [StringLength(200)]
        [Display(Name = "Titel")]
        public string Titel { get; set; } = "";

        /// <summary>Beschreibung.</summary>
        [StringLength(4000)]
        [Display(Name = "Beschreibung")]
        public string? Text { get; set; }

        /// <summary>Zur Veröffentlichung freigegeben.</summary>
        [Display(Name = "zur Veröffentlichung freigegeben (Jahresbericht, Presse, Homepage)")]
        public bool Veroeffentlichen { get; set; }

        /// <summary>Dokument oder Foto aus der Ablage.</summary>
        [Display(Name = "Dokument oder Foto aus der Ablage")]
        public Guid? DokumentId { get; set; }
    }

    /// <summary>Kennung beim Bearbeiten.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }

    /// <summary>Formular.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Dokumente, die alle sehen dürfen.</summary>
    public IReadOnlyList<SelectListItem> Dokumente { get; private set; } = [];

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id is { } id)
        {
            var c = await db.ChronikEintraege.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            if (c is null)
            {
                return NotFound();
            }

            Formular = new Eingabe { Datum = c.Datum, Titel = c.Titel, Text = c.Text, Veroeffentlichen = c.Veroeffentlichen, DokumentId = c.DokumentId };
        }
        else
        {
            Formular.Datum = Anzeige.Heute(zeit);
        }

        await DokumenteLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert den Eintrag.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Chronik oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Verknüpfbar sind nur Dokumente für alle Benutzer, denn die Chronik lesen alle Angehörigen.
        if (Formular.DokumentId is { } dok && !await db.Dokumente.AnyAsync(d => d.Id == dok && d.Sichtbarkeit == DokumentSichtbarkeit.Alle, ct))
        {
            ModelState.AddModelError("Formular.DokumentId", "Dieses Dokument ist nicht für alle sichtbar.");
        }

        if (!ModelState.IsValid)
        {
            await DokumenteLadenAsync(ct);
            return Page();
        }

        ChronikEintrag c;
        if (Id is { } id)
        {
            if (await db.ChronikEintraege.FirstOrDefaultAsync(e => e.Id == id, ct) is not { } vorhanden)
            {
                return NotFound();
            }

            c = vorhanden;
        }
        else
        {
            c = new ChronikEintrag { Titel = "", ErfasstVon = User.Identity?.Name };
            db.ChronikEintraege.Add(c);
        }

        c.Datum = Formular.Datum!.Value;
        c.Titel = Formular.Titel.Trim();
        c.Text = string.IsNullOrWhiteSpace(Formular.Text) ? null : Formular.Text.Trim();
        c.Veroeffentlichen = Formular.Veroeffentlichen;
        c.DokumentId = Formular.DokumentId;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Chronik-Eintrag gespeichert.";
        return RedirectToPage("Index", new { jahr = c.Datum.Year });
    }

    /// <summary>Löscht den Eintrag.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Chronik oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(CancellationToken ct)
    {
        if (await db.ChronikEintraege.FirstOrDefaultAsync(e => e.Id == Id, ct) is not { } c)
        {
            return NotFound();
        }

        db.ChronikEintraege.Remove(c);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Chronik-Eintrag gelöscht.";
        return RedirectToPage("Index", new { jahr = c.Datum.Year });
    }

    private async Task DokumenteLadenAsync(CancellationToken ct) =>
        Dokumente = await db.Dokumente.AsNoTracking().Where(d => d.Sichtbarkeit == DokumentSichtbarkeit.Alle).OrderBy(d => d.Titel)
            .Select(d => new SelectListItem(d.Titel, d.Id.ToString())).ToListAsync(ct);
}
