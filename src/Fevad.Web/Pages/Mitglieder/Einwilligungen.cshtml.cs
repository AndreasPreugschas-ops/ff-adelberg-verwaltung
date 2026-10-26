using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Einwilligungsregister eines Mitglieds (V39, V45): je Art der Stand, der Verlauf als Nachweis und das Eintragen einer Erklärung
/// auf Papier. Lesen mit Mitglieder.Lesen, Eintragen und Widerrufen mit Mitglieder.Bearbeiten (Jugendwart für seine Abteilungen).
/// </summary>
public class EinwilligungenModel(AppDbContext db, Datenbereich datenbereich, Einwilligungsregister register, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe einer Erklärung auf Papier.</summary>
    public sealed class Eingabe
    {
        /// <summary>Art der Einwilligung; leer heißt alle Foto-Kanäle zugleich.</summary>
        [Display(Name = "Einwilligung")] public Guid? ArtId { get; set; }
        /// <summary>Erteilt oder abgelehnt.</summary>
        [Display(Name = "Erklärung")] public bool Erteilt { get; set; } = true;
        /// <summary>Tag der Unterschrift.</summary>
        [Required(ErrorMessage = "Bitte das Datum angeben."), Display(Name = "unterschrieben am")] public DateOnly? Am { get; set; }
        /// <summary>Wer erklärt hat.</summary>
        [Display(Name = "erklärt von")] public EinwilligungDurch Durch { get; set; }
        /// <summary>Name der erklärenden Person.</summary>
        [StringLength(200), Display(Name = "Name")] public string? DurchName { get; set; }
        /// <summary>Ablageort des Papiers.</summary>
        [StringLength(200), Display(Name = "Ablage des Papiers")] public string? Nachweis { get; set; }
        /// <summary>Der Jugendliche hat mit unterschrieben.</summary>
        [Display(Name = "Jugendliche Person hat selbst zugestimmt")] public bool JugendlicherZugestimmt { get; set; }
    }

    /// <summary>Das Mitglied.</summary>
    public Mitglied Mitglied { get; private set; } = null!;
    /// <summary>Heutiger Tag in Ortszeit.</summary>
    public DateOnly Heute => Anzeige.Heute(zeit);
    /// <summary>Stand je Art.</summary>
    public IReadOnlyList<Einwilligungsregister.Stand> Stand { get; private set; } = [];
    /// <summary>Alle Erklärungen, neueste zuerst.</summary>
    public IReadOnlyList<Einwilligung> Verlauf { get; private set; } = [];
    /// <summary>Darf Erklärungen eintragen und widerrufen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Das Mitglied ist minderjährig.</summary>
    public bool Minderjaehrig { get; private set; }
    /// <summary>Das Mitglied muss selbst mitentscheiden (Alter ab dem Parameter, minderjährig).</summary>
    public bool Mitentscheidung { get; private set; }
    /// <summary>Namen der Erziehungsberechtigten als Vorschlag.</summary>
    public IReadOnlyList<string> Eltern { get; private set; } = [];
    /// <summary>Auswahl der Arten, oben „alle Foto-Kanäle“.</summary>
    public IReadOnlyList<SelectListItem> Arten { get; private set; } = [];

    /// <summary>Formular für eine neue Erklärung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Zeigt das Register.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Am = Heute,
            Durch = Minderjaehrig ? EinwilligungDurch.Erziehungsberechtigter : EinwilligungDurch.Mitglied,
            DurchName = Minderjaehrig ? Eltern.FirstOrDefault() : Mitglied.Anzeigename,
        };
        return Page();
    }

    /// <summary>Trägt eine Erklärung auf Papier ein; ohne Art für alle Foto-Kanäle.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostEintragenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        if (Formular.Am > Heute)
        {
            ModelState.AddModelError("Formular.Am", "Das Datum darf nicht in der Zukunft liegen.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var arten = Formular.ArtId is { } artId
            ? Stand.Where(s => s.Art.Id == artId).Select(s => s.Art.Id).ToList()
            : Stand.Where(s => s.Art.Foto).Select(s => s.Art.Id).ToList();
        if (arten.Count == 0)
        {
            return NotFound();
        }

        foreach (var art in arten)
        {
            await register.EintragenAsync(new Einwilligung
            {
                MitgliedId = id,
                EinwilligungsartId = art,
                Erteilt = Formular.Erteilt,
                Am = Formular.Am!.Value,
                Durch = Formular.Durch,
                DurchName = Formular.DurchName?.Trim() is { Length: > 0 } name ? name : null,
                Weg = EinwilligungWeg.Papier,
                Nachweis = Formular.Nachweis?.Trim() is { Length: > 0 } ablage ? ablage : null,
                JugendlicherZugestimmt = Mitentscheidung ? Formular.JugendlicherZugestimmt : null,
            }, ct);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = arten.Count == 1 ? "Erklärung eingetragen." : $"Erklärung für {arten.Count} Foto-Kanäle eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Trägt den Widerruf einer Art ab heute ein.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="artId">Die Art.</param>
    /// <param name="durch">Wer widerrufen hat.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostWiderrufenAsync(Guid id, Guid artId, string? durch, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        var ok = await register.WiderrufenAsync(id, artId, Heute, durch?.Trim() is { Length: > 0 } name ? name : "Mitteilung an die Verwaltung", ct);
        await db.SaveChangesAsync(ct);
        TempData[ok ? "Meldung" : "Fehler"] = ok ? "Widerruf eingetragen; er gilt ab heute." : "Es gibt keine wirksame Einwilligung zum Widerrufen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Vermerkt die Zustimmung des Jugendlichen zur Erklärung der Eltern.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="artId">Die Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostZustimmenAsync(Guid id, Guid artId, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        var ok = await register.ZustimmenAsync(id, artId, ct);
        await db.SaveChangesAsync(ct);
        TempData[ok ? "Meldung" : "Fehler"] = ok ? "Zustimmung eingetragen." : "Es gibt keine Erklärung der Eltern, der zugestimmt werden kann.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var sichtbar = await datenbereich.Filtern(db.Mitglieder, User, Heute);
        var mitglied = await sichtbar.AsNoTracking().Include(m => m.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mitglied is null)
        {
            return false;
        }

        Mitglied = mitglied;
        var jugend = mitglied.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.Abteilung!.Art is AbteilungArt.Jugend or AbteilungArt.Kinder
            && s.GueltigAb <= Heute && (s.GueltigBis == null || s.GueltigBis >= Heute));
        var grenzen = await register.GrenzenAsync(Heute, ct);
        var alter = mitglied.AlterAm(Heute);
        Minderjaehrig = alter < grenzen.VolljaehrigAb;
        Mitentscheidung = Minderjaehrig && alter >= grenzen.MitentscheidungAb;
        Stand = await register.StandAsync(mitglied, jugend || Minderjaehrig, Heute, ct);
        Verlauf = await register.VerlaufAsync(id, ct);
        DarfBearbeiten = await (await datenbereich.FilternBearbeitenAsync(db.Mitglieder, User, Heute, ct)).AnyAsync(m => m.Id == id, ct);
        Eltern = Minderjaehrig
            ? await db.Erziehungsberechtigte.AsNoTracking().Where(e => e.MitgliedId == id).OrderBy(e => e.Nummer)
                .Select(e => e.Vorname + " " + e.Nachname).ToListAsync(ct)
            : [];
        Arten =
        [
            new SelectListItem("alle Foto-Kanäle", ""),
            .. Stand.Select(s => new SelectListItem(s.Art.Bezeichnung, s.Art.Id.ToString())),
        ];
        return true;
    }
}
