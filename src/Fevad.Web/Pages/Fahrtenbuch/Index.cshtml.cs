using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
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

namespace Fevad.Web.Pages.Fahrtenbuch;

/// <summary>
/// Fahrt oder Betankung eintragen, gedacht fürs Handy nach der Fahrt (V63, E11). Fahrten aus Einsatz und Übung sind vorbelegt.
/// Jeder trägt sich selbst als Fahrer ein; Gerätewart, Kommandant und Einsatzleiter auch andere. Fehlt eine ausreichende
/// Fahrberechtigung, gibt es nur einen Hinweis, keine Sperre (Fahrerliste aus V59).
/// </summary>
public class IndexModel(AppDbContext db, UserManager<Benutzer> benutzer, IAuthorizationService autorisierung, TimeProvider zeit, Fahrerliste fahrerliste) : PageModel
{
    /// <summary>Wie weit zurück Einsätze und Übungen zur Vorbelegung angeboten werden.</summary>
    private const int VorbelegungTage = 30;

    /// <summary>Eingaben einer Fahrt.</summary>
    [BindProperty]
    public FahrtEingabe Fahrt { get; set; } = new();

    /// <summary>Eingaben einer Betankung.</summary>
    [BindProperty]
    public TankEingabe Tanken { get; set; } = new();

    /// <summary>Gewähltes Fahrzeug.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? FahrzeugId { get; set; }

    /// <summary>Fahrzeuge im Dienst.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];
    /// <summary>Fahrer zur Auswahl; leer, wenn nur für sich selbst eingetragen werden darf.</summary>
    public IReadOnlyList<SelectListItem> Fahrer { get; private set; } = [];
    /// <summary>Darf andere als Fahrer eintragen.</summary>
    public bool DarfFuerAndere { get; private set; }
    /// <summary>Das eigene Konto ist mit keinem Mitglied verknüpft.</summary>
    public bool OhneMitglied { get; private set; }
    /// <summary>Für das eigene Mitglied ist für dieses Fahrzeug keine ausreichende Fahrberechtigung erfasst.</summary>
    public bool OhneFahrberechtigung { get; private set; }
    /// <summary>Bezeichnung und letzter bekannter Kilometerstand des Fahrzeugs.</summary>
    public (string Name, int? Kilometerstand)? Fahrzeug { get; private set; }
    /// <summary>Einsätze und Übungen der letzten Wochen ohne Fahrtenbucheintrag für dieses Fahrzeug.</summary>
    public IReadOnlyList<Vorschlag> Vorschlaege { get; private set; } = [];
    /// <summary>Die letzten Fahrten des Fahrzeugs (ohne Fahrer).</summary>
    public IReadOnlyList<(DateTimeOffset Beginn, Fahrtzweck Zweck, string? Ziel, int? Strecke)> Letzte { get; private set; } = [];

    /// <summary>Einsatz oder Übung, aus dem eine Fahrt vorbelegt werden kann.</summary>
    /// <param name="EinsatzId">Kennung des Einsatzes.</param>
    /// <param name="DienstId">Kennung des Dienstes.</param>
    /// <param name="Text">Anzeige, z. B. „Einsatz 2026-041 B2“.</param>
    /// <param name="Beginn">Beginn.</param>
    public sealed record Vorschlag(Guid? EinsatzId, Guid? DienstId, string Text, DateTimeOffset Beginn);

    /// <summary>Formular einer Fahrt.</summary>
    public sealed class FahrtEingabe
    {
        /// <summary>Tag der Fahrt.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")]
        public DateOnly? Datum { get; set; }
        /// <summary>Abfahrt „HH:MM“.</summary>
        [Required(ErrorMessage = "Abfahrt fehlt."), Display(Name = "Abfahrt")]
        public string Abfahrt { get; set; } = "";
        /// <summary>Rückkehr „HH:MM“.</summary>
        [Display(Name = "Rückkehr")]
        public string? Rueckkehr { get; set; }
        /// <summary>Anlass.</summary>
        [Display(Name = "Anlass")]
        public Fahrtzweck Zweck { get; set; } = Fahrtzweck.Dienstfahrt;
        /// <summary>Ziel oder Strecke.</summary>
        [StringLength(200), Display(Name = "Ziel / Strecke")]
        public string? Ziel { get; set; }
        /// <summary>Kilometerstand bei Abfahrt.</summary>
        [Range(0, 9_999_999), Display(Name = "km-Stand Abfahrt")]
        public int? KmBeginn { get; set; }
        /// <summary>Kilometerstand bei Rückkehr.</summary>
        [Range(0, 9_999_999), Display(Name = "km-Stand Rückkehr")]
        public int? KmEnde { get; set; }
        /// <summary>Gefahrene Kilometer ohne Stände.</summary>
        [Range(0, 5000), Display(Name = "oder gefahrene km")]
        public int? Kilometer { get; set; }
        /// <summary>Fahrer.</summary>
        [Display(Name = "Fahrer")]
        public Guid? FahrerId { get; set; }
        /// <summary>Einsatz der Vorbelegung.</summary>
        public Guid? EinsatzId { get; set; }
        /// <summary>Dienst der Vorbelegung.</summary>
        public Guid? DienstId { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")]
        public string? Bemerkung { get; set; }
    }

