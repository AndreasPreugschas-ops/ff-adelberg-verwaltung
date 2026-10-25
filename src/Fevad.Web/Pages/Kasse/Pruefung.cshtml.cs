using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Eine Kassenprüfung (V05): Zahlen des Zeitraums, Beanstandungen der Prüfer mit Antwort des Kassenwarts, Ergebnis und Empfehlung zur
/// Entlastung, Bestätigung durch beide Prüfer und Prüfbericht als PDF. Ändert sich nach der ersten Bestätigung etwas, müssen beide neu
/// bestätigen.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class PruefungModel(
    AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, Kassenberichte berichte, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Prüfung mit Prüfern und Beanstandungen.</summary>
    public Kassenpruefung Pruefung { get; private set; } = null!;

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Der Benutzer ist einer der beiden Prüfer und hat das Prüfrecht.</summary>
    public bool IchPruefe { get; private set; }

    /// <summary>Der Benutzer ist der erste Prüfer (sonst der zweite).</summary>
    public bool IchBinErster { get; private set; }

    /// <summary>Der Benutzer darf buchen und beantwortet Beanstandungen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Einnahmen im Zeitraum.</summary>
    public decimal Einnahmen { get; private set; }

    /// <summary>Ausgaben im Zeitraum.</summary>
    public decimal Ausgaben { get; private set; }

    /// <summary>Zahl der Buchungen im Zeitraum.</summary>
    public int Anzahl { get; private set; }

    /// <summary>Buchungen im Zeitraum, die noch nicht festgeschrieben sind.</summary>
    public int NichtFestgeschrieben { get; private set; }

    /// <summary>Ausnahmebuchungen im Zeitraum.</summary>
    public IReadOnlyList<Buchung> Ausnahmen { get; private set; } = [];

    /// <summary>Kontostände am Ende des Zeitraums.</summary>
    public IReadOnlyList<(Zahlungsmittelkonto Konto, decimal Bestand)> Konten { get; private set; } = [];

    /// <summary>Buchungen des Zeitraums für die Zuordnung einer Beanstandung.</summary>
    public IReadOnlyList<SelectListItem> Belege { get; private set; } = [];

    /// <summary>Text einer neuen Beanstandung.</summary>
    [BindProperty, StringLength(2000)] public string? NeueBeanstandung { get; set; }

    /// <summary>Betroffene Buchung einer neuen Beanstandung.</summary>
    [BindProperty] public Guid? BuchungId { get; set; }

    /// <summary>Antwort des Kassenwarts.</summary>
    [BindProperty, StringLength(2000)] public string? Antwort { get; set; }

    /// <summary>Ergebnis der Prüfung.</summary>
    [BindProperty] public Ergebniseingabe Ergebnis { get; set; } = new();

    /// <summary>Ergebnisfelder.</summary>
    public sealed class Ergebniseingabe
    {
        /// <summary>Tag der Prüfung.</summary>
        [Display(Name = "Geprüft am")] public DateOnly? GeprueftAm { get; set; }

        /// <summary>Ergebnis in Worten.</summary>
        [StringLength(2000), Display(Name = "Ergebnis")] public string? Text { get; set; }

        /// <summary>Empfehlung zur Entlastung.</summary>
        [Display(Name = "Wir empfehlen der Hauptversammlung die Entlastung des Kassenwarts")] public bool EntlastungEmpfohlen { get; set; }
    }

    /// <summary>Zeigt die Prüfung.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Ergebnis = new Ergebniseingabe { GeprueftAm = Pruefung.GeprueftAm, Text = Pruefung.Ergebnis, EntlastungEmpfohlen = Pruefung.EntlastungEmpfohlen };
        return Page();
    }

    /// <summary>Lädt den Prüfbericht als PDF.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        var bericht = await berichte.PruefberichtAsync(Pruefung, Kasse, ct);
        return File(bericht.Inhalt, "application/pdf", bericht.Name);
    }

    /// <summary>Erfasst eine Beanstandung (nur Prüfer, solange die Prüfung läuft).</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBeanstandungAsync(Guid id, CancellationToken ct)
    {
        if (await PrueferAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (string.IsNullOrWhiteSpace(NeueBeanstandung))
        {
            ModelState.AddModelError(nameof(NeueBeanstandung), "Bitte beschreiben, was beanstandet wird.");
            return Page();
        }

        var p = await db.Kassenpruefungen.FirstAsync(x => x.Id == id, ct);
        db.Beanstandungen.Add(new Beanstandung
        {
            KassenpruefungId = id, Text = NeueBeanstandung.Trim(), BuchungId = Belege.Any(b => b.Value == BuchungId?.ToString()) ? BuchungId : null,
        });
        BestaetigungenZuruecksetzen(p);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Beanstandung erfasst; der Kassenwart sieht sie bei der Prüfung.";
        return RedirectToPage(new { id });
    }

    /// <summary>Der Kassenwart antwortet auf eine Beanstandung.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="beanstandungId">Die Beanstandung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAntwortAsync(Guid id, Guid beanstandungId, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Pruefung.Beanstandungen.All(b => b.Id != beanstandungId))
        {
            return NotFound();
        }

        if (!DarfBuchen || Pruefung.Abgeschlossen)
        {
            return Forbid();
        }

        var b = await db.Beanstandungen.FirstAsync(x => x.Id == beanstandungId, ct);
        b.Antwort = string.IsNullOrWhiteSpace(Antwort) ? null : Antwort.Trim();
        b.BeantwortetAm = b.Antwort is null ? null : zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Antwort gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Die Prüfer markieren eine Beanstandung als ausgeräumt oder wieder offen.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="beanstandungId">Die Beanstandung.</param>
    /// <param name="erledigt">Neuer Stand.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid id, Guid beanstandungId, bool erledigt, CancellationToken ct)
    {
        if (await PrueferAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (Pruefung.Beanstandungen.All(b => b.Id != beanstandungId))
        {
            return NotFound();
        }

        (await db.Beanstandungen.FirstAsync(x => x.Id == beanstandungId, ct)).Erledigt = erledigt;
        BestaetigungenZuruecksetzen(await db.Kassenpruefungen.FirstAsync(x => x.Id == id, ct));
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Speichert Tag, Ergebnis und Empfehlung.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostErgebnisAsync(Guid id, CancellationToken ct)
    {
        if (await PrueferAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var p = await db.Kassenpruefungen.FirstAsync(x => x.Id == id, ct);
        var text = string.IsNullOrWhiteSpace(Ergebnis.Text) ? null : Ergebnis.Text.Trim();
        if (p.GeprueftAm != Ergebnis.GeprueftAm || p.Ergebnis != text || p.EntlastungEmpfohlen != Ergebnis.EntlastungEmpfohlen)
        {
            (p.GeprueftAm, p.Ergebnis, p.EntlastungEmpfohlen) = (Ergebnis.GeprueftAm, text, Ergebnis.EntlastungEmpfohlen);
            BestaetigungenZuruecksetzen(p);
            await db.SaveChangesAsync(ct);
        }

        TempData["Meldung"] = "Ergebnis gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Der angemeldete Prüfer bestätigt die Prüfung; mit der zweiten Bestätigung ist sie abgeschlossen.</summary>
    /// <param name="id">Kennung der Prüfung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBestaetigenAsync(Guid id, CancellationToken ct)
    {
        if (await PrueferAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (Pruefung.GeprueftAm is null || string.IsNullOrWhiteSpace(Pruefung.Ergebnis))
        {
            TempData["Fehler"] = "Vor der Bestätigung bitte den Tag der Prüfung und das Ergebnis eintragen.";
            return RedirectToPage(new { id });
        }

        var p = await db.Kassenpruefungen.FirstAsync(x => x.Id == id, ct);
        if (IchBinErster)
        {
            p.Pruefer1BestaetigtAm = zeit.GetUtcNow();
        }
        else
        {
            p.Pruefer2BestaetigtAm = zeit.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = p.Abgeschlossen ? "Beide Prüfer haben bestätigt; der Prüfbericht ist fertig." : "Bestätigt; es fehlt noch der zweite Prüfer.";
        return RedirectToPage(new { id });
    }

    private static void BestaetigungenZuruecksetzen(Kassenpruefung p) => (p.Pruefer1BestaetigtAm, p.Pruefer2BestaetigtAm) = (null, null);

    private async Task<IActionResult?> PrueferAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        return IchPruefe && !Pruefung.Abgeschlossen ? null : Forbid();
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Kassenpruefungen.AsNoTracking().Include(x => x.Pruefer1).Include(x => x.Pruefer2)
            .Include(x => x.Beanstandungen).ThenInclude(b => b.Buchung).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, p.KasseId, ct) is not { } kasse)
        {
            return false;
        }

        (Pruefung, Kasse) = (p, kasse);
        p.Beanstandungen = p.Beanstandungen.OrderBy(b => b.ErstelltAm).ToList();
        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        var ich = benutzer.Id is { } bid ? await db.Users.Where(u => u.Id == bid).Select(u => u.MitgliedId).FirstOrDefaultAsync(ct) : null;
        IchPruefe = p.IstPruefer(ich) && await zugriff.DarfAsync(User, Berechtigungen.KassePruefen, kasse);
        IchBinErster = ich == p.Pruefer1Id;

        var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.Datum >= p.Von && b.Datum <= p.Bis)
            .OrderBy(b => b.Datum).ThenBy(b => b.Belegnummer).ToListAsync(ct);
        Einnahmen = buchungen.Where(b => b.Art == BuchungArt.Einnahme).Sum(Kassenregeln.Ist);
        Ausgaben = buchungen.Where(b => b.Art == BuchungArt.Ausgabe).Sum(Kassenregeln.Ist);
        Anzahl = buchungen.Count;
        NichtFestgeschrieben = buchungen.Count(b => !b.Festgeschrieben);
        Ausnahmen = buchungen.Where(b => b.AusnahmeBegruendung is not null).ToList();
        Konten = (await kassen.KontostaendeAsync(kasse.Id, p.Bis, ct)).Where(k => k.Konto.Aktiv || k.Bestand != 0).ToList();
        Belege = buchungen.Select(b => new SelectListItem($"{b.Kennung} · {Anzeige.Datum(b.Datum)} · {Anzeige.Euro(b.Betrag)} · {Kuerzen(b.Text)}", b.Id.ToString()))
            .ToList();
        return true;

        static string Kuerzen(string text) => text.Length <= 40 ? text : text[..39] + "…";
    }
}
