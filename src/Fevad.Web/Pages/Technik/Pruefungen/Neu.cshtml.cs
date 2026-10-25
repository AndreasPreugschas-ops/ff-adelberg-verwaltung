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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fevad.Web.Pages.Technik.Pruefungen;

/// <summary>
/// Prüfprotokoll erfassen. Prüfer ist das angemeldete Konto (V58); eine Fremdfirma steht zusätzlich als Text daneben.
/// Ein gespeichertes Protokoll ist abgeschlossen; Korrekturen nur durch Storno und neue Prüfung.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class NeuModel(AppDbContext db, UserManager<Benutzer> benutzer, IOptions<BetriebOptionen> betrieb, TimeProvider zeit, Pruefungserfassung erfassung) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Geprüftes Gerät oder Fahrzeug als Text.</summary>
    public string Gegenstand { get; private set; } = "";
    /// <summary>Prüfplan des Gegenstands.</summary>
    public IReadOnlyList<PruefplanPosition> Plan { get; private set; } = [];
    /// <summary>Gewählte Prüfplanposition.</summary>
    public PruefplanPosition? Position { get; private set; }
    /// <summary>Checkliste der gewählten Position.</summary>
    public IReadOnlyList<Pruefpunkt> Checkliste { get; private set; } = [];

    /// <summary>Mitglied des angemeldeten Kontos; ohne Zuordnung nur mit externem Prüfer.</summary>
    public Guid? EigenesMitglied { get; private set; }
    /// <summary>Name des Prüfers aus dem angemeldeten Konto.</summary>
    public string? EigenerName { get; private set; }
    /// <summary>Einsatz bei einer Prüfung nach Benutzung.</summary>
    public string? Einsatznummer { get; private set; }

    /// <summary>Formular eines Prüfprotokolls.</summary>
    public sealed class Eingabe
    {
        /// <summary>Geprüftes Gerät.</summary>
        public Guid? GeraetId { get; set; }
        /// <summary>Geprüftes Fahrzeug.</summary>
        public Guid? FahrzeugId { get; set; }
        /// <summary>Prüfplanposition.</summary>
        public Guid? PositionId { get; set; }

        /// <summary>Nur ohne Prüfplanposition, z. B. Prüfung nach Gebrauch.</summary>
        public Pruefart Pruefart { get; set; } = Pruefart.Sichtpruefung;

        /// <summary>Prüfdatum.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Prüfdatum")]
        public DateOnly? Datum { get; set; }

        /// <summary>Ergebnis.</summary>
        public PruefErgebnis Ergebnis { get; set; } = PruefErgebnis.Bestanden;

        /// <summary>Externer Prüfer, falls nicht selbst geprüft.</summary>
        [StringLength(200), Display(Name = "Externer Prüfer (Firma), falls nicht selbst geprüft")] public string? PrueferExtern { get; set; }

        /// <summary>Prüfung nach Benutzung in diesem Einsatz (V13).</summary>
        public Guid? EinsatzId { get; set; }

        /// <summary>Nächste Prüfung; leer nach Prüfplan.</summary>
        [Display(Name = "Nächste Prüfung (leer = nach Prüfplan)")]
        public DateOnly? NaechsteFaellig { get; set; }

        /// <summary>Ergebnisse der Prüfpunkte in der Reihenfolge der Checkliste.</summary>
        public List<PunktEingabe> Punkte { get; set; } = [];

        /// <summary>Bemerkung.</summary>
        [StringLength(2000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Festgestellter Mangel.</summary>
        [StringLength(2000), Display(Name = "Festgestellter Mangel")] public string? Mangel { get; set; }
        /// <summary>Schwere des Mangels.</summary>
        public MangelSchwere MangelSchwere { get; set; } = MangelSchwere.Einschraenkung;
    }

    /// <summary>Ergebnis eines Prüfpunkts.</summary>
    public sealed class PunktEingabe
    {
        /// <summary>Punkt in Ordnung.</summary>
        public bool InOrdnung { get; set; }
        /// <summary>Messwert oder Text.</summary>
        [StringLength(200)] public string? Wert { get; set; }
    }

    /// <summary>Zeigt das Formular, vorbelegt mit Prüfplanposition, Prüfart und Einsatz.</summary>
    public async Task<IActionResult> OnGetAsync(Guid? geraetId, Guid? fahrzeugId, Guid? positionId, Guid? einsatzId, Pruefart? pruefart, CancellationToken ct)
    {
        Formular.GeraetId = geraetId;
        Formular.FahrzeugId = geraetId is null ? fahrzeugId : null;
        Formular.PositionId = positionId;
        Formular.EinsatzId = einsatzId;
        Formular.Datum = Anzeige.Heute(zeit);
        if (pruefart is not null)
        {
            Formular.Pruefart = pruefart.Value;
        }

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

    /// <summary>Speichert das Protokoll samt Folgen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Gerät oder Fahrzeug, bei Fehlern die Seite oder 404.</returns>
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

        if (EigenesMitglied is null && string.IsNullOrWhiteSpace(Formular.PrueferExtern))
        {
            ModelState.AddModelError("Formular.PrueferExtern", "Ihr Konto ist keinem Mitglied zugeordnet. Bitte den externen Prüfer eintragen.");
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
            PrueferId = EigenesMitglied,
            PrueferExtern = string.IsNullOrWhiteSpace(Formular.PrueferExtern) ? null : Formular.PrueferExtern.Trim(),
            EinsatzId = Formular.EinsatzId,
            Bemerkung = Formular.Bemerkung,
            ErfasstImNotbetrieb = betrieb.Value.Notbetrieb,
        };

        await erfassung.AnlegenAsync(pruefung, Position, Formular.Mangel, Formular.MangelSchwere, ct);
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

        EigenesMitglied = (await benutzer.GetUserAsync(User))?.MitgliedId;
        EigenerName = EigenesMitglied is null
            ? null
            : await db.Mitglieder.Where(m => m.Id == EigenesMitglied).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct);
        if (Formular.EinsatzId is not null)
        {
            Einsatznummer = await db.Einsaetze.Where(e => e.Id == Formular.EinsatzId).Select(e => e.Einsatznummer).FirstOrDefaultAsync(ct);
            Formular.EinsatzId = Einsatznummer is null ? null : Formular.EinsatzId;
        }

        return true;
    }
}
