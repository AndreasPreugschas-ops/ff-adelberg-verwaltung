using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dokumente;

/// <summary>Neues Dokument mit erster Version hochladen.</summary>
[Authorize(Policy = Berechtigungen.DokumenteVerwalten)]
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class NeuModel(AppDbContext db, Dokumentablage ablage) : PageModel
{
    /// <summary>Formulardaten des neuen Dokuments.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();
    /// <summary>Hochgeladene Datei der ersten Version.</summary>
    [BindProperty, Display(Name = "Datei")] public IFormFile? Datei { get; set; }

    /// <summary>Kategorien zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];

    /// <summary>Eingabefelder eines Dokuments (auch für das Ändern der Angaben).</summary>
    public sealed class Eingabe
    {
        /// <summary>Titel des Dokuments.</summary>
        [Required(ErrorMessage = "Titel fehlt."), StringLength(200), Display(Name = "Titel")]
        public string Titel { get; set; } = "";

        /// <summary>Beschreibung des Dokuments.</summary>
        [StringLength(2000), Display(Name = "Beschreibung")]
        public string? Beschreibung { get; set; }

        /// <summary>Kategorie des Dokuments.</summary>
        [Required(ErrorMessage = "Kategorie fehlt."), Display(Name = "Kategorie")]
        public Guid? KategorieId { get; set; }

        /// <summary>Für wen das Dokument sichtbar ist.</summary>
        [Display(Name = "Sichtbar für")]
        public DokumentSichtbarkeit Sichtbarkeit { get; set; }

        /// <summary>Mitglieder müssen das Lesen bestätigen.</summary>
        [Display(Name = "Lesebestätigung verlangen")]
        public bool LesebestaetigungErforderlich { get; set; }

        /// <summary>Tag, ab dem das Dokument gilt.</summary>
        [Display(Name = "Gültig ab")]
        public DateOnly? GueltigAb { get; set; }

        /// <summary>Nächste Überprüfung.</summary>
        [Display(Name = "Nächste Überprüfung")]
        public DateOnly? RevisionAm { get; set; }

        /// <summary>Dem Bürgermeister mitgeteilt am.</summary>
        [Display(Name = "Dem Bürgermeister mitgeteilt am")]
        public DateOnly? BuergermeisterMitgeteiltAm { get; set; }

        /// <summary>Geräteart, zu der das Dokument gehört (V63).</summary>
        [Display(Name = "Zur Geräteart")]
        public Guid? GeraeteartId { get; set; }

        /// <summary>Inventarnummer des Geräts, zu dem das Dokument gehört (V63).</summary>
        [StringLength(50), Display(Name = "Zum Gerät (Inventarnummer)")]
        public string? GeraetNummer { get; set; }
    }

    /// <summary>Gerätearten zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];

    /// <summary>Prüft den Bezug zu Geräteart und Gerät und liefert die Kennungen.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="formular">Eingaben.</param>
    /// <param name="modelState">Fehler landen hier.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Geräteart und Gerät, jeweils leer, wenn nicht angegeben.</returns>
    public static async Task<(Guid? GeraeteartId, Guid? GeraetId)> BezugAsync(AppDbContext db, Eingabe formular, ModelStateDictionary modelState, CancellationToken ct)
    {
        if (formular.GeraeteartId is { } art && !await db.Geraetearten.AnyAsync(a => a.Id == art, ct))
        {
            modelState.AddModelError("Formular.GeraeteartId", "Unbekannte Geräteart.");
        }

        Guid? geraetId = null;
        if (!string.IsNullOrWhiteSpace(formular.GeraetNummer))
        {
            var nummer = formular.GeraetNummer.Trim();
            geraetId = await db.Geraete.Where(g => g.Inventarnummer == nummer).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);
            if (geraetId is null)
            {
                modelState.AddModelError("Formular.GeraetNummer", $"Kein Gerät mit der Nummer „{nummer}“.");
            }
        }

        return (formular.GeraeteartId, geraetId);
    }

    /// <summary>Gerätearten für die Auswahl.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Auswahlliste.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> GeraeteartenAsync(AppDbContext db, CancellationToken ct) =>
        await db.Geraetearten.AsNoTracking().OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
            .ToListAsync(ct);

    /// <summary>Lädt die Kategorien.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <param name="geraeteartId">Geräteart zum Vorbelegen (aus der Geräteakte).</param>
    /// <param name="geraet">Inventarnummer zum Vorbelegen (aus der Geräteakte).</param>
    public async Task OnGetAsync(CancellationToken ct, Guid? geraeteartId = null, string? geraet = null)
    {
        Formular.GeraeteartId = geraeteartId;
        Formular.GeraetNummer = geraet;
        await LadenAsync(ct);
    }

    /// <summary>Legt das Dokument mit der hochgeladenen Datei als erster Version an.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum neuen Dokument oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Datei is null)
        {
            ModelState.AddModelError(nameof(Datei), "Bitte eine Datei auswählen.");
        }
        else if (await ablage.PruefenAsync(Datei.FileName, Datei.Length, ct) is { } fehler)
        {
            ModelState.AddModelError(nameof(Datei), fehler);
        }

        if (Formular.KategorieId is { } k && !await db.Dokumentkategorien.AnyAsync(x => x.Id == k, ct))
        {
            ModelState.AddModelError("Formular.KategorieId", "Unbekannte Kategorie.");
        }

        var (geraeteartId, geraetId) = await BezugAsync(db, Formular, ModelState, ct);
        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var dokument = new Dokument
        {
            Titel = Formular.Titel.Trim(),
            Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim(),
            KategorieId = Formular.KategorieId!.Value,
            Sichtbarkeit = Formular.Sichtbarkeit,
            LesebestaetigungErforderlich = Formular.LesebestaetigungErforderlich,
            GueltigAb = Formular.GueltigAb,
            RevisionAm = Formular.RevisionAm,
            BuergermeisterMitgeteiltAm = Formular.BuergermeisterMitgeteiltAm,
            GeraeteartId = geraeteartId,
            GeraetId = geraetId,
        };
        db.Dokumente.Add(dokument);
        await using (var inhalt = Datei!.OpenReadStream())
        {
            await ablage.VersionHinzufuegenAsync(dokument, Datei.FileName, inhalt, null, ct);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{dokument.Titel}“ hochgeladen.";
        return RedirectToPage("Detail", new { id = dokument.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Kategorien = await db.Dokumentkategorien.AsNoTracking().Where(k => k.Aktiv).OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);
        Geraetearten = await GeraeteartenAsync(db, ct);
    }
}
