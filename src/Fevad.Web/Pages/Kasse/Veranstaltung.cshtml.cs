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

/// <summary>Eine Veranstaltung mit Abrechnung, Buchungen und Barkassenberichten (V03).</summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class VeranstaltungModel(AppDbContext db, Kassenzugriff zugriff) : PageModel
{
    /// <summary>Die Veranstaltung samt Barkassenberichten.</summary>
    public Veranstaltung Veranstaltung { get; private set; } = null!;

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Buchungen mit Verweis auf die Veranstaltung.</summary>
    public IReadOnlyList<Buchung> Buchungen { get; private set; } = [];

    /// <summary>Abrechnung aus den Buchungen.</summary>
    public Veranstaltungsabrechnung Abrechnung { get; private set; } = null!;

    /// <summary>Der Benutzer darf buchen und die Veranstaltung ändern.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Zahlungspartner sichtbar.</summary>
    public bool PartnerSichtbar { get; private set; }

    /// <summary>Eingabefelder.</summary>
    [BindProperty] public VeranstaltungenModel.Eingabe Formular { get; set; } = new();

    /// <summary>Die Abrechnung ist fertig.</summary>
    [BindProperty, Display(Name = "Abrechnung fertig")] public bool Abgerechnet { get; set; }

    /// <summary>Zeigt die Veranstaltung.</summary>
    /// <param name="id">Kennung der Veranstaltung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        var v = Veranstaltung;
        Formular = new VeranstaltungenModel.Eingabe
        {
            Name = v.Name, Beginn = v.Beginn, Ende = v.Ende, Mitveranstalter = v.Mitveranstalter, AnteilProzent = v.AnteilProzent, Bemerkung = v.Bemerkung,
        };
        Abgerechnet = v.Abgerechnet;
        return Page();
    }

    /// <summary>Speichert die Angaben der Veranstaltung.</summary>
    /// <param name="id">Kennung der Veranstaltung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBuchen)
        {
            return Forbid();
        }

        if (Formular.Ende is { } ende && Formular.Beginn is { } beginn && ende < beginn)
        {
            ModelState.AddModelError("Formular.Ende", "Der letzte Tag liegt vor dem ersten.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var v = await db.Veranstaltungen.FirstAsync(x => x.Id == id, ct);
        v.Name = Formular.Name.Trim();
        v.Beginn = Formular.Beginn!.Value;
        v.Ende = Formular.Ende;
        v.Mitveranstalter = string.IsNullOrWhiteSpace(Formular.Mitveranstalter) ? null : Formular.Mitveranstalter.Trim();
        v.AnteilProzent = Formular.AnteilProzent;
        v.Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();
        v.Abgerechnet = Abgerechnet;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Veranstaltung gespeichert.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var v = await db.Veranstaltungen.AsNoTracking().Include(x => x.Barkassenberichte).ThenInclude(b => b.Zaehlung)
            .Include(x => x.Barkassenberichte).ThenInclude(b => b.Buchung)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, v.KasseId, ct) is not { } kasse)
        {
            return false;
        }

        Veranstaltung = v;
        Kasse = kasse;
        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        PartnerSichtbar = await zugriff.PartnerSichtbarAsync(User, kasse);
        Buchungen = await db.Buchungen.AsNoTracking().Include(b => b.Planposition).Where(b => b.VeranstaltungId == id)
            .OrderBy(b => b.Datum).ThenBy(b => b.Belegnummer).ToListAsync(ct);
        Abrechnung = Veranstaltungsabrechnung.Berechnen(Buchungen, v.AnteilProzent);
        return true;
    }
}
