using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dokumente;

/// <summary>Liste der Dokumente mit Suche, Kategoriefilter und offenen Lesebestätigungen.</summary>
public class IndexModel(AppDbContext db, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Suchtext in Titel und Beschreibung.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    /// <summary>Filter auf eine Kategorie.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Kategorie { get; set; }
    /// <summary>Auch aufgehobene Dokumente zeigen.</summary>
    [BindProperty(SupportsGet = true)] public bool Aufgehobene { get; set; }

    /// <summary>Die gefundenen Dokumente.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Kategorien für den Filter.</summary>
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];
    /// <summary>Der Benutzer darf Dokumente verwalten.</summary>
    public bool DarfVerwalten { get; private set; }
    /// <summary>Anzahl der Dokumente, deren Lesebestätigung der Benutzer noch schuldet.</summary>
    public int OffeneBestaetigungen => Zeilen.Count(z => z.BestaetigungOffen);

    /// <summary>Ein Dokument in der Liste.</summary>
    /// <param name="Id">Kennung des Dokuments.</param>
    /// <param name="Titel">Titel des Dokuments.</param>
    /// <param name="Kategorie">Bezeichnung der Kategorie.</param>
    /// <param name="Sichtbarkeit">Für wen das Dokument sichtbar ist.</param>
    /// <param name="Version">Nummer der aktuellen Version.</param>
    /// <param name="Stand">Hochladezeitpunkt der aktuellen Version (UTC).</param>
    /// <param name="AufgehobenAm">Tag der Aufhebung, falls aufgehoben.</param>
    /// <param name="BestaetigungOffen">Der Benutzer muss die aktuelle Version noch bestätigen.</param>
    public sealed record Zeile(Guid Id, string Titel, string Kategorie, DokumentSichtbarkeit Sichtbarkeit, int? Version, DateTimeOffset? Stand, DateOnly? AufgehobenAm, bool BestaetigungOffen);

    /// <summary>Lädt die Dokumente nach Filter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfVerwalten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVerwalten)).Succeeded;
        var vertraulich = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVertraulichLesen)).Succeeded;
        var ich = benutzer.Id;

        Kategorien = await db.Dokumentkategorien.AsNoTracking().Where(k => k.Aktiv).OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);

        var abfrage = db.Dokumente.AsNoTracking()
            .Where(d => vertraulich || d.Sichtbarkeit == DokumentSichtbarkeit.Alle)
            .Where(d => Aufgehobene ? d.AufgehobenAm != null : d.AufgehobenAm == null);
        if (Kategorie is { } kategorie)
        {
            abfrage = abfrage.Where(d => d.KategorieId == kategorie);
        }

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var muster = $"%{Suche.Trim()}%";
            abfrage = abfrage.Where(d => EF.Functions.ILike(d.Titel, muster) || (d.Beschreibung != null && EF.Functions.ILike(d.Beschreibung, muster)));
        }

        Zeilen = await abfrage
            .OrderBy(d => d.Kategorie!.Sortierung).ThenBy(d => d.Titel)
            .Select(d => new
            {
                d.Id,
                d.Titel,
                Kategorie = d.Kategorie!.Bezeichnung,
                d.Sichtbarkeit,
                d.LesebestaetigungErforderlich,
                d.AufgehobenAm,
                Aktuell = d.Versionen.OrderByDescending(v => v.Nummer)
                    .Select(v => new { v.Nummer, v.ErstelltAm, Bestaetigt = v.Lesebestaetigungen.Any(l => l.BenutzerId == ich) })
                    .FirstOrDefault(),
            })
            .Take(500)
            .Select(d => new Zeile(d.Id, d.Titel, d.Kategorie, d.Sichtbarkeit, d.Aktuell == null ? null : d.Aktuell.Nummer, d.Aktuell == null ? null : d.Aktuell.ErstelltAm,
                d.AufgehobenAm, d.LesebestaetigungErforderlich && d.AufgehobenAm == null && d.Aktuell != null && !d.Aktuell.Bestaetigt))
            .ToListAsync(ct);
    }
}
