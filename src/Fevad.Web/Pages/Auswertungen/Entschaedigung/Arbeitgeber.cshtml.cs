using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Konto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>
/// Arbeitgeber (V33, § 15 FwG): Erstattungsanträge privater Arbeitgeber erfassen und abschließen, ausgestellte Bescheinigungen sehen und
/// widerrufen, für ein Mitglied eine Bescheinigung mit Ruhezeit nach Einsätzen ausstellen. Lesen darf, wer die Entschädigung sieht,
/// also auch die Gemeinde; ändern nur, wer abrechnet.
/// </summary>
public class ArbeitgeberModel(
    AppDbContext db, Arbeitgeberbescheinigungen bescheinigungen, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer,
    IOptions<AnmeldungOptionen> anmeldung, TimeProvider zeit) : PageModel
{
    /// <summary>Erstattungen an Arbeitgeber, neueste zuerst.</summary>
    public IReadOnlyList<Arbeitgebererstattung> Erstattungen { get; private set; } = [];

    /// <summary>Zuletzt ausgestellte Bescheinigungen.</summary>
    public IReadOnlyList<Arbeitgeberbescheinigung> Bescheinigungen { get; private set; } = [];

    /// <summary>Aktive Mitglieder der Einsatzabteilung zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Der Benutzer darf erfassen, ausstellen und widerrufen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Formular einer neuen Erstattung.</summary>
    [BindProperty] public ErstattungEingabe Erstattung { get; set; } = new();

    /// <summary>Formular einer Bescheinigung für ein Mitglied.</summary>
    [BindProperty] public BescheinigungEingabe Bescheinigung { get; set; } = new();

    /// <summary>Eingaben einer Erstattung.</summary>
    public sealed class ErstattungEingabe
    {
        /// <summary>Mitglied.</summary>
        [Required(ErrorMessage = "Bitte das Mitglied wählen."), Display(Name = "Mitglied")] public Guid? MitgliedId { get; set; }
        /// <summary>Arbeitgeber.</summary>
        [Required(ErrorMessage = "Bitte den Arbeitgeber angeben."), StringLength(200), Display(Name = "Arbeitgeber")] public string Arbeitgeber { get; set; } = "";
        /// <summary>Erster Tag.</summary>
        [Required(ErrorMessage = "Bitte den ersten Tag angeben."), Display(Name = "Von")] public DateOnly? Von { get; set; }
        /// <summary>Letzter Tag.</summary>
        [Required(ErrorMessage = "Bitte den letzten Tag angeben."), Display(Name = "Bis")] public DateOnly? Bis { get; set; }
        /// <summary>Fortgezahltes Entgelt.</summary>
        [Required(ErrorMessage = "Bitte das Entgelt angeben."), Range(0, 1_000_000), Display(Name = "Fortgezahltes Entgelt (Euro)")] public decimal? Entgelt { get; set; }
        /// <summary>Arbeitgeberanteile Sozialversicherung.</summary>
        [Range(0, 1_000_000), Display(Name = "Sozialversicherung Arbeitgeber (Euro)")] public decimal? Sozialversicherung { get; set; }
        /// <summary>Prüfcode der Bescheinigung.</summary>
        [StringLength(20), Display(Name = "Prüfcode der Bescheinigung")] public string? Pruefcode { get; set; }
    }

    /// <summary>Eingaben einer Bescheinigung für ein Mitglied.</summary>
    public sealed class BescheinigungEingabe
    {
        /// <summary>Mitglied.</summary>
        [Display(Name = "Mitglied")] public Guid? MitgliedId { get; set; }
        /// <summary>Erster Tag.</summary>
        [Display(Name = "Von")] public DateOnly? Von { get; set; }
        /// <summary>Letzter Tag.</summary>
        [Display(Name = "Bis")] public DateOnly? Bis { get; set; }
        /// <summary>Arbeitgeber; leer = hinterlegter Arbeitgeber.</summary>
        [StringLength(200), Display(Name = "Arbeitgeber (leer = hinterlegter)")] public string? Arbeitgeber { get; set; }
        /// <summary>Ruhezeit nach Einsätzen in Stunden.</summary>
        [Range(0, 24), Display(Name = "Ruhezeit nach Einsätzen (Stunden)")] public int? RuhezeitStunden { get; set; }
        /// <summary>Übungen und sonstige Dienste aufnehmen.</summary>
        [Display(Name = "auch Übungen und sonstige Dienste")] public bool Dienste { get; set; }
    }

    /// <summary>Zeigt Erstattungen und Bescheinigungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Erfasst den Erstattungsantrag eines Arbeitgebers.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostErstattungAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await LadenAsync(ct);
        ModelState.Clear();
        if (!TryValidateModel(Erstattung, nameof(Erstattung)))
        {
            return Page();
        }

        string? code = null;
        if (!string.IsNullOrWhiteSpace(Erstattung.Pruefcode)
            && ((code = Arbeitgeberbescheinigung.PruefcodeNormalisieren(Erstattung.Pruefcode)) is null
                || !await db.Arbeitgeberbescheinigungen.AnyAsync(b => b.Pruefcode == code && b.MitgliedId == Erstattung.MitgliedId && !b.Widerrufen, ct)))
        {
            ModelState.AddModelError("Erstattung.Pruefcode", "Zu diesem Prüfcode gibt es keine gültige Bescheinigung für dieses Mitglied.");
        }

        if (!Mitglieder.Any(m => m.Value == Erstattung.MitgliedId.ToString()))
        {
            ModelState.AddModelError("Erstattung.MitgliedId", "Dieses Mitglied steht nicht zur Wahl.");
        }

        if (Erstattung.Bis < Erstattung.Von)
        {
            ModelState.AddModelError("Erstattung.Bis", "Das Ende liegt vor dem Beginn.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.Arbeitgebererstattungen.Add(new Arbeitgebererstattung
        {
            MitgliedId = Erstattung.MitgliedId!.Value, Arbeitgeber = Erstattung.Arbeitgeber.Trim(), Von = Erstattung.Von!.Value, Bis = Erstattung.Bis!.Value,
            Entgelt = Erstattung.Entgelt!.Value, Sozialversicherung = Erstattung.Sozialversicherung ?? 0, Pruefcode = code,
            Stand = ErstattungStand.Eingegangen, EingegangenAm = Anzeige.Heute(zeit),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Erstattungsantrag erfasst. Für diese Tage zahlt die Abrechnung dem Mitglied keinen Verdienstausfall.";
        return RedirectToPage();
    }

    /// <summary>Schließt eine Erstattung ab: erstattet oder abgelehnt.</summary>
    /// <param name="id">Kennung der Erstattung.</param>
    /// <param name="stand">Erstattet oder Abgelehnt.</param>
    /// <param name="vermerk">Kassenzeichen oder Ablehnungsgrund.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEntscheidenAsync(Guid id, ErstattungStand stand, string? vermerk, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var e = await db.Arbeitgebererstattungen.FirstOrDefaultAsync(x => x.Id == id && x.Stand == ErstattungStand.Eingegangen, ct);
        if (e is null || stand == ErstattungStand.Eingegangen)
        {
            return NotFound();
        }

        (e.Stand, e.EntschiedenAm, e.Vermerk) = (stand, Anzeige.Heute(zeit), string.IsNullOrWhiteSpace(vermerk) ? null : vermerk.Trim()[..Math.Min(vermerk.Trim().Length, 500)]);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Erstattung an {e.Arbeitgeber}: {Anzeige.Text(stand)}.";
        return RedirectToPage();
    }

    /// <summary>Stellt für ein Mitglied eine Bescheinigung aus, auf Wunsch mit Ruhezeit nach Einsätzen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostAusstellenAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await LadenAsync(ct);
        ModelState.Clear();
        if (Bescheinigung.MitgliedId is not { } mitgliedId || !Mitglieder.Any(m => m.Value == mitgliedId.ToString())
            || Bescheinigung.Von is not { } von || Bescheinigung.Bis is not { } bis || bis < von)
        {
            ModelState.AddModelError("Bescheinigung.MitgliedId", "Bitte Mitglied und Zeitraum angeben.");
            return Page();
        }

        var arbeitgeber = string.IsNullOrWhiteSpace(Bescheinigung.Arbeitgeber)
            ? await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Arbeitgeber).FirstAsync(ct)
            : Bescheinigung.Arbeitgeber.Trim();
        if (string.IsNullOrWhiteSpace(arbeitgeber))
        {
            ModelState.AddModelError("Bescheinigung.Arbeitgeber", "Für das Mitglied ist kein Arbeitgeber hinterlegt; bitte angeben.");
            return Page();
        }

        try
        {
            var b = await bescheinigungen.AusstellenAsync(mitgliedId, von, bis, new(true, true, Bescheinigung.Dienste), arbeitgeber,
                Bescheinigung.RuhezeitStunden, benutzer.Id, ct);
            return File(await Konto.ArbeitgeberModel.PdfErzeugenAsync(db, anmeldung.Value, b, null, ct), "application/pdf", Arbeitgeberbescheinigungen.Dateiname(b));
        }
        catch (InvalidOperationException fehler)
        {
            ModelState.AddModelError("Bescheinigung.MitgliedId", fehler.Message);
            return Page();
        }
    }

    /// <summary>Erklärt eine Bescheinigung für ungültig; die Prüfseite zeigt das an.</summary>
    /// <param name="id">Kennung der Bescheinigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostWiderrufenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var b = await db.Arbeitgeberbescheinigungen.FirstOrDefaultAsync(x => x.Id == id && !x.Widerrufen, ct);
        if (b is null)
        {
            return NotFound();
        }

        b.Widerrufen = true;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Bescheinigung {b.Pruefcode} widerrufen.";
        return RedirectToPage();
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungAbrechnen)).Succeeded;

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        var heute = Anzeige.Heute(zeit);
        // Jugend- und Kinderfeuerwehr bleiben abgeschirmt: ohne Jugendzugang nur Mitglieder mit aktiver Einsatzabteilung.
        var mitJugend = (await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendZugang)).Succeeded;
        var sichtbar = db.Mitglieder.Where(m => mitJugend
            || m.Mitgliedschaften.Any(s => s.Abteilung!.Code == AbteilungCode.Einsatz && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
            || !m.Mitgliedschaften.Any(s => (s.Abteilung!.Code == AbteilungCode.Jugend || s.Abteilung.Code == AbteilungCode.Kinder) && s.GueltigAb <= heute
                && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .Select(m => m.Id);
        Erstattungen = await db.Arbeitgebererstattungen.AsNoTracking().Include(e => e.Mitglied).Where(e => sichtbar.Contains(e.MitgliedId))
            .OrderByDescending(e => e.EingegangenAm).Take(100).ToListAsync(ct);
        Bescheinigungen = await db.Arbeitgeberbescheinigungen.AsNoTracking().Include(b => b.Mitglied).Where(b => sichtbar.Contains(b.MitgliedId))
            .OrderByDescending(b => b.AusgestelltAm).Take(100).ToListAsync(ct);
        Mitglieder = DarfBearbeiten
            ? await db.Mitglieder.AsNoTracking()
                .Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Code == AbteilungCode.Einsatz && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
                .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
                .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString()))
                .ToListAsync(ct)
            : [];
    }
}
