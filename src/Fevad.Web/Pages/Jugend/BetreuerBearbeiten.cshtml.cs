using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Betreuer eintragen oder ändern und die Einsicht ins erweiterte Führungszeugnis vermerken (V47). Gespeichert wird nur, was
/// § 72a Abs. 5 SGB VIII erlaubt. Dazu die Bestätigung des Trägers für den Antrag auf das Zeugnis als PDF.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendBetreuerSehen)]
public class BetreuerBearbeitenModel(AppDbContext db, Datenbereich datenbereich, Betreuerregister register, Kassenberichte berichte, TimeProvider zeit) : PageModel
{
    /// <summary>Formular eines Betreuers.</summary>
    public sealed class Eingabe
    {
        /// <summary>Vorname.</summary>
        [Required(ErrorMessage = "Bitte den Vornamen angeben."), StringLength(100), Display(Name = "Vorname")] public string? Vorname { get; set; }
        /// <summary>Nachname.</summary>
        [Required(ErrorMessage = "Bitte den Nachnamen angeben."), StringLength(100), Display(Name = "Nachname")] public string? Nachname { get; set; }
        /// <summary>Angehöriger der Feuerwehr.</summary>
        [Display(Name = "Angehörige/r der Feuerwehr")] public bool Feuerwehrangehoeriger { get; set; }
        /// <summary>Abteilung.</summary>
        [Required(ErrorMessage = "Bitte die Abteilung wählen."), Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        /// <summary>Aufgabe.</summary>
        [Required(ErrorMessage = "Bitte die Aufgabe angeben."), StringLength(100), Display(Name = "Aufgabe")] public string? Aufgabe { get; set; } = "Betreuer/in";
        /// <summary>Beginn.</summary>
        [Required(ErrorMessage = "Bitte den Beginn angeben."), Display(Name = "tätig ab")] public DateOnly? Von { get; set; }
        /// <summary>Ende.</summary>
        [Display(Name = "tätig bis")] public DateOnly? Bis { get; set; }
        /// <summary>Zeugnis nötig.</summary>
        [Display(Name = "Erweitertes Führungszeugnis nötig")] public bool ZeugnisNoetig { get; set; } = true;
        /// <summary>Grund, falls nicht nötig.</summary>
        [StringLength(300), Display(Name = "Warum nicht nötig (Art, Intensität, Dauer des Kontakts)")] public string? ZeugnisNichtNoetigGrund { get; set; }
        /// <summary>Datum des Zeugnisses.</summary>
        [Display(Name = "Datum des Führungszeugnisses")] public DateOnly? ZeugnisVom { get; set; }
        /// <summary>Tag der Einsicht.</summary>
        [Display(Name = "eingesehen am")] public DateOnly? EingesehenAm { get; set; }
        /// <summary>Wer eingesehen hat.</summary>
        [StringLength(200), Display(Name = "eingesehen von")] public string? EingesehenVon { get; set; }
        /// <summary>Ergebnis.</summary>
        [Display(Name = "Ergebnis")] public bool? OhneEintrag { get; set; }
        /// <summary>Juleica bis.</summary>
        [Display(Name = "Juleica gültig bis")] public DateOnly? JuleicaBis { get; set; }
        /// <summary>Erste Hilfe am.</summary>
        [Display(Name = "Erste Hilfe zuletzt")] public DateOnly? ErsteHilfeAm { get; set; }
        /// <summary>Verhaltenskodex am.</summary>
        [Display(Name = "Verhaltenskodex unterschrieben am")] public DateOnly? VerhaltenskodexAm { get; set; }
    }

    /// <summary>Das Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Kennung beim Bearbeiten; leer beim Anlegen.</summary>
    public Guid? Id { get; private set; }

