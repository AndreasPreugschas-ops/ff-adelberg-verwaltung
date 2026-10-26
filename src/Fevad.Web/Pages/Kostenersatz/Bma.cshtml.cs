using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Brandmeldeanlagen (V24): Betreiberstamm je Objekt mit Aufschaltnummer und Konzessionär, Zahl der Fehlalarme je Objekt und Jahr und
/// Serienanlage der Kostenersatz-Vorgänge für alle Fehlalarme ohne Vorgang. Empfehlen und entscheiden bleibt beim einzelnen Vorgang.
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzLesen)]
public class BmaModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe eines Objekts.</summary>
    public sealed class ObjektEingabe
    {
        /// <summary>Kennung beim Bearbeiten, sonst null.</summary>
        public Guid? Id { get; set; }
        /// <summary>Bezeichnung des Objekts.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(200), Display(Name = "Objekt")] public string Bezeichnung { get; set; } = "";
        /// <summary>Anschrift des Objekts.</summary>
        [StringLength(300), Display(Name = "Anschrift des Objekts")] public string? Anschrift { get; set; }
        /// <summary>Aufschaltnummer.</summary>
        [StringLength(50), Display(Name = "Aufschaltnummer")] public string? Aufschaltnummer { get; set; }
        /// <summary>Konzessionär.</summary>
        [StringLength(200), Display(Name = "Konzessionär")] public string? Konzessionaer { get; set; }
        /// <summary>Betreiber.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(200), Display(Name = "Betreiber")] public string BetreiberName { get; set; } = "";
        /// <summary>Anschrift des Betreibers.</summary>
        [StringLength(300), Display(Name = "Anschrift des Betreibers")] public string? BetreiberAnschrift { get; set; }
        /// <summary>Ansprechpartner.</summary>
        [StringLength(300), Display(Name = "Ansprechpartner (Name, Telefon)")] public string? Ansprechpartner { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
        /// <summary>Noch aufgeschaltet.</summary>
        [Display(Name = "aufgeschaltet")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Ein Fehlalarm des Jahres.</summary>
    /// <param name="EinsatzId">Kennung des Einsatzes.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    /// <param name="Alarmiert">Alarmzeit (UTC).</param>
    /// <param name="ObjektId">Kennung des Objekts.</param>
    /// <param name="NummerImJahr">Der wievielte Fehlalarm des Objekts im Jahr.</param>
    /// <param name="Vorgang">Stand des Kostenersatz-Vorgangs oder null, wenn keiner angelegt ist.</param>
    public sealed record Fehlalarm(Guid EinsatzId, string Einsatznummer, DateTimeOffset Alarmiert, Guid ObjektId, int NummerImJahr, KostenersatzStand? Vorgang);

    /// <summary>Gewähltes Jahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Alle Objekte, aufgeschaltete zuerst.</summary>
    public IReadOnlyList<BmaObjekt> Objekte { get; private set; } = [];

    /// <summary>Fehlalarme des Jahres.</summary>
    public IReadOnlyList<Fehlalarm> Fehlalarme { get; private set; } = [];

    /// <summary>Der Benutzer darf Objekte pflegen und Vorgänge anlegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Formular für ein Objekt.</summary>
    [BindProperty]
    public ObjektEingabe Objekt { get; set; } = new();

    /// <summary>Lädt Objekte und Fehlalarme.</summary>
    /// <param name="jahr">Jahr; ohne Angabe das laufende.</param>
    /// <param name="bearbeiten">Kennung eines Objekts, das ins Formular geladen wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(int? jahr, Guid? bearbeiten, CancellationToken ct)
    {
        await LadenAsync(jahr, ct);
        if (bearbeiten is { } id && Objekte.FirstOrDefault(o => o.Id == id) is { } o)
        {
            Objekt = new ObjektEingabe
            {
                Id = o.Id, Bezeichnung = o.Bezeichnung, Anschrift = o.Anschrift, Aufschaltnummer = o.Aufschaltnummer, Konzessionaer = o.Konzessionaer,
                BetreiberName = o.BetreiberName, BetreiberAnschrift = o.BetreiberAnschrift, Ansprechpartner = o.Ansprechpartner, Bemerkung = o.Bemerkung, Aktiv = o.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Legt ein Objekt an oder speichert es.</summary>
    /// <param name="jahr">Gewähltes Jahr für die Rückkehr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostSpeichernAsync(int? jahr, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(jahr, ct);
            return Page();
        }

        BmaObjekt objekt;
        if (Objekt.Id is { } id)
        {
            var vorhanden = await db.BmaObjekte.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            objekt = vorhanden;
        }
        else
        {
            objekt = new BmaObjekt { Bezeichnung = "", BetreiberName = "" };
            db.BmaObjekte.Add(objekt);
        }

        objekt.Bezeichnung = Objekt.Bezeichnung.Trim();
        objekt.Anschrift = Leer(Objekt.Anschrift);
        objekt.Aufschaltnummer = Leer(Objekt.Aufschaltnummer);
        objekt.Konzessionaer = Leer(Objekt.Konzessionaer);
        objekt.BetreiberName = Objekt.BetreiberName.Trim();
        objekt.BetreiberAnschrift = Leer(Objekt.BetreiberAnschrift);
        objekt.Ansprechpartner = Leer(Objekt.Ansprechpartner);
        objekt.Bemerkung = Leer(Objekt.Bemerkung);
        objekt.AbgeschaltetAm = Objekt.Aktiv ? null : objekt.AbgeschaltetAm ?? Anzeige.Heute(zeit);
        objekt.Aktiv = Objekt.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Objekt „{objekt.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { jahr });
    }

    /// <summary>
    /// Legt für alle Fehlalarme des Jahres ohne Vorgang die Kostenersatz-Vorgänge an (Serie) und merkt den Kostenersatz am Einsatz als offen vor.
    /// </summary>
    /// <param name="jahr">Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostSerieAsync(int jahr, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await LadenAsync(jahr, ct);
        var objekte = Objekte.ToDictionary(o => o.Id);
        var offen = Fehlalarme.Where(f => f.Vorgang is null).ToList();
        var ids = offen.Select(f => f.EinsatzId).ToList();
        var einsaetze = await db.Einsaetze.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        foreach (var f in offen)
        {
            db.Kostenersatzvorgaenge.Add(objekte[f.ObjektId].SerienVorgang(f.EinsatzId, Anzeige.LokalesDatum(f.Alarmiert), f.NummerImJahr));
            einsaetze[f.EinsatzId].KostenersatzOffen = true;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = offen.Count == 0
            ? "Alle Fehlalarme haben schon einen Vorgang."
            : $"{offen.Count} Vorgänge angelegt. Bitte je Vorgang prüfen und empfehlen.";
        return RedirectToPage(new { jahr });
    }

    private async Task LadenAsync(int? jahr, CancellationToken ct)
    {
        Jahr = jahr ?? Anzeige.Heute(zeit).Year;
        DarfBearbeiten = await DarfAsync();
        Objekte = await db.BmaObjekte.AsNoTracking().OrderByDescending(o => o.Aktiv).ThenBy(o => o.Bezeichnung).ToListAsync(ct);
        var (von, bis) = Anzeige.Jahr(Jahr);
        var liste = await db.Einsaetze.AsNoTracking()
            .Where(e => e.BmaFehlalarm && e.BmaObjektId != null && e.Alarmiert >= von && e.Alarmiert < bis)
            .OrderBy(e => e.Alarmiert)
            .Select(e => new { e.Id, e.Einsatznummer, e.Alarmiert, ObjektId = e.BmaObjektId!.Value })
            .ToListAsync(ct);
        var ids = liste.Select(e => e.Id).ToList();
        var vorgaenge = await db.Kostenersatzvorgaenge.AsNoTracking().Where(v => ids.Contains(v.EinsatzId)).ToListAsync(ct);
        var stand = vorgaenge.ToDictionary(v => v.EinsatzId, v => v.Stand);
        Fehlalarme = liste.GroupBy(e => e.ObjektId)
            .SelectMany(g => g.Select((e, i) => new Fehlalarm(e.Id, e.Einsatznummer, e.Alarmiert, e.ObjektId, i + 1,
                stand.TryGetValue(e.Id, out var s) ? s : null)))
            .OrderBy(f => f.Alarmiert)
            .ToList();
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.KostenersatzEmpfehlen)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
