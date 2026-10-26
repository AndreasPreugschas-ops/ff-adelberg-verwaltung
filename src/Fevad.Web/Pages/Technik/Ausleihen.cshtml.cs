using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Ausleihen und Fremdgeräte (V63): eigene Geräte, die an andere Stellen verliehen sind, und geliehene Fremdgeräte,
/// mit vereinbarter Rückgabe und Rückgabevermerk. Partner sind Stellen, keine Privatpersonen.
/// </summary>
public class AusleihenModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Ausleihen, überfällige zuerst.</summary>
    public IReadOnlyList<Ausleihe> Offen { get; private set; } = [];

    /// <summary>Zurückgegebene Ausleihen der letzten zwölf Monate.</summary>
    public IReadOnlyList<Ausleihe> Zurueck { get; private set; } = [];

    /// <summary>Anlegen und Zurückbuchen darf, wer Prüfungen eintragen darf (Gerätewart).</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Stichtag für „überfällig“.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Richtungen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Richtungen { get; } = Enum.GetValues<AusleiheRichtung>().Select(r => new SelectListItem(Anzeige.Text(r), r.ToString())).ToList();

    /// <summary>Neue Ausleihe.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Formular einer neuen Ausleihe.</summary>
    public sealed class Eingabe
    {
        /// <summary>Verliehen oder geliehen.</summary>
        [Display(Name = "Richtung")] public AusleiheRichtung Richtung { get; set; }

        /// <summary>Inventarnummer des eigenen Geräts (oder eines als Gerät geführten Fremdgeräts).</summary>
        [StringLength(50), Display(Name = "Inventarnummer")] public string? Nummer { get; set; }

        /// <summary>Bezeichnung eines Fremdgeräts ohne Inventarnummer.</summary>
        [StringLength(200), Display(Name = "Fremdgerät (Bezeichnung)")] public string? Bezeichnung { get; set; }

        /// <summary>Andere Stelle.</summary>
        [Required(ErrorMessage = "Partner fehlt."), StringLength(200), Display(Name = "Partner (Feuerwehr, Firma, Stelle)")] public string? Partner { get; set; }

        /// <summary>Beginn; leer heißt heute.</summary>
        [Display(Name = "Von")] public DateOnly? Von { get; set; }

        /// <summary>Vereinbarte Rückgabe.</summary>
        [Display(Name = "Rückgabe vereinbart bis")] public DateOnly? BisGeplant { get; set; }

        /// <summary>Bemerkung (Sachstand).</summary>
        [StringLength(200), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt offene und zurückgegebene Ausleihen.</summary>
    /// <param name="nummer">Inventarnummer zum Vorbelegen (aus der Geräteakte).</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(string? nummer, CancellationToken ct)
    {
        Formular.Nummer = nummer;
        await LadenAsync(ct);
    }

    /// <summary>Legt eine Ausleihe an.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        var von = Formular.Von ?? heute;
        Geraet? geraet = null;
        if (!string.IsNullOrWhiteSpace(Formular.Nummer))
        {
            var inventarnummer = Geraetecode.NummerAusScan(Formular.Nummer);
            geraet = await db.Geraete.FirstOrDefaultAsync(g => g.Inventarnummer == inventarnummer && g.AusgesondertAm == null, ct);
            if (geraet is null)
            {
                ModelState.AddModelError("Formular.Nummer", $"Kein Gerät mit der Nummer „{inventarnummer}“.");
            }
            else if (await db.Ausleihen.AnyAsync(a => a.GeraetId == geraet.Id && a.ZurueckAm == null, ct))
            {
                ModelState.AddModelError("Formular.Nummer", $"{geraet.Inventarnummer} ist schon ausgeliehen.");
            }
        }
        else if (Formular.Richtung == AusleiheRichtung.Verliehen)
        {
            ModelState.AddModelError("Formular.Nummer", "Für ein verliehenes Gerät die Inventarnummer angeben.");
        }
        else if (string.IsNullOrWhiteSpace(Formular.Bezeichnung))
        {
            ModelState.AddModelError("Formular.Bezeichnung", "Inventarnummer oder Bezeichnung des Fremdgeräts angeben.");
        }

        if (von > heute)
        {
            ModelState.AddModelError("Formular.Von", "Das Datum liegt in der Zukunft.");
        }

        if (Formular.BisGeplant < von)
        {
            ModelState.AddModelError("Formular.BisGeplant", "Die Rückgabe liegt vor dem Beginn.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        db.Ausleihen.Add(new Ausleihe
        {
            Richtung = Formular.Richtung,
            GeraetId = geraet?.Id,
            Bezeichnung = geraet is null ? Formular.Bezeichnung!.Trim() : null,
            Partner = Formular.Partner!.Trim(),
            Von = von,
            BisGeplant = Formular.BisGeplant,
            Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = Formular.Richtung == AusleiheRichtung.Verliehen ? $"{geraet!.Inventarnummer} an {Formular.Partner!.Trim()} verliehen." : "Fremdgerät eingetragen.";
        return RedirectToPage();
    }

    /// <summary>Bucht eine Ausleihe zurück.</summary>
    /// <param name="ausleiheId">Die Ausleihe.</param>
    /// <param name="zurueckAm">Tag der Rückgabe; leer heißt heute.</param>
    /// <param name="vermerk">Zustand bei der Rückgabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task<IActionResult> OnPostZurueckAsync(Guid ausleiheId, DateOnly? zurueckAm, string? vermerk, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        var ausleihe = await db.Ausleihen.FirstOrDefaultAsync(a => a.Id == ausleiheId && a.ZurueckAm == null, ct);
        if (ausleihe is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        var tag = zurueckAm ?? heute;
        if (tag > heute || tag < ausleihe.Von)
        {
            TempData["Meldung"] = "Das Rückgabedatum liegt in der Zukunft oder vor dem Beginn.";
            return RedirectToPage();
        }

        ausleihe.ZurueckAm = tag;
        ausleihe.Rueckgabevermerk = string.IsNullOrWhiteSpace(vermerk) ? null : vermerk.Trim()[..Math.Min(vermerk.Trim().Length, 200)];
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Rückgabe gebucht.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        var offen = await db.Ausleihen.AsNoTracking().Include(a => a.Geraet).Where(a => a.ZurueckAm == null).ToListAsync(ct);
        Offen = offen.OrderByDescending(a => a.Ueberfaellig(Heute)).ThenBy(a => a.BisGeplant ?? DateOnly.MaxValue).ThenBy(a => a.Von).ToList();
        var seit = Heute.AddMonths(-12);
        Zurueck = await db.Ausleihen.AsNoTracking().Include(a => a.Geraet).Where(a => a.ZurueckAm >= seit).OrderByDescending(a => a.ZurueckAm).ToListAsync(ct);
    }
}
