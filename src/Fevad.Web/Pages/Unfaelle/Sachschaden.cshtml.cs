using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Unfaelle;

/// <summary>Ein Sachschaden: Meldung des Mitglieds, Weiterleitung, Entscheidung der Gemeinde, Erstattung und Rückgriff.</summary>
[Authorize(Policy = Berechtigungen.SachschaedenLesen)]
public class SachschadenModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Der Sachschaden samt Mitglied.</summary>
    public Sachschaden Schaden { get; private set; } = null!;
    /// <summary>Bezug: Einsatz oder Dienst als Text.</summary>
    public string? Bezug { get; private set; }
    /// <summary>Fotos (Kennung und Name).</summary>
    public IReadOnlyList<(Guid Id, string Dateiname)> Fotos { get; private set; } = [];
    /// <summary>Der Benutzer darf bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Bearbeitungsfelder.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Bearbeitungsfelder eines Sachschadens.</summary>
    public sealed class Eingabe
    {
        /// <summary>Bearbeitungsstand.</summary>
        [Display(Name = "Status")] public SachschadenStatus Status { get; set; }
        /// <summary>Ersetzter Betrag.</summary>
        [Range(0, 100000), Display(Name = "Erstattet (Euro)")] public decimal? ErstattetBetrag { get; set; }
        /// <summary>Rückgriff der Gemeinde.</summary>
        [Display(Name = "Gemeinde nimmt Rückgriff")] public bool Regress { get; set; }
        /// <summary>Angaben zum Rückgriff.</summary>
        [StringLength(1000), Display(Name = "Rückgriff gegen")] public string? RegressBemerkung { get; set; }
        /// <summary>Bemerkung, z. B. Begründung der Gemeinde.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt den Sachschaden.</summary>
    /// <param name="id">Kennung des Sachschadens.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Status = Schaden.Status, ErstattetBetrag = Schaden.ErstattetBetrag, Regress = Schaden.Regress,
            RegressBemerkung = Schaden.RegressBemerkung, Bemerkung = Schaden.Bemerkung,
        };
        return Page();
    }

    /// <summary>Speichert Status, Erstattung und Rückgriff.</summary>
    /// <param name="id">Kennung des Sachschadens.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.SachschaedenBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var schaden = await db.Sachschaeden.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (schaden is null)
        {
            return NotFound();
        }

        if (Formular.Status == SachschadenStatus.Erstattet && Formular.ErstattetBetrag is null)
        {
            ModelState.AddModelError("Formular.ErstattetBetrag", "Bitte den erstatteten Betrag angeben.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        schaden.StatusSetzen(Formular.Status, Anzeige.Heute(zeit));
        schaden.ErstattetBetrag = Formular.Status == SachschadenStatus.Abgelehnt ? null : Formular.ErstattetBetrag;
        schaden.Regress = Formular.Regress;
        schaden.RegressBemerkung = Formular.Regress && !string.IsNullOrWhiteSpace(Formular.RegressBemerkung) ? Formular.RegressBemerkung.Trim() : null;
        schaden.Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Zeigt ein Foto.</summary>
    /// <param name="id">Kennung des Sachschadens.</param>
    /// <param name="fotoId">Kennung des Fotos.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Bild oder 404.</returns>
    public async Task<IActionResult> OnGetFotoAsync(Guid id, Guid fotoId, CancellationToken ct)
    {
        var foto = await db.Meldungsfotos.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fotoId && f.SachschadenId == id, ct);
        return foto is null ? NotFound() : File(foto.Inhalt, foto.Inhaltstyp);
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var schaden = await db.Sachschaeden.AsNoTracking().Include(s => s.Mitglied).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (schaden is null)
        {
            return false;
        }

        Schaden = schaden;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.SachschaedenBearbeiten)).Succeeded;
        Bezug = schaden.EinsatzId is { } e
            ? await db.Einsaetze.Where(x => x.Id == e).Select(x => "Einsatz " + x.Einsatznummer + " " + x.Stichwort).FirstOrDefaultAsync(ct)
            : schaden.DienstId is { } d ? await db.Dienste.Where(x => x.Id == d).Select(x => x.Titel).FirstOrDefaultAsync(ct) : null;
        Fotos = (await db.Meldungsfotos.AsNoTracking().Where(f => f.SachschadenId == id).OrderBy(f => f.ErstelltAm)
            .Select(f => new { f.Id, f.Dateiname }).ToListAsync(ct)).Select(f => (f.Id, f.Dateiname)).ToList();
        return true;
    }
}
