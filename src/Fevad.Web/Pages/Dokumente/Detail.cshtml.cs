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

[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class DetailModel(AppDbContext db, Dokumentablage ablage, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public Dokument? Dokument { get; private set; }
    public DokumentVersion? Aktuell => Dokument?.AktuelleVersion;
    public string? Kategorie { get; private set; }
    public bool DarfVerwalten { get; private set; }
    public DateTimeOffset? MeineBestaetigung { get; private set; }
    public IReadOnlyList<Dokumentablage.Leser> Stand { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];

    [BindProperty] public NeuModel.Eingabe Formular { get; set; } = new();
    [BindProperty, Display(Name = "Datei")] public IFormFile? Datei { get; set; }
    [BindProperty, StringLength(500), Display(Name = "Was hat sich geändert?")] public string? Aenderungshinweis { get; set; }

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
