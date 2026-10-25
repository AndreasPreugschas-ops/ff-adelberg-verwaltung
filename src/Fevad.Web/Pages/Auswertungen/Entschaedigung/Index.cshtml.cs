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
public class IndexModel(AppDbContext db, Abrechnungen abrechnungen, IAuthorizationService autorisierung, TimeProvider zeit, ParameterLeser parameter) : PageModel
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
        var heute = Anzeige.Heute(zeit);
        var monate = await parameter.ZahlAsync(Entschaedigungssaetze.AbrechnungMonate, heute, 12, ct);
        var (von, bis) = LetzterZeitraum(heute, monate);
        Neu = new NeuFormular { Bezeichnung = Bezeichnung(von, bis), Von = von, Bis = bis };
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

    /// <summary>
    /// Letzter vollständig abgelaufener Abrechnungszeitraum im Rhythmus ab dem 1. Januar, z. B. bei sechs Monaten im Oktober
    /// der 01.01. bis 30.06.; Rhythmen, die nicht in zwölf Monate aufgehen, gelten als ein Jahr.
    /// </summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="monate">Abrechnungsrhythmus in Monaten.</param>
    /// <returns>Erster und letzter Tag des Zeitraums.</returns>
    public static (DateOnly Von, DateOnly Bis) LetzterZeitraum(DateOnly heute, int monate)
    {
        if (monate is < 1 or > 12 || 12 % monate != 0)
        {
            monate = 12;
        }

        var beginnLaufend = new DateOnly(heute.Year, (heute.Month - 1) / monate * monate + 1, 1);
        var von = beginnLaufend.AddMonths(-monate);
        return (von, beginnLaufend.AddDays(-1));
    }

    /// <summary>Bezeichnung des Zeitraums, z. B. „Jahr 2026“, „1. Halbjahr 2026“, „3. Quartal 2026“ oder „März 2026“.</summary>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <returns>Die Bezeichnung.</returns>
    public static string Bezeichnung(DateOnly von, DateOnly bis)
    {
        var monate = (bis.Year - von.Year) * 12 + bis.Month - von.Month + 1;
        var nummer = (von.Month - 1) / monate + 1;
        return monate switch
        {
            12 => $"Jahr {von.Year}",
            6 => $"{nummer}. Halbjahr {von.Year}",
            3 => $"{nummer}. Quartal {von.Year}",
            1 => von.ToString("MMMM yyyy", new System.Globalization.CultureInfo("de-DE")),
            _ => $"{von:dd.MM.yyyy} bis {bis:dd.MM.yyyy}",
        };
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
