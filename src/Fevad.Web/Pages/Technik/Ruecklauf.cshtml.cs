using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Werkstatt-Arbeitslisten nach dem Einsatz (V13), je Rücklaufweg: Gerätewart, Atemschutzwerkstatt, Schlauchwerkstatt.
/// Einzelgeräte verschwinden mit der bestandenen Prüfung, Mengenartikel mit „zurück und geprüft“.
/// </summary>
public class RuecklaufModel(AppDbContext db, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Posten, nach Werkstatt gruppiert.</summary>
    public IReadOnlyList<IGrouping<Ruecklauf, Posten>> Listen { get; private set; } = [];

    /// <summary>Prüfungen eintragen und Mengenartikel abhaken darf, wer TechnikPruefen hat.</summary>
    public bool DarfPruefen { get; private set; }

    /// <summary>Ein offener Posten der Werkstattliste.</summary>
    /// <param name="Werkstatt">Rücklaufweg aus der Geräteart.</param>
    /// <param name="Einsatznummer">Einsatz, in dem das Gerät benutzt wurde.</param>
    /// <param name="EinsatzId">Für die Prüfung nach Gebrauch.</param>
    /// <param name="Seit">Zeitpunkt der Erfassung.</param>
    /// <param name="GeraetId">Einzelgerät; leer bei Mengenartikeln.</param>
    /// <param name="ZeileId">Zeile der Einsatzgeräte, für „erledigt“ bei Mengenartikeln.</param>
    /// <param name="Gegenstand">Inventarnummer und Bezeichnung oder Anzahl und Material.</param>
    /// <param name="Fahrzeug">Herkunftsfahrzeug, für den Tausch gegen Reserve.</param>
    /// <param name="Belastung">Belastung im Einsatz.</param>
    /// <param name="Bemerkung">Hinweis des Einsatzleiters.</param>
    public sealed record Posten(Ruecklauf Werkstatt, string Einsatznummer, Guid EinsatzId, DateTimeOffset Seit, Guid? GeraetId, Guid? ZeileId,
        string Gegenstand, string? Fahrzeug, Belastung? Belastung, string? Bemerkung);

    /// <summary>Zeigt die Listen.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfPruefen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;

        var sperren = await db.Maengel.AsNoTracking()
            .Where(m => m.EinsatzId != null && m.GeraetId != null && m.Status != MangelStatus.Behoben)
            .Select(m => new
            {
                m.Geraet!.Geraeteart!.Ruecklauf,
                EinsatzId = m.EinsatzId!.Value,
                Einsatznummer = db.Einsaetze.Where(e => e.Id == m.EinsatzId).Select(e => e.Einsatznummer).FirstOrDefault(),
                m.GemeldetAm,
                m.GeraetId,
                Gegenstand = m.Geraet.Inventarnummer + " " + m.Geraet.Bezeichnung,
                Zeile = db.EinsatzGeraete.Where(z => z.MangelId == m.Id).Select(z => new { Fahrzeug = z.Fahrzeug!.Funkrufname, z.Belastung, z.Bemerkung }).FirstOrDefault(),
                Standort = m.Geraet.Fahrzeug!.Funkrufname,
            })
            .ToListAsync(ct);
        var mengen = await db.EinsatzGeraete.AsNoTracking()
            .Where(z => z.GeraetId == null && z.ErledigtAm == null)
            .Select(z => new
            {
                z.Material!.Geraeteart!.Ruecklauf,
                z.EinsatzId,
                z.Einsatz!.Einsatznummer,
                z.ErstelltAm,
                z.Id,
                Gegenstand = z.Anzahl + " × " + z.Material.Bezeichnung,
                Fahrzeug = z.Fahrzeug!.Funkrufname,
                z.Belastung,
                z.Bemerkung,
            })
            .ToListAsync(ct);

        Listen = sperren
            .Select(s => new Posten(s.Ruecklauf, s.Einsatznummer ?? "–", s.EinsatzId, s.GemeldetAm, s.GeraetId, null, s.Gegenstand,
                s.Zeile?.Fahrzeug ?? s.Standort, s.Zeile?.Belastung, s.Zeile?.Bemerkung))
            .Concat(mengen.Select(m => new Posten(m.Ruecklauf, m.Einsatznummer, m.EinsatzId, m.ErstelltAm, null, m.Id, m.Gegenstand, m.Fahrzeug, m.Belastung, m.Bemerkung)))
            .OrderBy(p => p.Seit)
            .GroupBy(p => p.Werkstatt)
            .OrderBy(g => g.Key)
            .ToList();
    }

    /// <summary>Mengenartikel ist zurück und geprüft.</summary>
    public async Task<IActionResult> OnPostErledigtAsync(Guid zeileId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        if (await db.EinsatzGeraete.FirstOrDefaultAsync(z => z.Id == zeileId && z.GeraetId == null && z.ErledigtAm == null, ct) is { } zeile)
        {
            zeile.ErledigtAm = zeit.GetUtcNow();
            zeile.ErledigtVon = aktuellerBenutzer.Id;
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = "Als erledigt vermerkt.";
        }

        return RedirectToPage();
    }
}
