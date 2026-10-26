using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Kleiderkammer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Sammelpflege (V63): Einzelstücke scannen oder die Einsatzkleidung eines Einsatzes laden, dann einmal Wäsche, Imprägnierung,
/// Reparatur oder Kontamination für alle eintragen. Jedes Stück bekommt einen Eintrag im Pflege-Lebenslauf.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class PflegeModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Mehr Stücke nimmt ein Durchgang nicht an.</summary>
    public const int HoechstZahl = 300;

    /// <summary>Art, Datum, Durchführung und die Liste der Stücke.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Einsätze der letzten Wochen zum Laden der Einsatzkleidung.</summary>
    public IReadOnlyList<SelectListItem> Einsaetze { get; private set; } = [];

    /// <summary>Pflegearten für die Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Arten { get; } = Enum.GetValues<Pflegeart>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Gefundenes Stück für die Liste.</summary>
    /// <param name="Id">Kennung des Geräts.</param>
    /// <param name="Nummer">Inventarnummer.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Waeschen">Bisherige Wäschen.</param>
    /// <param name="Grenze">Höchstzahl der Wäschen der Geräteart.</param>
    public sealed record Treffer(Guid Id, string Nummer, string Bezeichnung, int Waeschen, int? Grenze);

    /// <summary>Formular der Sammelpflege.</summary>
    public sealed class Eingabe
    {
        /// <summary>Art der Maßnahme für alle Stücke.</summary>
        [Display(Name = "Maßnahme")] public Pflegeart Art { get; set; } = Pflegeart.Waesche;

        /// <summary>Tag der Maßnahme, nicht in der Zukunft.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }

        /// <summary>Einsatz, nach dem gepflegt wird.</summary>
        [Display(Name = "Nach Einsatz")] public Guid? EinsatzId { get; set; }

        /// <summary>Firma oder Stelle, keine Namen.</summary>
        [StringLength(100), Display(Name = "Durchgeführt von (Firma oder Stelle)")] public string? Durchfuehrung { get; set; }

        /// <summary>Bemerkung für alle Einträge.</summary>
        [StringLength(200), Display(Name = "Bemerkung für alle")] public string? Bemerkung { get; set; }

        /// <summary>Gepflegte Stücke.</summary>
        public List<Guid> GeraetIds { get; set; } = [];
    }

    /// <summary>Zeigt die leere Liste.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Formular.Datum = Anzeige.Heute(zeit);
        await LadenAsync(ct);
    }

    /// <summary>Gescannte oder eingetippte Nummer nachschlagen; liefert JSON für die Liste.</summary>
    public async Task<IActionResult> OnGetGeraetAsync(string? nummer, CancellationToken ct)
    {
        var inventarnummer = Geraetecode.NummerAusScan(nummer);
        var treffer = await TrefferAsync(db.Geraete.Where(g => g.Inventarnummer == inventarnummer && g.AusgesondertAm == null), ct);
        return treffer.Count == 0 ? NotFound(new { meldung = $"Kein Gerät mit der Nummer „{inventarnummer}“." }) : new JsonResult(treffer[0]);
    }

    /// <summary>
    /// Einsatzkleidung (Einzelstücke aus der Kleiderkammer) aller Teilnehmenden eines Einsatzes, deren Kleidung in die Wäsche muss.
    /// Liefert nur die Stücke, nicht die Personen oder den Grund.
    /// </summary>
    public async Task<IActionResult> OnGetEinsatzAsync(Guid einsatzId, CancellationToken ct)
    {
        var mitglieder = db.EinsatzTeilnahmen
            .Where(t => t.EinsatzId == einsatzId && (t.PsaWaesche || t.Hautkontakt || t.Exposition == ExpositionStatus.Exponiert))
            .Select(t => t.MitgliedId);
        var geraete = db.Ausstattungen
            .Where(a => mitglieder.Contains(a.MitgliedId) && a.ZurueckAm == null && a.GeraetId != null && WaescheModel.Waeschekategorien.Contains(a.Artikel!.Kategorie))
            .Select(a => a.GeraetId!.Value);
        return new JsonResult(await TrefferAsync(db.Geraete.Where(g => geraete.Contains(g.Id) && g.AusgesondertAm == null), ct));
    }

    /// <summary>Legt je Stück einen Pflegeeintrag an.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        if (Formular.Datum > heute)
        {
            ModelState.AddModelError("Formular.Datum", "Das Datum liegt in der Zukunft.");
        }

        var ids = Formular.GeraetIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Kein Stück in der Liste.");
        }
        else if (ids.Count > HoechstZahl)
        {
            ModelState.AddModelError(string.Empty, $"Höchstens {HoechstZahl} Stücke je Durchgang.");
        }

        var vorhanden = await db.Geraete.Where(g => ids.Contains(g.Id) && g.AusgesondertAm == null).Select(g => g.Id).ToListAsync(ct);
        if (vorhanden.Count != ids.Count)
        {
            ModelState.AddModelError(string.Empty, "Ein Stück der Liste gibt es nicht mehr oder es ist ausgesondert.");
        }

        if (Formular.EinsatzId is { } einsatzId && !await db.Einsaetze.AnyAsync(e => e.Id == einsatzId, ct))
        {
            ModelState.AddModelError("Formular.EinsatzId", "Einsatz nicht gefunden.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var durchfuehrung = string.IsNullOrWhiteSpace(Formular.Durchfuehrung) ? null : Formular.Durchfuehrung.Trim();
        var bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();
        db.Pflegeeintraege.AddRange(ids.Select(id => new Pflegeeintrag
        {
            GeraetId = id,
            Datum = Formular.Datum!.Value,
            Art = Formular.Art,
            EinsatzId = Formular.EinsatzId,
            Durchfuehrung = durchfuehrung,
            Bemerkung = bemerkung,
        }));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Text(Formular.Art)} für {ids.Count} Stück(e) eingetragen.";
        return RedirectToPage();
    }

    private async Task<List<Treffer>> TrefferAsync(IQueryable<Geraet> abfrage, CancellationToken ct) =>
        await abfrage.OrderBy(g => g.Inventarnummer).Take(HoechstZahl)
            .Select(g => new Treffer(g.Id, g.Inventarnummer, g.Bezeichnung + (g.Groesse != null ? " " + g.Groesse : ""),
                db.Pflegeeintraege.Count(p => p.GeraetId == g.Id && p.Art == Pflegeart.Waesche), g.Geraeteart!.HoechstWaeschen))
            .ToListAsync(ct);

    private async Task LadenAsync(CancellationToken ct)
    {
        var seit = zeit.GetUtcNow().AddDays(-60);
        Einsaetze = (await db.Einsaetze.AsNoTracking().Where(e => e.Alarmiert >= seit).OrderByDescending(e => e.Alarmiert)
                .Select(e => new { e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort }).ToListAsync(ct))
            .Select(e => new SelectListItem($"{e.Einsatznummer} vom {Anzeige.Datum(Anzeige.LokalesDatum(e.Alarmiert))} {e.Stichwort}", e.Id.ToString()))
            .ToList();
    }
}
