using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dokumente;

/// <summary>Ein Dokument mit Versionen, Lesebestätigung und Verwaltung (neue Version, Angaben, Aufheben, Löschen).</summary>
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class DetailModel(AppDbContext db, Dokumentablage ablage, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Das angezeigte Dokument.</summary>
    public Dokument? Dokument { get; private set; }
    /// <summary>Die aktuelle Version des Dokuments.</summary>
    public DokumentVersion? Aktuell => Dokument?.AktuelleVersion;
    /// <summary>Bezeichnung der Kategorie.</summary>
    public string? Kategorie { get; private set; }
    /// <summary>Der Benutzer darf das Dokument verwalten.</summary>
    public bool DarfVerwalten { get; private set; }
    /// <summary>Der Benutzer darf Versionen endgültig löschen.</summary>
    public bool DarfEndgueltigLoeschen { get; private set; }
    /// <summary>Zeitpunkt der eigenen Lesebestätigung der aktuellen Version.</summary>
    public DateTimeOffset? MeineBestaetigung { get; private set; }
    /// <summary>Wer die aktuelle Version bestätigt hat und wer noch nicht.</summary>
    public IReadOnlyList<Dokumentablage.Leser> Stand { get; private set; } = [];
    /// <summary>Kategorien zur Auswahl beim Ändern der Angaben.</summary>
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];

    /// <summary>Formulardaten der Dokumentangaben.</summary>
    [BindProperty] public NeuModel.Eingabe Formular { get; set; } = new();
    /// <summary>Hochgeladene Datei für eine neue Version.</summary>
    [BindProperty, Display(Name = "Datei")] public IFormFile? Datei { get; set; }
    /// <summary>Änderungshinweis zur neuen Version.</summary>
    [BindProperty, StringLength(500), Display(Name = "Was hat sich geändert?")] public string? Aenderungshinweis { get; set; }

    /// <summary>Zeigt das Dokument.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Formular = new NeuModel.Eingabe
        {
            Titel = Dokument!.Titel,
            Beschreibung = Dokument.Beschreibung,
            KategorieId = Dokument.KategorieId,
            Sichtbarkeit = Dokument.Sichtbarkeit,
            LesebestaetigungErforderlich = Dokument.LesebestaetigungErforderlich,
            GueltigAb = Dokument.GueltigAb,
        };
        return Page();
    }

    /// <summary>Bestätigt das Lesen der aktuellen Version für den angemeldeten Benutzer.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Dokument oder 404.</returns>
    public async Task<IActionResult> OnPostBestaetigenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Aktuell is null)
        {
            return NotFound();
        }

        if (benutzer.Id is not { } ich)
        {
            return Forbid();
        }

        await ablage.BestaetigenAsync(Aktuell.Id, ich, ct);
        TempData["Meldung"] = $"Lesebestätigung für Version {Aktuell.Nummer} gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Lädt eine neue Version des Dokuments hoch.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Dokument, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostVersionAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfVerwalten)
        {
            return Forbid();
        }

        // Nur die Felder für die neue Version prüfen, nicht das Metadaten-Formular.
        foreach (var schluessel in ModelState.Keys.Where(k => k is not (nameof(Datei) or nameof(Aenderungshinweis))).ToList())
        {
            ModelState.Remove(schluessel);
        }

        if (Datei is null)
        {
            ModelState.AddModelError(nameof(Datei), "Bitte eine Datei auswählen.");
        }
        else if (await ablage.PruefenAsync(Datei.FileName, Datei.Length, ct) is { } fehler)
        {
            ModelState.AddModelError(nameof(Datei), fehler);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await using (var inhalt = Datei!.OpenReadStream())
        {
            var version = await ablage.VersionHinzufuegenAsync(Dokument!, Datei.FileName, inhalt, Aenderungshinweis, ct);
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = Dokument!.LesebestaetigungErforderlich
                ? $"Version {version.Nummer} hochgeladen. Alle Leser müssen sie neu bestätigen."
                : $"Version {version.Nummer} hochgeladen.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Speichert Titel, Beschreibung, Kategorie, Sichtbarkeit und Gültigkeit.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Dokument, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostMetadatenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfVerwalten)
        {
            return Forbid();
        }

        foreach (var schluessel in ModelState.Keys.Where(k => !k.StartsWith("Formular.", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(schluessel);
        }

        if (Formular.KategorieId is { } k && !await db.Dokumentkategorien.AnyAsync(x => x.Id == k, ct))
        {
            ModelState.AddModelError("Formular.KategorieId", "Unbekannte Kategorie.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Dokument!.Titel = Formular.Titel.Trim();
        Dokument.Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim();
        Dokument.KategorieId = Formular.KategorieId!.Value;
        Dokument.Sichtbarkeit = Formular.Sichtbarkeit;
        Dokument.LesebestaetigungErforderlich = Formular.LesebestaetigungErforderlich;
        Dokument.GueltigAb = Formular.GueltigAb;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Angaben gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Hebt das Dokument zum heutigen Tag auf oder setzt es wieder in Kraft.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="aufheben"><c>true</c> hebt auf, <c>false</c> setzt wieder in Kraft.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Dokument, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAufhebenAsync(Guid id, bool aufheben, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfVerwalten)
        {
            return Forbid();
        }

        Dokument!.AufgehobenAm = aufheben ? Anzeige.Heute(zeit) : null;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = aufheben ? "Dokument aufgehoben. Es bleibt unter „aufgehobene zeigen“ abrufbar." : "Dokument gilt wieder.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht alle älteren Versionen samt Dateien und Lesebestätigungen endgültig.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="bestaetigt">Der Benutzer hat das Löschen ausdrücklich bestätigt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Dokument, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAlteVersionenLoeschenAsync(Guid id, bool bestaetigt, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfEndgueltigLoeschen)
        {
            return Forbid();
        }

        if (!bestaetigt)
        {
            TempData["Meldung"] = "Bitte das Löschen der alten Versionen ausdrücklich bestätigen.";
            return RedirectToPage(new { id });
        }

        var anzahl = await ablage.AlteVersionenLoeschenAsync(id, benutzer.Id, ct);
        TempData["Meldung"] = $"{anzahl} alte Version(en) samt Dateien und Lesebestätigungen endgültig gelöscht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht das Dokument mit allen Versionen endgültig.</summary>
    /// <param name="id">Kennung des Dokuments.</param>
    /// <param name="bestaetigt">Der Benutzer hat das Löschen ausdrücklich bestätigt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste oder zum Dokument, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEndgueltigLoeschenAsync(Guid id, bool bestaetigt, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfEndgueltigLoeschen)
        {
            return Forbid();
        }

        if (!bestaetigt)
        {
            TempData["Meldung"] = "Bitte das endgültige Löschen ausdrücklich bestätigen.";
            return RedirectToPage(new { id });
        }

        var titel = Dokument!.Titel;
        await ablage.EndgueltigLoeschenAsync(id, benutzer.Id, ct);
        TempData["Meldung"] = $"„{titel}“ mit allen Versionen endgültig gelöscht.";
        return RedirectToPage("Index");
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var vertraulich = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVertraulichLesen)).Succeeded;
        Dokument = await db.Dokumente.Include(d => d.Versionen).Include(d => d.Kategorie)
            .FirstOrDefaultAsync(d => d.Id == id && (vertraulich || d.Sichtbarkeit == DokumentSichtbarkeit.Alle), ct);
        if (Dokument is null)
        {
            return false;
        }

        Kategorie = Dokument.Kategorie?.Bezeichnung;
        DarfVerwalten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVerwalten)).Succeeded;
        DarfEndgueltigLoeschen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteEndgueltigLoeschen)).Succeeded;
        Kategorien = await db.Dokumentkategorien.AsNoTracking().Where(k => k.Aktiv || k.Id == Dokument.KategorieId).OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);

        if (Aktuell is { } aktuell)
        {
            var ich = benutzer.Id;
            MeineBestaetigung = await db.Lesebestaetigungen.AsNoTracking()
                .Where(l => l.DokumentVersionId == aktuell.Id && l.BenutzerId == ich)
                .Select(l => (DateTimeOffset?)l.BestaetigtAm)
                .FirstOrDefaultAsync(ct);

            if (DarfVerwalten && Dokument.LesebestaetigungErforderlich)
            {
                Stand = await ablage.BestaetigungsstandAsync(aktuell.Id, Dokument.Sichtbarkeit, ct);
            }
        }

        return true;
    }
}
