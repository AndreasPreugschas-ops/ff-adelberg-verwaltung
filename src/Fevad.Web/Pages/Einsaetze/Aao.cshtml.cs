using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Alarm- und Ausrückeordnung als versionierte Stammdaten (V20): Stände mit Gültigkeitsbeginn, Beschlussdatum und Mitteilung an den
/// Bürgermeister (§ 9 FwG), je Stand Regeln Stichwort → Alarmstufe → Fahrzeuge → Mindeststärke. Ein beschlossener Stand ist gesperrt;
/// Änderungen kommen als neuer Stand, der die Regeln des letzten übernimmt. FEVAD alarmiert nicht.
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeLesen)]
public class AaoModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe der Kopfdaten eines Stands.</summary>
    public sealed class StandEingabe
    {
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Gültig ab.</summary>
        [Required(ErrorMessage = "Bitte angeben."), Display(Name = "Gültig ab")] public DateOnly? GueltigAb { get; set; }
        /// <summary>Beschlossen am.</summary>
        [Display(Name = "Beschlossen am")] public DateOnly? BeschlossenAm { get; set; }
        /// <summary>Dem Bürgermeister mitgeteilt am.</summary>
        [Display(Name = "Bürgermeister mitgeteilt am (§ 9 FwG)")] public DateOnly? BuergermeisterMitgeteiltAm { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Eingabe einer Regel.</summary>
    public sealed class RegelEingabe
    {
        /// <summary>Kennung beim Bearbeiten, sonst null.</summary>
        public Guid? Id { get; set; }
        /// <summary>Stichwort.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(50), Display(Name = "Stichwort")] public string Stichwort { get; set; } = "";
        /// <summary>Alarmstufe.</summary>
        [StringLength(200), Display(Name = "Alarmstufe / Umfang")] public string? Alarmstufe { get; set; }
        /// <summary>Fahrzeuge.</summary>
        [Display(Name = "Fahrzeuge")] public List<Guid> Fahrzeuge { get; set; } = [];
        /// <summary>Mindeststärke.</summary>
        [Range(0, 200), Display(Name = "Mindeststärke")] public int? Mindeststaerke { get; set; }
        /// <summary>Mindestzahl Atemschutzgeräteträger.</summary>
        [Range(0, 100), Display(Name = "davon Atemschutz")] public int? MindestAtemschutz { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Alle Stände, neueste zuerst.</summary>
    public IReadOnlyList<AaoStand> Staende { get; private set; } = [];

    /// <summary>Der angezeigte Stand mit Regeln.</summary>
    public AaoStand? Stand { get; private set; }

    /// <summary>Kennung des heute gültigen Stands.</summary>
    public Guid? HeuteGueltig { get; private set; }

    /// <summary>Anzeigenamen der Fahrzeuge.</summary>
    public IReadOnlyDictionary<Guid, string> Fahrzeugnamen { get; private set; } = new Dictionary<Guid, string>();

    /// <summary>Fahrzeuge, die heute im Dienst sind, für die Auswahl.</summary>
    public IReadOnlyList<(Guid Id, string Name)> Fahrzeugauswahl { get; private set; } = [];

    /// <summary>Darf pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Kopfdaten des Stands.</summary>
    [BindProperty] public StandEingabe Kopf { get; set; } = new();

    /// <summary>Formular einer Regel.</summary>
    [BindProperty] public RegelEingabe Regel { get; set; } = new();

    /// <summary>Zeigt einen Stand, ohne Angabe den heute gültigen.</summary>
    /// <param name="stand">Kennung des Stands.</param>
    /// <param name="regel">Kennung einer Regel, die ins Formular soll.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? stand, Guid? regel, CancellationToken ct)
    {
        await LadenAsync(stand, ct);
        if (Stand is not null)
        {
            Kopf = new StandEingabe
            {
                Bezeichnung = Stand.Bezeichnung, GueltigAb = Stand.GueltigAb, BeschlossenAm = Stand.BeschlossenAm,
                BuergermeisterMitgeteiltAm = Stand.BuergermeisterMitgeteiltAm, Bemerkung = Stand.Bemerkung,
            };
            if (regel is { } rid && Stand.Regeln.FirstOrDefault(r => r.Id == rid) is { } r)
            {
                Regel = new RegelEingabe
                {
                    Id = r.Id, Stichwort = r.Stichwort, Alarmstufe = r.Alarmstufe, Fahrzeuge = r.Fahrzeuge.ToList(), Mindeststaerke = r.Mindeststaerke,
                    MindestAtemschutz = r.MindestAtemschutz, Bemerkung = r.Bemerkung,
                };
            }
        }

        return Page();
    }

    /// <summary>Legt einen neuen Stand an und übernimmt die Regeln des jüngsten Stands.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum neuen Stand oder 403.</returns>
    public async Task<IActionResult> OnPostNeuAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        var vorlage = await db.AaoStaende.AsNoTracking().Include(s => s.Regeln).OrderByDescending(s => s.GueltigAb).FirstOrDefaultAsync(ct);
        var neu = new AaoStand
        {
            Bezeichnung = $"AAO {heute.Year}", GueltigAb = vorlage is not null && vorlage.GueltigAb >= heute ? vorlage.GueltigAb.AddDays(1) : heute,
            Regeln = vorlage?.Regeln.Select(r => new AaoRegel
            {
                Stichwort = r.Stichwort, Alarmstufe = r.Alarmstufe, Fahrzeuge = r.Fahrzeuge.ToList(), Mindeststaerke = r.Mindeststaerke,
                MindestAtemschutz = r.MindestAtemschutz, Bemerkung = r.Bemerkung,
            }).ToList() ?? [],
        };
        db.AaoStaende.Add(neu);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = vorlage is null ? "Neuer Stand angelegt." : $"Neuer Stand angelegt, {neu.Regeln.Count} Regeln aus „{vorlage.Bezeichnung}“ übernommen.";
        return RedirectToPage(new { stand = neu.Id });
    }

    /// <summary>Speichert die Kopfdaten. Bei einem beschlossenen Stand nur noch Mitteilung an den Bürgermeister und Bemerkung.</summary>
    /// <param name="stand">Kennung des Stands.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostKopfAsync(Guid stand, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (await db.AaoStaende.FirstOrDefaultAsync(s => s.Id == stand, ct) is not { } s)
        {
            return NotFound();
        }

        ModelState.Clear();
        if (s.Gesperrt)
        {
            Kopf.Bezeichnung = s.Bezeichnung;
            Kopf.GueltigAb = s.GueltigAb;
        }

        if (!TryValidateModel(Kopf, nameof(Kopf)))
        {
            await LadenAsync(stand, ct);
            return Page();
        }

        if (!s.Gesperrt)
        {
            s.Bezeichnung = Kopf.Bezeichnung.Trim();
            s.GueltigAb = Kopf.GueltigAb!.Value;
            s.BeschlossenAm = Kopf.BeschlossenAm;
        }

        s.BuergermeisterMitgeteiltAm = Kopf.BuergermeisterMitgeteiltAm;
        s.Bemerkung = string.IsNullOrWhiteSpace(Kopf.Bemerkung) ? null : Kopf.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = s.Gesperrt ? $"„{s.Bezeichnung}“ gespeichert; der Stand ist beschlossen und damit gesperrt." : $"„{s.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { stand });
    }

    /// <summary>Legt eine Regel an oder ändert sie; nur in einem nicht beschlossenen Stand.</summary>
    /// <param name="stand">Kennung des Stands.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostRegelAsync(Guid stand, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (await db.AaoStaende.Include(s => s.Regeln).FirstOrDefaultAsync(s => s.Id == stand, ct) is not { } s)
        {
            return NotFound();
        }

        if (s.Gesperrt)
        {
            TempData["Fehler"] = "Der Stand ist beschlossen. Für Änderungen bitte einen neuen Stand anlegen.";
            return RedirectToPage(new { stand });
        }

        ModelState.Clear();
        var schluessel = AaoRegel.Schluessel(Regel.Stichwort);
        if (TryValidateModel(Regel, nameof(Regel)) && s.Regeln.Any(r => r.Id != Regel.Id && AaoRegel.Schluessel(r.Stichwort) == schluessel))
        {
            ModelState.AddModelError("Regel.Stichwort", "Für dieses Stichwort gibt es im Stand schon eine Regel.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(stand, ct);
            return Page();
        }

        AaoRegel regel;
        if (Regel.Id is { } id)
        {
            if (s.Regeln.FirstOrDefault(r => r.Id == id) is not { } vorhanden)
            {
                return NotFound();
            }

            regel = vorhanden;
        }
        else
        {
            regel = new AaoRegel { AaoStandId = s.Id, Stichwort = "" };
            db.AaoRegeln.Add(regel);
        }

        var fahrzeuge = await db.Fahrzeuge.Where(f => Regel.Fahrzeuge.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        regel.Stichwort = Regel.Stichwort.Trim();
        regel.Alarmstufe = string.IsNullOrWhiteSpace(Regel.Alarmstufe) ? null : Regel.Alarmstufe.Trim();
        regel.Fahrzeuge = Regel.Fahrzeuge.Where(fahrzeuge.Contains).Distinct().ToList();
        regel.Mindeststaerke = Regel.Mindeststaerke;
        regel.MindestAtemschutz = Regel.MindestAtemschutz;
        regel.Bemerkung = string.IsNullOrWhiteSpace(Regel.Bemerkung) ? null : Regel.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Regel „{regel.Stichwort}“ gespeichert.";
        return RedirectToPage(new { stand });
    }

    /// <summary>Löscht eine Regel aus einem nicht beschlossenen Stand.</summary>
    /// <param name="stand">Kennung des Stands.</param>
    /// <param name="regelId">Kennung der Regel.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostRegelLoeschenAsync(Guid stand, Guid regelId, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var regel = await db.AaoRegeln.Include(r => r.AaoStand).FirstOrDefaultAsync(r => r.Id == regelId && r.AaoStandId == stand, ct);
        if (regel is null)
        {
            return NotFound();
        }

        if (regel.AaoStand!.Gesperrt)
        {
            TempData["Fehler"] = "Der Stand ist beschlossen und bleibt unverändert.";
            return RedirectToPage(new { stand });
        }

        db.AaoRegeln.Remove(regel);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Regel „{regel.Stichwort}“ gelöscht.";
        return RedirectToPage(new { stand });
    }

    /// <summary>Löscht einen nicht beschlossenen Stand, z. B. einen verworfenen Entwurf.</summary>
    /// <param name="stand">Kennung des Stands.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid stand, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (await db.AaoStaende.FirstOrDefaultAsync(s => s.Id == stand, ct) is not { } s)
        {
            return NotFound();
        }

        if (s.Gesperrt)
        {
            TempData["Fehler"] = "Ein beschlossener Stand bleibt als Nachweis erhalten.";
            return RedirectToPage(new { stand });
        }

        db.AaoStaende.Remove(s);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Entwurf „{s.Bezeichnung}“ gelöscht.";
        return RedirectToPage();
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeAao)).Succeeded;

    private async Task LadenAsync(Guid? stand, CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        var heute = Anzeige.Heute(zeit);
        Staende = await db.AaoStaende.AsNoTracking().OrderByDescending(s => s.GueltigAb).ThenByDescending(s => s.ErstelltAm).ToListAsync(ct);
        HeuteGueltig = Staende.Where(s => s.GueltigAb <= heute && s.BeschlossenAm != null).Select(s => (Guid?)s.Id).FirstOrDefault();
        var id = stand ?? HeuteGueltig ?? Staende.Select(s => (Guid?)s.Id).FirstOrDefault();
        Stand = id is null ? null : await db.AaoStaende.AsNoTracking().Include(s => s.Regeln).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (Stand is not null)
        {
            Stand.Regeln = Stand.Regeln.OrderBy(r => r.Stichwort, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().OrderBy(f => f.Funkrufname)
            .Select(f => new { f.Id, f.Funkrufname, f.Typ, f.AusserDienstAm }).ToListAsync(ct);
        Fahrzeugnamen = fahrzeuge.ToDictionary(f => f.Id, f => $"{f.Funkrufname} ({f.Typ})");
        Fahrzeugauswahl = fahrzeuge.Where(f => f.AusserDienstAm is null || f.AusserDienstAm > heute).Select(f => (f.Id, $"{f.Funkrufname} ({f.Typ})")).ToList();
    }
}
