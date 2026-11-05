using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>
/// Ausrüstung eines Mitglieds: Schutzausrüstung mit Inventarnummer und Fristen, Kleidung, Größen und Quittungen.
/// Die Rücknahme hat eine eigene Seite, damit die Übersicht schmal bleibt und nichts abgeschnitten wird.
/// </summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class MitgliedModel(AppDbContext db, Kleiderkammerbuchungen buchungen, Kleiderkammerstand stand, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Kennung des Mitglieds.</summary>
    public Guid Id { get; private set; }
    /// <summary>Name des Mitglieds.</summary>
    public string Name { get; private set; } = "";
    /// <summary>Personalnummer des Mitglieds.</summary>
    public string Personalnummer { get; private set; } = "";
    /// <summary>Der Benutzer darf Größen, Ausgaben und Rücknahmen buchen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Der Benutzer darf Einzelstücke in der Technik öffnen.</summary>
    public bool DarfTechnik { get; private set; }

    /// <summary>Größenprofil je Größenart.</summary>
    [BindProperty]
    public Dictionary<Groessenart, string?> Groessen { get; set; } = [];

    /// <summary>Aktuelle Ausstattung des Mitglieds.</summary>
    public IReadOnlyList<AusruestungStueck> Ausstattung { get; private set; } = [];
    /// <summary>Ausgaben und Rücknahmen des Mitglieds, neueste zuerst.</summary>
    public IReadOnlyList<Kleiderkammervorgang> Vorgaenge { get; private set; } = [];

    /// <summary>Einzelstücke mit Inventarnummer.</summary>
    public IEnumerable<AusruestungStueck> Einzelstuecke => Ausstattung.Where(a => a.Einzelstueck);
    /// <summary>Kleidung und andere Stücke ohne Inventarnummer.</summary>
    public IEnumerable<AusruestungStueck> Kleidung => Ausstattung.Where(a => !a.Einzelstueck);

    /// <summary>Zeigt Größen, Ausstattung und Vorgänge des Mitglieds.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Speichert das Größenprofil.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 403.</returns>
    public async Task<IActionResult> OnPostGroessenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.KleiderkammerBearbeiten))
        {
            return Forbid();
        }

        await buchungen.GroessenSetzenAsync(id, Groessen, ct);
        TempData["Meldung"] = "Größen gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Prüft, ob der angemeldete Benutzer die Policy erfüllt.</summary>
    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    /// <summary>Lädt die Daten der Seite; false, wenn der Datensatz fehlt oder nicht gelesen werden darf.</summary>
    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var mitglied = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Personalnummer })
            .FirstOrDefaultAsync(ct);
        if (mitglied is null)
        {
            return false;
        }

        Id = mitglied.Id;
        Name = $"{mitglied.Nachname}, {mitglied.Vorname}";
        Personalnummer = mitglied.Personalnummer;
        DarfBearbeiten = await DarfAsync(Berechtigungen.KleiderkammerBearbeiten);
        DarfTechnik = await DarfAsync(Berechtigungen.TechnikLesen);

        Groessen = await db.Groessenangaben.AsNoTracking()
            .Where(g => g.MitgliedId == id)
            .ToDictionaryAsync(g => g.Groessenart, g => (string?)g.Wert, ct);
        Ausstattung = await Ausruestung.LadenAsync(db, stand, id, Anzeige.Heute(zeit), ct);
        Vorgaenge = await db.Kleiderkammervorgaenge.AsNoTracking()
            .Where(v => v.MitgliedId == id)
            .OrderByDescending(v => v.Zeitpunkt)
            .ToListAsync(ct);
        return true;
    }
}
