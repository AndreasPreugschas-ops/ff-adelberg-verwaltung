using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Einsatztagebuch, Einsatzleitung und Einsatzabschnitte (V14), für das Handy gebaut: auch live während des Einsatzes führbar (Andi,
/// 25.10.2026). Lesen mit Einsätze lesen, schreiben mit Einsätze bearbeiten, solange der Bericht nicht freigegeben ist.
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeLesen)]
public class TagebuchModel(AppDbContext db, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Der Einsatz mit Tagebuch, Leitung und Abschnitten.</summary>
    public Einsatz Einsatz { get; private set; } = null!;
    /// <summary>Darf schreiben.</summary>
    public bool DarfSchreiben { get; private set; }
    /// <summary>Auswahl der Mitglieder für Einsatzleiter und Abschnittsleiter.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Neuer Tagebucheintrag.</summary>
    [BindProperty]
    public Eintragsformular Eintrag { get; set; } = new();

    /// <summary>Neue Übergabe der Einsatzleitung.</summary>
    [BindProperty]
    public Leitungsformular Leitung { get; set; } = new();

    /// <summary>Neuer Einsatzabschnitt.</summary>
    [BindProperty]
    public Abschnittsformular Abschnitt { get; set; } = new();

    /// <summary>Führungsstufe.</summary>
    [BindProperty]
    public Fuehrungsstufe? Stufe { get; set; }

    /// <summary>Formular eines Tagebucheintrags.</summary>
    public sealed class Eintragsformular
    {
        /// <summary>Uhrzeit „HH:MM“ oder „TT.MM.JJJJ HH:MM“; leer = jetzt.</summary>
        public string? Zeit { get; set; }
        /// <summary>Art des Eintrags.</summary>
        public TagebuchArt Art { get; set; } = TagebuchArt.Lagemeldung;
        /// <summary>Inhalt.</summary>
        [StringLength(2000)] public string? Text { get; set; }
    }

    /// <summary>Formular einer Übergabe der Einsatzleitung.</summary>
    public sealed class Leitungsformular
    {
        /// <summary>Uhrzeit der Übernahme; leer = jetzt.</summary>
        public string? Zeit { get; set; }
        /// <summary>Einsatzleiter aus der Feuerwehr.</summary>
        public Guid? MitgliedId { get; set; }
        /// <summary>Externer Einsatzleiter.</summary>
        [StringLength(200)] public string? Extern { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500)] public string? Bemerkung { get; set; }
    }

    /// <summary>Formular eines Einsatzabschnitts.</summary>
    public sealed class Abschnittsformular
    {
        /// <summary>Bezeichnung.</summary>
        [StringLength(100)] public string? Bezeichnung { get; set; }
        /// <summary>Abschnittsleiter aus der Feuerwehr.</summary>
        public Guid? LeiterId { get; set; }
        /// <summary>Externer Abschnittsleiter.</summary>
        [StringLength(200)] public string? LeiterExtern { get; set; }
        /// <summary>Auftrag.</summary>
        [StringLength(500)] public string? Auftrag { get; set; }
    }

    /// <summary>Zeigt Tagebuch, Leitung und Abschnitte.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Stufe = Einsatz.Fuehrungsstufe;
        return Page();
    }

    /// <summary>Legt einen Tagebucheintrag an.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostEintragAsync(Guid id, CancellationToken ct)
    {
        if (await SchreibenPruefenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (string.IsNullOrWhiteSpace(Eintrag.Text))
        {
            TempData["Fehler"] = "Bitte einen Text eingeben.";
            return RedirectToPage(new { id });
        }

        if (!ZeitLesen(Eintrag.Zeit, out var zeitpunkt))
        {
            return RedirectToPage(new { id });
        }

        db.Tagebucheintraege.Add(new Tagebucheintrag
        {
            EinsatzId = id, Zeitpunkt = zeitpunkt, Art = Eintrag.Art, Text = Eintrag.Text.Trim(), VerfasstVon = benutzer.Id,
            Verfasser = User.Identity?.Name,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Zeit(zeitpunkt)} Uhr eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Trägt eine Übergabe der Einsatzleitung ein; der erste eigene Einsatzleiter wird in den Bericht übernommen.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostLeitungAsync(Guid id, CancellationToken ct)
    {
        if (await SchreibenPruefenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var extern_ = string.IsNullOrWhiteSpace(Leitung.Extern) ? null : Leitung.Extern.Trim();
        if (Leitung.MitgliedId is null == (extern_ is null))
        {
            TempData["Fehler"] = "Bitte entweder ein Mitglied wählen oder einen externen Einsatzleiter eintragen.";
            return RedirectToPage(new { id });
        }

        if (!ZeitLesen(Leitung.Zeit, out var ab))
        {
            return RedirectToPage(new { id });
        }

        var leitung = new Einsatzleitung
        {
            EinsatzId = id, Ab = ab, MitgliedId = Leitung.MitgliedId, Extern = extern_,
            Bemerkung = string.IsNullOrWhiteSpace(Leitung.Bemerkung) ? null : Leitung.Bemerkung.Trim(),
        };
        db.Einsatzleitungen.Add(leitung);
        var name = Leitung.MitgliedId is { } mid
            ? await db.Mitglieder.Where(m => m.Id == mid).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct)
            : extern_;
        db.Tagebucheintraege.Add(new Tagebucheintrag
        {
            EinsatzId = id, Zeitpunkt = ab, Art = TagebuchArt.Fuehrung, Text = $"Einsatzleitung übernimmt {name}.", VerfasstVon = benutzer.Id,
            Verfasser = User.Identity?.Name,
        });
        await db.SaveChangesAsync(ct);
        await EinsatzleiterAbgleichenAsync(id, ct);
        TempData["Meldung"] = "Einsatzleitung eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Legt einen Einsatzabschnitt an.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostAbschnittAsync(Guid id, CancellationToken ct)
    {
        if (await SchreibenPruefenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (string.IsNullOrWhiteSpace(Abschnitt.Bezeichnung))
        {
            TempData["Fehler"] = "Bitte den Abschnitt benennen.";
            return RedirectToPage(new { id });
        }

        db.Einsatzabschnitte.Add(new Einsatzabschnitt
        {
            EinsatzId = id, Bezeichnung = Abschnitt.Bezeichnung.Trim(), LeiterId = Abschnitt.LeiterId,
            LeiterExtern = string.IsNullOrWhiteSpace(Abschnitt.LeiterExtern) ? null : Abschnitt.LeiterExtern.Trim(),
            Auftrag = string.IsNullOrWhiteSpace(Abschnitt.Auftrag) ? null : Abschnitt.Auftrag.Trim(),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Abschnitt angelegt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Speichert die Führungsstufe.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostStufeAsync(Guid id, CancellationToken ct)
    {
        if (await SchreibenPruefenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var einsatz = await db.Einsaetze.FirstAsync(e => e.Id == id, ct);
        einsatz.Fuehrungsstufe = Stufe;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Führungsstufe gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Entfernt einen Eintrag, eine Übergabe oder einen Abschnitt (protokolliert).</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="was">„eintrag“, „leitung“ oder „abschnitt“.</param>
    /// <param name="eintragId">Kennung des Datensatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostEntfernenAsync(Guid id, string was, Guid eintragId, CancellationToken ct)
    {
        if (await SchreibenPruefenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        switch (was)
        {
            case "eintrag":
                await db.Tagebucheintraege.Where(t => t.EinsatzId == id && t.Id == eintragId).ForEachAsync(t => db.Remove(t), ct);
                break;
            case "leitung":
                await db.Einsatzleitungen.Where(t => t.EinsatzId == id && t.Id == eintragId).ForEachAsync(t => db.Remove(t), ct);
                break;
            case "abschnitt":
                await db.Einsatzabschnitte.Where(t => t.EinsatzId == id && t.Id == eintragId).ForEachAsync(t => db.Remove(t), ct);
                break;
            default:
                return BadRequest();
        }

        await db.SaveChangesAsync(ct);
        if (was == "leitung")
        {
            await EinsatzleiterAbgleichenAsync(id, ct);
        }

        TempData["Meldung"] = "Entfernt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Übernimmt den ersten eigenen Einsatzleiter aus dem Verlauf in den Bericht, wenn der Verlauf einen hat.</summary>
    private async Task EinsatzleiterAbgleichenAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.Include(e => e.Einsatzleitungen).FirstAsync(e => e.Id == id, ct);
        if (Einsatzfuehrung.ErsterEigener(einsatz.Einsatzleitungen) is { } erster && einsatz.EinsatzleiterId != erster)
        {
            einsatz.EinsatzleiterId = erster;
            await db.SaveChangesAsync(ct);
        }
    }

    private bool ZeitLesen(string? text, out DateTimeOffset zeitpunkt)
    {
        zeitpunkt = zeit.GetUtcNow();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // Nur Uhrzeit: der Tag der Alarmierung, über Mitternacht hinaus der Folgetag.
        if (!ZeitEingabe.TryLesen(text, Einsatz.Alarmiert, out var wert) || wert is null)
        {
            TempData["Fehler"] = "Zeit im Format HH:MM oder TT.MM.JJJJ HH:MM angeben.";
            return false;
        }

        zeitpunkt = wert.Value;
        return true;
    }

    private async Task<IActionResult?> SchreibenPruefenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfSchreiben)
        {
            return Forbid();
        }

        return null;
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.AsNoTracking()
            .Include(e => e.Tagebuch)
            .Include(e => e.Einsatzleitungen).ThenInclude(l => l.Mitglied)
            .Include(e => e.Abschnitte).ThenInclude(a => a.Leiter)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return false;
        }

        Einsatz = einsatz;
        DarfSchreiben = einsatz.TagebuchOffen && (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded;
        if (DarfSchreiben)
        {
            var tag = Anzeige.LokalesDatum(einsatz.Alarmiert);
            Mitglieder = await Datenbereich.OhneJugendpersonal(db.Mitglieder.AsNoTracking(), tag)
                .Where(m => m.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)))
                .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
                .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString())).ToListAsync(ct);
        }

        return true;
    }
}
