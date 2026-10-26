using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Funktion bei Einsatz und Übung anlegen oder bearbeiten (V14), mit nötigen Qualifikationen für den Warnhinweis im Einsatzbericht.</summary>
[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class EinsatzfunktionModel(AppDbContext db) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird eine neue Funktion angelegt.</summary>
    public bool IstNeu => Formular.Id is null;

    /// <summary>Formular einer Funktion.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuer Funktion.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Kurzcode.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")] public string Code { get; set; } = "";
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Nötige Qualifikationen als Codes, Alternativen mit „|“.</summary>
        [StringLength(100), Display(Name = "Nötige Qualifikation (Codes, Alternativen mit |)")] public string? Qualifikationen { get; set; }
        /// <summary>Zählt als Atemschutzeinsatz.</summary>
        [Display(Name = "Atemschutz")] public bool Atemschutz { get; set; }
        /// <summary>Reihenfolge in Listen.</summary>
        [Display(Name = "Sortierung")] public int Sortierung { get; set; } = 100;
        /// <summary>In Auswahllisten angeboten.</summary>
        [Display(Name = "Aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt eine Funktion oder ein leeres Formular.</summary>
    /// <param name="id">Kennung der Funktion; leer für eine neue.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var t = await db.Einsatzfunktionen.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (t is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = t.Id, Version = db.Entry(t).Property<uint>("Version").CurrentValue, Code = t.Code, Bezeichnung = t.Bezeichnung,
                Qualifikationen = t.Qualifikationen, Atemschutz = t.Atemschutz, Sortierung = t.Sortierung, Aktiv = t.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Speichert die Funktion.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zu den Katalogen, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Einsatzfunktionen.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Einsatzfunktion funktion;
        if (IstNeu)
        {
            funktion = new Einsatzfunktion { Code = code, Bezeichnung = "" };
            db.Einsatzfunktionen.Add(funktion);
        }
        else
        {
            var vorhanden = await db.Einsatzfunktionen.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            funktion = vorhanden;
            db.Entry(funktion).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        funktion.Code = code;
        funktion.Bezeichnung = Formular.Bezeichnung.Trim();
        funktion.Qualifikationen = string.IsNullOrWhiteSpace(Formular.Qualifikationen)
            ? null
            : string.Join('|', Formular.Qualifikationen.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();
        funktion.Atemschutz = Formular.Atemschutz;
        funktion.Sortierung = Formular.Sortierung;
        funktion.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            return Page();
        }

        TempData["Meldung"] = $"{funktion.Bezeichnung} gespeichert.";
        return RedirectToPage("Kataloge");
    }
}