    /// <summary>Formular einer Betankung.</summary>
    public sealed class TankEingabe
    {
        /// <summary>Tag.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")]
        public DateOnly? Datum { get; set; }
        /// <summary>Kraftstoff.</summary>
        [Display(Name = "Kraftstoff")]
        public Kraftstoff Kraftstoff { get; set; }
        /// <summary>Liter.</summary>
        [Required(ErrorMessage = "Liter fehlen."), Range(0.01, 2000, ErrorMessage = "Bitte die getankten Liter eingeben."), Display(Name = "Liter")]
        public decimal? Liter { get; set; }
        /// <summary>Betrag.</summary>
        [Range(0, 100_000), Display(Name = "Betrag brutto (€)")]
        public decimal? Betrag { get; set; }
        /// <summary>Kilometerstand.</summary>
        [Range(0, 9_999_999), Display(Name = "km-Stand")]
        public int? Kilometerstand { get; set; }
        /// <summary>Beleg oder Tankstelle.</summary>
        [StringLength(100), Display(Name = "Beleg / Tankstelle")]
        public string? Beleg { get; set; }
    }

    /// <summary>Zeigt die Formulare, auf Wunsch vorbelegt aus einem Einsatz oder einer Übung.</summary>
    /// <param name="einsatzId">Einsatz zur Vorbelegung.</param>
    /// <param name="dienstId">Übung zur Vorbelegung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(Guid? einsatzId, Guid? dienstId, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Fahrt.Datum = heute;
        Fahrt.Abfahrt = Anzeige.Lokal(zeit.GetUtcNow()).ToString("HH:mm");
        Tanken.Datum = heute;
        if (FahrzeugId is { } fahrzeugId && einsatzId is { } e
            && await db.EinsatzFahrzeuge.Where(x => x.EinsatzId == e && x.FahrzeugId == fahrzeugId)
                .Select(x => new { x.Ausgerueckt, x.Zurueck, x.Kilometer, x.Einsatz!.Alarmiert, x.Einsatz.Einsatznummer, x.Einsatz.Stichwort })
                .FirstOrDefaultAsync(ct) is { } ef)
        {
            var beginn = ef.Ausgerueckt ?? ef.Alarmiert;
            Fahrt.Datum = Anzeige.LokalesDatum(beginn);
            Fahrt.Abfahrt = Anzeige.Lokal(beginn).ToString("HH:mm");
            Fahrt.Rueckkehr = ef.Zurueck is { } z ? Anzeige.Lokal(z).ToString("HH:mm") : null;
            Fahrt.Kilometer = ef.Kilometer;
            Fahrt.Zweck = Fahrtzweck.Einsatz;
            Fahrt.Ziel = $"Einsatz {ef.Einsatznummer} {ef.Stichwort}".Trim();
            Fahrt.EinsatzId = e;
        }
        else if (dienstId is { } d && await db.Dienste.Where(x => x.Id == d).Select(x => new { x.Beginn, x.Ende, x.Titel }).FirstOrDefaultAsync(ct) is { } dienst)
        {
            Fahrt.Datum = Anzeige.LokalesDatum(dienst.Beginn);
            Fahrt.Abfahrt = Anzeige.Lokal(dienst.Beginn).ToString("HH:mm");
            Fahrt.Rueckkehr = Anzeige.Lokal(dienst.Ende).ToString("HH:mm");
            Fahrt.Zweck = Fahrtzweck.Uebung;
            Fahrt.Ziel = dienst.Titel;
            Fahrt.DienstId = d;
        }

        await LadenAsync(ct);
        Fahrt.FahrerId ??= await EigenesMitgliedAsync();
        if (Fahrzeug?.Kilometerstand is { } km && Fahrt.KmBeginn is null && Fahrt.Kilometer is null)
        {
            Fahrt.KmBeginn = km;
        }
    }

