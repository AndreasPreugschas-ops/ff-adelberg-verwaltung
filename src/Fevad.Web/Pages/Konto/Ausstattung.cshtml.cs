using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Meine Ausrüstung (V44): Tausch eines ausgegebenen Stücks anfordern, Ausgaben und Rücknahmen der Kleiderkammer digital bestätigen statt auf
/// Papier zu unterschreiben. Mängel an der eigenen PSA meldet das Mitglied wie bisher mit Foto über „Mangel melden“.
/// </summary>
public class AusstattungModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, Benachrichtigungsdienst benachrichtigungen,
    IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Stück, das das Mitglied gerade hat.</summary>
    /// <param name="Id">Kennung der Ausstattung.</param>
    /// <param name="Artikel">Artikelbezeichnung.</param>
    /// <param name="Groesse">Größe.</param>
    /// <param name="Menge">Stückzahl.</param>
    /// <param name="Inventarnummer">Inventarnummer bei Einzelstücken.</param>
    /// <param name="AusgegebenAm">Tag der Ausgabe.</param>
    /// <param name="TauschOffen">Es läuft schon eine Tauschanforderung.</param>
    public sealed record Stueck(Guid Id, string Artikel, string Groesse, int Menge, string? Inventarnummer, DateOnly AusgegebenAm, bool TauschOffen);

    /// <summary>Ein Vorgang der Kleiderkammer mit seinen Positionen.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Nummer">Quittungsnummer.</param>
    /// <param name="Art">Ausgabe oder Rücknahme.</param>
    /// <param name="Zeitpunkt">Zeitpunkt.</param>
    /// <param name="Positionen">Artikel mit Größe und Menge.</param>
    /// <param name="QuittiertAm">Bestätigt am.</param>
    /// <param name="Digital">In FEVAD bestätigt.</param>
    public sealed record Vorgang(Guid Id, string Nummer, VorgangArt Art, DateTimeOffset Zeitpunkt, IReadOnlyList<string> Positionen, DateTimeOffset? QuittiertAm, bool Digital);

    /// <summary>Stücke im Besitz.</summary>
    public IReadOnlyList<Stueck> Stuecke { get; private set; } = [];

    /// <summary>Vorgänge der letzten Zeit, unbestätigte zuerst.</summary>
    public IReadOnlyList<Vorgang> Vorgaenge { get; private set; } = [];

    /// <summary>Eigene Tauschanforderungen, neueste zuerst.</summary>
    public IReadOnlyList<Tauschanforderung> Anforderungen { get; private set; } = [];

    /// <summary>Darf Mängel melden.</summary>
    public bool DarfMelden { get; private set; }

    /// <summary>Formular der Tauschanforderung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Eingaben einer Tauschanforderung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Das Stück.</summary>
        [Required(ErrorMessage = "Bitte das Stück wählen."), Display(Name = "Stück")] public Guid? AusstattungId { get; set; }
        /// <summary>Grund.</summary>
        [Display(Name = "Grund")] public Tauschgrund Grund { get; set; } = Tauschgrund.Groesse;
        /// <summary>Gewünschte Größe.</summary>
        [StringLength(20), Display(Name = "Gewünschte Größe (falls anders)")] public string? GewuenschteGroesse { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt Ausrüstung, Vorgänge und Anforderungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        return Page();
    }

    /// <summary>Fordert den Tausch eines eigenen Stücks an und benachrichtigt die Kleiderkammer.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostTauschAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var stueck = Formular.AusstattungId is { } aid
            ? await db.Ausstattungen.Include(a => a.Artikel).FirstOrDefaultAsync(a => a.Id == aid && a.MitgliedId == mitgliedId && a.ZurueckAm == null, ct)
            : null;
        if (stueck is null)
        {
            ModelState.AddModelError($"{nameof(Formular)}.{nameof(Eingabe.AusstattungId)}", "Bitte ein Stück aus Ihrer Ausrüstung wählen.");
        }
        else if (await db.Tauschanforderungen.AnyAsync(t => t.AusstattungId == stueck.Id && t.Status == TauschStatus.Offen, ct))
        {
            ModelState.AddModelError($"{nameof(Formular)}.{nameof(Eingabe.AusstattungId)}", "Für dieses Stück läuft schon eine Anforderung.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(mitgliedId, ct);
            return Page();
        }

        var anforderung = new Tauschanforderung
        {
            MitgliedId = mitgliedId, AusstattungId = stueck!.Id, Grund = Formular.Grund,
            GewuenschteGroesse = string.IsNullOrWhiteSpace(Formular.GewuenschteGroesse) ? null : Formular.GewuenschteGroesse.Trim(),
            Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
        };
        db.Tauschanforderungen.Add(anforderung);
        await db.SaveChangesAsync(ct);
        await benachrichtigungen.AnRechtAsync(Berechtigungen.KleiderkammerBearbeiten,
            new(Benachrichtigungsart.Aufgabe, $"Tausch angefordert: {stueck.Artikel?.Bezeichnung} ({Anzeige.Text(Formular.Grund)})", "/Kleiderkammer/Tausch", anforderung.Id),
            benutzer.Id, ct);
        TempData["Meldung"] = "Ihre Anforderung ist bei der Kleiderkammer. Sie bekommen eine Antwort in FEVAD.";
        return RedirectToPage();
    }

    /// <summary>Bestätigt eine eigene Ausgabe oder Rücknahme digital.</summary>
    /// <param name="vorgangId">Der Vorgang.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostBestaetigenAsync(Guid vorgangId, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var vorgang = await db.Kleiderkammervorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId && v.MitgliedId == mitgliedId, ct);
        if (vorgang is null)
        {
            return NotFound();
        }

        if (vorgang.QuittiertAm is null)
        {
            vorgang.QuittiertAm = zeit.GetUtcNow();
            vorgang.QuittiertDigital = true;
            await db.SaveChangesAsync(ct);
        }

        TempData["Meldung"] = $"{Anzeige.Text(vorgang.Art)} {vorgang.Nummer} bestätigt.";
        return RedirectToPage();
    }

    /// <summary>Zieht eine offene Anforderung zurück.</summary>
    /// <param name="id">Die Anforderung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        var mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
        var t = await db.Tauschanforderungen.FirstOrDefaultAsync(x => x.Id == id && x.MitgliedId == mitgliedId && x.Status == TauschStatus.Offen, ct);
        if (t is null)
        {
            return NotFound();
        }

        db.Tauschanforderungen.Remove(t);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Anforderung zurückgezogen.";
        return RedirectToPage();
    }

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        DarfMelden = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MaengelMelden)).Succeeded;
        Anforderungen = await db.Tauschanforderungen.AsNoTracking().Include(t => t.Ausstattung).ThenInclude(a => a!.Artikel)
            .Where(t => t.MitgliedId == mitgliedId).OrderByDescending(t => t.ErstelltAm).ToListAsync(ct);
        var offen = Anforderungen.Where(t => t.Status == TauschStatus.Offen).Select(t => t.AusstattungId).ToHashSet();
        Stuecke = (await db.Ausstattungen.AsNoTracking().Where(a => a.MitgliedId == mitgliedId && a.ZurueckAm == null)
                .Select(a => new { a.Id, Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, Nummer = a.Geraet == null ? null : a.Geraet.Inventarnummer, a.AusgegebenAm })
                .ToListAsync(ct))
            .OrderBy(a => a.Artikel).Select(a => new Stueck(a.Id, a.Artikel, a.Groesse, a.Menge, a.Nummer, a.AusgegebenAm, offen.Contains(a.Id))).ToList();

        var vorgaenge = await db.Kleiderkammervorgaenge.AsNoTracking().Where(v => v.MitgliedId == mitgliedId)
            .OrderByDescending(v => v.Zeitpunkt).Take(20).ToListAsync(ct);
        var ids = vorgaenge.Select(v => v.Id).ToList();
        var positionen = await db.Ausstattungen.AsNoTracking()
            .Where(a => ids.Contains(a.AusgabeVorgangId) || (a.RuecknahmeVorgangId != null && ids.Contains(a.RuecknahmeVorgangId.Value)))
            .Select(a => new { a.AusgabeVorgangId, a.RuecknahmeVorgangId, Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge })
            .ToListAsync(ct);
        Vorgaenge = vorgaenge
            .Select(v => new Vorgang(v.Id, v.Nummer, v.Art, v.Zeitpunkt,
                positionen.Where(p => v.Art == VorgangArt.Ausgabe ? p.AusgabeVorgangId == v.Id : p.RuecknahmeVorgangId == v.Id)
                    .Select(p => $"{p.Artikel}, Größe {p.Groesse}{(p.Menge > 1 ? $", {p.Menge} Stück" : "")}").ToList(),
                v.QuittiertAm, v.QuittiertDigital))
            .OrderBy(v => v.QuittiertAm is not null).ThenByDescending(v => v.Zeitpunkt).ToList();
    }
}
