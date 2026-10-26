using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>
/// Bauteile eines Geräts (V61): eingebaut, fällig zum Tausch, Lebenslauf der ausgebauten. Nur bei eingeschalteten Bauteildaten;
/// einbauen und ausbauen darf, wer TechnikPruefen hat.
/// </summary>
public class BauteileModel(AppDbContext db, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Das Gerät.</summary>
    public Geraet Geraet { get; private set; } = null!;

    /// <summary>Bauteildaten sind eingeschaltet.</summary>
    public bool Aktiv { get; private set; }

    /// <summary>Darf ein- und ausbauen.</summary>
    public bool DarfPruefen { get; private set; }

    /// <summary>Bauteile, eingebaute zuerst.</summary>
    public IReadOnlyList<Zeile> Bauteile { get; private set; } = [];

    /// <summary>Bauteilarten der Geräteart.</summary>
    public IReadOnlyList<SelectListItem> Arten { get; private set; } = [];

    /// <summary>Gelb so viele Tage vor dem Tausch.</summary>
    public int WarnungTage { get; private set; }

    /// <summary>Einbau eines Bauteils.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Ein Bauteil in der Liste.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Art">Bezeichnung der Bauteilart.</param>
    /// <param name="HergestelltAm">Herstelldatum.</param>
    /// <param name="EingebautAm">Einbau.</param>
    /// <param name="AusgebautAm">Ausbau.</param>
    /// <param name="TauschFaellig">Spätester Tausch.</param>
    /// <param name="Ampel">Ampel des Tauschs.</param>
    /// <param name="Bemerkung">Bemerkung.</param>
    public sealed record Zeile(Guid Id, string Art, DateOnly? HergestelltAm, DateOnly EingebautAm, DateOnly? AusgebautAm, DateOnly? TauschFaellig, Fristampel Ampel, string? Bemerkung);

    /// <summary>Formular des Einbaus.</summary>
    public sealed class Eingabe
    {
        /// <summary>Bauteilart.</summary>
        [Required(ErrorMessage = "Bauteil fehlt."), Display(Name = "Bauteil")] public Guid? BauteilartId { get; set; }
        /// <summary>Herstelldatum.</summary>
        [Display(Name = "Hergestellt am")] public DateOnly? HergestelltAm { get; set; }
        /// <summary>Einbau.</summary>
        [Required(ErrorMessage = "Einbaudatum fehlt."), Display(Name = "Eingebaut am")] public DateOnly? EingebautAm { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(200), Display(Name = "Bemerkung (z. B. Charge)")] public string? Bemerkung { get; set; }
        /// <summary>Das bisher eingebaute Bauteil gleicher Art zum selben Tag ausbauen.</summary>
        [Display(Name = "bisheriges Bauteil dieser Art ausbauen (Tausch)")] public bool AltesAusbauen { get; set; } = true;
    }

    /// <summary>Zeigt die Bauteile.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular.EingebautAm = Anzeige.Heute(zeit);
        return Page();
    }

    /// <summary>Baut ein Bauteil ein, auf Wunsch als Tausch gegen das bisherige.</summary>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfPruefen || !Aktiv)
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        if (Formular.EingebautAm > heute)
        {
            ModelState.AddModelError("Formular.EingebautAm", "Das Datum liegt in der Zukunft.");
        }

        if (Formular.HergestelltAm > Formular.EingebautAm)
        {
            ModelState.AddModelError("Formular.HergestelltAm", "Herstellung nach dem Einbau.");
        }

        if (Formular.BauteilartId is { } artId && !Arten.Any(a => a.Value == artId.ToString()))
        {
            ModelState.AddModelError("Formular.BauteilartId", "Das Bauteil passt nicht zu dieser Geräteart.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Formular.AltesAusbauen)
        {
            foreach (var alt in await db.Bauteile.Where(b => b.GeraetId == id && b.BauteilartId == Formular.BauteilartId && b.AusgebautAm == null).ToListAsync(ct))
            {
                alt.AusgebautAm = Formular.EingebautAm;
            }
        }

        db.Bauteile.Add(new Bauteil
        {
            GeraetId = id,
            BauteilartId = Formular.BauteilartId!.Value,
            HergestelltAm = Formular.HergestelltAm,
            EingebautAm = Formular.EingebautAm!.Value,
            Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Bauteil eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Baut ein Bauteil ohne Ersatz aus.</summary>
    public async Task<IActionResult> OnPostAusbauenAsync(Guid id, Guid bauteilId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        if (await db.Bauteile.FirstOrDefaultAsync(b => b.Id == bauteilId && b.GeraetId == id && b.AusgebautAm == null, ct) is { } bauteil)
        {
            bauteil.AusgebautAm = Anzeige.Heute(zeit);
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = "Bauteil ausgebaut.";
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var geraet = await db.Geraete.AsNoTracking().Include(g => g.Geraeteart).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (geraet is null)
        {
            return false;
        }

        Geraet = geraet;
        var heute = Anzeige.Heute(zeit);
        Aktiv = await parameter.ZahlAsync(ParameterSchluessel.BauteildatenAktiv, heute, 0, ct) == 1;
        WarnungTage = await parameter.ZahlAsync(ParameterSchluessel.PruefungWarnungTage, heute, 30, ct);
        DarfPruefen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        Arten = await db.Bauteilarten.AsNoTracking().Where(b => b.GeraeteartId == geraet.GeraeteartId && b.Aktiv).OrderBy(b => b.Sortierung)
            .Select(b => new SelectListItem(b.Bezeichnung, b.Id.ToString())).ToListAsync(ct);
        var bauteile = await db.Bauteile.AsNoTracking().Include(b => b.Bauteilart).Where(b => b.GeraetId == id).ToListAsync(ct);
        Bauteile = bauteile
            .Select(b =>
            {
                var faellig = b.AusgebautAm is null ? b.TauschFaellig(b.Bauteilart!.EingebautMonate, b.Bauteilart.GesamtMonate) : null;
                return new Zeile(b.Id, b.Bauteilart!.Bezeichnung, b.HergestelltAm, b.EingebautAm, b.AusgebautAm, faellig,
                    Frist.Bewerten(faellig, heute, WarnungTage), b.Bemerkung);
            })
            .OrderBy(z => z.AusgebautAm is not null).ThenBy(z => z.Art, StringComparer.CurrentCulture).ThenByDescending(z => z.EingebautAm)
            .ToList();
        return true;
    }
}
