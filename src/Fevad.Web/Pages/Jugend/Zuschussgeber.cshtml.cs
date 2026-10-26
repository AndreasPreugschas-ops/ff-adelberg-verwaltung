using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Zuschussgeber der Jugend (V48) mit Sätzen, Altersgrenzen und Fristen. Die Werte sind unbekannt und bleiben leer, bis Jugendwart oder
/// Kassenwart sie bei der Kreisjugendpflege oder beim Kreisjugendring erfragt haben.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendVeranstaltungenSehen)]
public class ZuschussgeberModel(AppDbContext db) : PageModel
{
    /// <summary>Formular eines Zuschussgebers.</summary>
    public sealed class Eingabe
    {
        /// <summary>Name.</summary>
        [Required(ErrorMessage = "Bitte den Namen angeben."), StringLength(200), Display(Name = "Name")] public string? Name { get; set; }
        /// <summary>Satz je Teilnehmer und Tag.</summary>
        [Range(0, 1000), Display(Name = "Zuschuss je Teilnehmer und Tag (€)")] public decimal? SatzJeTeilnehmertag { get; set; }
        /// <summary>Mindestalter.</summary>
        [Range(0, 99), Display(Name = "Teilnehmer ab (Jahre)")] public int? MindestalterJahre { get; set; }
        /// <summary>Höchstalter.</summary>
        [Range(0, 99), Display(Name = "Teilnehmer bis einschließlich (Jahre)")] public int? HoechstalterJahre { get; set; }
        /// <summary>Mindestdauer.</summary>
        [Range(1, 365), Display(Name = "gefördert ab (Tage Dauer)")] public int? MindestTage { get; set; }
        /// <summary>Mindestteilnehmer.</summary>
        [Range(1, 1000), Display(Name = "mindestens (Teilnehmer)")] public int? MindestTeilnehmer { get; set; }
        /// <summary>Betreuer zählen mit.</summary>
        [Display(Name = "Betreuer zählen als Teilnehmer")] public bool BetreuerZaehlen { get; set; }
        /// <summary>Antragsfrist.</summary>
        [Range(0, 730), Display(Name = "Antrag spätestens (Tage nach dem letzten Tag)")] public int? AntragsfristTage { get; set; }
        /// <summary>Aufbewahrung.</summary>
        [Range(0, 240), Display(Name = "Teilnehmerliste aufbewahren (Monate)")] public int? AufbewahrungMonate { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung (Ansprechpartner, Richtlinie)")] public string? Bemerkung { get; set; }
        /// <summary>Wählbar.</summary>
        [Display(Name = "wählbar")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Das Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Kennung beim Bearbeiten.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    /// <summary>Alle Zuschussgeber.</summary>
    public IReadOnlyList<Fevad.Domain.Dienstbetrieb.Zuschussgeber> Liste { get; private set; } = [];

    /// <summary>Zeigt Liste und Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id is { } id)
        {
            if (await db.Zuschussgeber.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct) is not { } z)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Name = z.Name, SatzJeTeilnehmertag = z.SatzJeTeilnehmertag, MindestalterJahre = z.MindestalterJahre, HoechstalterJahre = z.HoechstalterJahre,
                MindestTage = z.MindestTage, MindestTeilnehmer = z.MindestTeilnehmer, BetreuerZaehlen = z.BetreuerZaehlen, AntragsfristTage = z.AntragsfristTage,
                AufbewahrungMonate = z.AufbewahrungMonate, Bemerkung = z.Bemerkung, Aktiv = z.Aktiv,
            };
        }

        Liste = await db.Zuschussgeber.AsNoTracking().OrderBy(z => z.Name).ToListAsync(ct);
        return Page();
    }

    /// <summary>Speichert einen Zuschussgeber.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Liste = await db.Zuschussgeber.AsNoTracking().OrderBy(z => z.Name).ToListAsync(ct);
            return Page();
        }

        Fevad.Domain.Dienstbetrieb.Zuschussgeber z;
        if (Id is { } id)
        {
            if (await db.Zuschussgeber.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } geladen)
            {
                return NotFound();
            }

            z = geladen;
        }
        else
        {
            z = new Fevad.Domain.Dienstbetrieb.Zuschussgeber { Name = "" };
            db.Zuschussgeber.Add(z);
        }

        var f = Formular;
        z.Name = f.Name!.Trim();
        z.SatzJeTeilnehmertag = f.SatzJeTeilnehmertag;
        z.MindestalterJahre = f.MindestalterJahre;
        z.HoechstalterJahre = f.HoechstalterJahre;
        z.MindestTage = f.MindestTage;
        z.MindestTeilnehmer = f.MindestTeilnehmer;
        z.BetreuerZaehlen = f.BetreuerZaehlen;
        z.AntragsfristTage = f.AntragsfristTage;
        z.AufbewahrungMonate = f.AufbewahrungMonate;
        z.Bemerkung = string.IsNullOrWhiteSpace(f.Bemerkung) ? null : f.Bemerkung.Trim();
        z.Aktiv = f.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{z.Name} gespeichert.";
        return RedirectToPage(new { id = (Guid?)null });
    }
}
