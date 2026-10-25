using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Technik.Pruefungen;

/// <summary>
/// Sammelprüfung (V58): Geräte scannen oder ein Fach laden, einmal „bestanden“ für alle, Ausnahmen einzeln.
/// Prüfer ist das angemeldete Konto; jedes Gerät bekommt ein eigenes, abgeschlossenes Protokoll.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class SammelModel(AppDbContext db, UserManager<Benutzer> benutzer, IOptions<BetriebOptionen> betrieb, TimeProvider zeit, Pruefungserfassung erfassung) : PageModel
{
    /// <summary>Mehr Geräte nimmt eine Sammelprüfung nicht an; größere Bestände in mehreren Durchgängen.</summary>
    public const int HoechstZahl = 300;

    /// <summary>Prüfart, Datum, externer Prüfer und die Liste der Geräte.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Name des Prüfers aus dem angemeldeten Konto.</summary>
    public string? EigenerName { get; private set; }
    /// <summary>Mitglied des angemeldeten Kontos.</summary>
    public Guid? EigenesMitglied { get; private set; }
    /// <summary>Fahrzeuge für „Fach laden“.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Liste nach einem Fehler beim Speichern, damit nichts neu gescannt werden muss.</summary>
    public IReadOnlyList<object> Vorbelegt { get; private set; } = [];

    /// <summary>Geräte, die nach dem Speichern eine Plakette bekommen können.</summary>
    public IReadOnlyList<Guid> Erfasst { get; private set; } = [];

    /// <summary>Gefundenes Gerät für die Liste; Hinweis, wenn die Prüfart nicht im Prüfplan steht.</summary>
    public sealed record Treffer(Guid Id, string Nummer, string Bezeichnung, string? Standort, string? Hinweis, bool Gesperrt);

    /// <summary>Formular der Sammelprüfung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Gilt für alle Geräte der Liste.</summary>
        public Pruefart Pruefart { get; set; } = Pruefart.Sichtpruefung;

        /// <summary>Prüfdatum, nicht in der Zukunft.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Prüfdatum")]
        public DateOnly? Datum { get; set; }

        /// <summary>Fremdfirma als Text, falls nicht selbst geprüft.</summary>
        [StringLength(200), Display(Name = "Externer Prüfer (Firma), falls nicht selbst geprüft")]
        public string? PrueferExtern { get; set; }

        /// <summary>Bemerkung für alle Protokolle.</summary>
        [StringLength(2000), Display(Name = "Bemerkung für alle")]
        public string? Bemerkung { get; set; }

        /// <summary>Geräte mit Ergebnis.</summary>
        public List<Zeile> Zeilen { get; set; } = [];
    }

    /// <summary>Ein Gerät der Sammelprüfung.</summary>
    public sealed class Zeile
    {
        /// <summary>Geprüftes Gerät.</summary>
        public Guid GeraetId { get; set; }
        /// <summary>Vorbelegt mit „bestanden“; Ausnahmen einzeln.</summary>
        public PruefErgebnis Ergebnis { get; set; } = PruefErgebnis.Bestanden;
        /// <summary>Befund bei einer Ausnahme.</summary>
        [StringLength(2000)] public string? Mangel { get; set; }
    }

    /// <summary>Zeigt die leere Liste; nach dem Speichern mit Links zu den Plaketten.</summary>
    public async Task OnGetAsync(Pruefart? pruefart, string? erfasst, CancellationToken ct)
    {
        Formular.Pruefart = pruefart ?? Pruefart.Sichtpruefung;
        Formular.Datum = Anzeige.Heute(zeit);
        Erfasst = (erfasst ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        await LadenAsync(ct);
    }

    /// <summary>Gescannte oder eingetippte Nummer nachschlagen; liefert JSON für die Liste.</summary>
    public async Task<IActionResult> OnGetGeraetAsync(string? nummer, Pruefart pruefart, CancellationToken ct)
    {
        var inventarnummer = Geraetecode.NummerAusScan(nummer);
        var treffer = await TrefferAsync(db.Geraete.Where(g => g.Inventarnummer == inventarnummer), pruefart, ct);
        return treffer.Count == 0 ? NotFound(new { meldung = $"Kein Gerät mit der Nummer „{inventarnummer}“." }) : new JsonResult(treffer[0]);
    }

    /// <summary>Alle Geräte eines Fahrzeugs oder eines Fachs (Lagerort) laden.</summary>
    public async Task<IActionResult> OnGetFachAsync(Guid? fahrzeugId, string? lagerort, Pruefart pruefart, CancellationToken ct)
    {
        var abfrage = db.Geraete.Where(g => g.AusgesondertAm == null);
        abfrage = fahrzeugId is null ? abfrage.Where(g => g.FahrzeugId == null) : abfrage.Where(g => g.FahrzeugId == fahrzeugId);
        if (!string.IsNullOrWhiteSpace(lagerort))
        {
            var ort = lagerort.Trim();
            abfrage = abfrage.Where(g => g.Lagerort == ort);
        }

        return new JsonResult(await TrefferAsync(abfrage, pruefart, ct));
    }

    /// <summary>Alle Geräte, die nach einem Einsatz noch gesperrt sind (V13).</summary>
    public async Task<IActionResult> OnGetGesperrteAsync(Pruefart pruefart, CancellationToken ct) =>
        new JsonResult(await TrefferAsync(
            db.Geraete.Where(g => g.AusgesondertAm == null && db.Maengel.Any(m => m.GeraetId == g.Id && m.EinsatzId != null && m.Status != MangelStatus.Behoben)),
            pruefart, ct));

    /// <summary>Legt je Gerät ein abgeschlossenes Protokoll an.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var zeilen = Formular.Zeilen.Where(z => z.GeraetId != Guid.Empty).DistinctBy(z => z.GeraetId).ToList();
        if (zeilen.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Noch kein Gerät in der Liste.");
        }
        else if (zeilen.Count > HoechstZahl)
        {
            ModelState.AddModelError(string.Empty, $"Höchstens {HoechstZahl} Geräte je Sammelprüfung.");
        }

        if (Formular.Datum > Anzeige.Heute(zeit))
        {
            ModelState.AddModelError("Formular.Datum", "Das Prüfdatum liegt in der Zukunft.");
        }

        if (EigenesMitglied is null && string.IsNullOrWhiteSpace(Formular.PrueferExtern))
        {
            ModelState.AddModelError("Formular.PrueferExtern", "Ihr Konto ist keinem Mitglied zugeordnet. Bitte den externen Prüfer eintragen.");
        }

        var ids = zeilen.Select(z => z.GeraetId).ToList();
        var geraete = await db.Geraete.AsNoTracking()
            .Where(g => ids.Contains(g.Id) && g.AusgesondertAm == null)
            .Select(g => new { g.Id, g.Inventarnummer, g.GeraeteartId })
            .ToDictionaryAsync(g => g.Id, ct);
        var artIds = geraete.Values.Select(g => g.GeraeteartId).Distinct().ToList();
        var positionen = (await db.PruefplanPositionen.AsNoTracking()
                .Where(p => artIds.Contains(p.GeraeteartId) && p.Pruefart == Formular.Pruefart)
                .ToListAsync(ct))
            .GroupBy(p => p.GeraeteartId)
            .ToDictionary(g => g.Key, g => g.First());
        var einsaetze = (await db.Maengel.AsNoTracking()
                .Where(m => m.GeraetId != null && ids.Contains(m.GeraetId.Value) && m.EinsatzId != null && m.Status != MangelStatus.Behoben)
                .OrderBy(m => m.GemeldetAm)
                .Select(m => new { GeraetId = m.GeraetId!.Value, EinsatzId = m.EinsatzId!.Value })
                .ToListAsync(ct))
            .GroupBy(m => m.GeraetId)
            .ToDictionary(g => g.Key, g => g.Last().EinsatzId);

        foreach (var (zeile, i) in zeilen.Select((z, i) => (z, i)))
        {
            if (!geraete.ContainsKey(zeile.GeraetId))
            {
                ModelState.AddModelError(string.Empty, "Ein Gerät der Liste gibt es nicht mehr oder es ist ausgesondert.");
            }
            else if (zeile.Ergebnis == PruefErgebnis.BestandenMitMaengeln && string.IsNullOrWhiteSpace(zeile.Mangel))
            {
                ModelState.AddModelError(string.Empty, $"{geraete[zeile.GeraetId].Inventarnummer}: Bitte den Mangel beschreiben.");
            }
        }

        if (!ModelState.IsValid)
        {
            var treffer = (await TrefferAsync(db.Geraete.Where(g => ids.Contains(g.Id)), Formular.Pruefart, ct)).ToDictionary(t => t.Id);
            Vorbelegt = zeilen.Where(z => treffer.ContainsKey(z.GeraetId))
                .Select(z => (object)new
                {
                    treffer[z.GeraetId].Id, treffer[z.GeraetId].Nummer, treffer[z.GeraetId].Bezeichnung, treffer[z.GeraetId].Standort,
                    treffer[z.GeraetId].Hinweis, treffer[z.GeraetId].Gesperrt, Ergebnis = z.Ergebnis.ToString(), Mangel = z.Mangel ?? "",
                })
                .ToList();
            return Page();
        }

        var extern_ = string.IsNullOrWhiteSpace(Formular.PrueferExtern) ? null : Formular.PrueferExtern.Trim();
        var bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();
        foreach (var zeile in zeilen)
        {
            var geraet = geraete[zeile.GeraetId];
            var position = positionen.GetValueOrDefault(geraet.GeraeteartId);

            // Eine Sammelprüfung bestätigt die Prüfpunkte nur als Ganzes; bei Ausnahmen bleibt der einzelne Punkt offen.
            var punkte = Pruefpunkt.AusJson(position?.Checkliste)
                .Select(p => new PruefpunktErgebnis(p.Text, p.Typ, p.Einheit,
                    p.Typ == PruefpunktTyp.JaNein && zeile.Ergebnis == PruefErgebnis.Bestanden ? true : null, null));
            var pruefung = new Pruefung
            {
                GeraetId = zeile.GeraetId,
                PruefplanPositionId = position?.Id,
                Pruefart = Formular.Pruefart,
                Datum = Formular.Datum!.Value,
                Ergebnis = zeile.Ergebnis,
                Ergebnisse = PruefpunktErgebnis.AlsJson(punkte),
                PrueferId = EigenesMitglied,
                PrueferExtern = extern_,
                EinsatzId = einsaetze.TryGetValue(zeile.GeraetId, out var einsatzId) ? einsatzId : null,
                Bemerkung = bemerkung is null ? "Sammelprüfung" : $"Sammelprüfung: {bemerkung}",
                ErfasstImNotbetrieb = betrieb.Value.Notbetrieb,
            };
            await erfassung.AnlegenAsync(pruefung, position, zeile.Mangel, MangelSchwere.Einschraenkung, ct);
        }

        await db.SaveChangesAsync(ct);
        var bestanden = zeilen.Where(z => z.Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln).Select(z => z.GeraetId).ToList();
        TempData["Meldung"] = $"{zeilen.Count} Prüfungen gespeichert ({bestanden.Count} bestanden).";
        return RedirectToPage(new { pruefart = Formular.Pruefart, erfasst = string.Join(',', bestanden) });
    }

    private async Task<List<Treffer>> TrefferAsync(IQueryable<Geraet> quelle, Pruefart pruefart, CancellationToken ct)
    {
        var geraete = await quelle.AsNoTracking()
            .OrderBy(g => g.Fahrzeug!.Funkrufname).ThenBy(g => g.Lagerort).ThenBy(g => g.Inventarnummer)
            .Take(HoechstZahl)
            .Select(g => new
            {
                g.Id,
                g.Inventarnummer,
                g.Bezeichnung,
                Fahrzeug = g.Fahrzeug!.Funkrufname,
                g.Lagerort,
                g.AusgesondertAm,
                MitPosition = db.PruefplanPositionen.Any(p => p.GeraeteartId == g.GeraeteartId && p.Pruefart == pruefart),
                Gesperrt = db.Maengel.Any(m => m.GeraetId == g.Id && m.EinsatzId != null && m.Status != MangelStatus.Behoben),
            })
            .ToListAsync(ct);

        return geraete
            .Select(g => new Treffer(g.Id, g.Inventarnummer, g.Bezeichnung,
                g.Fahrzeug is null ? g.Lagerort : g.Lagerort is null ? g.Fahrzeug : $"{g.Fahrzeug}, {g.Lagerort}",
                g.AusgesondertAm is not null ? "ausgesondert" : g.MitPosition ? null : "Prüfart nicht im Prüfplan dieser Geräteart",
                g.Gesperrt))
            .ToList();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        EigenesMitglied = (await benutzer.GetUserAsync(User))?.MitgliedId;
        EigenerName = EigenesMitglied is null
            ? null
            : await db.Mitglieder.Where(m => m.Id == EigenesMitglied).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => f.AusserDienstAm == null)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname + " (" + f.Typ + ")", f.Id.ToString()))
            .ToListAsync(ct);
    }
}
