using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
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
/// Objektakte (V20): BMA- und Sonderobjekte mit Nutzung, Ansprechpartner, Feuerwehrplan (DIN 14095) als Dokument und Begehungen.
/// Fällig wird eine Begehung nach dem Intervall aus den Parametern. Die Betreiberdaten für den Kostenersatz bei Fehlalarm pflegt
/// weiter der Kostenersatz; Hydranten und Karten bleiben außen vor. Lesen mit <see cref="Berechtigungen.EinsaetzeLesen"/>,
/// pflegen mit <see cref="Berechtigungen.EinsaetzeAao"/>.
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeLesen)]
public class ObjekteModel(AppDbContext db, IAuthorizationService autorisierung, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe der Objektakte.</summary>
    public sealed class Eingabe
    {
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(200), Display(Name = "Objekt")] public string Bezeichnung { get; set; } = "";
        /// <summary>Anschrift.</summary>
        [StringLength(300), Display(Name = "Anschrift")] public string? Anschrift { get; set; }
        /// <summary>Nutzung.</summary>
        [StringLength(200), Display(Name = "Nutzung")] public string? Nutzung { get; set; }
        /// <summary>Hat eine aufgeschaltete BMA.</summary>
        [Display(Name = "Brandmeldeanlage aufgeschaltet")] public bool HatBma { get; set; }
        /// <summary>Betreiber bzw. Eigentümer.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(200), Display(Name = "Betreiber / Eigentümer")] public string BetreiberName { get; set; } = "";
        /// <summary>Ansprechpartner.</summary>
        [StringLength(300), Display(Name = "Ansprechpartner (Name, Telefon)")] public string? Ansprechpartner { get; set; }
        /// <summary>Feuerwehrplan als Dokument.</summary>
        [Display(Name = "Feuerwehrplan (Dokument)")] public Guid? FeuerwehrplanDokumentId { get; set; }
        /// <summary>Stand des Feuerwehrplans.</summary>
        [Display(Name = "Stand des Feuerwehrplans")] public DateOnly? FeuerwehrplanStand { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung (Schlüsseldepot, Laufkarten, Besonderheiten)")] public string? Bemerkung { get; set; }
        /// <summary>Objekt ist noch in Betrieb.</summary>
        [Display(Name = "in Betrieb")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Eingabe einer Begehung.</summary>
    public sealed class BegehungEingabe
    {
        /// <summary>Tag.</summary>
        [Required(ErrorMessage = "Bitte angeben."), Display(Name = "Tag der Begehung")] public DateOnly? Am { get; set; }
        /// <summary>Teilnehmer.</summary>
        [StringLength(300), Display(Name = "Teilnehmer")] public string? Teilnehmer { get; set; }
        /// <summary>Ergebnis.</summary>
        [StringLength(2000), Display(Name = "Ergebnis und Maßnahmen")] public string? Ergebnis { get; set; }
    }

    /// <summary>Zeile der Übersicht.</summary>
    /// <param name="Objekt">Das Objekt.</param>
    /// <param name="LetzteBegehung">Tag der letzten Begehung.</param>
    /// <param name="Faellig">Nächste Begehung ist fällig (oder noch nie begangen).</param>
    public sealed record Zeile(BmaObjekt Objekt, DateOnly? LetzteBegehung, bool Faellig);

    /// <summary>Alle Objekte.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Das gewählte Objekt mit Begehungen.</summary>
    public BmaObjekt? Objekt { get; private set; }

    /// <summary>Titel des verknüpften Feuerwehrplans.</summary>
    public string? FeuerwehrplanTitel { get; private set; }

    /// <summary>Begehungsintervall in Monaten.</summary>
    public int IntervallMonate { get; private set; }

    /// <summary>Auswahl der Dokumente für den Feuerwehrplan.</summary>
    public IReadOnlyList<SelectListItem> Dokumentauswahl { get; private set; } = [];

    /// <summary>Darf pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Formular der Objektakte.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Formular einer Begehung.</summary>
    [BindProperty] public BegehungEingabe Begehung { get; set; } = new();

    /// <summary>Zeigt die Übersicht und auf Wunsch ein Objekt.</summary>
    /// <param name="id">Kennung des Objekts; <see cref="Guid.Empty"/> für ein neues.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        await LadenAsync(id, ct);
        if (id is { } oid && oid != Guid.Empty && Objekt is null)
        {
            return NotFound();
        }

        if (Objekt is { } o)
        {
            Formular = new Eingabe
            {
                Bezeichnung = o.Bezeichnung, Anschrift = o.Anschrift, Nutzung = o.Nutzung, HatBma = o.HatBma, BetreiberName = o.BetreiberName,
                Ansprechpartner = o.Ansprechpartner, FeuerwehrplanDokumentId = o.FeuerwehrplanDokumentId, FeuerwehrplanStand = o.FeuerwehrplanStand,
                Bemerkung = o.Bemerkung, Aktiv = o.Aktiv,
            };
        }

        Begehung.Am = Anzeige.Heute(zeit);
        return Page();
    }

    /// <summary>Speichert die Objektakte; <see cref="Guid.Empty"/> legt ein neues Objekt an.</summary>
    /// <param name="id">Kennung des Objekts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        ModelState.Clear();
        if (!TryValidateModel(Formular, nameof(Formular)))
        {
            await LadenAsync(id, ct);
            return Page();
        }

        BmaObjekt objekt;
        if (id == Guid.Empty)
        {
            objekt = new BmaObjekt { Bezeichnung = "", BetreiberName = "" };
            db.BmaObjekte.Add(objekt);
        }
        else if (await db.BmaObjekte.FirstOrDefaultAsync(o => o.Id == id, ct) is { } vorhanden)
        {
            objekt = vorhanden;
        }
        else
        {
            return NotFound();
        }

        var plan = Formular.FeuerwehrplanDokumentId is { } d && await db.Dokumente.AnyAsync(x => x.Id == d, ct) ? d : (Guid?)null;
        objekt.Bezeichnung = Formular.Bezeichnung.Trim();
        objekt.Anschrift = Leer(Formular.Anschrift);
        objekt.Nutzung = Leer(Formular.Nutzung);
        objekt.HatBma = Formular.HatBma;
        objekt.BetreiberName = Formular.BetreiberName.Trim();
        objekt.Ansprechpartner = Leer(Formular.Ansprechpartner);
        objekt.FeuerwehrplanDokumentId = plan;
        objekt.FeuerwehrplanStand = Formular.FeuerwehrplanStand;
        objekt.Bemerkung = Leer(Formular.Bemerkung);
        objekt.AbgeschaltetAm = Formular.Aktiv ? null : objekt.AbgeschaltetAm ?? Anzeige.Heute(zeit);
        objekt.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Objektakte „{objekt.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { id = objekt.Id });
    }

    /// <summary>Trägt eine Begehung ein.</summary>
    /// <param name="id">Kennung des Objekts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBegehungAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (!await db.BmaObjekte.AnyAsync(o => o.Id == id, ct))
        {
            return NotFound();
        }

        ModelState.Clear();
        if (!TryValidateModel(Begehung, nameof(Begehung)))
        {
            await LadenAsync(id, ct);
            return Page();
        }

        if (Begehung.Am > Anzeige.Heute(zeit))
        {
            ModelState.AddModelError("Begehung.Am", "Eine Begehung in der Zukunft bitte als Termin planen.");
            await LadenAsync(id, ct);
            return Page();
        }

        db.ObjektBegehungen.Add(new ObjektBegehung
        {
            BmaObjektId = id, Am = Begehung.Am!.Value, Teilnehmer = Leer(Begehung.Teilnehmer), Ergebnis = Leer(Begehung.Ergebnis),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Begehung eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht eine falsch eingetragene Begehung.</summary>
    /// <param name="id">Kennung des Objekts.</param>
    /// <param name="begehungId">Kennung der Begehung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBegehungLoeschenAsync(Guid id, Guid begehungId, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (await db.ObjektBegehungen.FirstOrDefaultAsync(b => b.Id == begehungId && b.BmaObjektId == id, ct) is not { } b)
        {
            return NotFound();
        }

        db.ObjektBegehungen.Remove(b);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Begehung gelöscht.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeAao)).Succeeded;

    private async Task LadenAsync(Guid? id, CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        var heute = Anzeige.Heute(zeit);
        IntervallMonate = await parameter.ZahlAsync(ParameterSchluessel.ObjektBegehungIntervallMonate, heute, 24, ct);
        var objekte = await db.BmaObjekte.AsNoTracking().OrderByDescending(o => o.Aktiv).ThenBy(o => o.Bezeichnung).ToListAsync(ct);
        var letzte = await db.ObjektBegehungen.AsNoTracking().GroupBy(b => b.BmaObjektId)
            .Select(g => new { g.Key, Am = g.Max(b => b.Am) }).ToDictionaryAsync(x => x.Key, x => x.Am, ct);
        Zeilen = objekte.Select(o =>
        {
            DateOnly? am = letzte.TryGetValue(o.Id, out var l) ? l : null;
            return new Zeile(o, am, o.Aktiv && (am is null || am.Value.AddMonths(IntervallMonate) <= heute));
        }).ToList();

        if (id is { } oid && oid != Guid.Empty)
        {
            Objekt = await db.BmaObjekte.AsNoTracking().Include(o => o.Begehungen).FirstOrDefaultAsync(o => o.Id == oid, ct);
            if (Objekt is not null)
            {
                Objekt.Begehungen = Objekt.Begehungen.OrderByDescending(b => b.Am).ToList();
                FeuerwehrplanTitel = Objekt.FeuerwehrplanDokumentId is { } d
                    ? await db.Dokumente.Where(x => x.Id == d).Select(x => x.Titel).FirstOrDefaultAsync(ct)
                    : null;
            }
        }

        if (DarfBearbeiten)
        {
            Dokumentauswahl = await db.Dokumente.AsNoTracking().Where(d => d.AufgehobenAm == null).OrderBy(d => d.Titel)
                .Select(d => new SelectListItem(d.Titel, d.Id.ToString())).ToListAsync(ct);
        }
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
