using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>Veranstaltungen einer Kasse in einem Jahr mit Abrechnung (V03); der Kassenwart legt neue an.</summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class VeranstaltungenModel(AppDbContext db, Kassenzugriff zugriff, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Veranstaltung mit Abrechnung.</summary>
    /// <param name="Veranstaltung">Die Veranstaltung.</param>
    /// <param name="Abrechnung">Einnahmen, Ausgaben und Ergebnis aus den Buchungen.</param>
    /// <param name="Berichte">Zahl der Barkassenberichte.</param>
    /// <param name="Offen">Barkassenberichte, die noch nicht im Kassenbuch stehen.</param>
    public sealed record Zeile(Veranstaltung Veranstaltung, Veranstaltungsabrechnung Abrechnung, int Berichte, int Offen);

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Gezeigtes Jahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Veranstaltungen des Jahres.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Jahre mit Veranstaltungen und das laufende.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Der Benutzer darf buchen und Veranstaltungen anlegen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Eingabe einer neuen Veranstaltung.</summary>
    [BindProperty] public Eingabe Neu { get; set; } = new();

    /// <summary>Eingabefelder einer Veranstaltung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Name.</summary>
        [Required(ErrorMessage = "Bitte den Namen angeben."), StringLength(150), Display(Name = "Name")] public string Name { get; set; } = "";

        /// <summary>Erster Tag.</summary>
        [Required(ErrorMessage = "Bitte den ersten Tag angeben."), Display(Name = "Erster Tag")] public DateOnly? Beginn { get; set; }

        /// <summary>Letzter Tag.</summary>
        [Display(Name = "Letzter Tag")] public DateOnly? Ende { get; set; }

        /// <summary>Mitveranstalter.</summary>
        [StringLength(200), Display(Name = "Mitveranstalter")] public string? Mitveranstalter { get; set; }

        /// <summary>Anteil der Feuerwehr in Prozent.</summary>
        [Range(0.01, 100, ErrorMessage = "Der Anteil liegt zwischen 0,01 und 100 %."), Display(Name = "Anteil Feuerwehr (%)")] public decimal AnteilProzent { get; set; } = 100;

        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt die Veranstaltungen.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) => await LadenAsync(kasse, ct) ? Page() : NotFound();

    /// <summary>Legt eine Veranstaltung an.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Veranstaltung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        if (!DarfBuchen)
        {
            return Forbid();
        }

        if (Neu.Ende is { } ende && Neu.Beginn is { } beginn && ende < beginn)
        {
            ModelState.AddModelError("Neu.Ende", "Der letzte Tag liegt vor dem ersten.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var veranstaltung = new Veranstaltung
        {
            KasseId = Kasse.Id, Name = Neu.Name.Trim(), Beginn = Neu.Beginn!.Value, Ende = Neu.Ende, Mitveranstalter = Leer(Neu.Mitveranstalter),
            AnteilProzent = Neu.AnteilProzent, Bemerkung = Leer(Neu.Bemerkung),
        };
        db.Veranstaltungen.Add(veranstaltung);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Veranstaltung „{veranstaltung.Name}“ angelegt.";
        return RedirectToPage("Veranstaltung", new { id = veranstaltung.Id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

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
            Jahr = heute.Year;
        }

        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        var veranstaltungen = await db.Veranstaltungen.AsNoTracking().Include(v => v.Barkassenberichte)
            .Where(v => v.KasseId == kasseId && v.Beginn.Year == Jahr)
            .OrderBy(v => v.Beginn).ToListAsync(ct);
        var ids = veranstaltungen.Select(v => v.Id).ToList();
        var buchungen = (await db.Buchungen.AsNoTracking().Where(b => b.VeranstaltungId != null && ids.Contains(b.VeranstaltungId.Value)).ToListAsync(ct))
            .ToLookup(b => b.VeranstaltungId!.Value);
        Zeilen = veranstaltungen.Select(v => new Zeile(v, Veranstaltungsabrechnung.Berechnen(buchungen[v.Id], v.AnteilProzent),
            v.Barkassenberichte.Count, v.Barkassenberichte.Count(b => b.BuchungId is null))).ToList();
        Jahre = (await db.Veranstaltungen.AsNoTracking().Where(v => v.KasseId == kasseId).Select(v => v.Beginn.Year).Distinct().ToListAsync(ct))
            .Append(heute.Year).Append(Jahr).Distinct().OrderDescending().ToList();
        return true;
    }
}
