using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Wiederkehrende Buchungen einer Kasse (V07) pflegen: Betrag fest oder aus einem Parameter, Rhythmus und nächste Fälligkeit. Fällige
/// erscheinen auf der Kassenseite mit vorbefüllter Buchung; eine Fälligkeit lässt sich auch überspringen.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class WiederkehrendModel(AppDbContext db, Kassenzugriff zugriff, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Die wiederkehrenden Buchungen mit dem Betrag zur nächsten Fälligkeit.</summary>
    public IReadOnlyList<(WiederkehrendeBuchung Buchung, decimal? Betrag)> Eintraege { get; private set; } = [];

    /// <summary>Die bearbeitete Buchung; null bei einer neuen.</summary>
    public Guid? BearbeitetId { get; private set; }

    /// <summary>Konten der Kasse.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Positionen der Kasse.</summary>
    public IReadOnlyList<SelectListItem> Positionen { get; private set; } = [];

    /// <summary>Parameter mit Eurobeträgen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Parameter { get; private set; } = [];

    /// <summary>Eingaben.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Felder einer wiederkehrenden Buchung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bitte die Bezeichnung angeben."), StringLength(200), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";

        /// <summary>Einnahme oder Ausgabe.</summary>
        [Display(Name = "Art")] public BuchungArt Art { get; set; } = BuchungArt.Einnahme;

        /// <summary>Fester Betrag.</summary>
        [Range(0.01, 1_000_000, ErrorMessage = "Der Betrag muss größer als 0 sein."), Display(Name = "Fester Betrag (Euro)")] public decimal? Betrag { get; set; }

        /// <summary>Parameter mit dem Betrag.</summary>
        [StringLength(200), Display(Name = "oder Betrag aus Parameter")] public string? BetragParameter { get; set; }

        /// <summary>Konto.</summary>
        [Display(Name = "Konto")] public Guid? KontoId { get; set; }

        /// <summary>Position.</summary>
        [Display(Name = "Position")] public Guid? PlanpositionId { get; set; }

        /// <summary>Zahlungspartner.</summary>
        [StringLength(200), Display(Name = "Zahlungspartner")] public string? Partner { get; set; }

        /// <summary>Verwendungszweck.</summary>
        [Required(ErrorMessage = "Bitte den Verwendungszweck angeben."), StringLength(500), Display(Name = "Verwendungszweck ({Jahr} wird ersetzt)")]
        public string Text { get; set; } = "";

        /// <summary>Rhythmus.</summary>
        [Display(Name = "Rhythmus")] public Buchungsrhythmus Rhythmus { get; set; } = Buchungsrhythmus.Jaehrlich;

        /// <summary>Nächste Fälligkeit; ihr Tag im Monat gilt auch für die folgenden.</summary>
        [Required(ErrorMessage = "Bitte die nächste Fälligkeit angeben."), Display(Name = "Nächste Fälligkeit")] public DateOnly? NaechsteFaelligkeit { get; set; }

        /// <summary>Wird vorgeschlagen.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt die Liste, auf Wunsch mit einer Buchung im Formular.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Zu bearbeitende Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, Guid? id, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (id is { } wid)
        {
            if (Eintraege.Select(e => e.Buchung).FirstOrDefault(w => w.Id == wid) is not { } w)
            {
                return NotFound();
            }

            BearbeitetId = wid;
            Formular = new Eingabe
            {
                Bezeichnung = w.Bezeichnung, Art = w.Art, Betrag = w.Betrag, BetragParameter = w.BetragParameter, KontoId = w.KontoId,
                PlanpositionId = w.PlanpositionId, Partner = w.Partner, Text = w.Text, Rhythmus = w.Rhythmus,
                NaechsteFaelligkeit = w.NaechsteFaelligkeit, Aktiv = w.Aktiv,
            };
        }
        else
        {
            Formular.NaechsteFaelligkeit = Anzeige.Heute(zeit);
        }

        return Page();
    }

    /// <summary>Speichert eine neue oder geänderte wiederkehrende Buchung.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Geänderte Buchung; leer für eine neue.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid kasse, Guid? id, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        BearbeitetId = id;
        if (Formular.Art == BuchungArt.Umbuchung)
        {
            ModelState.AddModelError("Formular.Art", "Umbuchungen wiederholen sich nicht; bitte Einnahme oder Ausgabe wählen.");
        }

        var schluessel = string.IsNullOrWhiteSpace(Formular.BetragParameter) ? null : Formular.BetragParameter.Trim();
        if (schluessel is not null && !Parameter.Any(p => p.Value == schluessel))
        {
            ModelState.AddModelError("Formular.BetragParameter", "Diesen Parameter gibt es nicht.");
        }

        if (schluessel is null && Formular.Betrag is null)
        {
            ModelState.AddModelError("Formular.Betrag", "Bitte einen festen Betrag angeben oder einen Parameter wählen.");
        }

        if (Formular.KontoId is { } k && !Konten.Any(x => x.Value == k.ToString()))
        {
            ModelState.AddModelError("Formular.KontoId", "Dieses Konto steht nicht zur Wahl.");
        }

        if (Formular.PlanpositionId is { } p && !Positionen.Any(x => x.Value == p.ToString()))
        {
            ModelState.AddModelError("Formular.PlanpositionId", "Diese Position steht nicht zur Wahl.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var w = id is { } wid ? await db.WiederkehrendeBuchungen.FirstOrDefaultAsync(x => x.Id == wid && x.KasseId == kasse, ct) : null;
        if (id is not null && w is null)
        {
            return NotFound();
        }

        if (w is null)
        {
            w = new WiederkehrendeBuchung { KasseId = kasse, Bezeichnung = "", Text = "" };
            db.WiederkehrendeBuchungen.Add(w);
        }

        w.Bezeichnung = Formular.Bezeichnung.Trim();
        w.Art = Formular.Art;
        w.Betrag = Formular.Betrag;
        w.BetragParameter = schluessel;
        w.KontoId = Formular.KontoId;
        w.PlanpositionId = Formular.PlanpositionId;
        w.Partner = string.IsNullOrWhiteSpace(Formular.Partner) ? null : Formular.Partner.Trim();
        w.Text = Formular.Text.Trim();
        w.Rhythmus = Formular.Rhythmus;
        w.NaechsteFaelligkeit = Formular.NaechsteFaelligkeit!.Value;
        w.Tag = w.NaechsteFaelligkeit.Day;
        w.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{w.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { kasse });
    }

    /// <summary>Überspringt die nächste Fälligkeit, z. B. wenn die Zahlung schon von Hand gebucht ist.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="id">Die wiederkehrende Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostUeberspringenAsync(Guid kasse, Guid id, CancellationToken ct)
    {
        if (await LadenAsync(kasse, ct) is { } fehler)
        {
            return fehler;
        }

        var w = await db.WiederkehrendeBuchungen.FirstOrDefaultAsync(x => x.Id == id && x.KasseId == kasse, ct);
        if (w is null)
        {
            return NotFound();
        }

        var bisher = w.NaechsteFaelligkeit;
        w.NaechsteFaelligkeit = Wiederkehrend.Naechste(w.NaechsteFaelligkeit, w.Rhythmus, w.Tag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Fälligkeit {Anzeige.Datum(bisher)} übersprungen; nächste am {Anzeige.Datum(w.NaechsteFaelligkeit)}.";
        return RedirectToPage(new { kasse });
    }

    private async Task<IActionResult?> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return NotFound();
        }

        if (!await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse))
        {
            return Forbid();
        }

        Kasse = kasse;
        var liste = await db.WiederkehrendeBuchungen.AsNoTracking().Include(w => w.Konto).Include(w => w.Planposition)
            .Where(w => w.KasseId == kasseId).OrderByDescending(w => w.Aktiv).ThenBy(w => w.NaechsteFaelligkeit).ToListAsync(ct);
        var eintraege = new List<(WiederkehrendeBuchung, decimal?)>();
        foreach (var w in liste)
        {
            eintraege.Add((w, await Kassenauswahl.VorlageBetragAsync(w, parameter, ct)));
        }

        Eintraege = eintraege;
        Konten = await Kassenauswahl.KontenAsync(db, kasseId, [Formular.KontoId], ct);
        Positionen = await Kassenauswahl.PositionenAsync(db, kasseId, Formular.PlanpositionId, ct);
        Parameter = (await db.Parameter.AsNoTracking().Where(p => p.Schluessel.EndsWith("Euro")).Select(p => p.Schluessel).Distinct().ToListAsync(ct))
            .Order().Select(s => new SelectListItem(s, s)).ToList();
        return null;
    }
}
