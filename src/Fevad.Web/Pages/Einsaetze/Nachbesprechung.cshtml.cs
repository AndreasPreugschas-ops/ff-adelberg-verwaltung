using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Einsatznachbesprechung (V17): Protokoll, PSNV-Kennzeichen ohne Personenbezug und Maßnahmen mit Verantwortlichem und Termin.
/// Führungskräfte schreiben und schlagen vor, Kommandant und Stellvertreter geben frei (Andi zu V17).
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeBearbeiten)]
public class NachbesprechungModel(AppDbContext db, Massnahmenliste massnahmen, ParameterLeser parameter, IAuthorizationService autorisierung,
    IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Der Einsatz.</summary>
    public Einsatz Einsatz { get; private set; } = null!;
    /// <summary>Die Nachbesprechung; <c>null</c>, solange keine gespeichert ist.</summary>
    public Nachbesprechung? Besprechung { get; private set; }
    /// <summary>Darf Maßnahmen freigeben und verwerfen (Kommandant, Stellvertreter).</summary>
    public bool DarfFreigeben { get; private set; }
    /// <summary>Auswahl der Mitglieder für den Verantwortlichen.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];
    /// <summary>Auswahl der Fahrzeuge für Mängel.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];
    /// <summary>Heutiges Datum für die Ampel.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Tage vor dem Termin, ab denen gelb gilt.</summary>
    public int WarnTage { get; private set; }

    /// <summary>Protokoll und PSNV-Kennzeichen.</summary>
    [BindProperty]
    public Protokollformular Protokoll { get; set; } = new();

    /// <summary>Neue Maßnahme.</summary>
    [BindProperty]
    public Massnahmeformular Neu { get; set; } = new();

    /// <summary>Formular des Protokolls.</summary>
    public sealed class Protokollformular
    {
        /// <summary>Tag der Nachbesprechung.</summary>
        public DateOnly? Datum { get; set; }
        /// <summary>Teilnehmer als Funktionen oder Einheiten.</summary>
        [StringLength(500)] public string? Teilnehmer { get; set; }
        /// <summary>Was gut lief.</summary>
        [StringLength(4000)] public string? Gut { get; set; }
        /// <summary>Was besser werden muss.</summary>
        [StringLength(4000)] public string? Verbesserung { get; set; }
        /// <summary>PSNV: belastender Einsatz.</summary>
        public bool PsnvBelastend { get; set; }
        /// <summary>PSNV: Nachbesprechung zur Belastung erfolgt.</summary>
        public bool PsnvNachbesprechung { get; set; }
        /// <summary>PSNV: Nachsorge angeboten.</summary>
        public bool PsnvNachsorgeAngeboten { get; set; }
    }

    /// <summary>Formular einer neuen Maßnahme.</summary>
    public sealed class Massnahmeformular
    {
        /// <summary>Was zu tun ist.</summary>
        [StringLength(300)] public string? Titel { get; set; }
        /// <summary>Art.</summary>
        public MassnahmeArt Art { get; set; }
        /// <summary>Verantwortliches Mitglied.</summary>
        public Guid? VerantwortlichId { get; set; }
        /// <summary>Termin.</summary>
        public DateOnly? ErledigenBis { get; set; }
        /// <summary>Fahrzeug bei einem Mangel.</summary>
        public Guid? FahrzeugId { get; set; }
        /// <summary>Inventarnummer des Geräts bei einem Mangel.</summary>
        [StringLength(30)] public string? Inventarnummer { get; set; }
    }

    /// <summary>Zeigt die Nachbesprechung.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Protokoll = new Protokollformular
        {
            Datum = Besprechung?.Datum ?? Heute, Teilnehmer = Besprechung?.Teilnehmer, Gut = Besprechung?.Gut, Verbesserung = Besprechung?.Verbesserung,
            PsnvBelastend = Einsatz.PsnvBelastend, PsnvNachbesprechung = Einsatz.PsnvNachbesprechung, PsnvNachsorgeAngeboten = Einsatz.PsnvNachsorgeAngeboten,
        };
        return Page();
    }

    /// <summary>Speichert Protokoll und PSNV-Kennzeichen.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostProtokollAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.Include(e => e.Nachbesprechung).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["Fehler"] = "Ein Text ist zu lang.";
            return RedirectToPage(new { id });
        }

        var besprechung = einsatz.Nachbesprechung ?? new Nachbesprechung { EinsatzId = id };
        if (einsatz.Nachbesprechung is null)
        {
            db.Nachbesprechungen.Add(besprechung);
        }

        besprechung.Datum = Protokoll.Datum ?? Anzeige.Heute(zeit);
        besprechung.Teilnehmer = Leer(Protokoll.Teilnehmer);
        besprechung.Gut = Leer(Protokoll.Gut);
        besprechung.Verbesserung = Leer(Protokoll.Verbesserung);
        einsatz.PsnvBelastend = Protokoll.PsnvBelastend;
        einsatz.PsnvNachbesprechung = Protokoll.PsnvNachbesprechung;
        einsatz.PsnvNachsorgeAngeboten = Protokoll.PsnvNachsorgeAngeboten;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Nachbesprechung gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Schlägt eine Maßnahme vor; legt die Nachbesprechung an, falls es noch keine gibt.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostMassnahmeAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.Include(e => e.Nachbesprechung).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(Neu.Titel) || !ModelState.IsValid)
        {
            TempData["Fehler"] = "Bitte beschreiben, was zu tun ist.";
            return RedirectToPage(new { id });
        }

        Guid? geraetId = null;
        Guid? fahrzeugId = null;
        if (Neu.Art == MassnahmeArt.Mangel)
        {
            var nummer = Neu.Inventarnummer?.Trim();
            if (!string.IsNullOrEmpty(nummer))
            {
                geraetId = await db.Geraete.Where(g => g.Inventarnummer == nummer && g.AusgesondertAm == null).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);
                if (geraetId is null)
                {
                    TempData["Fehler"] = "Kein Gerät mit dieser Inventarnummer.";
                    return RedirectToPage(new { id });
                }
            }

            fahrzeugId = Neu.FahrzeugId is { } f && await db.Fahrzeuge.AnyAsync(x => x.Id == f, ct) ? f : null;
            if (geraetId is null && fahrzeugId is null)
            {
                TempData["Fehler"] = "Bei einem Mangel bitte Fahrzeug oder Inventarnummer angeben.";
                return RedirectToPage(new { id });
            }
        }

        var besprechung = einsatz.Nachbesprechung;
        if (besprechung is null)
        {
            besprechung = new Nachbesprechung { EinsatzId = id, Datum = Anzeige.Heute(zeit) };
            db.Nachbesprechungen.Add(besprechung);
        }

        var verantwortlich = Neu.VerantwortlichId is { } v && await db.Mitglieder.AnyAsync(m => m.Id == v, ct) ? v : (Guid?)null;
        db.Massnahmen.Add(new Massnahme
        {
            NachbesprechungId = besprechung.Id, Titel = Neu.Titel.Trim(), Art = Neu.Art, Status = MassnahmeStatus.Vorgeschlagen,
            VerantwortlichId = verantwortlich, ErledigenBis = Neu.ErledigenBis, FahrzeugId = fahrzeugId, GeraetId = geraetId,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Maßnahme vorgeschlagen. Kommandant oder Stellvertreter geben sie frei.";
        return RedirectToPage(new { id });
    }

    /// <summary>Gibt frei, verwirft, erledigt oder entfernt eine Maßnahme.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="massnahmeId">Kennung der Maßnahme.</param>
    /// <param name="aktion">„freigeben“, „verwerfen“, „erledigt“ oder „entfernen“.</param>
    /// <param name="grund">Grund beim Verwerfen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAktionAsync(Guid id, Guid massnahmeId, string aktion, string? grund, CancellationToken ct)
    {
        var massnahme = await db.Massnahmen.Include(m => m.Nachbesprechung).FirstOrDefaultAsync(m => m.Id == massnahmeId && m.Nachbesprechung!.EinsatzId == id, ct);
        if (massnahme is null)
        {
            return NotFound();
        }

        var darfFreigeben = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeFreigeben)).Succeeded;
        var konto = benutzer.Id ?? Guid.Empty;
        switch (aktion)
        {
            case "freigeben" when darfFreigeben:
                await massnahmen.FreigebenAsync(massnahmeId, konto, zeit.GetUtcNow(), ct);
                TempData["Meldung"] = massnahme.Art == MassnahmeArt.Mangel ? "Freigegeben; der Mangel steht in der Mängelliste der Gerätewarte." : "Freigegeben.";
                return RedirectToPage(new { id });
            case "verwerfen" when darfFreigeben && massnahme.Status == MassnahmeStatus.Vorgeschlagen:
                massnahme.Verwerfen(konto, grund);
                break;
            case "erledigt" when darfFreigeben && massnahme.Status == MassnahmeStatus.Freigegeben:
                massnahme.Erledigen(Anzeige.Heute(zeit));
                break;
            case "entfernen" when massnahme.Status == MassnahmeStatus.Vorgeschlagen:
                db.Massnahmen.Remove(massnahme);
                break;
            default:
                return Forbid();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Gespeichert.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var einsatz = await db.Einsaetze.AsNoTracking()
            .Include(e => e.Nachbesprechung!).ThenInclude(n => n.Massnahmen).ThenInclude(m => m.Verantwortlich)
            .Include(e => e.Nachbesprechung!).ThenInclude(n => n.Massnahmen).ThenInclude(m => m.Fahrzeug)
            .Include(e => e.Nachbesprechung!).ThenInclude(n => n.Massnahmen).ThenInclude(m => m.Geraet)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return false;
        }

        Einsatz = einsatz;
        Besprechung = einsatz.Nachbesprechung;
        Heute = Anzeige.Heute(zeit);
        DarfFreigeben = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeFreigeben)).Succeeded;
        WarnTage = await parameter.ZahlAsync(Domain.Gemeinsam.ParameterSchluessel.MassnahmeWarnTage, Heute, 14, ct);
        var tag = Anzeige.LokalesDatum(einsatz.Alarmiert);
        Mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigBis == null || s.GueltigBis >= Heute))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString()))
            .ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => f.AusserDienstAm == null || f.AusserDienstAm >= tag)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString()))
            .ToListAsync(ct);
        return true;
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
