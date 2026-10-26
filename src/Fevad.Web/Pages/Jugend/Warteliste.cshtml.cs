using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Warteliste der Jugend- und Kinderfeuerwehr (V50): Interessenten mit Wiedervorlage, sobald sie das Mindestalter der Abteilung erreichen,
/// Schnupperdienst nur mit Zustimmung der Eltern, Abschluss mit Ergebnis. Gelöscht wird nach der Frist (L56).
/// </summary>
[Authorize(Policy = Berechtigungen.JugendBearbeiten)]
public class WartelisteModel(AppDbContext db, Datenbereich datenbereich, Laufbahnabfrage laufbahn, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Formular eines Interessenten.</summary>
    public sealed class Eingabe
    {
        /// <summary>Vorname.</summary>
        [Required(ErrorMessage = "Bitte den Vornamen angeben."), StringLength(100), Display(Name = "Vorname des Kindes")] public string? Vorname { get; set; }
        /// <summary>Nachname.</summary>
        [Required(ErrorMessage = "Bitte den Nachnamen angeben."), StringLength(100), Display(Name = "Nachname des Kindes")] public string? Nachname { get; set; }
        /// <summary>Geburtsdatum.</summary>
        [Required(ErrorMessage = "Bitte das Geburtsdatum angeben."), Display(Name = "Geburtsdatum")] public DateOnly? Geburtsdatum { get; set; }
        /// <summary>Abteilung.</summary>
        [Required(ErrorMessage = "Bitte die Abteilung wählen."), Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        /// <summary>Ansprechpartner.</summary>
        [Required(ErrorMessage = "Bitte einen Elternteil als Ansprechpartner angeben."), StringLength(200), Display(Name = "Ansprechpartner (Elternteil)")] public string? Ansprechpartner { get; set; }
        /// <summary>Telefon.</summary>
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>E-Mail.</summary>
        [StringLength(200), EmailAddress(ErrorMessage = "Das ist keine gültige E-Mail-Adresse."), Display(Name = "E-Mail")] public string? Email { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Das Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Kennung beim Bearbeiten.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    /// <summary>Auch erledigte Einträge zeigen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Erledigte { get; set; }

    /// <summary>Die Einträge, offene nach Wiedervorlage.</summary>
    public IReadOnlyList<Interessent> Liste { get; private set; } = [];

    /// <summary>Freigegebene Jugend- und Kinderfeuerwehren.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Kommende Dienste je Abteilung für den Schnupperdienst.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<SelectListItem>> Dienste { get; private set; } = new Dictionary<Guid, IReadOnlyList<SelectListItem>>();

    /// <summary>Stichtag.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Löschfrist der Warteliste in Monaten (L56), für den Datenschutzhinweis.</summary>
    public int LoeschenNachMonate { get; private set; }

    /// <summary>Kontakt des Datenschutzbeauftragten aus den Parametern, für den Datenschutzhinweis.</summary>
    public string Datenschutzbeauftragter { get; private set; } = "";

    /// <summary>Zeigt Liste und Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        if (Id is { } id)
        {
            if (await EintragAsync(id, ct) is not { } i)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Vorname = i.Vorname, Nachname = i.Nachname, Geburtsdatum = i.Geburtsdatum, AbteilungId = i.AbteilungId, Ansprechpartner = i.Ansprechpartner,
                Telefon = i.Telefon, Email = i.Email, Bemerkung = i.Bemerkung,
            };
        }

        return Page();
    }

    /// <summary>Trägt ein Kind ein oder ändert den Eintrag; die Wiedervorlage folgt aus Geburtsdatum und Mindestalter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var f = Formular;
        if (f.AbteilungId is { } abteilung && Abteilungen.All(a => a.Value != abteilung.ToString()))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(f.Telefon) && string.IsNullOrWhiteSpace(f.Email))
        {
            ModelState.AddModelError("Formular.Telefon", "Bitte Telefon oder E-Mail angeben, damit ihr euch melden könnt.");
        }

        if (f.Geburtsdatum > Heute)
        {
            ModelState.AddModelError("Formular.Geburtsdatum", "Das Geburtsdatum liegt in der Zukunft.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Interessent i;
        if (Id is { } id)
        {
            if (await EintragAsync(id, ct) is not { } geladen)
            {
                return NotFound();
            }

            i = geladen;
        }
        else
        {
            i = new Interessent { Vorname = "", Nachname = "", Ansprechpartner = "", EingetragenAm = Heute };
            db.Interessenten.Add(i);
        }

        i.Vorname = f.Vorname!.Trim();
        i.Nachname = f.Nachname!.Trim();
        i.Geburtsdatum = f.Geburtsdatum!.Value;
        i.AbteilungId = f.AbteilungId!.Value;
        i.Ansprechpartner = f.Ansprechpartner!.Trim();
        i.Telefon = Leer(f.Telefon);
        i.Email = Leer(f.Email);
        i.Bemerkung = Leer(f.Bemerkung);
        var art = await db.Abteilungen.Where(a => a.Id == i.AbteilungId).Select(a => a.Art).FirstAsync(ct);
        var regeln = await laufbahn.RegelnAsync(Heute, ct);
        i.Wiedervorlage = Interessent.WiedervorlageAm(i.Geburtsdatum, art == AbteilungArt.Kinder ? regeln.KinderAufnahmeAbAlter : regeln.JugendAufnahmeAbAlter, i.EingetragenAm);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{i.Vorname} {i.Nachname} steht auf der Warteliste; Wiedervorlage am {Anzeige.Datum(i.Wiedervorlage)}.";
        return RedirectToPage(new { id = (Guid?)null });
    }

    /// <summary>Lädt das Kind zu einem Schnupperdienst ein; nur mit Zustimmung der Eltern.</summary>
    /// <param name="eintrag">Der Eintrag.</param>
    /// <param name="dienstId">Der Dienst; leer nimmt die Einladung zurück.</param>
    /// <param name="zustimmung">Die Eltern haben zugestimmt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostSchnupperAsync(Guid eintrag, Guid? dienstId, bool zustimmung, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (await EintragAsync(eintrag, ct) is not { } i)
        {
            return NotFound();
        }

        if (dienstId is null)
        {
            i.SchnupperdienstId = null;
            i.SchnupperZustimmungAm = null;
        }
        else if (!zustimmung)
        {
            TempData["Fehler"] = "Schnuppern geht nur mit Zustimmung der Eltern.";
            return RedirectToPage();
        }
        else if (!Dienste.TryGetValue(i.AbteilungId, out var auswahl) || auswahl.All(d => d.Value != dienstId.ToString()))
        {
            return NotFound();
        }
        else
        {
            i.SchnupperdienstId = dienstId;
            i.SchnupperZustimmungAm = Heute;
        }

        await db.SaveChangesAsync(ct);
        return RedirectToPage();
    }

    /// <summary>Schließt einen Eintrag ab; gelöscht wird er nach der Frist (L56).</summary>
    /// <param name="eintrag">Der Eintrag.</param>
    /// <param name="ergebnis">Wie es ausgegangen ist.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid eintrag, Wartelistenergebnis ergebnis, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (await EintragAsync(eintrag, ct) is not { } i)
        {
            return NotFound();
        }

        i.ErledigtAm = Heute;
        i.Ergebnis = ergebnis;
        await db.SaveChangesAsync(ct);
        return RedirectToPage();
    }

    /// <summary>Löscht einen Eintrag sofort, z. B. auf Wunsch der Eltern.</summary>
    /// <param name="eintrag">Der Eintrag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid eintrag, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (await EintragAsync(eintrag, ct) is not { } i)
        {
            return NotFound();
        }

        db.Interessenten.Remove(i);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eintrag gelöscht.";
        return RedirectToPage();
    }

    private async Task<Interessent?> EintragAsync(Guid id, CancellationToken ct)
    {
        var erlaubt = Abteilungen.Select(a => Guid.Parse(a.Value)).ToList();
        return await db.Interessenten.Include(i => i.Schnupperdienst).FirstOrDefaultAsync(i => i.Id == id && erlaubt.Contains(i.AbteilungId), ct);
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        LoeschenNachMonate = await parameter.ZahlAsync(ParameterSchluessel.WartelisteLoeschenNachMonate, Heute, 12, ct);
        Datenschutzbeauftragter = await parameter.WertAsync(ParameterSchluessel.DatenschutzBeauftragter, Heute, ct) ?? "[fehlt]";
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.MitgliederBearbeiten);
        var freigegeben = freigabe.Abteilungen.ToList();
        var abteilungen = await db.Abteilungen.AsNoTracking()
            .Where(a => (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder) && (freigabe.Alle || freigegeben.Contains(a.Id)))
            .OrderBy(a => a.Sortierung)
            .ToListAsync(ct);
        Abteilungen = abteilungen.Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToList();
        var ids = abteilungen.Select(a => a.Id).ToList();
        var abfrage = db.Interessenten.AsNoTracking().Include(i => i.Abteilung).Include(i => i.Schnupperdienst).Where(i => ids.Contains(i.AbteilungId));
        if (!Erledigte)
        {
            abfrage = abfrage.Where(i => i.ErledigtAm == null);
        }

        Liste = await abfrage.OrderBy(i => i.ErledigtAm != null).ThenBy(i => i.Wiedervorlage).ThenBy(i => i.Nachname).ToListAsync(ct);
        var ab = Ortszeit.TagesbeginnUtc(Heute);
        var kommende = await db.Dienste.AsNoTracking().Include(d => d.Abteilungen)
            .Where(d => d.Beginn >= ab && d.Status == DienstStatus.Geplant && d.Abteilungen.Any(a => ids.Contains(a.Id)))
            .OrderBy(d => d.Beginn).Take(100)
            .ToListAsync(ct);
        Dienste = ids.ToDictionary(id => id, id => (IReadOnlyList<SelectListItem>)kommende.Where(d => d.Abteilungen.Any(a => a.Id == id)).Take(12)
            .Select(d => new SelectListItem($"{Anzeige.DatumZeit(d.Beginn)} {d.Titel}", d.Id.ToString())).ToList());
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
