using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Eigene Nachweise einreichen (V44): Erste-Hilfe-Kurs, Führerschein oder einen externen Lehrgang mit Scan oder Foto zur Prüfung durch die
/// Ausbildung. Gesundheitsbezogene Nachweise gehören nicht hierher. Die Datei wird mit der Prüfung gelöscht.
/// </summary>
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class NachweiseModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, Benachrichtigungsdienst benachrichtigungen,
    ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Einreichungen, neueste zuerst.</summary>
    public IReadOnlyList<Nachweiseinreichung> Einreichungen { get; private set; } = [];

    /// <summary>Auswahl: Nachweisarten und Qualifikationen, gruppiert.</summary>
    public IReadOnlyList<SelectListItem> Arten { get; private set; } = [];

    /// <summary>Formular.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Scan oder Foto.</summary>
    [BindProperty] public IFormFile? Datei { get; set; }

    /// <summary>Eingaben einer Einreichung.</summary>
    public sealed class Eingabe
    {
        /// <summary>„n:“ plus Nachweisart, „q:“ plus Qualifikation oder leer für eine freie Bezeichnung.</summary>
        [Display(Name = "Was weisen Sie nach?")] public string? Art { get; set; }
        /// <summary>Freie Bezeichnung.</summary>
        [StringLength(200), Display(Name = "Bezeichnung (wenn nichts in der Liste passt)")] public string? Bezeichnung { get; set; }
        /// <summary>Datum.</summary>
        [Required(ErrorMessage = "Bitte das Datum angeben."), Display(Name = "Datum des Kurses oder der Ausstellung")] public DateOnly? Datum { get; set; }
        /// <summary>Gültig bis.</summary>
        [Display(Name = "Gültig bis (falls angegeben)")] public DateOnly? GueltigBis { get; set; }
        /// <summary>Aussteller.</summary>
        [StringLength(100), Display(Name = "Ausgestellt von")] public string? Aussteller { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500), Display(Name = "Hinweis an die Ausbildung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt Formular und eigene Einreichungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        return Page();
    }

    /// <summary>Reicht einen Nachweis ein und benachrichtigt die Ausbildung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var einreichung = new Nachweiseinreichung
        {
            MitgliedId = mitgliedId, Datum = Formular.Datum ?? default, GueltigBis = Formular.GueltigBis,
            Bezeichnung = Leer(Formular.Bezeichnung), Aussteller = Leer(Formular.Aussteller), Bemerkung = Leer(Formular.Bemerkung),
        };
        var feld = $"{nameof(Formular)}.{nameof(Eingabe.Art)}";
        if (Formular.Art?.StartsWith("n:", StringComparison.Ordinal) == true && Guid.TryParse(Formular.Art[2..], out var nid)
            && await db.Nachweisarten.AnyAsync(n => n.Id == nid && n.Aktiv && !n.Gesundheitsbezogen, ct))
        {
            einreichung.NachweisartId = nid;
        }
        else if (Formular.Art?.StartsWith("q:", StringComparison.Ordinal) == true && Guid.TryParse(Formular.Art[2..], out var qid)
            && await db.Qualifikationen.AnyAsync(q => q.Id == qid && q.Aktiv && q.Art != QualifikationArt.Abzeichen, ct))
        {
            einreichung.QualifikationId = qid;
        }
        else if (!string.IsNullOrEmpty(Formular.Art))
        {
            ModelState.AddModelError(feld, "Bitte einen Eintrag aus der Liste wählen.");
        }
        else if (einreichung.Bezeichnung is null)
        {
            ModelState.AddModelError(feld, "Bitte wählen, was Sie nachweisen, oder eine Bezeichnung eintragen.");
        }

        var heute = Anzeige.Heute(zeit);
        if (Formular.Datum > heute)
        {
            ModelState.AddModelError($"{nameof(Formular)}.{nameof(Eingabe.Datum)}", "Das Datum liegt in der Zukunft.");
        }

        if (Datei is { Length: > 0 })
        {
            if (Hochladen.BildOderPdfPruefen(Datei, await parameter.ZahlAsync(ParameterSchluessel.DokumentMaxMegabyte, heute, 20, ct)) is { } fehler)
            {
                ModelState.AddModelError(nameof(Datei), fehler);
            }
            else
            {
                var (name, typ, inhalt) = await Hochladen.BildOderPdfLesenAsync(Datei, ct);
                if (inhalt is null)
                {
                    ModelState.AddModelError(nameof(Datei), $"„{name}“ ist keine gültige Bilddatei.");
                }

                (einreichung.Dateiname, einreichung.Inhaltstyp, einreichung.Inhalt) = (name, typ, inhalt);
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(mitgliedId, ct);
            return Page();
        }

        db.Nachweiseinreichungen.Add(einreichung);
        await db.SaveChangesAsync(ct);
        await benachrichtigungen.AnRechtAsync(Berechtigungen.AusbildungNachweise,
            new(Benachrichtigungsart.Aufgabe, "Nachweis zur Prüfung eingereicht", "/Personal/Einreichungen", einreichung.Id), benutzer.Id, ct);
        TempData["Meldung"] = "Ihr Nachweis ist eingereicht. Die Ausbildung prüft ihn und trägt ihn in Ihre Akte ein.";
        return RedirectToPage();
    }

    /// <summary>Zieht eine noch nicht geprüfte Einreichung samt Datei zurück.</summary>
    /// <param name="id">Die Einreichung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var e = await db.Nachweiseinreichungen.FirstOrDefaultAsync(x => x.Id == id && x.MitgliedId == mitgliedId && x.Status == EinreichungStatus.Eingereicht, ct);
        if (e is null)
        {
            return NotFound();
        }

        db.Nachweiseinreichungen.Remove(e);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Einreichung zurückgezogen.";
        return RedirectToPage();
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        Einreichungen = await db.Nachweiseinreichungen.AsNoTracking().Include(e => e.Nachweisart).Include(e => e.Qualifikation)
            .Where(e => e.MitgliedId == mitgliedId).OrderByDescending(e => e.ErstelltAm).ToListAsync(ct);
        var nachweise = new SelectListGroup { Name = "Wiederkehrende Nachweise" };
        var lehrgaenge = new SelectListGroup { Name = "Lehrgänge und Fahrerlaubnisse" };
        var arten = await db.Nachweisarten.AsNoTracking().Where(n => n.Aktiv && !n.Gesundheitsbezogen).OrderBy(n => n.Sortierung).ThenBy(n => n.Bezeichnung)
            .Select(n => new { n.Id, n.Bezeichnung }).ToListAsync(ct);
        var qualifikationen = await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv && q.Art != QualifikationArt.Abzeichen)
            .OrderBy(q => q.Art).ThenBy(q => q.Sortierung).ThenBy(q => q.Bezeichnung)
            .Select(q => new { q.Id, q.Bezeichnung }).ToListAsync(ct);
        Arten =
        [
            .. arten.Select(n => new SelectListItem(n.Bezeichnung, $"n:{n.Id}") { Group = nachweise }),
            .. qualifikationen.Select(q => new SelectListItem(q.Bezeichnung, $"q:{q.Id}") { Group = lehrgaenge }),
        ];
    }
}
