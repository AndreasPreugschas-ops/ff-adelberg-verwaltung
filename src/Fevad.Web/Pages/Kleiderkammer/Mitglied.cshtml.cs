using Fevad.Domain.Gemeinsam;
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

/// <summary>Größenprofil, aktuelle Ausstattung und Quittungen eines Mitglieds; Rücknahme.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class MitgliedModel(AppDbContext db, Kleiderkammerbuchungen buchungen, Kleiderkammerstand stand, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Kennung des Mitglieds.</summary>
    public Guid Id { get; private set; }
    /// <summary>Name des Mitglieds.</summary>
    public string Name { get; private set; } = "";
    /// <summary>Personalnummer des Mitglieds.</summary>
    public string Personalnummer { get; private set; } = "";
    /// <summary>Der Benutzer darf Größen und Rücknahmen buchen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Größenprofil je Größenart.</summary>
    [BindProperty]
    public Dictionary<Groessenart, string?> Groessen { get; set; } = [];

    /// <summary>Rücknahmeangaben je ausgegebenem Stück.</summary>
    [BindProperty]
    public List<RuecknahmeEingabe> Ruecknahme { get; set; } = [];

    /// <summary>Bemerkung zur Rücknahme, erscheint auf der Quittung.</summary>
    [BindProperty]
    public string? Bemerkung { get; set; }

    /// <summary>Aktuelle Ausstattung des Mitglieds.</summary>
    public IReadOnlyList<AusstattungZeile> Ausstattung { get; private set; } = [];
    /// <summary>Ausgaben und Rücknahmen des Mitglieds, neueste zuerst.</summary>
    public IReadOnlyList<Kleiderkammervorgang> Vorgaenge { get; private set; } = [];

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

    /// <summary>Ein ausgegebenes Stück.</summary>
    /// <param name="Id">Kennung der Ausstattung.</param>
    /// <param name="Artikel">Bezeichnung des Artikels.</param>
    /// <param name="Groesse">Größe.</param>
    /// <param name="Menge">Ausgegebene Menge.</param>
    /// <param name="Inventarnummer">Inventarnummer bei Einzelstücken.</param>
    /// <param name="AusgegebenAm">Tag der Ausgabe.</param>
    /// <param name="NaechstePruefung">Nächste fällige Prüfung des Einzelstücks.</param>
    /// <param name="NieGeprueft">Das Einzelstück wurde noch nie geprüft.</param>
    /// <param name="Aussonderung">Ende der Lebensdauer.</param>
    /// <param name="Ampel">Dringlichkeit aus Prüfung und Lebensdauer.</param>
    public sealed record AusstattungZeile(
        Guid Id, string Artikel, string Groesse, int Menge, string? Inventarnummer, DateOnly AusgegebenAm,
        DateOnly? NaechstePruefung, bool NieGeprueft, DateOnly? Aussonderung, Fristampel? Ampel);

    /// <summary>Zeigt Größen, Ausstattung und Vorgänge des Mitglieds.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Speichert das Größenprofil.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostGroessenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await buchungen.GroessenSetzenAsync(id, Groessen, ct);
        TempData["Meldung"] = "Größen gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Bucht die Rücknahme der gewählten Stücke und öffnet die Quittung.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Quittung, bei Fehler zurück zur Seite; 403 ohne Recht.</returns>
    public async Task<IActionResult> OnPostRuecknahmeAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

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

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

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
        DarfBearbeiten = await DarfAsync();

        Groessen = await db.Groessenangaben.AsNoTracking()
            .Where(g => g.MitgliedId == id)
            .ToDictionaryAsync(g => g.Groessenart, g => (string?)g.Wert, ct);

        var heute = Anzeige.Heute(zeit);
        var psa = (await stand.PsaAsync(heute, ct)).Where(p => p.MitgliedId == id).ToDictionary(p => p.Stand.Id);
        var zeilen = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.MitgliedId == id && a.ZurueckAm == null)
            .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.AusgegebenAm)
            .Select(a => new { a.Id, Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.GeraetId, Inventarnummer = a.Geraet!.Inventarnummer, a.AusgegebenAm })
            .ToListAsync(ct);
        Ausstattung = zeilen
            .Select(a =>
            {
                var p = a.GeraetId is { } g ? psa.GetValueOrDefault(g) : null;
                return new AusstattungZeile(a.Id, a.Artikel, a.Groesse, a.Menge, a.Inventarnummer, a.AusgegebenAm,
                    p?.Stand.NaechsterTermin, p?.Stand.NieGeprueft == true, p?.Stand.AussonderungFaellig, p?.Stand.Ampel);
            })
            .ToList();
        Ruecknahme = Ausstattung.Select(a => new RuecknahmeEingabe { AusstattungId = a.Id, Menge = a.Menge, Ordnungsgemaess = true }).ToList();

        Vorgaenge = await db.Kleiderkammervorgaenge.AsNoTracking()
            .Where(v => v.MitgliedId == id)
            .OrderByDescending(v => v.Zeitpunkt)
            .ToListAsync(ct);
        return true;
    }
}
