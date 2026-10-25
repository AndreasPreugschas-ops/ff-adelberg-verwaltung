using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Kasse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Auslagenerstattung (V07): Wer für die Feuerwehr Geld vorgestreckt hat, reicht hier den Beleg ein. Der Kassenwart prüft, zahlt
/// und bucht. Das Mitglied sieht seine eigenen Erstattungen immer, mit Stand. Eine IBAN lässt sich angeben; sie landet bei den
/// Bankverbindungen der Kasse und wird hier nur maskiert gezeigt.
/// </summary>
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class AuslagenModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Erstattungen, neueste zuerst.</summary>
    public IReadOnlyList<Zahlungsanordnung> Erstattungen { get; private set; } = [];

    /// <summary>Kassen, bei denen eingereicht werden kann.</summary>
    public IReadOnlyList<SelectListItem> Kassen { get; private set; } = [];

    /// <summary>Maskierte IBAN der hinterlegten Bankverbindung; null ohne Bankverbindung.</summary>
    public string? IbanMaskiert { get; private set; }

    /// <summary>Formular der Erstattung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Hochgeladene Belege.</summary>
    [BindProperty] public List<IFormFile> Dateien { get; set; } = [];

    /// <summary>Eingaben einer Erstattung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kasse, die erstatten soll.</summary>
        [Required(ErrorMessage = "Bitte die Kasse wählen."), Display(Name = "Kasse")] public Guid? KasseId { get; set; }

        /// <summary>Betrag in Euro.</summary>
        [Required(ErrorMessage = "Bitte den Betrag angeben."), Range(0.01, 100_000, ErrorMessage = "Der Betrag muss größer als 0 sein."), Display(Name = "Betrag (Euro)")]
        public decimal? Betrag { get; set; }

        /// <summary>Was gekauft wurde und wofür.</summary>
        [Required(ErrorMessage = "Bitte angeben, was du wofür gekauft hast."), StringLength(500), Display(Name = "Was und wofür")]
        public string Zweck { get; set; } = "";

        /// <summary>Neue IBAN, falls noch keine hinterlegt ist oder sie sich geändert hat.</summary>
        [StringLength(40), Display(Name = "IBAN für die Überweisung")] public string? Iban { get; set; }

        /// <summary>Kontoinhaber, falls nicht das Mitglied selbst.</summary>
        [StringLength(200), Display(Name = "Kontoinhaber, falls nicht du")] public string? Kontoinhaber { get; set; }
    }

    /// <summary>Zeigt das Formular und die eigenen Erstattungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        Formular.KasseId = Kassen.Select(k => Guid.Parse(k.Value)).FirstOrDefault();
        return Page();
    }

    /// <summary>Reicht eine Erstattung mit Beleg ein; eine angegebene IBAN ersetzt die hinterlegte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        if (Formular.KasseId is { } k && !Kassen.Any(x => x.Value == k.ToString()))
        {
            ModelState.AddModelError("Formular.KasseId", "Diese Kasse steht nicht zur Wahl.");
        }

        string? iban = null;
        if (!string.IsNullOrWhiteSpace(Formular.Iban) && (iban = Domain.Entschaedigung.Iban.Normalisieren(Formular.Iban)) is null)
        {
            ModelState.AddModelError("Formular.Iban", "Die IBAN ist ungültig; bitte prüfen.");
        }

        var heute = Anzeige.Heute(zeit);
        var belege = await Kassenauswahl.BelegeLesenAsync(Dateien, parameter, heute, ModelState, nameof(Dateien), ct);
        if (ModelState.IsValid && belege.Count == 0)
        {
            ModelState.AddModelError(nameof(Dateien), "Bitte den Beleg hochladen (Foto oder PDF des Kassenzettels).");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var mitglied = await db.Mitglieder.AsNoTracking().FirstAsync(m => m.Id == mitgliedId, ct);
        db.Zahlungsanordnungen.Add(new Zahlungsanordnung
        {
            KasseId = Formular.KasseId!.Value, Art = AnordnungArt.Auslagenerstattung, MitgliedId = mitgliedId, Betrag = Formular.Betrag!.Value,
            Empfaenger = mitglied.Anzeigename, Zweck = Formular.Zweck.Trim(), Status = AnordnungStatus.Vorbereitet, Belege = belege,
        });
        if (iban is not null)
        {
            var kontoinhaber = string.IsNullOrWhiteSpace(Formular.Kontoinhaber) ? null : Formular.Kontoinhaber.Trim();
            if (await db.Bankverbindungen.FirstOrDefaultAsync(b => b.MitgliedId == mitgliedId, ct) is { } vorhanden)
            {
                (vorhanden.Iban, vorhanden.Kontoinhaber, vorhanden.ErfasstAm) = (iban, kontoinhaber, heute);
            }
            else
            {
                db.Bankverbindungen.Add(new Bankverbindung { MitgliedId = mitgliedId, Iban = iban, Kontoinhaber = kontoinhaber, ErfasstAm = heute });
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eingereicht. Der Kassenwart prüft den Beleg und überweist; den Stand siehst du hier.";
        return RedirectToPage();
    }

    /// <summary>Zieht eine noch nicht bearbeitete Erstattung zurück.</summary>
    /// <param name="id">Kennung der Erstattung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var a = await db.Zahlungsanordnungen.FirstOrDefaultAsync(
            x => x.Id == id && x.MitgliedId == mitgliedId && x.Art == AnordnungArt.Auslagenerstattung && x.Status == AnordnungStatus.Vorbereitet, ct);
        if (a is null)
        {
            return NotFound();
        }

        a.Status = AnordnungStatus.Abgelehnt;
        a.Bemerkung = "vom Mitglied zurückgezogen";
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Erstattung zurückgezogen.";
        return RedirectToPage();
    }

    /// <summary>Liefert einen Beleg einer eigenen Erstattung aus.</summary>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetBelegAsync(Guid belegId, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        // Nach der Zahlung hängt der Beleg an der Buchung; der Bezug zur Erstattung bleibt über die Buchung erhalten.
        var beleg = await db.Kassenbelege.AsNoTracking()
            .Where(b => b.Id == belegId && db.Zahlungsanordnungen.Any(a => a.MitgliedId == mitgliedId && a.Art == AnordnungArt.Auslagenerstattung
                && (a.Id == b.ZahlungsanordnungId || (a.BuchungId != null && a.BuchungId == b.BuchungId))))
            .FirstOrDefaultAsync(ct);
        return beleg is null ? NotFound() : File(beleg.Inhalt, beleg.Inhaltstyp, beleg.Dateiname);
    }

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        Erstattungen = await db.Zahlungsanordnungen.AsNoTracking().Include(a => a.Kasse).Include(a => a.Belege)
            .Where(a => a.MitgliedId == mitgliedId && a.Art == AnordnungArt.Auslagenerstattung)
            .OrderByDescending(a => a.ErstelltAm).Take(100).ToListAsync(ct);
        Kassen = await db.Kassen.AsNoTracking().Where(k => k.Aktiv).OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Name, k.Id.ToString())).ToListAsync(ct);
        var iban = await db.Bankverbindungen.AsNoTracking().Where(b => b.MitgliedId == mitgliedId).Select(b => b.Iban).FirstOrDefaultAsync(ct);
        IbanMaskiert = iban is null ? null : Domain.Entschaedigung.Iban.Maskieren(iban);
    }
}
