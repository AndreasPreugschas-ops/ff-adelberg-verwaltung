using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Unfaelle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>Schaden an privatem Eigentum im Dienst melden (V40, § 17 FwG): Gegenstand, Hergang, Betrag und Fotos.</summary>
public class SachschadenModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Meldungen, neueste zuerst.</summary>
    public IReadOnlyList<Sachschaden> Meldungen { get; private set; } = [];
    /// <summary>Eigene Einsätze der letzten Wochen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Einsaetze { get; private set; } = [];
    /// <summary>Dienste der letzten Wochen, die das Mitglied betreffen.</summary>
    public IReadOnlyList<SelectListItem> Dienste { get; private set; } = [];

    /// <summary>Formular der Meldung.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Hochgeladene Fotos.</summary>
    [BindProperty]
    public List<IFormFile> Fotos { get; set; } = [];

    /// <summary>Eingaben einer Schadensmeldung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag des Schadens.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        /// <summary>Uhrzeit als „HH:MM“.</summary>
        [Required(ErrorMessage = "Uhrzeit fehlt."), Display(Name = "Uhrzeit")] public string Uhrzeit { get; set; } = "";
        /// <summary>Gelegenheit.</summary>
        [Display(Name = "Bei")] public UnfallAnlass Anlass { get; set; } = UnfallAnlass.Einsatz;
        /// <summary>Eigener Einsatz.</summary>
        [Display(Name = "Einsatz")] public Guid? EinsatzId { get; set; }
        /// <summary>Dienst.</summary>
        [Display(Name = "Dienst")] public Guid? DienstId { get; set; }
        /// <summary>Ort.</summary>
        [Required(ErrorMessage = "Ort fehlt."), StringLength(200), Display(Name = "Ort")] public string Ort { get; set; } = "";
        /// <summary>Beschädigter Gegenstand.</summary>
        [Required(ErrorMessage = "Was ist beschädigt?"), StringLength(200), Display(Name = "Beschädigter Gegenstand")] public string Gegenstand { get; set; } = "";
        /// <summary>Hergang.</summary>
        [Required(ErrorMessage = "Bitte kurz beschreiben, wie es passiert ist."), StringLength(4000), Display(Name = "Wie ist es passiert?")] public string Hergang { get; set; } = "";
        /// <summary>Schaden in Euro.</summary>
        [Required(ErrorMessage = "Betrag fehlt."), Range(0.01, 100000, ErrorMessage = "Betrag zwischen 0,01 und 100.000 €."), Display(Name = "Schaden in Euro (Reparatur oder Zeitwert)")]
        public decimal? Betrag { get; set; }
    }

    /// <summary>Zeigt das Formular, auf Wunsch mit vorgewähltem Einsatz oder Dienst.</summary>
    /// <param name="einsatzId">Vorgewählter Einsatz.</param>
    /// <param name="dienstId">Vorgewählter Dienst.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? einsatzId, Guid? dienstId, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var jetzt = Anzeige.Lokal(zeit.GetUtcNow());
        Formular = new Eingabe { Datum = DateOnly.FromDateTime(jetzt.DateTime), Uhrzeit = jetzt.ToString("HH:mm") };
        await LadenAsync(mitgliedId, ct);
        if (einsatzId is { } e && Einsaetze.Any(x => x.Value == e.ToString()))
        {
            (Formular.Anlass, Formular.EinsatzId) = (UnfallAnlass.Einsatz, e);
        }
        else if (dienstId is { } d && Dienste.Any(x => x.Value == d.ToString()))
        {
            (Formular.Anlass, Formular.DienstId) = (UnfallAnlass.Dienst, d);
        }

        return Page();
    }

    /// <summary>Speichert die Meldung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        DateTimeOffset? zeitpunkt = null;
        if (Formular.Datum is { } datum && ZeitEingabe.TryUhrzeit(Formular.Uhrzeit, out var uhrzeit))
        {
            zeitpunkt = Anzeige.AlsUtc(datum, uhrzeit);
        }
        else if (!string.IsNullOrWhiteSpace(Formular.Uhrzeit))
        {
            ModelState.AddModelError("Formular.Uhrzeit", "Uhrzeit bitte als HH:MM (24 Stunden).");
        }

        if (zeitpunkt > zeit.GetUtcNow().AddMinutes(5))
        {
            ModelState.AddModelError("Formular.Datum", "Das liegt in der Zukunft.");
        }

        if (Formular.EinsatzId is { } e && !Einsaetze.Any(x => x.Value == e.ToString()))
        {
            ModelState.AddModelError("Formular.EinsatzId", "Dieser Einsatz steht nicht zur Auswahl.");
        }

        if (Formular.DienstId is { } d && !Dienste.Any(x => x.Value == d.ToString()))
        {
            ModelState.AddModelError("Formular.DienstId", "Dieser Dienst steht nicht zur Auswahl.");
        }

        var fotos = await Meldungsauswahl.FotosLesenAsync(Fotos, parameter, Anzeige.Heute(zeit), ModelState, nameof(Fotos), ct);
        if (!ModelState.IsValid || zeitpunkt is null)
        {
            return Page();
        }

        db.Sachschaeden.Add(new Sachschaden
        {
            MitgliedId = mitgliedId, Anlass = Formular.Anlass,
            EinsatzId = Formular.Anlass == UnfallAnlass.Einsatz ? Formular.EinsatzId : null,
            DienstId = Formular.Anlass == UnfallAnlass.Dienst ? Formular.DienstId : null,
            Zeitpunkt = zeitpunkt.Value, Ort = Formular.Ort.Trim(), Gegenstand = Formular.Gegenstand.Trim(), Hergang = Formular.Hergang.Trim(),
            Betrag = Formular.Betrag!.Value, Status = SachschadenStatus.Gemeldet, Fotos = fotos,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Schaden gemeldet. Der Kommandant leitet ihn an die Gemeinde weiter; hier siehst du, wie es steht.";
        return RedirectToPage();
    }

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        Meldungen = await db.Sachschaeden.AsNoTracking().Where(s => s.MitgliedId == mitgliedId)
            .OrderByDescending(s => s.Zeitpunkt).Take(20).ToListAsync(ct);
        (Einsaetze, Dienste) = await UnfallModel.TeilnahmenAsync(db, planung, mitgliedId, zeit, Formular.EinsatzId, Formular.DienstId, ct);
    }
}
