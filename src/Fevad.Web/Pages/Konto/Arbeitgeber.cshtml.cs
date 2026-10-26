using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Bescheinigungen für den Arbeitgeber (V33): Das Mitglied stellt sich selbst eine Bescheinigung über Einsätze, Lehrgänge und Dienste eines
/// Zeitraums aus oder eine Vorab-Mitteilung über einen Lehrgang. Jede trägt einen Prüfcode; unterschrieben wird auf Papier vom Kommandanten.
/// </summary>
public class ArbeitgeberModel(
    AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, Arbeitgeberbescheinigungen bescheinigungen, IOptions<AnmeldungOptionen> anmeldung,
    TimeProvider zeit) : PageModel
{
    /// <summary>Eine kommende Lehrgangsteilnahme.</summary>
    /// <param name="Id">Kennung der Teilnahme.</param>
    /// <param name="Bezeichnung">Lehrgang.</param>
    /// <param name="Von">Erster Tag.</param>
    /// <param name="Bis">Letzter Tag.</param>
    public sealed record Lehrgang(Guid Id, string Bezeichnung, DateOnly Von, DateOnly? Bis);

    /// <summary>Eigene Bescheinigungen, neueste zuerst.</summary>
    public IReadOnlyList<Arbeitgeberbescheinigung> Ausgestellt { get; private set; } = [];

    /// <summary>Kommende Lehrgänge für die Vorab-Mitteilung.</summary>
    public IReadOnlyList<Lehrgang> Lehrgaenge { get; private set; } = [];

    /// <summary>Vorschau der Zeilen nach „Vorschau“.</summary>
    public IReadOnlyList<Bescheinigungszeile>? Vorschau { get; private set; }

    /// <summary>Formular der Bescheinigung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Eingaben einer Bescheinigung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Erster Tag.</summary>
        [Required(ErrorMessage = "Bitte den ersten Tag angeben."), Display(Name = "Von")] public DateOnly? Von { get; set; }
        /// <summary>Letzter Tag.</summary>
        [Required(ErrorMessage = "Bitte den letzten Tag angeben."), Display(Name = "Bis")] public DateOnly? Bis { get; set; }
        /// <summary>Arbeitgeber.</summary>
        [Required(ErrorMessage = "Bitte den Arbeitgeber angeben."), StringLength(200), Display(Name = "Arbeitgeber")] public string Arbeitgeber { get; set; } = "";
        /// <summary>Einsätze aufnehmen.</summary>
        [Display(Name = "Einsätze")] public bool Einsaetze { get; set; } = true;
        /// <summary>Lehrgänge aufnehmen.</summary>
        [Display(Name = "Lehrgänge")] public bool Lehrgaenge { get; set; } = true;
        /// <summary>Übungen und sonstige Dienste aufnehmen.</summary>
        [Display(Name = "Übungen und sonstige Dienste")] public bool Dienste { get; set; }
    }

    /// <summary>Zeigt das Formular, vorbelegt mit dem Vormonat und dem hinterlegten Arbeitgeber.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        var heute = Anzeige.Heute(zeit);
        var erster = new DateOnly(heute.Year, heute.Month, 1);
        Formular.Von = erster.AddMonths(-1);
        Formular.Bis = erster.AddDays(-1);
        Formular.Arbeitgeber = await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Arbeitgeber).FirstAsync(ct) ?? "";
        return Page();
    }

    /// <summary>Zeigt, was bescheinigt würde, ohne auszustellen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostVorschauAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        if (Pruefen())
        {
            Vorschau = await bescheinigungen.ZeilenAsync(mitgliedId, Formular.Von!.Value, Formular.Bis!.Value, Auswahl, null, ct);
        }

        return Page();
    }

    /// <summary>Stellt die Bescheinigung aus und liefert das PDF.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAusstellenAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        if (!Pruefen())
        {
            return Page();
        }

        try
        {
            var b = await bescheinigungen.AusstellenAsync(mitgliedId, Formular.Von!.Value, Formular.Bis!.Value, Auswahl, Formular.Arbeitgeber, null, benutzer.Id, ct);
            return await PdfAsync(b, ct);
        }
        catch (InvalidOperationException fehler)
        {
            ModelState.AddModelError("", fehler.Message);
            return Page();
        }
    }

    /// <summary>Stellt die Vorab-Mitteilung über einen eigenen Lehrgang aus.</summary>
    /// <param name="teilnahmeId">Kennung der Lehrgangsteilnahme.</param>
    /// <param name="arbeitgeber">Arbeitgeber.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnPostMitteilungAsync(Guid teilnahmeId, string? arbeitgeber, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var teilnahme = await db.Lehrgangsteilnahmen.AsNoTracking().Include(l => l.Qualifikation)
            .FirstOrDefaultAsync(l => l.Id == teilnahmeId && l.MitgliedId == mitgliedId && l.Von != null, ct);
        if (teilnahme is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(arbeitgeber))
        {
            TempData["Fehler"] = "Bitte den Arbeitgeber angeben.";
            return RedirectToPage();
        }

        var b = await bescheinigungen.MitteilungAsync(teilnahme, arbeitgeber.Trim()[..Math.Min(arbeitgeber.Trim().Length, 200)], benutzer.Id, ct);
        return await PdfAsync(b, ct, $"{teilnahme.Qualifikation?.Bezeichnung}{(teilnahme.Lehrgangsort is null ? "" : ", " + teilnahme.Lehrgangsort)}");
    }

    /// <summary>Liefert eine eigene Bescheinigung erneut als PDF.</summary>
    /// <param name="id">Kennung der Bescheinigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var b = await db.Arbeitgeberbescheinigungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.MitgliedId == mitgliedId && !x.Widerrufen, ct);
        return b is null ? NotFound() : await PdfAsync(b, ct);
    }

    private Arbeitgeberbescheinigungen.Auswahl Auswahl => new(Formular.Einsaetze, Formular.Lehrgaenge, Formular.Dienste);

    private bool Pruefen()
    {
        if (Formular.Von is { } von && Formular.Bis is { } bis && (bis < von || bis.DayNumber - von.DayNumber > 366))
        {
            ModelState.AddModelError("Formular.Bis", "Der Zeitraum muss zwischen einem Tag und einem Jahr liegen.");
        }

        if (!Formular.Einsaetze && !Formular.Lehrgaenge && !Formular.Dienste)
        {
            ModelState.AddModelError("", "Bitte mindestens Einsätze, Lehrgänge oder Dienste wählen.");
        }

        return ModelState.IsValid;
    }

    private async Task<IActionResult> PdfAsync(Arbeitgeberbescheinigung b, CancellationToken ct, string? lehrgang = null) =>
        File(await PdfErzeugenAsync(db, anmeldung.Value, b, lehrgang, ct), "application/pdf", Arbeitgeberbescheinigungen.Dateiname(b));

    /// <summary>Erzeugt das PDF einer Bescheinigung mit Kopf der Feuerwehr und Prüfadresse.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="optionen">Anmeldeoptionen mit der öffentlichen Adresse.</param>
    /// <param name="b">Die Bescheinigung.</param>
    /// <param name="lehrgang">Bei einer Lehrgangsmitteilung Bezeichnung und Ort; sonst null.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public static async Task<byte[]> PdfErzeugenAsync(AppDbContext db, AnmeldungOptionen optionen, Arbeitgeberbescheinigung b, string? lehrgang, CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        var m = await db.Mitglieder.AsNoTracking().FirstAsync(x => x.Id == b.MitgliedId, ct);
        if (b.Art == BescheinigungArt.Lehrgangsmitteilung && lehrgang is null)
        {
            lehrgang = "Lehrgang laut Anmeldung";
        }

        var daten = new Arbeitgeberbescheinigungen.PdfDaten(new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp),
            o.FeuerwehrName, o.GemeindeName, $"{m.Vorname} {m.Nachname}", b, Pruefadresse(optionen), lehrgang);
        return Arbeitgeberbescheinigungen.Pdf(daten);
    }

    /// <summary>Adresse der öffentlichen Prüfseite oder null, wenn keine öffentliche Adresse eingestellt ist.</summary>
    /// <param name="optionen">Anmeldeoptionen mit der öffentlichen Adresse.</param>
    /// <returns>Die Adresse.</returns>
    public static string? Pruefadresse(AnmeldungOptionen optionen) =>
        string.IsNullOrWhiteSpace(optionen.OeffentlicheAdresse) ? null : optionen.OeffentlicheAdresse.TrimEnd('/') + "/Pruefen";

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        Ausgestellt = await db.Arbeitgeberbescheinigungen.AsNoTracking().Where(b => b.MitgliedId == mitgliedId)
            .OrderByDescending(b => b.AusgestelltAm).Take(50).ToListAsync(ct);
        var heute = Anzeige.Heute(zeit);
        Lehrgaenge = await db.Lehrgangsteilnahmen.AsNoTracking()
            .Where(l => l.MitgliedId == mitgliedId && l.Von != null && l.Von >= heute)
            .OrderBy(l => l.Von)
            .Select(l => new Lehrgang(l.Id, l.Qualifikation!.Bezeichnung, l.Von!.Value, l.Bis))
            .ToListAsync(ct);
    }
}