    /// <summary>Speichert eine Fahrt und schreibt den Kilometerstand am Fahrzeug fort.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostFahrtAsync(CancellationToken ct)
    {
        ModelState.Clear();
        TryValidateModel(Fahrt, nameof(Fahrt));
        var fahrzeug = FahrzeugId is { } id ? await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Id == id && f.AusserDienstAm == null, ct) : null;
        if (fahrzeug is null)
        {
            ModelState.AddModelError(nameof(FahrzeugId), "Bitte ein Fahrzeug wählen.");
        }

        if (!ZeitEingabe.TryUhrzeit(Fahrt.Abfahrt, out var abfahrt))
        {
            ModelState.AddModelError("Fahrt.Abfahrt", "Abfahrt bitte als HH:MM (24 Stunden).");
        }

        TimeOnly? rueckkehr = null;
        if (!string.IsNullOrWhiteSpace(Fahrt.Rueckkehr))
        {
            if (ZeitEingabe.TryUhrzeit(Fahrt.Rueckkehr, out var r))
            {
                rueckkehr = r;
            }
            else
            {
                ModelState.AddModelError("Fahrt.Rueckkehr", "Rückkehr bitte als HH:MM (24 Stunden).");
            }
        }

        if (Fahrt.KmBeginn is { } a && Fahrt.KmEnde is { } b && b < a)
        {
            ModelState.AddModelError("Fahrt.KmEnde", "Der Stand bei Rückkehr ist kleiner als bei Abfahrt.");
        }

        var fahrerId = await FahrerPruefenAsync(Fahrt.FahrerId, "Fahrt.FahrerId", ct);
        if (!ModelState.IsValid || fahrzeug is null)
        {
            await LadenAsync(ct);
            return Page();
        }

