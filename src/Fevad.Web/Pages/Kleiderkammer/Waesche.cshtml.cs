using Fevad.Domain.Einsaetze;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>
/// Sammelwäsche nach dem Einsatz (V13): wessen Einsatzkleidung gewaschen werden muss, mit der ausgegebenen Kleidung.
/// Die Kleiderkammer sieht nur Einsatz und Namen, nicht den Grund (Exposition, Hautkontakt).
/// </summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class WaescheModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Artikelkategorien, die in die Sammelwäsche gehen.</summary>
    public static readonly ArtikelKategorie[] Waeschekategorien = [ArtikelKategorie.Einsatzkleidung, ArtikelKategorie.Kopfschutz, ArtikelKategorie.Handschutz];

    /// <summary>Offene Wäsche, ältester Einsatz zuerst.</summary>
    public IReadOnlyList<Posten> Offen { get; private set; } = [];

    /// <summary>Abhaken darf, wer die Kleiderkammer bearbeiten darf.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Ein Mitglied, dessen Einsatzkleidung in die Wäsche muss.</summary>
    /// <param name="TeilnahmeId">ID der Einsatzteilnahme.</param>
    /// <param name="Einsatznummer">Einsatz.</param>
    /// <param name="Alarmiert">Alarmzeit.</param>
    /// <param name="MitgliedId">Für den Link zur Ausstattung.</param>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Kleidung">Ausgegebene Einsatzkleidung als Text (Artikel, Größe, Inventarnummer).</param>
    public sealed record Posten(Guid TeilnahmeId, string Einsatznummer, DateTimeOffset Alarmiert, Guid MitgliedId, string Name, IReadOnlyList<string> Kleidung);

    /// <summary>Zeigt die Liste.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;
        var teilnahmen = await db.EinsatzTeilnahmen.AsNoTracking()
            .Where(t => t.PsaWaescheErledigtAm == null && (t.PsaWaesche || t.Hautkontakt || t.Exposition == ExpositionStatus.Exponiert))
            .OrderBy(t => t.Einsatz!.Alarmiert).ThenBy(t => t.Mitglied!.Nachname)
            .Select(t => new { t.Id, t.Einsatz!.Einsatznummer, t.Einsatz.Alarmiert, t.MitgliedId, Name = t.Mitglied!.Nachname + ", " + t.Mitglied.Vorname })
            .ToListAsync(ct);
        var mitglieder = teilnahmen.Select(t => t.MitgliedId).Distinct().ToList();
        var kleidung = (await db.Ausstattungen.AsNoTracking()
                .Where(a => mitglieder.Contains(a.MitgliedId) && a.ZurueckAm == null && Waeschekategorien.Contains(a.Artikel!.Kategorie))
                .OrderBy(a => a.Artikel!.Sortierung)
                .Select(a => new { a.MitgliedId, Text = a.Artikel!.Bezeichnung + " " + a.Groesse + (a.Geraet != null ? " (" + a.Geraet.Inventarnummer + ")" : "") })
                .ToListAsync(ct))
            .ToLookup(a => a.MitgliedId, a => a.Text);
        Offen = teilnahmen.Select(t => new Posten(t.Id, t.Einsatznummer, t.Alarmiert, t.MitgliedId, t.Name, kleidung[t.MitgliedId].ToList())).ToList();
    }

    /// <summary>Wäsche für eine oder mehrere Teilnahmen als erledigt vermerken.</summary>
    public async Task<IActionResult> OnPostErledigtAsync(List<Guid> teilnahmeIds, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var jetzt = zeit.GetUtcNow();
        var heute = Anzeige.Heute(zeit);
        var teilnahmen = await db.EinsatzTeilnahmen.Where(t => teilnahmeIds.Contains(t.Id) && t.PsaWaescheErledigtAm == null).ToListAsync(ct);
        var mitglieder = teilnahmen.Select(t => t.MitgliedId).Distinct().ToList();
        var stuecke = (await db.Ausstattungen
                .Where(a => mitglieder.Contains(a.MitgliedId) && a.ZurueckAm == null && a.GeraetId != null && Waeschekategorien.Contains(a.Artikel!.Kategorie))
                .Select(a => new { a.MitgliedId, GeraetId = a.GeraetId!.Value })
                .ToListAsync(ct))
            .ToLookup(a => a.MitgliedId, a => a.GeraetId);
        foreach (var teilnahme in teilnahmen)
        {
            teilnahme.PsaWaescheErledigtAm = jetzt;

            // Pflege-Lebenslauf der ausgegebenen Einzelstücke (V63): eine Wäsche je Stück und Einsatz.
            foreach (var geraetId in stuecke[teilnahme.MitgliedId])
            {
                db.Pflegeeintraege.Add(new Pflegeeintrag { GeraetId = geraetId, Datum = heute, Art = Pflegeart.Waesche, EinsatzId = teilnahme.EinsatzId });
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{teilnahmen.Count} als gewaschen vermerkt.";
        return RedirectToPage();
    }
}
