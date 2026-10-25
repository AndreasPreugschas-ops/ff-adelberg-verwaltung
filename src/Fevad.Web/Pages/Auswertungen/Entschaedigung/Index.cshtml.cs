using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>Liste der Entschädigungsabrechnungen und Anlegen einer neuen für einen Zeitraum.</summary>
public class IndexModel(AppDbContext db, Abrechnungen abrechnungen, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Abrechnung in der Liste.</summary>
    /// <param name="Id">Kennung der Abrechnung.</param>
    /// <param name="Bezeichnung">Bezeichnung, z. B. „1. Halbjahr 2026“.</param>
    /// <param name="Von">Erster Tag des Zeitraums.</param>
    /// <param name="Bis">Letzter Tag des Zeitraums.</param>
    /// <param name="Status">Entwurf oder abgeschlossen.</param>
    /// <param name="Personen">Anzahl der Empfänger.</param>
    /// <param name="Summe">Gesamtbetrag in Euro.</param>
    /// <param name="AbgeschlossenAm">Zeitpunkt des Abschlusses (UTC).</param>
    public sealed record Zeile(Guid Id, string Bezeichnung, DateOnly Von, DateOnly Bis, AbrechnungStatus Status, int Personen, decimal Summe, DateTimeOffset? AbgeschlossenAm);

    /// <summary>Die Abrechnungen, neueste zuerst.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Der Benutzer darf abrechnen.</summary>
    public bool DarfAbrechnen { get; private set; }

    /// <summary>Formulardaten einer neuen Abrechnung.</summary>
    [BindProperty]
    public NeuFormular Neu { get; set; } = new();

    /// <summary>Eingabefelder einer neuen Abrechnung.</summary>
    public sealed class NeuFormular
    {
        /// <summary>Bezeichnung der Abrechnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Erster Tag des Zeitraums.</summary>
        [Required(ErrorMessage = "Beginn fehlt."), Display(Name = "Von")]
        public DateOnly? Von { get; set; }

        /// <summary>Letzter Tag des Zeitraums.</summary>
        [Required(ErrorMessage = "Ende fehlt."), Display(Name = "Bis")]
        public DateOnly? Bis { get; set; }
    }

    /// <summary>Lädt die Abrechnungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var vorjahr = Anzeige.Heute(zeit).Year - 1;
        Neu = new NeuFormular { Bezeichnung = $"Jahr {vorjahr}", Von = new DateOnly(vorjahr, 1, 1), Bis = new DateOnly(vorjahr, 12, 31) };
        await LadenAsync(ct);
    }

    /// <summary>Legt eine Abrechnung für den Zeitraum an und berechnet sie.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur neuen Abrechnung, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungAbrechnen)).Succeeded)
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            foreach (var fehler in Abrechnung.ZeitraumPruefen(Neu.Von!.Value, Neu.Bis!.Value))
            {
                ModelState.AddModelError("Neu.Bis", fehler);
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var abrechnung = await abrechnungen.AnlegenAsync(Neu.Bezeichnung, Neu.Von!.Value, Neu.Bis!.Value, ct);
        TempData["Meldung"] = $"Entwurf „{abrechnung.Bezeichnung}“ mit {abrechnung.Posten.Count} Posten berechnet.";
        return RedirectToPage("Abrechnung", new { id = abrechnung.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfAbrechnen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungAbrechnen)).Succeeded;
        Zeilen = await db.Abrechnungen.AsNoTracking()
            .OrderByDescending(a => a.Von).ThenByDescending(a => a.ErstelltAm)
            .Select(a => new Zeile(a.Id, a.Bezeichnung, a.Von, a.Bis, a.Status,
                a.Posten.Select(p => p.MitgliedId).Distinct().Count(),
                a.Posten.Sum(p => p.Betrag),
                a.AbgeschlossenAm))
            .ToListAsync(ct);
    }
}