        var datum = Fahrt.Datum!.Value;
        var beginn = Anzeige.AlsUtc(datum, abfahrt);
        DateTimeOffset? ende = rueckkehr is { } rk ? Anzeige.AlsUtc(rk < abfahrt ? datum.AddDays(1) : datum, rk) : null;
        var eintrag = new Fahrt
        {
            FahrzeugId = fahrzeug.Id,
            Beginn = beginn,
            Ende = ende,
            Zweck = Fahrt.Zweck,
            Ziel = Leer(Fahrt.Ziel),
            EinsatzId = Fahrt.EinsatzId is { } e && await db.Einsaetze.AnyAsync(x => x.Id == e, ct) ? e : null,
            DienstId = Fahrt.DienstId is { } d && await db.Dienste.AnyAsync(x => x.Id == d, ct) ? d : null,
            FahrerId = fahrerId,
            KmBeginn = Fahrt.KmBeginn,
            KmEnde = Fahrt.KmEnde,
            Kilometer = Fahrt.KmBeginn is not null && Fahrt.KmEnde is not null ? null : Fahrt.Kilometer,
            Bemerkung = Leer(Fahrt.Bemerkung),
        };
        db.Fahrten.Add(eintrag);
        StandFortschreiben(fahrzeug, eintrag.KmEnde, datum);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Fahrt mit {fahrzeug.Funkrufname} eingetragen" + (eintrag.Strecke is { } km ? $" ({km} km)." : ".");
        return RedirectToPage(new { fahrzeugId = fahrzeug.Id });
    }

    /// <summary>Speichert eine Betankung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostTankenAsync(CancellationToken ct)
    {
        ModelState.Clear();
        TryValidateModel(Tanken, nameof(Tanken));
        var fahrzeug = FahrzeugId is { } id ? await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Id == id && f.AusserDienstAm == null, ct) : null;
        if (fahrzeug is null)
        {
            ModelState.AddModelError(nameof(FahrzeugId), "Bitte ein Fahrzeug wählen.");
        }

        var getanktVon = await FahrerPruefenAsync(null, "Tanken.Liter", ct);
        if (!ModelState.IsValid || fahrzeug is null)
        {
            await LadenAsync(ct);
            return Page();
        }

        db.Betankungen.Add(new Betankung
        {
            FahrzeugId = fahrzeug.Id,
            Datum = Tanken.Datum!.Value,
            Kraftstoff = Tanken.Kraftstoff,
            Liter = Tanken.Liter!.Value,
            Betrag = Tanken.Betrag,
            Kilometerstand = Tanken.Kilometerstand,
            Beleg = Leer(Tanken.Beleg),
            GetanktVonId = getanktVon,
        });
        StandFortschreiben(fahrzeug, Tanken.Kilometerstand, Tanken.Datum.Value);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Betankung {fahrzeug.Funkrufname} eingetragen: {Tanken.Liter:0.##} l {Anzeige.Text(Tanken.Kraftstoff)}.";
        return RedirectToPage(new { fahrzeugId = fahrzeug.Id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Ein höherer Kilometerstand wird am Fahrzeug übernommen (für Fahrzeugakte und HU-Planung).</summary>
    private static void StandFortschreiben(Fahrzeug fahrzeug, int? km, DateOnly datum)
    {
        if (km is { } stand && (fahrzeug.Kilometerstand is null || stand > fahrzeug.Kilometerstand))
        {
            fahrzeug.Kilometerstand = stand;
            fahrzeug.StandAm = datum;
        }
    }

    /// <summary>Ohne Recht für andere ist der Fahrer immer das eigene Mitglied.</summary>
    private async Task<Guid?> FahrerPruefenAsync(Guid? gewaehlt, string feld, CancellationToken ct)
    {
        var eigenes = await EigenesMitgliedAsync();
        if (!await DarfFuerAndereAsync() || gewaehlt is null)
        {
            if (eigenes is null && !await DarfFuerAndereAsync())
            {
                ModelState.AddModelError(feld, "Dein Konto ist mit keinem Mitglied verknüpft. Bitte den Gerätewart eintragen lassen.");
            }

            return eigenes;
        }

        if (!await db.Mitglieder.AnyAsync(m => m.Id == gewaehlt, ct))
        {
            ModelState.AddModelError(feld, "Unbekannter Fahrer.");
        }

        return gewaehlt;
    }

    private async Task<Guid?> EigenesMitgliedAsync() => (await benutzer.GetUserAsync(User))?.MitgliedId;

    private async Task<bool> DarfFuerAndereAsync() =>
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded
        || (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded;

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        DarfFuerAndere = await DarfFuerAndereAsync();
        var eigenes = await EigenesMitgliedAsync();
        OhneMitglied = eigenes is null && !DarfFuerAndere;
        Fahrzeuge = await db.Fahrzeuge.Where(f => f.AusserDienstAm == null).OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname + " (" + f.Typ + ")", f.Id.ToString())).ToListAsync(ct);
        if (FahrzeugId is not { } fahrzeugId || await db.Fahrzeuge.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fahrzeugId, ct) is not { } fahrzeug)
        {
            return;
        }

        Fahrzeug = ($"{fahrzeug.Funkrufname} ({fahrzeug.Typ})", fahrzeug.Kilometerstand);
        var liste = await fahrerliste.LadenAsync(fahrzeug.ZulaessigeGesamtmasseKg ?? 0, heute, ct);
        OhneFahrberechtigung = eigenes is { } ich && !liste.Any(z => z.MitgliedId == ich && z.Ausreichend);
        if (DarfFuerAndere)
        {
            Fahrer = liste.Select(z => new SelectListItem(z.Ausreichend ? z.Name : z.Name + " (Fahrberechtigung nicht erfasst)", z.MitgliedId.ToString())).ToList();
        }

        var seit = zeit.GetUtcNow().AddDays(-VorbelegungTage);
        var erfasst = await db.Fahrten.Where(f => f.FahrzeugId == fahrzeugId && (f.EinsatzId != null || f.DienstId != null))
            .Select(f => f.EinsatzId ?? f.DienstId).ToListAsync(ct);
        var einsaetze = await db.EinsatzFahrzeuge.Where(x => x.FahrzeugId == fahrzeugId && x.Einsatz!.Alarmiert >= seit && !erfasst.Contains(x.EinsatzId))
            .Select(x => new Vorschlag(x.EinsatzId, null, "Einsatz " + x.Einsatz!.Einsatznummer + " " + (x.Einsatz.Stichwort ?? ""), x.Einsatz.Alarmiert))
            .ToListAsync(ct);
        var uebungen = await db.Dienste.Where(d => d.Art == DienstArt.Uebung && d.Status != DienstStatus.Abgesagt && d.Beginn >= seit && d.Beginn <= zeit.GetUtcNow() && !erfasst.Contains(d.Id))
            .Select(d => new Vorschlag(null, d.Id, "Übung " + d.Titel, d.Beginn))
            .ToListAsync(ct);
        Vorschlaege = einsaetze.Concat(uebungen).OrderByDescending(v => v.Beginn).Take(10).ToList();
        Letzte = (await db.Fahrten.AsNoTracking().Where(f => f.FahrzeugId == fahrzeugId).OrderByDescending(f => f.Beginn).Take(5).ToListAsync(ct))
            .Select(f => (f.Beginn, f.Zweck, f.Ziel, f.Strecke)).ToList();
    }
}
