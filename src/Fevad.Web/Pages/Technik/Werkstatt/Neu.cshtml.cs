using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Vorbelegung = Fevad.Infrastructure.Persistenz.Seed.Stammdaten;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Werkstatt;

/// <summary>
/// Geräte an eine fremde Werkstatt abgeben (V61): Geräte scannen oder abhaken, Lieferschein drucken.
/// Vorgeschlagen sind die Geräte, die nach einem Einsatz gesperrt sind.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class NeuModel(AppDbContext db, Werkstattauftraege auftraege, Pruefstaende pruefstaende, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Abgebbare Geräte nach Geräteart.</summary>
    public IReadOnlyList<IGrouping<string, Kandidat>> Gruppen { get; private set; } = [];

    /// <summary>Eingaben.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Ein Gerät, das abgegeben werden kann.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Standort">Fahrzeug oder Lagerort.</param>
    /// <param name="Hinweis">„nach Einsatz“ oder „Prüfung fällig“.</param>
    /// <param name="NachEinsatz">Nach einem Einsatz gesperrt; vorausgewählt.</param>
    public sealed record Kandidat(Guid Id, string Inventarnummer, string Bezeichnung, string? Standort, string? Hinweis, bool NachEinsatz);

    /// <summary>Formular der Abgabe.</summary>
    public sealed class Eingabe
    {
        /// <summary>Rücklaufweg.</summary>
        public Ruecklauf Werkstatt { get; set; } = Ruecklauf.Atemschutzwerkstatt;

        /// <summary>Name der Werkstatt.</summary>
        [Required(ErrorMessage = "Werkstatt fehlt."), StringLength(200), Display(Name = "Werkstatt")]
        public string? Werkstattname { get; set; }

        /// <summary>Tag der Abgabe.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Abgegeben am")]
        public DateOnly? AbgegebenAm { get; set; }

        /// <summary>Bemerkung, steht auf dem Lieferschein.</summary>
        [StringLength(1000), Display(Name = "Bemerkung für die Werkstatt")]
        public string? Bemerkung { get; set; }

        /// <summary>Ausgewählte Geräte.</summary>
        public List<Guid> Geraete { get; set; } = [];
    }

    /// <summary>Zeigt die Auswahl; nach dem Einsatz gesperrte Geräte sind vorausgewählt.</summary>
    /// <param name="werkstatt">Rücklaufweg (Atemschutz- oder Schlauchwerkstatt).</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task<IActionResult> OnGetAsync(Ruecklauf werkstatt, CancellationToken ct)
    {
        if (werkstatt == Ruecklauf.Geraetewart)
        {
            return RedirectToPage("Index");
        }

        Formular.Werkstatt = werkstatt;
        Formular.Werkstattname = werkstatt == Ruecklauf.Schlauchwerkstatt ? Vorbelegung.SchlauchwerkstattName : Vorbelegung.AtemschutzwerkstattName;
        Formular.AbgegebenAm = Anzeige.Heute(zeit);
        await LadenAsync(ct);
        Formular.Geraete = Gruppen.SelectMany(g => g).Where(k => k.NachEinsatz).Select(k => k.Id).ToList();
        return Page();
    }

    /// <summary>Legt den Auftrag an und führt zum Lieferschein.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Formular.AbgegebenAm > Anzeige.Heute(zeit))
        {
            ModelState.AddModelError("Formular.AbgegebenAm", "Das Datum liegt in der Zukunft.");
        }

        if (Formular.Werkstatt == Ruecklauf.Geraetewart)
        {
            ModelState.AddModelError("Formular.Werkstatt", "Bitte eine Werkstatt wählen.");
        }

        var erlaubt = await db.Geraete.Where(g => Formular.Geraete.Contains(g.Id) && g.Geraeteart!.Ruecklauf == Formular.Werkstatt).CountAsync(ct);
        if (erlaubt != Formular.Geraete.Distinct().Count())
        {
            ModelState.AddModelError("Formular.Geraete", "Ein Gerät gehört nicht zu dieser Werkstatt.");
        }

        var auftrag = new Werkstattauftrag
        {
            Nummer = "",
            Werkstatt = Formular.Werkstatt,
            Werkstattname = Formular.Werkstattname?.Trim() ?? "",
            AbgegebenAm = Formular.AbgegebenAm ?? Anzeige.Heute(zeit),
            AbgegebenVonId = (await benutzer.GetUserAsync(User))?.MitgliedId,
            Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
        };
        if (ModelState.IsValid && await auftraege.AnlegenAsync(auftrag, Formular.Geraete, ct) is { } fehler)
        {
            ModelState.AddModelError("Formular.Geraete", fehler);
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{auftrag.Nummer} angelegt: {auftrag.Positionen.Count} Geräte in der Werkstatt. Lieferschein ausdrucken und mitgeben.";
        return RedirectToPage("Details", new { id = auftrag.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var geraete = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && g.Geraeteart!.Ruecklauf == Formular.Werkstatt
                && !db.Werkstattpositionen.Any(p => p.GeraetId == g.Id && p.Werkstattauftrag!.ZurueckAm == null))
            .Select(g => new
            {
                g.Id,
                g.Inventarnummer,
                g.Bezeichnung,
                Art = g.Geraeteart!.Bezeichnung,
                g.Geraeteart.Sortierung,
                Standort = g.Fahrzeug != null ? g.Fahrzeug.Funkrufname : g.Lagerort,
                NachEinsatz = db.Maengel.Any(m => m.GeraetId == g.Id && m.EinsatzId != null && m.SperreBisPruefung && m.Status != MangelStatus.Behoben),
            })
            .ToListAsync(ct);
        var faellig = (await pruefstaende.GeraeteAsync(heute, ct: ct)).Where(s => s.Ampel != Fristampel.Gruen).Select(s => s.Id).ToHashSet();
        Gruppen = geraete
            .OrderBy(g => g.Sortierung).ThenBy(g => g.Art, StringComparer.CurrentCulture).ThenBy(g => g.Inventarnummer, StringComparer.Ordinal)
            .Select(g => (g.Art, Kandidat: new Kandidat(g.Id, g.Inventarnummer, g.Bezeichnung, g.Standort,
                g.NachEinsatz ? "nach Einsatz" : faellig.Contains(g.Id) ? "Prüfung fällig" : null, g.NachEinsatz)))
            .GroupBy(x => x.Art, x => x.Kandidat)
            .ToList();
    }
}
