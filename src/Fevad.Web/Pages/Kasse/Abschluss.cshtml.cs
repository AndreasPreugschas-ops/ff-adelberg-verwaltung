using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Jahresabschluss einer Kasse (V05): Vermögensübersicht, Plan und Ist, Bestandsverzeichnis und Ausnahmebuchungen eines Jahres; Abschließen
/// sperrt das Jahr für normale Buchungen. Vorlage an den Bürgermeister und Entlastungsbeschluss der Hauptversammlung werden vermerkt, das
/// PDF trägt die Unterschriftsfelder.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class AbschlussModel(AppDbContext db, Kassenzugriff zugriff, Kassenabschluss abschluss, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Die Jahresrechnung.</summary>
    public Kassenabschluss.Jahresrechnung Rechnung { get; private set; } = null!;

    /// <summary>Gründe, warum das Jahr noch nicht abgeschlossen werden kann.</summary>
    public IReadOnlyList<string> Hindernisse { get; private set; } = [];

    /// <summary>Der Benutzer darf buchen und abschließen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Jahre mit Buchungen oder Abschluss zur Auswahl.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Beschlüsse für die Entlastung.</summary>
    public IReadOnlyList<SelectListItem> Beschluesse { get; private set; } = [];

    /// <summary>Das Haushaltsjahr; 0 wählt das Vorjahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Vermerke zu Vorlage und Entlastung.</summary>
    [BindProperty] public Eingabe Vermerk { get; set; } = new();

    /// <summary>Vermerke zu einem Abschluss.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag der Vorlage an den Bürgermeister.</summary>
        [Display(Name = "Dem Bürgermeister vorgelegt am")] public DateOnly? VorgelegtAm { get; set; }

        /// <summary>Entlastungsbeschluss.</summary>
        [Display(Name = "Entlastung durch Beschluss")] public Guid? EntlastungBeschlussId { get; set; }

        /// <summary>Freitext.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt den Abschluss.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        if (Rechnung.Abschluss is { } a)
        {
            Vermerk = new Eingabe { VorgelegtAm = a.VorgelegtAm, EntlastungBeschlussId = a.EntlastungBeschlussId, Bemerkung = a.Bemerkung };
        }

        return Page();
    }

    /// <summary>Lädt den Jahresabschluss als PDF.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        var organisation = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        var kopf = new AntragPdf.Kopfdaten(organisation.FeuerwehrName, organisation.FeuerwehrAnschrift, null, null, organisation.Logo, organisation.LogoInhaltstyp);
        var pdf = JahresabschlussPdf.Erzeugen(kopf, Rechnung, Anzeige.DatumZeit(zeit.GetUtcNow()),
            Rechnung.Abschluss is { } a ? Anzeige.Datum(Anzeige.LokalesDatum(a.AbgeschlossenAm)) : null);
        return File(pdf, "application/pdf", JahresabschlussPdf.Dateiname(Rechnung));
    }

    /// <summary>Schließt das Jahr ab.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAbschliessenAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        if (!DarfBuchen)
        {
            return Forbid();
        }

        try
        {
            await abschluss.AbschliessenAsync(Kasse, Jahr, benutzer.Id, Anzeige.Heute(zeit), ct);
            TempData["Meldung"] = $"Jahr {Jahr} abgeschlossen. Buchungen in das Jahr gehen nur noch als Ausnahmebuchung mit Begründung.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { kasse, Jahr });
    }

    /// <summary>Speichert Vorlage, Entlastung und Bemerkung.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostVermerkAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct) || Rechnung.Abschluss is null)
        {
            return NotFound();
        }

        if (!DarfBuchen)
        {
            return Forbid();
        }

        var a = await db.Jahresabschluesse.FirstAsync(j => j.Id == Rechnung.Abschluss.Id, ct);
        a.VorgelegtAm = Vermerk.VorgelegtAm;
        a.EntlastungBeschlussId = Beschluesse.Any(b => b.Value == Vermerk.EntlastungBeschlussId?.ToString()) ? Vermerk.EntlastungBeschlussId : null;
        a.Bemerkung = string.IsNullOrWhiteSpace(Vermerk.Bemerkung) ? null : Vermerk.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Vermerke gespeichert.";
        return RedirectToPage(new { kasse, Jahr });
    }

    private async Task<bool> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return false;
        }

        Kasse = kasse;
        var heute = Anzeige.Heute(zeit);
        if (Jahr == 0)
        {
            Jahr = heute.Year - 1;
        }

        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        Rechnung = await abschluss.RechnungAsync(kasse, Jahr, ct);
        Hindernisse = await abschluss.HindernisseAsync(Rechnung, heute, ct);
        Jahre = (await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId).Select(b => b.Jahr).Distinct().ToListAsync(ct))
            .Append(heute.Year - 1).Append(Jahr).Distinct().OrderDescending().ToList();
        Beschluesse = await Kassenauswahl.BeschluesseAsync(db, heute, Rechnung.Abschluss?.EntlastungBeschlussId, ct);
        return true;
    }
}
