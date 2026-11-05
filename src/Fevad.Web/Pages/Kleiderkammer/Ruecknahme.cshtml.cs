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

/// <summary>Rücknahme von Ausrüstung eines Mitglieds: Stücke wählen, Zustand und Verbleib festhalten, Quittung erzeugen.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerBearbeiten)]
public class RuecknahmeModel(AppDbContext db, Kleiderkammerbuchungen buchungen, Kleiderkammerstand stand, TimeProvider zeit) : PageModel
{
    /// <summary>Kennung des Mitglieds.</summary>
    public Guid Id { get; private set; }
    /// <summary>Name des Mitglieds.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Die Stücke, die das Mitglied gerade hat.</summary>
    public IReadOnlyList<AusruestungStueck> Ausstattung { get; private set; } = [];

    /// <summary>Rücknahmeangaben je ausgegebenem Stück.</summary>
    [BindProperty]
    public List<RuecknahmeEingabe> Ruecknahme { get; set; } = [];

    /// <summary>Bemerkung zur Rücknahme, erscheint auf der Quittung.</summary>
    [BindProperty]
    public string? Bemerkung { get; set; }

    /// <summary>Rücknahmeangaben zu einem ausgegebenen Stück.</summary>
    public sealed class RuecknahmeEingabe
    {
        /// <summary>Kennung der Ausstattung.</summary>
        public Guid AusstattungId { get; set; }
        /// <summary>Das Stück wird zurückgenommen.</summary>
        public bool Gewaehlt { get; set; }
        /// <summary>Zurückgenommene Menge.</summary>
        public int Menge { get; set; }
        /// <summary>Was mit dem Stück geschieht (wieder einlagern oder aussondern).</summary>
        public RuecknahmeZustand Zustand { get; set; }
        /// <summary>Das Stück ist in ordnungsgemäßem Zustand.</summary>
        public bool Ordnungsgemaess { get; set; }
        /// <summary>Festgestellte Mängel.</summary>
        public string? Maengel { get; set; }
    }

    /// <summary>Zeigt die Stücke zur Auswahl.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var mitglied = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .FirstOrDefaultAsync(ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        Id = mitglied.Id;
        Name = $"{mitglied.Nachname}, {mitglied.Vorname}";
        Ausstattung = await Ausruestung.LadenAsync(db, stand, id, Anzeige.Heute(zeit), ct);
        return Page();
    }

    /// <summary>Bucht die Rücknahme der gewählten Stücke und öffnet die Quittung.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Quittung, bei Fehler zurück zur Seite.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var positionen = Ruecknahme
            .Where(r => r.Gewaehlt)
            .Select(r => new RuecknahmePosition(r.AusstattungId, r.Menge, r.Zustand, r.Ordnungsgemaess, r.Maengel))
            .ToList();
        try
        {
            var vorgang = await buchungen.ZuruecknehmenAsync(id, positionen, Bemerkung, ct);
            TempData["Meldung"] = $"Rücknahme {vorgang.Nummer} gebucht.";
            return RedirectToPage("Quittung", new { id = vorgang.Id });
        }
        catch (KleiderkammerFehler fehler)
        {
            TempData["Fehler"] = fehler.Message;
            return RedirectToPage(new { id });
        }
    }
}
