using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Rücklauf nach dem Einsatz (V13): Der Einsatzleiter markiert benutzte Geräte, Mengenartikel und Einsatzkleidung für die Wäsche.
/// Einzelgeräte sind ab sofort bis zur nächsten bestandenen Prüfung gesperrt; Mengenartikel wie Schläuche kommen nur auf die Werkstattliste.
/// Jede Aktion speichert sofort; das geht auch nach der Freigabe des Berichts.
/// </summary>
public class GeraeteModel(AppDbContext db, IAuthorizationService autorisierung, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Der Einsatz mit Nummer, Stichwort und Alarmzeit.</summary>
    public Einsatz Einsatz { get; private set; } = null!;

    /// <summary>Mit EinsaetzeBearbeiten dürfen Zeilen angelegt und entfernt werden.</summary>
    public bool DarfAendern { get; private set; }

    /// <summary>Bereits erfasste Geräte und Mengenartikel.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Teilnehmer mit Kennzeichen für die Sammelwäsche.</summary>
    public IReadOnlyList<Waesche> Teilnehmer { get; private set; } = [];

    /// <summary>Inventarnummern der Geräte auf den Einsatzfahrzeugen als Eingabehilfe.</summary>
    public IReadOnlyList<string> Vorschlaege { get; private set; } = [];

    /// <summary>Fächer (Lagerorte) je Einsatzfahrzeug für „ganzes Fach“.</summary>
    public IReadOnlyList<SelectListItem> Faecher { get; private set; } = [];

    /// <summary>Einsatzfahrzeuge zur Auswahl bei Mengenartikeln.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Aktive Materialstämme für Mengenartikel.</summary>
    public IReadOnlyList<SelectListItem> Materialien { get; private set; } = [];

    /// <summary>PA- und Maskennummern aus den Teilnehmern, die noch nicht erfasst sind.</summary>
    public IReadOnlyList<string> AtemschutzOffen { get; private set; } = [];

    /// <summary>Eine erfasste Zeile mit Prüfstand.</summary>
    /// <param name="Id">Zeilen-ID.</param>
    /// <param name="Gegenstand">Inventarnummer und Bezeichnung oder Anzahl und Material.</param>
    /// <param name="Fahrzeug">Funkrufname, falls bekannt.</param>
    /// <param name="Belastung">Wie das Gerät belastet wurde.</param>
    /// <param name="Mengenartikel">Ohne Sperre, nur Werkstattliste.</param>
    /// <param name="Erledigt">Geprüft bzw. zurück.</param>
    /// <param name="Bemerkung">Freitext.</param>
    public sealed record Zeile(Guid Id, string Gegenstand, string? Fahrzeug, Belastung Belastung, bool Mengenartikel, bool Erledigt, string? Bemerkung);

    /// <summary>Teilnehmer für die Wäscheliste.</summary>
    /// <param name="TeilnahmeId">ID der Einsatzteilnahme.</param>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Vermerkt">Vom Einsatzleiter für die Wäsche vermerkt.</param>
    /// <param name="AusExposition">Hautkontakt oder Exposition aus dem Bericht; dann ist die Wäsche ohnehin fällig. Nur mit Atemschutz-Recht gesetzt.</param>
    /// <param name="Erledigt">Kleiderkammer hat gewaschen.</param>
    public sealed record Waesche(Guid TeilnahmeId, string Name, bool Vermerkt, bool AusExposition, bool Erledigt);

    /// <summary>Zeigt die Seite.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Einzelgerät nach Inventarnummer erfassen und sperren.</summary>
    public async Task<IActionResult> OnPostGeraetAsync(Guid id, string? nummer, Belastung belastung, string? bemerkung, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var inventarnummer = Geraetecode.NummerAusScan(nummer);
        var geraet = await db.Geraete.FirstOrDefaultAsync(g => g.Inventarnummer == inventarnummer && g.AusgesondertAm == null, ct);
        if (geraet is null)
        {
            TempData["Fehler"] = $"Kein Gerät mit der Nummer „{inventarnummer}“.";
            return RedirectToPage(new { id });
        }

        var neu = await SperrenAsync([geraet], belastung, bemerkung, ct);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = neu == 0 ? $"{geraet.Inventarnummer} ist schon erfasst." : $"{geraet.Inventarnummer} {geraet.Bezeichnung} bis zur Prüfung gesperrt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Alle Geräte eines Fachs auf einem Einsatzfahrzeug erfassen (ganze Fahrzeugbeladung eines Geräteraums).</summary>
    public async Task<IActionResult> OnPostFachAsync(Guid id, string? fach, Belastung belastung, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        // Wert: "{FahrzeugId}|{Lagerort}"
        var teile = (fach ?? "").Split('|', 2);
        if (teile.Length != 2 || !Guid.TryParse(teile[0], out var fahrzeugId) || Einsatz.Fahrzeuge.All(f => f.FahrzeugId != fahrzeugId))
        {
            TempData["Fehler"] = "Bitte ein Fach auswählen.";
            return RedirectToPage(new { id });
        }

        var lagerort = teile[1];
        var geraete = await db.Geraete
            .Where(g => g.FahrzeugId == fahrzeugId && g.AusgesondertAm == null && g.Lagerort == lagerort)
            .ToListAsync(ct);
        var neu = await SperrenAsync(geraete, belastung, null, ct);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{neu} Geräte aus Fach {lagerort} bis zur Prüfung gesperrt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Pressluftatmer und Masken aus den Teilnehmerangaben übernehmen, soweit es Geräte mit diesen Nummern gibt.</summary>
    public async Task<IActionResult> OnPostAtemschutzAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var nummern = AtemschutzNummern();
        var geraete = await db.Geraete.Where(g => nummern.Contains(g.Inventarnummer) && g.AusgesondertAm == null).ToListAsync(ct);
        var neu = await SperrenAsync(geraete, Belastung.Benutzt, "aus den Atemschutzangaben", ct);
        await db.SaveChangesAsync(ct);
        var unbekannt = nummern.Except(geraete.Select(g => g.Inventarnummer)).ToList();
        TempData["Meldung"] = $"{neu} Atemschutzgeräte übernommen." + (unbekannt.Count > 0 ? $" Ohne passendes Gerät: {string.Join(", ", unbekannt)}." : "");
        return RedirectToPage(new { id });
    }

    /// <summary>Mengenartikel (z. B. Schläuche) als Anzahl erfassen; keine Sperre.</summary>
    public async Task<IActionResult> OnPostMengeAsync(Guid id, Guid? materialId, int anzahl, Guid? fahrzeugId, Belastung belastung, string? bemerkung, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (materialId is null || anzahl is < 1 or > 999 || !await db.Materialien.AnyAsync(m => m.Id == materialId, ct))
        {
            TempData["Fehler"] = "Bitte Material und eine Anzahl von 1 bis 999 angeben.";
            return RedirectToPage(new { id });
        }

        db.EinsatzGeraete.Add(new EinsatzGeraet
        {
            EinsatzId = id,
            MaterialId = materialId,
            Anzahl = anzahl,
            FahrzeugId = Einsatz.Fahrzeuge.Any(f => f.FahrzeugId == fahrzeugId) ? fahrzeugId : null,
            Belastung = belastung,
            Bemerkung = Kuerzen(bemerkung),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Auf die Werkstattliste gesetzt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Zeile entfernen; eine noch offene Sperre fällt mit weg, eine schon bearbeitete bleibt beim Gerätewart.</summary>
    public async Task<IActionResult> OnPostEntfernenAsync(Guid id, Guid zeileId, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var zeile = await db.EinsatzGeraete.FirstOrDefaultAsync(z => z.Id == zeileId && z.EinsatzId == id, ct);
        if (zeile is not null)
        {
            if (zeile.MangelId is not null && await db.Maengel.FirstOrDefaultAsync(m => m.Id == zeile.MangelId && m.Status == MangelStatus.Offen, ct) is { } sperre)
            {
                db.Maengel.Remove(sperre);
            }

            db.EinsatzGeraete.Remove(zeile);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Vermerk „Einsatzkleidung zur Sammelwäsche“ je Teilnehmer setzen.</summary>
    public async Task<IActionResult> OnPostWaescheAsync(Guid id, List<Guid> teilnahmeIds, CancellationToken ct)
    {
        if (await VorbereitenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var gewaehlt = teilnahmeIds.ToHashSet();
        foreach (var teilnahme in await db.EinsatzTeilnahmen.Where(t => t.EinsatzId == id).ToListAsync(ct))
        {
            teilnahme.PsaWaesche = gewaehlt.Contains(teilnahme.Id);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Wäscheliste gespeichert.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult?> VorbereitenAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var einsatz = await db.Einsaetze.Include(e => e.Fahrzeuge).Include(e => e.Teilnahmen).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        Einsatz = einsatz;
        return null;
    }

    /// <summary>Legt je Gerät eine Zeile und eine Sperre an; schon erfasste Geräte bleiben unverändert.</summary>
    private async Task<int> SperrenAsync(IReadOnlyList<Geraet> geraete, Belastung belastung, string? bemerkung, CancellationToken ct)
    {
        var ids = geraete.Select(g => g.Id).ToList();
        var vorhanden = (await db.EinsatzGeraete.Where(z => z.EinsatzId == Einsatz.Id && z.GeraetId != null && ids.Contains(z.GeraetId.Value))
            .Select(z => z.GeraetId!.Value).ToListAsync(ct)).ToHashSet();
        var mitgliedId = (await benutzer.GetUserAsync(User))?.MitgliedId;
        var neu = 0;
        foreach (var geraet in geraete.Where(g => !vorhanden.Contains(g.Id)))
        {
            var sperre = new Mangel
            {
                GeraetId = geraet.Id,
                Beschreibung = EinsatzGeraet.Sperrtext(Einsatz.Einsatznummer, belastung),
                Schwere = MangelSchwere.AusserBetrieb,
                Status = MangelStatus.Offen,
                GemeldetAm = zeit.GetUtcNow(),
                GemeldetVonId = mitgliedId,
                EinsatzId = Einsatz.Id,
            };
            db.Maengel.Add(sperre);
            db.EinsatzGeraete.Add(new EinsatzGeraet
            {
                EinsatzId = Einsatz.Id,
                GeraetId = geraet.Id,
                FahrzeugId = Einsatz.Fahrzeuge.Any(f => f.FahrzeugId == geraet.FahrzeugId) ? geraet.FahrzeugId : null,
                Belastung = belastung,
                Bemerkung = Kuerzen(bemerkung),
                MangelId = sperre.Id,
            });
            neu++;
        }

        return neu;
    }

    private List<string> AtemschutzNummern() =>
        Einsatz.Teilnahmen
            .SelectMany(t => new[] { t.PaGeraet, t.Maske })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct()
            .ToList();

    private static string? Kuerzen(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= 500 ? text.Trim() : text.Trim()[..500];

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.AsNoTracking()
            .Include(e => e.Fahrzeuge).ThenInclude(f => f.Fahrzeug)
            .Include(e => e.Teilnahmen).ThenInclude(t => t.Mitglied)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return false;
        }

        Einsatz = einsatz;
        DarfAendern = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded;

        // Hautkontakt und Exposition sieht nur, wer Atemschutz lesen darf (wie im Bericht).
        var darfAtemschutz = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AtemschutzLesen)).Succeeded;

        var zeilen = await db.EinsatzGeraete.AsNoTracking()
            .Where(z => z.EinsatzId == id)
            .OrderBy(z => z.ErstelltAm)
            .Select(z => new
            {
                z.Id,
                Nummer = z.Geraet != null ? z.Geraet.Inventarnummer + " " + z.Geraet.Bezeichnung : null,
                Material = z.Material != null ? z.Material.Bezeichnung : null,
                z.Anzahl,
                Fahrzeug = z.Fahrzeug != null ? z.Fahrzeug.Funkrufname : null,
                z.Belastung,
                z.GeraetId,
                z.ErledigtAm,
                SperreOffen = z.MangelId != null && db.Maengel.Any(m => m.Id == z.MangelId && m.Status != MangelStatus.Behoben),
                z.Bemerkung,
            })
            .ToListAsync(ct);
        Zeilen = zeilen
            .Select(z => new Zeile(z.Id, z.Nummer ?? $"{z.Anzahl} × {z.Material}", z.Fahrzeug, z.Belastung, z.GeraetId is null,
                z.GeraetId is null ? z.ErledigtAm is not null : !z.SperreOffen, z.Bemerkung))
            .ToList();

        Teilnehmer = einsatz.Teilnahmen
            .OrderBy(t => t.Mitglied!.Nachname).ThenBy(t => t.Mitglied!.Vorname)
            .Select(t => new Waesche(t.Id, $"{t.Mitglied!.Nachname}, {t.Mitglied.Vorname}", t.PsaWaesche,
                darfAtemschutz && (t.Hautkontakt || t.Exposition == ExpositionStatus.Exponiert), t.PsaWaescheErledigtAm is not null))
            .ToList();

        var fahrzeugIds = einsatz.Fahrzeuge.Select(f => f.FahrzeugId).ToList();
        var beladung = await db.Geraete.AsNoTracking()
            .Where(g => g.FahrzeugId != null && fahrzeugIds.Contains(g.FahrzeugId.Value) && g.AusgesondertAm == null)
            .Select(g => new { g.Inventarnummer, g.Bezeichnung, FahrzeugId = g.FahrzeugId!.Value, Fahrzeug = g.Fahrzeug!.Funkrufname, g.Lagerort })
            .ToListAsync(ct);
        Vorschlaege = beladung.OrderBy(g => g.Inventarnummer).Select(g => g.Inventarnummer).ToList();
        Faecher = beladung.Where(g => g.Lagerort != null)
            .GroupBy(g => (g.FahrzeugId, g.Fahrzeug, g.Lagerort))
            .OrderBy(g => g.Key.Fahrzeug).ThenBy(g => g.Key.Lagerort)
            .Select(g => new SelectListItem($"{g.Key.Fahrzeug}: {g.Key.Lagerort} ({g.Count()} Geräte)", $"{g.Key.FahrzeugId}|{g.Key.Lagerort}"))
            .ToList();
        Fahrzeuge = einsatz.Fahrzeuge
            .Select(f => new SelectListItem(f.Fahrzeug!.Funkrufname, f.FahrzeugId.ToString()))
            .ToList();
        Materialien = await db.Materialien.AsNoTracking()
            .Where(m => m.Aktiv)
            .OrderBy(m => m.Geraeteart!.Ruecklauf == Ruecklauf.Schlauchwerkstatt ? 0 : 1).ThenBy(m => m.Bezeichnung)
            .Select(m => new SelectListItem(m.Bezeichnung + (m.Typ != null ? " " + m.Typ : ""), m.Id.ToString()))
            .ToListAsync(ct);

        var erfasst = await db.EinsatzGeraete.Where(z => z.EinsatzId == id && z.GeraetId != null).Select(z => z.Geraet!.Inventarnummer).ToListAsync(ct);
        AtemschutzOffen = AtemschutzNummern().Except(erfasst).ToList();
        return true;
    }
}
