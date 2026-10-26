using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Thema der Standortausbildung anlegen oder bearbeiten (V53).</summary>
[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class AusbildungsthemaModel(AppDbContext db) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neues Thema angelegt.</summary>
    public bool IstNeu => Formular.Id is null;

    /// <summary>Formular eines Themas.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuem Thema.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Kurzcode.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")] public string Code { get; set; } = "";
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Grundlage, z. B. FwDV 3.</summary>
        [StringLength(100), Display(Name = "Grundlage")] public string? Grundlage { get; set; }
        /// <summary>Soll jedes Jahr vorkommen.</summary>
        [Display(Name = "Pflichtthema")] public bool Pflicht { get; set; }
        /// <summary>Reihenfolge in Listen.</summary>
        [Display(Name = "Sortierung")] public int Sortierung { get; set; } = 100;
        /// <summary>In Auswahllisten angeboten.</summary>
        [Display(Name = "Aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt ein Thema oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Themas; leer für ein neues.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var t = await db.Ausbildungsthemen.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (t is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = t.Id, Version = db.Entry(t).Property<uint>("Version").CurrentValue, Code = t.Code, Bezeichnung = t.Bezeichnung,
                Grundlage = t.Grundlage, Pflicht = t.Pflicht, Sortierung = t.Sortierung, Aktiv = t.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Speichert das Thema.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zu den Katalogen, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Ausbildungsthemen.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Ausbildungsthema thema;
        if (IstNeu)
        {
            thema = new Ausbildungsthema { Code = code, Bezeichnung = "" };
            db.Ausbildungsthemen.Add(thema);
        }
        else
        {
            var vorhanden = await db.Ausbildungsthemen.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            thema = vorhanden;
            db.Entry(thema).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        thema.Code = code;
        thema.Bezeichnung = Formular.Bezeichnung.Trim();
        thema.Grundlage = string.IsNullOrWhiteSpace(Formular.Grundlage) ? null : Formular.Grundlage.Trim();
        thema.Pflicht = Formular.Pflicht;
        thema.Sortierung = Formular.Sortierung;
        thema.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            return Page();
        }

        TempData["Meldung"] = $"{thema.Bezeichnung} gespeichert.";
        return RedirectToPage("Kataloge");
    }
}
