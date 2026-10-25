using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Ausgabe an ein Mitglied. Größen kommen aus dem Größenprofil, Einzelstücke werden per Inventarnummer gewählt.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerBearbeiten)]
public class AusgabeModel(AppDbContext db, Kleiderkammerbuchungen buchungen) : PageModel
{
    /// <summary>Kennung des Mitglieds.</summary>
    public Guid Id { get; private set; }
    /// <summary>Name des Mitglieds.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Auszugebende Positionen je Artikel.</summary>
    [BindProperty]
    public List<Eingabe> Positionen { get; set; } = [];

    /// <summary>Bemerkung zur Ausgabe, erscheint auf der Quittung.</summary>
    [BindProperty]
    public string? Bemerkung { get; set; }

    /// <summary>Aktive Artikel mit Größenvorschlag, Lagerbestand und passenden Einzelstücken.</summary>
    public IReadOnlyList<ArtikelAuswahl> Artikel { get; private set; } = [];

    /// <summary>Eine Ausgabeposition.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung des Artikels.</summary>
        public Guid ArtikelId { get; set; }
        /// <summary>Ausgegebene Größe.</summary>
        public string? Groesse { get; set; }
        /// <summary>Stückzahl; 0 gibt nichts aus.</summary>
        public int Menge { get; set; }
        /// <summary>Gewähltes Einzelstück bei Artikeln mit Inventarnummer.</summary>
        public Guid? GeraetId { get; set; }
    }

    /// <summary>Ein Einzelstück im Lager.</summary>
    /// <param name="Id">Kennung des Geräts.</param>
    /// <param name="Inventarnummer">Inventarnummer.</param>
    /// <param name="Groesse">Größe des Stücks.</param>
    /// <param name="Passend">Passt zur Größe im Größenprofil des Mitglieds.</param>
    public sealed record Stueck(Guid Id, string Inventarnummer, string? Groesse, bool Passend);

    /// <summary>Ein Artikel mit allem, was die Ausgabe braucht.</summary>
    /// <param name="Id">Kennung des Artikels.</param>
    /// <param name="Bezeichnung">Bezeichnung des Artikels.</param>
    /// <param name="Groessenart">Nach welcher Größe der Artikel ausgegeben wird.</param>
    /// <param name="Einzelstueck">Der Artikel wird als Einzelstück mit Inventarnummer geführt.</param>
    /// <param name="Vorschlag">Größe aus dem Größenprofil des Mitglieds.</param>
    /// <param name="Lager">Lagerbestand je Größe.</param>
    /// <param name="Stuecke">Einzelstücke im Lager, passende zuerst.</param>
    /// <param name="SchonAusgegeben">Menge, die das Mitglied von diesem Artikel schon hat.</param>
    public sealed record ArtikelAuswahl(Guid Id, string Bezeichnung, Groessenart Groessenart, bool Einzelstueck, string? Vorschlag,
        IReadOnlyDictionary<string, int> Lager, IReadOnlyList<Stueck> Stuecke, int SchonAusgegeben);

    /// <summary>Zeigt das Ausgabeformular.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Bucht die Ausgabe und öffnet die Quittung.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Quittung oder die Seite mit Fehlern; 404, wenn das Mitglied fehlt.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var positionen = Positionen
            .Where(p => p.GeraetId is not null || p.Menge > 0)
            .Select(p => new AusgabePosition(p.ArtikelId, p.Groesse ?? "", p.GeraetId is null ? p.Menge : 1, p.GeraetId))
            .ToList();
        try
        {
            var vorgang = await buchungen.AusgebenAsync(id, positionen, Bemerkung, ct);
            TempData["Meldung"] = $"Ausgabe {vorgang.Nummer} gebucht. Bitte Quittung drucken und unterschreiben lassen.";
            return RedirectToPage("Quittung", new { id = vorgang.Id });
        }
        catch (KleiderkammerFehler fehler)
        {
            ModelState.AddModelError("", fehler.Message);
            return await LadenAsync(id, ct) ? Page() : NotFound();
        }
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var mitglied = await db.Mitglieder.AsNoTracking().Where(m => m.Id == id).Select(m => new { m.Nachname, m.Vorname }).FirstOrDefaultAsync(ct);
        if (mitglied is null)
        {
            return false;
        }

        Id = id;
        Name = $"{mitglied.Nachname}, {mitglied.Vorname}";

        var groessen = await db.Groessenangaben.AsNoTracking().Where(g => g.MitgliedId == id).ToDictionaryAsync(g => g.Groessenart, g => g.Wert, ct);
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.Aktiv)
            .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung)
            .Select(a => new { a.Id, a.Bezeichnung, a.Groessenart, a.GeraeteartId })
            .ToListAsync(ct);
        var lager = await db.Lagerbestaende.AsNoTracking().Where(l => l.Menge > 0).Select(l => new { l.ArtikelId, l.Groesse, l.Menge }).ToListAsync(ct);
        var artIds = artikel.Where(a => a.GeraeteartId != null).Select(a => a.GeraeteartId!.Value).ToList();
        var stuecke = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && artIds.Contains(g.GeraeteartId) && !db.Ausstattungen.Any(a => a.GeraetId == g.Id && a.ZurueckAm == null))
            .OrderBy(g => g.Inventarnummer)
            .Select(g => new { g.Id, g.GeraeteartId, g.Inventarnummer, g.Groesse })
            .ToListAsync(ct);
        var hat = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.MitgliedId == id && a.ZurueckAm == null)
            .GroupBy(a => a.ArtikelId)
            .Select(g => new { g.Key, Menge = g.Sum(a => a.Menge) })
            .ToDictionaryAsync(g => g.Key, g => g.Menge, ct);

        Artikel = artikel
            .Select(a =>
            {
                var vorschlag = a.Groessenart == Groessenart.Einheitsgroesse ? null : groessen.GetValueOrDefault(a.Groessenart);
                var passende = stuecke
                    .Where(s => s.GeraeteartId == a.GeraeteartId)
                    .Select(s => new Stueck(s.Id, s.Inventarnummer, s.Groesse, vorschlag is not null && s.Groesse == vorschlag))
                    .OrderByDescending(s => s.Passend).ThenBy(s => s.Inventarnummer)
                    .ToList();
                return new ArtikelAuswahl(a.Id, a.Bezeichnung, a.Groessenart, a.GeraeteartId is not null, vorschlag,
                    lager.Where(l => l.ArtikelId == a.Id).ToDictionary(l => l.Groesse, l => l.Menge), passende, hat.GetValueOrDefault(a.Id));
            })
            .ToList();

        if (Positionen.Count != Artikel.Count)
        {
            Positionen = Artikel.Select(a => new Eingabe { ArtikelId = a.Id, Groesse = a.Vorschlag }).ToList();
        }

        return true;
    }
}
