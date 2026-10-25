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
/// Spende oder Sponsoring im Register erfassen oder ändern (V08): Geber, Geld oder Sache, Zweck, Zweckbindung, Buchung im Kassenbuch,
/// Annahmebeschluss des Gemeinderats und Dankschreiben als PDF. Nur mit Buchungsrecht für die Kasse.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class SpendeModel(AppDbContext db, Kassenzugriff zugriff, Kassenberichte berichte, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Der gespeicherte Eintrag; null bei einem neuen.</summary>
    public Zuwendung? Zuwendung { get; private set; }

    /// <summary>Einnahmen der Kasse, mit denen sich der Eintrag verknüpfen lässt.</summary>
    public IReadOnlyList<SelectListItem> Einnahmen { get; private set; } = [];

    /// <summary>Eingaben.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Felder einer Zuwendung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag des Eingangs.</summary>
        [Required(ErrorMessage = "Bitte den Tag des Eingangs angeben."), Display(Name = "Eingang")] public DateOnly? Eingang { get; set; }

        /// <summary>Geber.</summary>
        [Required(ErrorMessage = "Bitte den Geber angeben."), StringLength(200), Display(Name = "Geber (Person oder Firma)")] public string Geber { get; set; } = "";

        /// <summary>Anschrift des Gebers.</summary>
        [StringLength(500), Display(Name = "Anschrift für das Dankschreiben")] public string? Anschrift { get; set; }

        /// <summary>Geld oder Sache.</summary>
        [Display(Name = "Art")] public ZuwendungArt Art { get; set; }

        /// <summary>Betrag oder Wert.</summary>
        [Required(ErrorMessage = "Bitte den Betrag oder Wert angeben."), Range(0.01, 10_000_000, ErrorMessage = "Der Betrag muss größer als 0 sein."), Display(Name = "Betrag oder Wert (Euro)")]
        public decimal? Betrag { get; set; }

        /// <summary>Beschreibung der Sache.</summary>
        [StringLength(500), Display(Name = "Sache")] public string? Sache { get; set; }

        /// <summary>Zweck.</summary>
        [StringLength(500), Display(Name = "Zweck")] public string? Zweck { get; set; }

        /// <summary>An den Zweck gebunden.</summary>
        [Display(Name = "Zweckgebunden")] public bool Zweckgebunden { get; set; }

        /// <summary>Sponsoring mit Gegenleistung.</summary>
        [Display(Name = "Sponsoring mit Gegenleistung (wirtschaftlicher Bereich)")] public bool Sponsoring { get; set; }

        /// <summary>Buchung im Kassenbuch.</summary>
        [Display(Name = "Buchung im Kassenbuch")] public Guid? BuchungId { get; set; }

        /// <summary>Tag des Annahmebeschlusses.</summary>
        [Display(Name = "Annahme beschlossen am")] public DateOnly? AnnahmeAm { get; set; }

        /// <summary>Verweis auf den Beschluss.</summary>
        [StringLength(200), Display(Name = "Beschluss")] public string? AnnahmeVerweis { get; set; }

        /// <summary>Tag des Dankschreibens.</summary>
        [Display(Name = "Dankschreiben verschickt am")] public DateOnly? DankschreibenAm { get; set; }

        /// <summary>Freitext.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="id">Der Eintrag; leer für einen neuen.</param>
    /// <param name="kasse">Kasse eines neuen Eintrags.</param>
    /// <param name="buchung">Einnahme, aus der erfasst wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? kasse, Guid? buchung, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (Zuwendung is { } z)
        {
            Formular = new Eingabe
            {
                Eingang = z.Eingang, Geber = z.Geber, Anschrift = z.Anschrift, Art = z.Art, Betrag = z.Betrag, Sache = z.Sache, Zweck = z.Zweck,
                Zweckgebunden = z.Zweckgebunden, Sponsoring = z.Sponsoring, BuchungId = z.BuchungId, AnnahmeAm = z.AnnahmeAm,
                AnnahmeVerweis = z.AnnahmeVerweis, DankschreibenAm = z.DankschreibenAm, Bemerkung = z.Bemerkung,
            };
        }
        else
        {
            var b = buchung is { } bid ? await db.Buchungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bid && x.KasseId == Kasse.Id && x.Art == BuchungArt.Einnahme, ct) : null;
            Formular = new Eingabe
            {
                Eingang = b?.Datum ?? Anzeige.Heute(zeit), Geber = b?.Partner ?? "", Betrag = b?.Betrag, BuchungId = b?.Id, Zweck = b?.Text,
                Sponsoring = b?.Steuerbereich == Domain.Kassen.Steuerbereich.Wirtschaftlich,
            };
        }

        return Page();
    }

    /// <summary>Speichert den Eintrag.</summary>
    /// <param name="id">Der Eintrag; leer für einen neuen.</param>
    /// <param name="kasse">Kasse eines neuen Eintrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, Guid? kasse, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, ct) is { } fehler)
        {
            return fehler;
        }

        if (Formular.Art == ZuwendungArt.Sache && string.IsNullOrWhiteSpace(Formular.Sache))
        {
            ModelState.AddModelError("Formular.Sache", "Bitte beschreiben, was gegeben wurde.");
        }

        if (Formular.BuchungId is { } bid && !Einnahmen.Any(e => e.Value == bid.ToString()))
        {
            ModelState.AddModelError("Formular.BuchungId", "Diese Buchung passt nicht zur Kasse.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var z = id is { } zid ? await db.Zuwendungen.FirstAsync(x => x.Id == zid, ct) : new Zuwendung { KasseId = Kasse.Id, Geber = "" };
        z.Eingang = Formular.Eingang!.Value;
        z.Geber = Formular.Geber.Trim();
        z.Anschrift = Leer(Formular.Anschrift);
        z.Art = Formular.Art;
        z.Betrag = Formular.Betrag!.Value;
        z.Sache = Formular.Art == ZuwendungArt.Sache ? Leer(Formular.Sache) : null;
        z.Zweck = Leer(Formular.Zweck);
        z.Zweckgebunden = Formular.Zweckgebunden;
        z.Sponsoring = Formular.Sponsoring;
        z.BuchungId = Formular.BuchungId;
        z.AnnahmeAm = Formular.AnnahmeAm;
        z.AnnahmeVerweis = Leer(Formular.AnnahmeVerweis);
        z.DankschreibenAm = Formular.DankschreibenAm;
        z.Bemerkung = Leer(Formular.Bemerkung);
        if (id is null)
        {
            db.Zuwendungen.Add(z);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Zuwendung von {z.Geber} gespeichert.";
        return RedirectToPage("Spenden", new { kasse = Kasse.Id, jahr = z.Eingang.Year });
    }

    /// <summary>Dankschreiben als PDF; vermerkt das heutige Datum, falls noch keins eingetragen ist.</summary>
    /// <param name="id">Der Eintrag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostDankschreibenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, ct) is { } fehler)
        {
            return fehler;
        }

        var z = await db.Zuwendungen.FirstAsync(x => x.Id == id, ct);
        var mitgliedId = benutzer.Id is { } bid ? await db.Users.Where(u => u.Id == bid).Select(u => u.MitgliedId).FirstOrDefaultAsync(ct) : null;
        var unterzeichner = mitgliedId is { } mid
            ? await db.Mitglieder.Where(m => m.Id == mid).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct)
            : null;
        var datei = await berichte.DankschreibenAsync(z, Kasse, unterzeichner, ct);
        z.DankschreibenAm ??= Anzeige.Heute(zeit);
        await db.SaveChangesAsync(ct);
        return File(datei.Inhalt, "application/pdf", datei.Name);
    }

    /// <summary>Entfernt einen versehentlich angelegten Eintrag.</summary>
    /// <param name="id">Der Eintrag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, ct) is { } fehler)
        {
            return fehler;
        }

        db.Zuwendungen.Remove(await db.Zuwendungen.FirstAsync(x => x.Id == id, ct));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Eintrag von {Zuwendung!.Geber} entfernt.";
        return RedirectToPage("Spenden", new { kasse = Kasse.Id, jahr = Zuwendung.Eingang.Year });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<IActionResult?> LadenAsync(Guid? id, Guid? kasseId, CancellationToken ct)
    {
        if (id is { } zid)
        {
            Zuwendung = await db.Zuwendungen.AsNoTracking().FirstOrDefaultAsync(z => z.Id == zid, ct);
            if (Zuwendung is null)
            {
                return NotFound();
            }

            kasseId = Zuwendung.KasseId;
        }

        if (kasseId is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId.Value, ct) is not { } kasse)
        {
            return NotFound();
        }

        if (!await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse))
        {
            return Forbid();
        }

        Kasse = kasse;
        var ab = new DateOnly(Anzeige.Heute(zeit).Year - 1, 1, 1);
        var aktuelle = Zuwendung?.BuchungId;
        var verknuepft = db.Zuwendungen.Where(z => z.BuchungId != null && z.Id != id).Select(z => z.BuchungId);
        Einnahmen = (await db.Buchungen.AsNoTracking()
                .Where(b => b.KasseId == kasse.Id && b.Art == BuchungArt.Einnahme && b.StornoVonId == null
                    && (b.Datum >= ab || b.Id == aktuelle) && !verknuepft.Contains(b.Id))
                .OrderByDescending(b => b.Datum).ThenByDescending(b => b.Belegnummer).ToListAsync(ct))
            .Select(b => new SelectListItem($"{b.Kennung} · {Anzeige.Datum(b.Datum)} · {Anzeige.Euro(b.Betrag)} · {b.Partner ?? b.Text}", b.Id.ToString()))
            .ToList();
        return null;
    }
}
