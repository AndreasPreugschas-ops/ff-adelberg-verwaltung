using System.ComponentModel.DataAnnotations;
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
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Technik.Pruefungen;

/// <summary>Prüfprotokoll erfassen. Ein gespeichertes Protokoll ist abgeschlossen; Korrekturen nur als neue Prüfung.</summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class NeuModel(AppDbContext db, UserManager<Benutzer> benutzer, IOptions<BetriebOptionen> betrieb, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public string Gegenstand { get; private set; } = "";
    public IReadOnlyList<PruefplanPosition> Plan { get; private set; } = [];
    public PruefplanPosition? Position { get; private set; }
    public IReadOnlyList<Pruefpunkt> Checkliste { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Pruefer { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? GeraetId { get; set; }
        public Guid? FahrzeugId { get; set; }
        public Guid? PositionId { get; set; }

        /// <summary>Nur ohne Prüfplanposition, z. B. Prüfung nach Gebrauch.</summary>
        public Pruefart Pruefart { get; set; } = Pruefart.Sichtpruefung;

        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Prüfdatum")]
        public DateOnly? Datum { get; set; }

        public PruefErgebnis Ergebnis { get; set; } = PruefErgebnis.Bestanden;

        [Display(Name = "Prüfer (Mitglied)")] public Guid? PrueferId { get; set; }
        [StringLength(200), Display(Name = "oder externer Prüfer (Firma)")] public string? PrueferExtern { get; set; }

        [Display(Name = "Nächste Prüfung (leer = nach Prüfplan)")]
        public DateOnly? NaechsteFaellig { get; set; }

        public List<PunktEingabe> Punkte { get; set; } = [];

        [StringLength(2000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        [StringLength(2000), Display(Name = "Festgestellter Mangel")] public string? Mangel { get; set; }
        public MangelSchwere MangelSchwere { get; set; } = MangelSchwere.Einschraenkung;
    }

    public sealed class PunktEingabe
    {
        public bool InOrdnung { get; set; }
        [StringLength(200)] public string? Wert { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? geraetId, Guid? fahrzeugId, Guid? positionId, CancellationToken ct)
    {
        Formular.GeraetId = geraetId;
        Formular.FahrzeugId = geraetId is null ? fahrzeugId : null;
        Formular.PositionId = positionId;
        Formular.Datum = Anzeige.Heute(zeit);
        Formular.PrueferId = (await benutzer.GetUserAsync(User))?.MitgliedId;

        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        if (Position is not null)
        {
            Formular.Pruefart = Position.Pruefart;
        }

        // Vorbelegt: alles in Ordnung. Der Prüfer nimmt weg, was nicht passt.
        Formular.Punkte = Checkliste.Select(p => new PunktEingabe { InOrdnung = p.Typ == PruefpunktTyp.JaNein }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        if (Formular.Datum > heute)
        {
            ModelState.AddModelError("Formular.Datum", "Das Prüfdatum liegt in der Zukunft.");
        }

        if (Formular.PrueferId is null && string.IsNullOrWhiteSpace(Formular.PrueferExtern))
        {
            ModelState.AddModelError("Formular.PrueferId", "Wer hat geprüft? Mitglied wählen oder externen Prüfer eintragen.");
        }
        else if (Formular.PrueferId is not null && !await db.Mitglieder.AnyAsync(m => m.Id == Formular.PrueferId, ct))
        {
            ModelState.AddModelError("Formular.PrueferId", "Unbekanntes Mitglied.");
        }

        var punkte = Checkliste
            .Select((p, i) => (Punkt: p, Eingabe: i < Formular.Punkte.Count ? Formular.Punkte[i] : new PunktEingabe()))
            .ToList();
        var nichtInOrdnung = punkte.Any(p => p.Punkt.Typ == PruefpunktTyp.JaNein && !p.Eingabe.InOrdnung);
        if (Formular.Ergebnis == PruefErgebnis.Bestanden && nichtInOrdnung)
        {
            ModelState.AddModelError("Formular.Ergebnis", "Nicht alle Prüfpunkte sind in Ordnung. Bitte Ergebnis „mit Mängeln“ oder „nicht bestanden“ wählen.");
        }

        if (Formular.Ergebnis == PruefErgebnis.BestandenMitMaengeln && string.IsNullOrWhiteSpace(Formular.Mangel))
        {
            ModelState.AddModelError("Formular.Mangel", "Bitte den Mangel beschreiben.");
        }

        if (Formular.Ergebnis == PruefErgebnis.Ausgesondert && Formular.FahrzeugId is not null)
        {
            ModelState.AddModelError("Formular.Ergebnis", "Fahrzeuge werden über „Außer Dienst seit“ in den Stammdaten abgemeldet.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var pruefung = new Pruefung
        {
            GeraetId = Formular.GeraetId,
            FahrzeugId = Formular.FahrzeugId,
            PruefplanPositionId = Position?.Id,
            Pruefart = Position?.Pruefart ?? Formular.Pruefart,
            Datum = Formular.Datum!.Value,
            Ergebnis = Formular.Ergebnis,
            Ergebnisse = PruefpunktErgebnis.AlsJson(punkte.Select(p => new PruefpunktErgebnis(
                p.Punkt.Text, p.Punkt.Typ, p.Punkt.Einheit,
                p.Punkt.Typ == PruefpunktTyp.JaNein ? p.Eingabe.InOrdnung : null,
                string.IsNullOrWhiteSpace(p.Eingabe.Wert) ? null : p.Eingabe.Wert.Trim()))),
            NaechsteFaellig = Formular.NaechsteFaellig,
            PrueferId = Formular.PrueferId,
            PrueferExtern = string.IsNullOrWhiteSpace(Formular.PrueferExtern) ? null : Formular.PrueferExtern.Trim(),
            Bemerkung = Formular.Bemerkung,
            Abgeschlossen = true,
            ErfasstImNotbetrieb = betrieb.Value.Notbetrieb,
        };

        // Nur eine bestandene Prüfung setzt die Frist neu.
        if (Position is { IntervallMonate: > 0 } && pruefung.Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln)
        {
            pruefung.NaechsteFaelligkeitVorbelegen(Position);
        }

        db.Pruefungen.Add(pruefung);

        var mangel = Formular.Mangel?.Trim();
        // Nicht bestanden heißt außer Betrieb, auch wenn der Mangel beschrieben ist.
        if (pruefung.Ergebnis == PruefErgebnis.NichtBestanden)
        {
            mangel = string.IsNullOrEmpty(mangel) ? $"{Anzeige.Text(pruefung.Pruefart)} nicht bestanden" : mangel;
            Formular.MangelSchwere = MangelSchwere.AusserBetrieb;
        }

        if (!string.IsNullOrEmpty(mangel) && pruefung.Ergebnis != PruefErgebnis.Ausgesondert)
        {
            db.Maengel.Add(new Mangel
            {
                GeraetId = pruefung.GeraetId,
                FahrzeugId = pruefung.FahrzeugId,
                Beschreibung = mangel,
                Schwere = Formular.MangelSchwere,
                Status = MangelStatus.Offen,
                GemeldetAm = zeit.GetUtcNow(),
                GemeldetVonId = pruefung.PrueferId,
                PruefungId = pruefung.Id,
            });
        }

        if (pruefung.Ergebnis == PruefErgebnis.Ausgesondert && await db.Geraete.FirstOrDefaultAsync(g => g.Id == pruefung.GeraetId, ct) is { } geraet)
        {
            geraet.AusgesondertAm ??= pruefung.Datum;
            // Die volle Bemerkung steht im Prüfprotokoll; der Grund ist auf 200 Zeichen begrenzt.
            var grund = $"{Anzeige.Text(pruefung.Pruefart)} vom {Anzeige.Datum(pruefung.Datum)}" + (pruefung.Bemerkung is null ? "" : $": {pruefung.Bemerkung}");
            geraet.Aussonderungsgrund ??= grund.Length <= 200 ? grund : grund[..199] + "…";
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Prüfung für {Gegenstand} gespeichert.";
        return pruefung.GeraetId is not null
            ? RedirectToPage("/Technik/Geraete/Details", new { id = pruefung.GeraetId })
            : RedirectToPage("/Technik/Fahrzeuge/Details", new { id = pruefung.FahrzeugId });
    }

    private async Task<bool> LadenAsync(CancellationToken ct)
    {
        Guid? artId;
        if (Formular.GeraetId is not null)
        {
            var geraet = await db.Geraete.AsNoTracking()
                .Where(g => g.Id == Formular.GeraetId && g.AusgesondertAm == null)
                .Select(g => new { g.Inventarnummer, g.Bezeichnung, g.GeraeteartId })
                .FirstOrDefaultAsync(ct);
            if (geraet is null)
            {
                return false;
            }

            Gegenstand = $"{geraet.Inventarnummer} {geraet.Bezeichnung}";
            artId = geraet.GeraeteartId;
            Formular.FahrzeugId = null;
        }
        else if (Formular.FahrzeugId is not null)
        {
            var fahrzeug = await db.Fahrzeuge.AsNoTracking()
                .Where(f => f.Id == Formular.FahrzeugId)
                .Select(f => new { f.Funkrufname, f.PruefvorlageId })
                .FirstOrDefaultAsync(ct);
            if (fahrzeug is null)
            {
                return false;
            }

            Gegenstand = fahrzeug.Funkrufname;
            artId = fahrzeug.PruefvorlageId;
        }
        else
        {
            return false;
        }

        Plan = artId is null
            ? []
            : await db.PruefplanPositionen.AsNoTracking().Where(p => p.GeraeteartId == artId).OrderBy(p => p.Pruefart).ToListAsync(ct);
        Position = Plan.FirstOrDefault(p => p.Id == Formular.PositionId);
        Formular.PositionId = Position?.Id;
        Checkliste = Pruefpunkt.AusJson(Position?.Checkliste);

        Pruefer = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigBis == null) || m.Id == Formular.PrueferId)
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString()))
            .ToListAsync(ct);
        return true;
    }
}
