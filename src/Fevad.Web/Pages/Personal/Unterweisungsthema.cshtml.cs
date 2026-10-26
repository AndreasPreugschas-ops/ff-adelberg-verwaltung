using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>Thema der Unterweisung anlegen oder bearbeiten (V53, F22: Liste änderbar).</summary>
[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class UnterweisungsthemaModel(AppDbContext db) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neues Thema angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Auswahl der Qualifikationen für die Zielgruppe.</summary>
    public IReadOnlyList<SelectListItem> Qualifikationen { get; private set; } = [];
    /// <summary>Auswahl der wiederkehrenden Nachweise ohne Gesundheitsbezug.</summary>
    public IReadOnlyList<SelectListItem> Nachweisarten { get; private set; } = [];

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
        /// <summary>Pflichtthema im Kalenderjahr.</summary>
        [Display(Name = "Pflichtthema")] public bool Pflicht { get; set; } = true;
        /// <summary>Zielgruppe nur Inhaber dieser Qualifikation.</summary>
        [Display(Name = "Zielgruppe")] public Guid? QualifikationId { get; set; }
        /// <summary>Nachweis, den der Abschluss einträgt.</summary>
        [Display(Name = "Nachweis eintragen")] public Guid? NachweisartId { get; set; }
        /// <summary>Zählt als Atemschutz-Unterweisung (FwDV 7).</summary>
        [Display(Name = "Atemschutz-Unterweisung")] public bool Atemschutzunterweisung { get; set; }
        /// <summary>Beschreibung.</summary>
        [StringLength(500), Display(Name = "Beschreibung")] public string? Beschreibung { get; set; }
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
            var t = await db.Unterweisungsthemen.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (t is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = t.Id, Version = db.Entry(t).Property<uint>("Version").CurrentValue, Code = t.Code, Bezeichnung = t.Bezeichnung, Pflicht = t.Pflicht,
                QualifikationId = t.QualifikationId, NachweisartId = t.NachweisartId, Atemschutzunterweisung = t.Atemschutzunterweisung,
                Beschreibung = t.Beschreibung, Sortierung = t.Sortierung, Aktiv = t.Aktiv,
            };
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert das Thema.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zu den Katalogen, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Unterweisungsthemen.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        if (Formular.NachweisartId is { } nid && await db.Nachweisarten.AnyAsync(a => a.Id == nid && a.Gesundheitsbezogen, ct))
        {
            ModelState.AddModelError("Formular.NachweisartId", "Nachweise mit Gesundheitsbezug lassen sich nicht über eine Unterweisung eintragen.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Unterweisungsthema thema;
        if (IstNeu)
        {
            thema = new Unterweisungsthema { Code = code, Bezeichnung = "" };
            db.Unterweisungsthemen.Add(thema);
        }
        else
        {
            var vorhanden = await db.Unterweisungsthemen.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            thema = vorhanden;
            db.Entry(thema).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        thema.Code = code;
        thema.Bezeichnung = Formular.Bezeichnung.Trim();
        thema.Pflicht = Formular.Pflicht;
        thema.QualifikationId = Formular.QualifikationId;
        thema.NachweisartId = Formular.NachweisartId;
        thema.Atemschutzunterweisung = Formular.Atemschutzunterweisung;
        thema.Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim();
        thema.Sortierung = Formular.Sortierung;
        thema.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{thema.Bezeichnung} gespeichert.";
        return RedirectToPage("Kataloge");
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Qualifikationen = await db.Qualifikationen.AsNoTracking().OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToListAsync(ct);
        Nachweisarten = await db.Nachweisarten.AsNoTracking().Where(a => !a.Gesundheitsbezogen).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);
    }
}
