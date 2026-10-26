using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>Fahrzeug mit Prüfstand, Beladung, Prüfbuch und offenen Mängeln.</summary>
public class DetailsModel(AppDbContext db, Pruefstaende pruefstaende, Fahrerliste fahrerliste, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug Fahrzeug { get; private set; } = null!;
    /// <summary>Prüfstand des Fahrzeugs.</summary>
    public Pruefstand Stand { get; private set; } = null!;
    /// <summary>Geräte auf dem Fahrzeug mit Prüfstand.</summary>
    public Seite<Pruefstand> Beladung { get; private set; } = Seite<Pruefstand>.Leer;
    /// <summary>Prüfbuch des Fahrzeugs.</summary>
    public IReadOnlyList<PruefbuchZeile> Pruefbuch { get; private set; } = [];
    /// <summary>Offene Mängel am Fahrzeug.</summary>
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];

    /// <summary>Fahrerliste „ja/nein bis Datum“ (V59); null ohne Recht oder ohne zulässige Gesamtmasse.</summary>
    public IReadOnlyList<Fahrerliste.Zeile>? Fahrer { get; private set; }

    /// <summary>Soll/Ist der Beladung je Position (V59).</summary>
    public IReadOnlyList<Beladeabgleich.Zeile> SollIst { get; private set; } = [];

    /// <summary>Die letzten Beladekontrollen.</summary>
    public IReadOnlyList<KontrollZeile> Kontrollen { get; private set; } = [];

    /// <summary>Zweckbindung läuft bei der Außerdienststellung noch, Fördergeber nicht informiert.</summary>
    public bool ZweckbindungWarnung { get; private set; }

    /// <summary>Eine Beladekontrolle in der Übersicht.</summary>
    /// <param name="Datum">Tag der Kontrolle.</param>
    /// <param name="Kontrolleur">Name des Kontrollierenden.</param>
    /// <param name="Vollstaendig">Alles war da.</param>
    /// <param name="Fehlliste">Fehlliste als Text.</param>
    public sealed record KontrollZeile(DateOnly Datum, string? Kontrolleur, bool Vollstaendig, string Fehlliste);

    /// <summary>Lädt das Fahrzeug.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var fahrzeug = await db.Fahrzeuge.AsNoTracking().Include(f => f.Pruefvorlage).FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fahrzeug is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        Fahrzeug = fahrzeug;
        Stand = (await pruefstaende.FahrzeugeAsync(heute, id, ct)).Single();
        Beladung = (await pruefstaende.GeraeteAsync(heute, fahrzeugId: id, ct: ct)).Ganz();
        Pruefbuch = await Ansichten.PruefbuchAsync(db, null, id, ct);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.FahrzeugId == id && m.Status != MangelStatus.Behoben)).ToListAsync(ct);
        ZweckbindungWarnung = fahrzeug.ZweckbindungWarnung(fahrzeug.AusserDienstAm);

        var positionen = await db.Beladepositionen.AsNoTracking().Where(p => p.FahrzeugId == id).ToListAsync(ct);
        if (positionen.Count > 0)
        {
            SollIst = Beladeabgleich.SollIst(positionen, await BeladungAsync(db, id, ct)).Where(z => !z.Stimmt).ToList();
        }

        var kontrollen = await db.Beladekontrollen.AsNoTracking().Where(k => k.FahrzeugId == id)
            .OrderByDescending(k => k.Datum).ThenByDescending(k => k.ErstelltAm).Take(5)
            .Select(k => new { k.Datum, Name = k.KontrolliertVon == null ? null : k.KontrolliertVon.Nachname + ", " + k.KontrolliertVon.Vorname, k.Vollstaendig, k.Ergebnis })
            .ToListAsync(ct);
        Kontrollen = kontrollen
            .Select(k => new KontrollZeile(k.Datum, k.Name, k.Vollstaendig, Beladeabgleich.Fehlliste(new Beladekontrolle { Ergebnis = k.Ergebnis }.Zeilen())))
            .ToList();

        if (fahrzeug.ZulaessigeGesamtmasseKg is { } masse && (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            Fahrer = await fahrerliste.LadenAsync(masse, heute, ct);
        }

        return Page();
    }

    /// <summary>Geräte auf dem Fahrzeug für den Beladeabgleich, ohne ausgesonderte.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="fahrzeugId">Fahrzeug.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Geräte.</returns>
    internal static async Task<List<Beladeabgleich.Geraet>> BeladungAsync(AppDbContext db, Guid fahrzeugId, CancellationToken ct) =>
        await db.Geraete.AsNoTracking()
            .Where(g => g.FahrzeugId == fahrzeugId && g.AusgesondertAm == null)
            .Select(g => new Beladeabgleich.Geraet(g.Id, g.Inventarnummer, g.Bezeichnung, g.MaterialId, g.GeraeteartId, g.Lagerort))
            .ToListAsync(ct);
}
