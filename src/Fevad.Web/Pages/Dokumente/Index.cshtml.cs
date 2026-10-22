using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dokumente;

public class IndexModel(AppDbContext db, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Kategorie { get; set; }
    [BindProperty(SupportsGet = true)] public bool Aufgehobene { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];
    public bool DarfVerwalten { get; private set; }
    public int OffeneBestaetigungen => Zeilen.Count(z => z.BestaetigungOffen);

    public sealed record Zeile(Guid Id, string Titel, string Kategorie, DokumentSichtbarkeit Sichtbarkeit, int? Version, DateTimeOffset? Stand, DateOnly? AufgehobenAm, bool BestaetigungOffen);

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
