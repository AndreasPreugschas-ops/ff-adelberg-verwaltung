using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>
/// Beladekontrolle eines Fahrzeugs (V59): Geräte scannen oder abhaken, Mengenpositionen zählen; das Ergebnis wird festgeschrieben,
/// die Fehlliste kann als Mangel am Fahrzeug gemeldet werden.
/// </summary>
[Authorize(Policy = Berechtigungen.TechnikPruefen)]
public class BeladekontrolleModel(AppDbContext db, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug Fahrzeug { get; private set; } = null!;

    /// <summary>Fächer mit ihren Positionen und erwarteten Geräten.</summary>
    public IReadOnlyList<FachAnsicht> Faecher { get; private set; } = [];

    /// <summary>Name des angemeldeten Mitglieds.</summary>
    public string? EigenerName { get; private set; }

    /// <summary>Eingaben.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Ein Fach in der Kontrolle.</summary>
    /// <param name="Fach">Bezeichnung des Fachs.</param>
    /// <param name="Positionen">Positionen mit ihren Geräten.</param>
    public sealed record FachAnsicht(string Fach, IReadOnlyList<PositionAnsicht> Positionen);

    /// <summary>Eine Position mit den ihr zugeordneten Geräten; ohne Position stehen hier Geräte ohne Soll.</summary>
    /// <param name="Id">Kennung der Position; leer bei Geräten ohne Soll.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Soll">Soll-Menge.</param>
    /// <param name="Mengenposition">Wird gezählt statt gescannt.</param>
    /// <param name="Geraete">Erwartete Geräte.</param>
    public sealed record PositionAnsicht(Guid? Id, string Bezeichnung, int Soll, bool Mengenposition, IReadOnlyList<Beladeabgleich.Geraet> Geraete);

    /// <summary>Formular der Kontrolle.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag der Kontrolle.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        /// <summary>Gefundene Geräte.</summary>
        public List<Guid> Gefunden { get; set; } = [];
        /// <summary>Gezählte Menge je Mengenposition.</summary>
        public Dictionary<Guid, int> Gezaehlt { get; set; } = [];
        /// <summary>Fehlliste als Mangel am Fahrzeug melden.</summary>
        [Display(Name = "Fehlliste als Mangel am Fahrzeug melden")] public bool MangelMelden { get; set; } = true;
        /// <summary>Schwere des Mangels.</summary>
        [Display(Name = "Schwere")] public MangelSchwere Schwere { get; set; } = MangelSchwere.Einschraenkung;
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt die Kontrollliste.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is null)
        {
            return NotFound();
        }

        Formular.Datum = Anzeige.Heute(zeit);
        return Page();
    }

    /// <summary>Schreibt das Ergebnis fest und meldet auf Wunsch die Fehlliste als Mangel.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Fahrzeug oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var daten = await LadenAsync(id, ct);
        if (daten is null)
        {
            return NotFound();
        }

        if (Formular.Datum > Anzeige.Heute(zeit))
        {
            ModelState.AddModelError("Formular.Datum", "Das Datum liegt in der Zukunft.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (positionen, geraete) = daten.Value;
        var zeilen = Beladeabgleich.Kontrolle(positionen, geraete, Formular.Gefunden.ToHashSet(), Formular.Gezaehlt);
        var fehlend = Beladeabgleich.FehlendGesamt(zeilen);
        var mitglied = (await benutzer.GetUserAsync(User))?.MitgliedId;
        var kontrolle = new Beladekontrolle
        {
            FahrzeugId = id,
            Datum = Formular.Datum!.Value,
            KontrolliertVonId = mitglied,
            Vollstaendig = fehlend == 0,
            Fehlend = fehlend,
            Ergebnis = JsonSerializer.Serialize(zeilen, Beladeabgleich.Json),
            Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim(),
        };

        if (fehlend > 0 && Formular.MangelMelden)
        {
            var text = $"Beladekontrolle vom {Anzeige.Datum(kontrolle.Datum)}: Fehlbestand\n{Beladeabgleich.Fehlliste(zeilen)}";
            var mangel = new Mangel
            {
                FahrzeugId = id,
                Beschreibung = text.Length > 2000 ? text[..1997] + "…" : text,
                Schwere = Formular.Schwere,
                Status = MangelStatus.Offen,
                GemeldetAm = zeit.GetUtcNow(),
                GemeldetVonId = mitglied,
            };
            db.Maengel.Add(mangel);
            kontrolle.MangelId = mangel.Id;
        }

        db.Beladekontrollen.Add(kontrolle);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = fehlend == 0
            ? "Beladekontrolle gespeichert: vollständig."
            : $"Beladekontrolle gespeichert: {fehlend} Stück fehlen{(kontrolle.MangelId is null ? "" : ", Mangel gemeldet")}.";
        return RedirectToPage("Details", new { id });
    }

    private async Task<(IReadOnlyList<Beladeposition>, IReadOnlyList<Beladeabgleich.Geraet>)?> LadenAsync(Guid id, CancellationToken ct)
    {
        var fahrzeug = await db.Fahrzeuge.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fahrzeug is null)
        {
            return null;
        }

        Fahrzeug = fahrzeug;
        var mitglied = (await benutzer.GetUserAsync(User))?.MitgliedId;
        EigenerName = mitglied is null ? null
            : await db.Mitglieder.Where(m => m.Id == mitglied).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct);

        var positionen = await db.Beladepositionen.AsNoTracking().Where(p => p.FahrzeugId == id).ToListAsync(ct);
        var geraete = await DetailsModel.BeladungAsync(db, id, ct);
        var (jePosition, ohneSoll) = Beladeabgleich.Zuordnen(positionen, geraete);
        Faecher = positionen
            .Select(p => (Fach: p.Fach, Ansicht: new PositionAnsicht(p.Id, p.Bezeichnung, p.Menge, p.IstMengenposition, jePosition[p.Id]), p.Sortierung))
            .Concat(ohneSoll.Select(g => (Fach: g.Fach ?? "", Ansicht: new PositionAnsicht(null, g.Bezeichnung, 0, false, [g]), Sortierung: int.MaxValue)))
            .GroupBy(x => Beladeabgleich.Fachschluessel(x.Fach))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FachAnsicht(
                string.IsNullOrWhiteSpace(g.First().Fach) ? "ohne Fach" : g.First().Fach.Trim(),
                g.OrderBy(x => x.Sortierung).ThenBy(x => x.Ansicht.Bezeichnung, StringComparer.CurrentCulture).Select(x => x.Ansicht).ToList()))
            .ToList();
        return (positionen, geraete);
    }
}
