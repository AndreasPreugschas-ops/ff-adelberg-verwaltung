using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Kostenersatz-Vorgang eines Einsatzes (V21, V15): Tatbestand nach § 34 FwG, Kostenschuldner, Kosten nach tatsächlichem Aufwand und
/// die berechnete Aufstellung. Die Feuerwehr gibt eine Empfehlung ab; entscheiden kann nur die Gemeinde (Hauptamt). Bis zur Entscheidung
/// darf das Hauptamt Schuldner ergänzen, etwa nach der Halterauskunft.
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzLesen)]
public class VorgangModel(AppDbContext db, Kostenersatzrechner rechner, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Der Einsatz.</summary>
    public Einsatz Einsatz { get; private set; } = null!;

    /// <summary>Der Vorgang oder null, solange keiner angelegt ist.</summary>
    public Kostenersatzvorgang? Vorgang { get; private set; }

    /// <summary>Berechnete Aufstellung samt Rundung und Zuschlag.</summary>
    public Kostenersatzrechner.Ergebnis? Berechnung { get; private set; }

    /// <summary>Beteiligte des Einsatzberichts, die sich als Schuldner übernehmen lassen.</summary>
    public IReadOnlyList<EinsatzBeteiligter> Beteiligte { get; private set; } = [];

    /// <summary>Darf anlegen und empfehlen (Feuerwehr).</summary>
    public bool DarfEmpfehlen { get; private set; }

    /// <summary>Darf entscheiden (Gemeinde).</summary>
    public bool DarfEntscheiden { get; private set; }

    /// <summary>Darf Tatbestand, Schuldner und Zusatzkosten jetzt ändern.</summary>
    public bool DarfBearbeiten => Vorgang is { } v
        && ((DarfEmpfehlen && v.FeuerwehrDarfAendern) || (DarfEntscheiden && v.Stand != KostenersatzStand.Entschieden));

    /// <summary>Auswahl der Tatbestände.</summary>
    public static IReadOnlyList<SelectListItem> Tatbestaende { get; } = Auswahl<Kostentatbestand>();
    /// <summary>Auswahl der Schuldnerarten.</summary>
    public static IReadOnlyList<SelectListItem> Schuldnerarten { get; } = Auswahl<SchuldnerArt>();
    /// <summary>Auswahl der Zustellwege.</summary>
    public static IReadOnlyList<SelectListItem> Zustellwege { get; } = Auswahl<Zustellweg>();
    /// <summary>Auswahl der Arten von Zusatzkosten.</summary>
    public static IReadOnlyList<SelectListItem> Zusatzarten { get; } = Auswahl<ZusatzkostenArt>();
    /// <summary>Auswahl der Ergebnisse.</summary>
    public static IReadOnlyList<SelectListItem> Ergebnisse { get; } = Auswahl<KostenersatzErgebnis>();
    /// <summary>Auswahl der Verzichtsgründe.</summary>
    public static IReadOnlyList<SelectListItem> Verzichtsgruende { get; } = Auswahl<Verzichtsgrund>();

    /// <summary>Angaben zum Vorgang.</summary>
    [BindProperty] public VorgangEingabe Angaben { get; set; } = new();
    /// <summary>Neuer Schuldner.</summary>
    [BindProperty] public SchuldnerEingabe NeuerSchuldner { get; set; } = new();
    /// <summary>Neue Zusatzkosten.</summary>
    [BindProperty] public ZusatzEingabe NeueKosten { get; set; } = new();
    /// <summary>Empfehlung oder Entscheidung.</summary>
    [BindProperty] public ErgebnisEingabe Ergebnis { get; set; } = new();

    /// <summary>Eingaben zum Vorgang.</summary>
    public sealed class VorgangEingabe
    {
        /// <summary>Tatbestand.</summary>
        [Display(Name = "Tatbestand nach § 34 FwG")] public Kostentatbestand? Tatbestand { get; set; }
        /// <summary>Sachverhalt.</summary>
        [StringLength(2000), Display(Name = "Sachverhalt (ohne Gesundheitsangaben)")] public string? Sachverhalt { get; set; }
        /// <summary>Gesamtschuld.</summary>
        [Display(Name = "Mehrere Schuldner haften als Gesamtschuldner")] public bool Gesamtschuld { get; set; } = true;
        /// <summary>Verwaltungsgebühr.</summary>
        [Display(Name = "Verwaltungsgebühr erheben")] public bool Verwaltungsgebuehr { get; set; } = true;
    }

    /// <summary>Eingaben eines Schuldners.</summary>
    public sealed class SchuldnerEingabe
    {
        /// <summary>Art.</summary>
        [Display(Name = "Art")] public SchuldnerArt Art { get; set; }
        /// <summary>Name.</summary>
        [Required(ErrorMessage = "Name fehlt."), StringLength(200), Display(Name = "Name")] public string Name { get; set; } = "";
        /// <summary>Zustellanschrift.</summary>
        [StringLength(300), Display(Name = "Zustellanschrift")] public string? Anschrift { get; set; }
        /// <summary>Kennzeichen.</summary>
        [StringLength(20), Display(Name = "Kennzeichen")] public string? Kennzeichen { get; set; }
        /// <summary>Halterauskunft.</summary>
        [StringLength(200), Display(Name = "Halterauskunft")] public string? Halterauskunft { get; set; }
        /// <summary>Versicherung.</summary>
        [StringLength(200), Display(Name = "Versicherung")] public string? Versicherung { get; set; }
        /// <summary>Versicherungsnummer.</summary>
        [StringLength(100), Display(Name = "Versicherungs- / Schadennummer")] public string? Versicherungsnummer { get; set; }
        /// <summary>Zustellweg.</summary>
        [Display(Name = "Zustellweg")] public Zustellweg Zustellweg { get; set; }
        /// <summary>Anteil ohne Gesamtschuld.</summary>
        [Range(0, 100), Display(Name = "Anteil in % (nur ohne Gesamtschuld)")] public decimal? AnteilProzent { get; set; }
    }

    /// <summary>Eingaben von Zusatzkosten.</summary>
    public sealed class ZusatzEingabe
    {
        /// <summary>Art.</summary>
        [Display(Name = "Art")] public ZusatzkostenArt Art { get; set; }
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Betrag als Text.</summary>
        [Required(ErrorMessage = "Betrag fehlt."), Display(Name = "Betrag (€)")] public string Betrag { get; set; } = "";
    }

    /// <summary>Eingaben einer Empfehlung oder Entscheidung.</summary>
    public sealed class ErgebnisEingabe
    {
        /// <summary>Ergebnis.</summary>
        [Display(Name = "Ergebnis")] public KostenersatzErgebnis Ergebnis { get; set; }
        /// <summary>Grund bei Verzicht.</summary>
        [Display(Name = "Grund des Verzichts")] public Verzichtsgrund? Grund { get; set; }
        /// <summary>Begründung.</summary>
        [StringLength(2000), Display(Name = "Begründung (persönliche Verhältnisse nur so weit nötig, keine Gesundheitsangaben)")] public string? Begruendung { get; set; }
    }

    /// <summary>Zeigt den Vorgang.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid einsatzId, CancellationToken ct)
    {
        return await LadenAsync(einsatzId, ct) ? Page() : NotFound();
    }

    /// <summary>Legt den Vorgang an; bei § 2 Abs. 2 FwG mit dem passenden Tatbestand.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAnlegenAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.KostenersatzEmpfehlen))
        {
            return Forbid();
        }

        var einsatz = await db.Einsaetze.FirstOrDefaultAsync(e => e.Id == einsatzId, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (!await db.Kostenersatzvorgaenge.AnyAsync(v => v.EinsatzId == einsatzId, ct))
        {
            db.Kostenersatzvorgaenge.Add(new Kostenersatzvorgang
            {
                EinsatzId = einsatzId,
                Tatbestand = einsatz.Rechtsgrundlage == Rechtsgrundlage.Para2Abs2 ? Kostentatbestand.Abs2Leistung : null,
            });
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Speichert Tatbestand, Sachverhalt, Gesamtschuld und Gebühr.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostSpeichernAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct, angabenAusVorgang: false) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        ModelState.Clear();
        if (!TryValidateModel(Angaben, nameof(Angaben)))
        {
            return Page();
        }

        Vorgang.Tatbestand = Angaben.Tatbestand;
        Vorgang.Sachverhalt = Leer(Angaben.Sachverhalt);
        Vorgang.Gesamtschuld = Angaben.Gesamtschuld;
        Vorgang.Verwaltungsgebuehr = Angaben.Verwaltungsgebuehr;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Vorgang gespeichert.";
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Fügt einen Schuldner hinzu.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostSchuldnerAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        ModelState.Clear();
        if (!TryValidateModel(NeuerSchuldner, nameof(NeuerSchuldner)))
        {
            return Page();
        }

        var s = NeuerSchuldner;
        db.Kostenschuldner.Add(new Kostenschuldner
        {
            KostenersatzvorgangId = Vorgang.Id, Art = s.Art, Name = s.Name.Trim(), Anschrift = Leer(s.Anschrift), Kennzeichen = Leer(s.Kennzeichen)?.ToUpperInvariant(),
            Halterauskunft = Leer(s.Halterauskunft), Versicherung = Leer(s.Versicherung), Versicherungsnummer = Leer(s.Versicherungsnummer),
            Zustellweg = s.Zustellweg, AnteilProzent = Vorgang.Gesamtschuld ? null : s.AnteilProzent,
        });
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Übernimmt einen Beteiligten des Einsatzberichts als Schuldner.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="beteiligterId">Kennung des Beteiligten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostUebernehmenAsync(Guid einsatzId, Guid beteiligterId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        var b = Beteiligte.FirstOrDefault(x => x.Id == beteiligterId);
        if (b is null)
        {
            return NotFound();
        }

        if (Vorgang.Schuldner.All(s => s.EinsatzBeteiligterId != b.Id))
        {
            db.Kostenschuldner.Add(new Kostenschuldner
            {
                KostenersatzvorgangId = Vorgang.Id,
                Art = b.Rolle == BeteiligtenRolle.Verursacher
                    ? (string.IsNullOrWhiteSpace(b.Kennzeichen) ? SchuldnerArt.Verursacher : SchuldnerArt.Halter)
                    : SchuldnerArt.Sonstiger,
                Name = b.Name, Anschrift = b.Anschrift, Kennzeichen = b.Kennzeichen, Versicherung = b.Versicherung, Versicherungsnummer = b.Versicherungsnummer,
                EinsatzBeteiligterId = b.Id,
            });
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Entfernt einen Schuldner.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="id">Kennung des Schuldners.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostSchuldnerEntfernenAsync(Guid einsatzId, Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang?.Schuldner.FirstOrDefault(s => s.Id == id) is not { } schuldner)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        db.Kostenschuldner.Remove(schuldner);
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Fügt Kosten nach tatsächlichem Aufwand hinzu.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZusatzAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        ModelState.Clear();
        TryValidateModel(NeueKosten, nameof(NeueKosten));
        var betrag = Entschaedigungssaetze.Lesen(NeueKosten.Betrag);
        if (!string.IsNullOrWhiteSpace(NeueKosten.Betrag) && (betrag is null || betrag < 0))
        {
            ModelState.AddModelError("NeueKosten.Betrag", "Bitte eine Zahl ab 0 eingeben, z. B. 120 oder 12,50.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.Zusatzkosten.Add(new Zusatzkosten { KostenersatzvorgangId = Vorgang.Id, Art = NeueKosten.Art, Bezeichnung = NeueKosten.Bezeichnung.Trim(), Betrag = betrag!.Value });
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Entfernt Zusatzkosten.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="id">Kennung der Zusatzkosten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZusatzEntfernenAsync(Guid einsatzId, Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang?.Zusatzkosten.FirstOrDefault(z => z.Id == id) is not { } kosten)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        db.Zusatzkosten.Remove(kosten);
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Gibt die Empfehlung der Feuerwehr ab; der Kostenersatz gilt bis zur Entscheidung als offen.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEmpfehlenAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfEmpfehlen || !Vorgang.FeuerwehrDarfAendern)
        {
            return Forbid();
        }

        var fehler = Vorgang.Pruefen(Ergebnis.Ergebnis, Ergebnis.Grund, Ergebnis.Begruendung);
        if (fehler.Count > 0)
        {
            ModelState.Clear();
            foreach (var f in fehler)
            {
                ModelState.AddModelError(string.Empty, f);
            }

            return Page();
        }

        Vorgang.Empfehlung = Ergebnis.Ergebnis;
        Vorgang.EmpfehlungGrund = Ergebnis.Ergebnis == KostenersatzErgebnis.Verzicht ? Ergebnis.Grund : null;
        Vorgang.EmpfehlungBegruendung = Leer(Ergebnis.Begruendung);
        Vorgang.EmpfohlenAm = zeit.GetUtcNow();
        Vorgang.EmpfohlenVon = benutzer.Id;
        Vorgang.EmpfohlenVonName = User.Identity?.Name;
        Einsatz.KostenersatzOffen = true;
        Einsatz.KostenersatzAbgeschlossenAm = null;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Empfehlung abgegeben. Die Gemeinde entscheidet.";
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Nimmt die Empfehlung zurück, solange die Gemeinde nicht entschieden hat.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZuruecknehmenAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfEmpfehlen || Vorgang.Stand != KostenersatzStand.Empfohlen)
        {
            return Forbid();
        }

        Vorgang.Empfehlung = null;
        Vorgang.EmpfehlungGrund = null;
        Vorgang.EmpfehlungBegruendung = null;
        Vorgang.EmpfohlenAm = null;
        Vorgang.EmpfohlenVon = null;
        Vorgang.EmpfohlenVonName = null;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>
    /// Entscheidung der Gemeinde. Weicht sie von der Empfehlung ab, braucht sie eine Begründung. Unentgeltlich und Verzicht schließen den
    /// Kostenersatz ab; kostenpflichtig lässt ihn bis zur Zahlung offen.
    /// </summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEntscheidenAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfEntscheiden || Vorgang.Stand != KostenersatzStand.Empfohlen)
        {
            return Forbid();
        }

        var fehler = Vorgang.Pruefen(Ergebnis.Ergebnis, Ergebnis.Grund, Ergebnis.Begruendung).ToList();
        if (Ergebnis.Ergebnis != Vorgang.Empfehlung && string.IsNullOrWhiteSpace(Ergebnis.Begruendung))
        {
            fehler.Add("Weicht die Entscheidung von der Empfehlung der Feuerwehr ab, ist sie zu begründen.");
        }

        if (fehler.Count > 0)
        {
            ModelState.Clear();
            foreach (var f in fehler)
            {
                ModelState.AddModelError(string.Empty, f);
            }

            return Page();
        }

        var heute = Anzeige.Heute(zeit);
        Vorgang.Entscheidung = Ergebnis.Ergebnis;
        Vorgang.EntscheidungGrund = Ergebnis.Ergebnis == KostenersatzErgebnis.Verzicht ? Ergebnis.Grund : null;
        Vorgang.EntscheidungBegruendung = Leer(Ergebnis.Begruendung);
        Vorgang.EntschiedenAm = heute;
        Vorgang.EntschiedenVon = benutzer.Id;
        Vorgang.EntschiedenVonName = User.Identity?.Name;
        var offen = Ergebnis.Ergebnis == KostenersatzErgebnis.Kostenpflichtig;
        Einsatz.KostenersatzOffen = offen;
        Einsatz.KostenersatzAbgeschlossenAm = offen ? null : heute;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Entschieden: {Anzeige.Text(Ergebnis.Ergebnis)}.";
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Hebt die Entscheidung der Gemeinde auf, z. B. nach einem Fehler; der Vorgang steht wieder auf „empfohlen“.</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAufhebenAsync(Guid einsatzId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang is null)
        {
            return NotFound();
        }

        if (!DarfEntscheiden || Vorgang.Stand != KostenersatzStand.Entschieden)
        {
            return Forbid();
        }

        if (Vorgang.Bescheide.Count > 0)
        {
            TempData["Fehler"] = "Es gibt schon Bescheide. Sie werden einzeln aufgehoben; die Entscheidung bleibt.";
            return RedirectToPage(new { einsatzId });
        }

        Vorgang.Entscheidung = null;
        Vorgang.EntscheidungGrund = null;
        Vorgang.EntscheidungBegruendung = null;
        Vorgang.EntschiedenAm = null;
        Vorgang.EntschiedenVon = null;
        Vorgang.EntschiedenVonName = null;
        Einsatz.KostenersatzOffen = true;
        Einsatz.KostenersatzAbgeschlossenAm = null;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { einsatzId });
    }

    /// <summary>Legt nach einer kostenpflichtigen Entscheidung den Bescheid an einen Schuldner an (V22).</summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="schuldnerId">Kennung des Schuldners.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Bescheid, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBescheidAsync(Guid einsatzId, Guid schuldnerId, CancellationToken ct)
    {
        if (!await LadenAsync(einsatzId, ct) || Vorgang?.Schuldner.FirstOrDefault(s => s.Id == schuldnerId) is not { } schuldner)
        {
            return NotFound();
        }

        if (!DarfEntscheiden || Vorgang.Entscheidung != KostenersatzErgebnis.Kostenpflichtig)
        {
            return Forbid();
        }

        var bescheid = Vorgang.Bescheide.FirstOrDefault(b => b.KostenschuldnerId == schuldner.Id);
        if (bescheid is null)
        {
            bescheid = new Kostenbescheid { KostenersatzvorgangId = Vorgang.Id, KostenschuldnerId = schuldner.Id };
            db.Kostenbescheide.Add(bescheid);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage("Bescheid", new { id = bescheid.Id });
    }

    private async Task<bool> LadenAsync(Guid einsatzId, CancellationToken ct, bool angabenAusVorgang = true)
    {
        var einsatz = await db.Einsaetze
            .Include(e => e.Fahrzeuge).Include(e => e.Teilnahmen).Include(e => e.Ausruestung).ThenInclude(a => a.Kostensatz)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == einsatzId, ct);
        if (einsatz is null)
        {
            return false;
        }

        Einsatz = einsatz;
        Vorgang = await db.Kostenersatzvorgaenge.Include(v => v.Schuldner).Include(v => v.Zusatzkosten).Include(v => v.Bescheide).ThenInclude(b => b.Ereignisse).AsSplitQuery()
            .FirstOrDefaultAsync(v => v.EinsatzId == einsatzId, ct);
        DarfEmpfehlen = await DarfAsync(Berechtigungen.KostenersatzEmpfehlen);
        DarfEntscheiden = await DarfAsync(Berechtigungen.KostenersatzEntscheiden);
        Beteiligte = await db.EinsatzBeteiligte.AsNoTracking().Where(b => b.EinsatzId == einsatzId && b.Rolle != BeteiligtenRolle.Meldender)
            .OrderBy(b => b.Rolle).ThenBy(b => b.Name).ToListAsync(ct);
        Berechnung = await rechner.BerechnenAsync(einsatz, Vorgang, ct);
        if (angabenAusVorgang && Vorgang is { } v)
        {
            Angaben = new VorgangEingabe { Tatbestand = v.Tatbestand, Sachverhalt = v.Sachverhalt, Gesamtschuld = v.Gesamtschuld, Verwaltungsgebuehr = v.Verwaltungsgebuehr };
        }

        return true;
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static IReadOnlyList<SelectListItem> Auswahl<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(w => new SelectListItem(Anzeige.Text(w), w.ToString())).ToList();
}
