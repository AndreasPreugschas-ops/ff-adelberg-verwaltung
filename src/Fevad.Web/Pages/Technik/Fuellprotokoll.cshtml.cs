using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Vorbelegung = Fevad.Infrastructure.Persistenz.Seed.Stammdaten;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Füllprotokoll für Atemluftflaschen (V61, Kann): Datum, Flaschen, Druck, Füller, Anlage. Nur sichtbar, wenn der Parameter
/// <see cref="ParameterSchluessel.FuellprotokollAktiv"/> eingeschaltet ist; in Adelberg füllt die Feuerwehr Göppingen.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class FuellprotokollModel(AppDbContext db, ParameterLeser parameter, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>So viele Füllungen zeigt die Liste.</summary>
    public const int Anzeigen = 100;

    /// <summary>Das Füllprotokoll ist eingeschaltet.</summary>
    public bool Aktiv { get; private set; }

    /// <summary>Letzte Füllungen.</summary>
    public IReadOnlyList<Zeile> Fuellungen { get; private set; } = [];

    /// <summary>Füllanlagen (Atemluftkompressoren).</summary>
    public IReadOnlyList<SelectListItem> Anlagen { get; private set; } = [];

    /// <summary>Eingaben.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Eine Füllung in der Liste.</summary>
    /// <param name="GefuelltAm">Zeitpunkt (UTC).</param>
    /// <param name="Flasche">Inventarnummer der Flasche.</param>
    /// <param name="DruckBar">Fülldruck.</param>
    /// <param name="Fueller">Name des Füllers.</param>
    /// <param name="Anlage">Inventarnummer der Anlage.</param>
    public sealed record Zeile(DateTimeOffset GefuelltAm, string Flasche, int DruckBar, string? Fueller, string? Anlage);

    /// <summary>Formular einer Füllung, auch für mehrere Flaschen.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag der Füllung.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        /// <summary>Uhrzeit „HH:MM“.</summary>
        [Required(ErrorMessage = "Uhrzeit fehlt."), Display(Name = "Uhrzeit")] public string Uhrzeit { get; set; } = "";
        /// <summary>Inventarnummern, durch Leerzeichen, Komma oder Zeilen getrennt.</summary>
        [Required(ErrorMessage = "Bitte die Flaschen angeben."), StringLength(4000), Display(Name = "Flaschen (Inventarnummern, gescannt oder getippt)")]
        public string? Flaschen { get; set; }
        /// <summary>Fülldruck in bar.</summary>
        [Range(1, 450, ErrorMessage = "Druck zwischen 1 und 450 bar."), Display(Name = "Fülldruck (bar)")] public int DruckBar { get; set; } = 300;
        /// <summary>Füllanlage.</summary>
        [Display(Name = "Füllanlage")] public Guid? AnlageId { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(200), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt Formular und Liste.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var jetzt = Anzeige.Lokal(zeit.GetUtcNow());
        Formular = new Eingabe { Datum = DateOnly.FromDateTime(jetzt.DateTime), Uhrzeit = jetzt.ToString("HH:mm") };
        await LadenAsync(ct);
    }

    /// <summary>Trägt die Füllung für jede genannte Flasche ein.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        if (!Aktiv)
        {
            return Forbid();
        }

        DateTimeOffset? zeitpunkt = null;
        if (Formular.Datum is { } datum && ZeitEingabe.TryUhrzeit(Formular.Uhrzeit, out var uhrzeit))
        {
            zeitpunkt = Anzeige.AlsUtc(datum, uhrzeit);
        }
        else if (!string.IsNullOrWhiteSpace(Formular.Uhrzeit))
        {
            ModelState.AddModelError("Formular.Uhrzeit", "Uhrzeit bitte als HH:MM (24 Stunden).");
        }

        if (zeitpunkt > zeit.GetUtcNow().AddMinutes(5))
        {
            ModelState.AddModelError("Formular.Datum", "Der Zeitpunkt liegt in der Zukunft.");
        }

        var nummern = (Formular.Flaschen ?? "").Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim().ToUpperInvariant()).Distinct().ToList();
        var flaschen = await db.Geraete
            .Where(g => nummern.Contains(g.Inventarnummer.ToUpper()) && g.AusgesondertAm == null && g.Geraeteart!.Code.StartsWith(Vorbelegung.FlaschenCodeAnfang))
            .Select(g => new { g.Id, g.Inventarnummer, g.NutzbarBis })
            .ToListAsync(ct);
        var unbekannt = nummern.Except(flaschen.Select(f => f.Inventarnummer.ToUpperInvariant())).ToList();
        if (unbekannt.Count > 0)
        {
            ModelState.AddModelError("Formular.Flaschen", $"Keine Atemluftflasche im Bestand: {string.Join(", ", unbekannt)}.");
        }

        var abgelaufen = flaschen.Where(f => f.NutzbarBis < Formular.Datum).Select(f => f.Inventarnummer).ToList();
        if (abgelaufen.Count > 0)
        {
            ModelState.AddModelError("Formular.Flaschen", $"Nutzungsende laut Prägung überschritten, nicht füllen: {string.Join(", ", abgelaufen)}.");
        }

        if (!ModelState.IsValid || zeitpunkt is null)
        {
            return Page();
        }

        var fueller = (await benutzer.GetUserAsync(User))?.MitgliedId;
        foreach (var flasche in flaschen)
        {
            db.Flaschenfuellungen.Add(new Flaschenfuellung
            {
                GeraetId = flasche.Id,
                GefuelltAm = zeitpunkt.Value,
                DruckBar = Formular.DruckBar,
                GefuelltVonId = fueller,
                AnlageId = Formular.AnlageId,
                Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
            });
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{flaschen.Count} Füllungen eingetragen.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Aktiv = await parameter.ZahlAsync(ParameterSchluessel.FuellprotokollAktiv, heute, 0, ct) == 1;
        Anlagen = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && g.Geraeteart!.Code == Vorbelegung.KompressorCode)
            .OrderBy(g => g.Inventarnummer)
            .Select(g => new SelectListItem(g.Inventarnummer + " " + g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        Fuellungen = await db.Flaschenfuellungen.AsNoTracking()
            .OrderByDescending(f => f.GefuelltAm)
            .Take(Anzeigen)
            .Select(f => new Zeile(f.GefuelltAm, f.Geraet!.Inventarnummer, f.DruckBar,
                f.GefuelltVon == null ? null : f.GefuelltVon.Vorname + " " + f.GefuelltVon.Nachname, f.Anlage == null ? null : f.Anlage.Inventarnummer))
            .ToListAsync(ct);
    }
}