    /// <summary>Wählbare Jugend- und Kinderfeuerwehren.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Höchstalter des Zeugnisses bei der Einsicht in Monaten.</summary>
    public int HoechstalterMonate { get; private set; }

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="id">Kennung oder leer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is { } vorhanden)
        {
            if (await LadenAsync(vorhanden, ct) is not { } b)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Vorname = b.Vorname, Nachname = b.Nachname, Feuerwehrangehoeriger = b.Feuerwehrangehoeriger, AbteilungId = b.AbteilungId,
                Aufgabe = b.Aufgabe, Von = b.Von, Bis = b.Bis, ZeugnisNoetig = b.ZeugnisNoetig, ZeugnisNichtNoetigGrund = b.ZeugnisNichtNoetigGrund,
                ZeugnisVom = b.ZeugnisVom, EingesehenAm = b.EingesehenAm, EingesehenVon = b.EingesehenVon, OhneEintrag = b.OhneEintrag,
                JuleicaBis = b.JuleicaBis, ErsteHilfeAm = b.ErsteHilfeAm, VerhaltenskodexAm = b.VerhaltenskodexAm,
            };
        }
        else
        {
            Formular.Von = Anzeige.Heute(zeit);
        }

        Id = id;
        await AuswahlAsync(ct);
        return Page();
    }

    /// <summary>Speichert den Betreuer.</summary>
    /// <param name="id">Kennung oder leer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        Id = id;
        await AuswahlAsync(ct);
        var f = Formular;
        if (f.AbteilungId is { } abteilung && !Abteilungen.Any(a => a.Value == abteilung.ToString()))
        {
            return Forbid();
        }

        if (f.ZeugnisNoetig && f.EingesehenAm is not null)
        {
            if (f.ZeugnisVom is not { } vom)
            {
                ModelState.AddModelError("Formular.ZeugnisVom", "Bitte das Datum des Führungszeugnisses angeben.");
            }
            else if (vom > f.EingesehenAm || Fuehrungszeugnisregeln.ZuAlt(vom, f.EingesehenAm.Value, HoechstalterMonate))
            {
                ModelState.AddModelError("Formular.ZeugnisVom", $"Das Zeugnis darf bei der Einsicht höchstens {HoechstalterMonate} Monate alt sein.");
            }

            if (f.OhneEintrag is null)
            {
                ModelState.AddModelError("Formular.OhneEintrag", "Bitte das Ergebnis der Einsicht angeben.");
            }
        }

        if (!f.ZeugnisNoetig && string.IsNullOrWhiteSpace(f.ZeugnisNichtNoetigGrund))
        {
            ModelState.AddModelError("Formular.ZeugnisNichtNoetigGrund", "Bitte begründen, warum kein Führungszeugnis nötig ist.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Jugendbetreuer b;
        if (id is { } vorhanden)
        {
            if (await LadenAsync(vorhanden, ct) is not { } geladen)
            {
                return NotFound();
            }

            b = geladen;
        }
        else
        {
            b = new Jugendbetreuer { Vorname = "", Nachname = "", Aufgabe = "" };
            db.Jugendbetreuer.Add(b);
        }

        b.Vorname = f.Vorname!.Trim();
        b.Nachname = f.Nachname!.Trim();
        b.Feuerwehrangehoeriger = f.Feuerwehrangehoeriger;
        b.AbteilungId = f.AbteilungId!.Value;
        b.Aufgabe = f.Aufgabe!.Trim();
        b.Von = f.Von!.Value;
        b.Bis = f.Bis;
        b.ZeugnisNoetig = f.ZeugnisNoetig;
        b.ZeugnisNichtNoetigGrund = f.ZeugnisNoetig ? null : f.ZeugnisNichtNoetigGrund?.Trim();
        // Ohne Einsicht gibt es auch kein Ergebnis; ohne Pflicht wird nichts zum Zeugnis gespeichert.
        var einsicht = f.ZeugnisNoetig && f.EingesehenAm is not null;
        b.EingesehenAm = einsicht ? f.EingesehenAm : null;
        b.ZeugnisVom = einsicht ? f.ZeugnisVom : null;
        b.OhneEintrag = einsicht ? f.OhneEintrag : null;
        b.EingesehenVon = einsicht ? (string.IsNullOrWhiteSpace(f.EingesehenVon) ? User.Identity?.Name : f.EingesehenVon.Trim()) : null;
        b.JuleicaBis = f.JuleicaBis;
        b.ErsteHilfeAm = f.ErsteHilfeAm;
        b.VerhaltenskodexAm = f.VerhaltenskodexAm;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = b.OhneEintrag == false
            ? $"{b.Vorname} {b.Nachname} gespeichert. Wegen einer einschlägigen Eintragung darf die Person nicht eingesetzt werden."
            : $"{b.Vorname} {b.Nachname} gespeichert.";
        return RedirectToPage("Betreuer");
    }

    /// <summary>Löscht den Eintrag, z. B. wenn die Tätigkeit nicht aufgenommen wurde.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is not { } b)
        {
            return NotFound();
        }

        db.Jugendbetreuer.Remove(b);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eintrag gelöscht; der Löschlauf entfernt ihn endgültig (L53).";
        return RedirectToPage("Betreuer");
    }

    /// <summary>Bestätigung des Trägers für den Antrag auf das erweiterte Führungszeugnis.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetBescheinigungAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is not { } b)
        {
            return NotFound();
        }

        var organisation = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        var pdf = TraegerbescheinigungPdf.Erzeugen(new(await berichte.KopfAsync(ct), $"{b.Vorname} {b.Nachname}", organisation.GemeindeName,
            organisation.FeuerwehrName, b.Abteilung?.Bezeichnung ?? "Jugendfeuerwehr", b.Aufgabe, Anzeige.Datum(Anzeige.Heute(zeit))));
        return File(pdf, "application/pdf", $"bestaetigung-fuehrungszeugnis-{Anzeige.Heute(zeit):yyyy-MM-dd}.pdf");
    }

    /// <summary>Lädt einen Betreuer, wenn er in einer freigegebenen Abteilung ist.</summary>
    private async Task<Jugendbetreuer?> LadenAsync(Guid id, CancellationToken ct)
    {
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.JugendBetreuer);
        var b = await db.Jugendbetreuer.Include(x => x.Abteilung).FirstOrDefaultAsync(x => x.Id == id, ct);
        return b is not null && (freigabe.Alle || freigabe.Abteilungen.Contains(b.AbteilungId)) ? b : null;
    }

    private async Task AuswahlAsync(CancellationToken ct)
    {
        HoechstalterMonate = (await register.FristenAsync(Anzeige.Heute(zeit), ct)).HoechstalterMonate;
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.JugendBetreuer);
        var ids = freigabe.Abteilungen.ToList();
        Abteilungen = await db.Abteilungen.AsNoTracking()
            .Where(a => (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder) && (freigabe.Alle || ids.Contains(a.Id)))
            .OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
            .ToListAsync(ct);
    }
}
