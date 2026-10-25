using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
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

/// <summary>
/// Unfall, Kleinverletzung (Verbandbuch) oder Beinaheunfall selbst melden (V40), auch direkt aus Einsatz oder Dienst heraus.
/// Die Meldung ist die Vorstufe der Unfallanzeige; Kommandant und Stellvertreter prüfen und ergänzen sie.
/// </summary>
public class UnfallModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Meldungen, neueste zuerst.</summary>
    public IReadOnlyList<Unfall> Meldungen { get; private set; } = [];
    /// <summary>Eigene Einsätze der letzten Wochen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Einsaetze { get; private set; } = [];
    /// <summary>Dienste der letzten Wochen, die das Mitglied betreffen.</summary>
    public IReadOnlyList<SelectListItem> Dienste { get; private set; } = [];
    /// <summary>Verletzungsarten aus dem Katalog.</summary>
    public IReadOnlyList<SelectListItem> Verletzungsarten { get; private set; } = [];
    /// <summary>Anzeigefrist der Unfallkasse in Tagen.</summary>
    public int FristTage { get; private set; }
    /// <summary>Höchstzahl der Fotos.</summary>
    public int FotosHoechst { get; private set; }

    /// <summary>Formular der Meldung.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Hochgeladene Fotos.</summary>
    [BindProperty]
    public List<IFormFile> Fotos { get; set; } = [];

    /// <summary>Eingaben einer Meldung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Dienstunfall, Kleinverletzung oder Beinaheunfall.</summary>
        [Display(Name = "Was ist passiert?")] public UnfallArt Art { get; set; }
        /// <summary>Tag des Ereignisses.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        /// <summary>Uhrzeit als „HH:MM“.</summary>
        [Required(ErrorMessage = "Uhrzeit fehlt."), Display(Name = "Uhrzeit")] public string Uhrzeit { get; set; } = "";
        /// <summary>Gelegenheit.</summary>
        [Display(Name = "Bei")] public UnfallAnlass Anlass { get; set; } = UnfallAnlass.Dienst;
        /// <summary>Eigener Einsatz.</summary>
        [Display(Name = "Einsatz")] public Guid? EinsatzId { get; set; }
        /// <summary>Dienst.</summary>
        [Display(Name = "Dienst")] public Guid? DienstId { get; set; }
        /// <summary>Ort.</summary>
        [Required(ErrorMessage = "Ort fehlt."), StringLength(200), Display(Name = "Ort")] public string Ort { get; set; } = "";
        /// <summary>Tätigkeit.</summary>
        [StringLength(200), Display(Name = "Was hast du gerade gemacht?")] public string? Taetigkeit { get; set; }
        /// <summary>Hergang.</summary>
        [Required(ErrorMessage = "Bitte kurz beschreiben, was passiert ist."), StringLength(4000), Display(Name = "Hergang")] public string Hergang { get; set; } = "";
        /// <summary>Verletzte Körperteile.</summary>
        [StringLength(200), Display(Name = "Verletzte Körperteile")] public string? VerletzteKoerperteile { get; set; }
        /// <summary>Verletzungsart aus dem Katalog.</summary>
        [Display(Name = "Art der Verletzung")] public Guid? VerletzungsartId { get; set; }
        /// <summary>Durchgangsarzt oder Krankenhaus.</summary>
        [StringLength(200), Display(Name = "Durchgangsarzt oder Krankenhaus")] public string? Erstbehandlung { get; set; }
        /// <summary>Voraussichtlich mehr als drei Tage arbeitsunfähig.</summary>
        [Display(Name = "Ich bin voraussichtlich mehr als drei Tage arbeitsunfähig")] public bool MehrAlsDreiTage { get; set; }
        /// <summary>Name eines Zeugen.</summary>
        [StringLength(200), Display(Name = "Zeuge oder Ersthelfer")] public string? Zeuge { get; set; }
        /// <summary>Weiterer Zeuge.</summary>
        [StringLength(200), Display(Name = "Weiterer Zeuge")] public string? Zeuge2 { get; set; }
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

        if (Formular.VerletzungsartId is { } v && !Verletzungsarten.Any(x => x.Value == v.ToString()))
        {
            ModelState.AddModelError("Formular.VerletzungsartId", "Diese Verletzungsart steht nicht zur Auswahl.");
        }

        var heute = Anzeige.Heute(zeit);
        var fotos = await Meldungsauswahl.FotosLesenAsync(Fotos, parameter, heute, ModelState, nameof(Fotos), ct);
        if (!ModelState.IsValid || zeitpunkt is null)
        {
            return Page();
        }

        var beinahe = Formular.Art == UnfallArt.Beinaheunfall;
        var unfall = new Unfall
        {
            MitgliedId = mitgliedId, Art = Formular.Art, Selbstmeldung = true, Anlass = Formular.Anlass,
            EinsatzId = Formular.Anlass == UnfallAnlass.Einsatz ? Formular.EinsatzId : null,
            DienstId = Formular.Anlass == UnfallAnlass.Dienst ? Formular.DienstId : null,
            Zeitpunkt = zeitpunkt.Value, Ort = Formular.Ort.Trim(), Taetigkeit = Leer(Formular.Taetigkeit), Hergang = Formular.Hergang.Trim(),
            VerletzteKoerperteile = beinahe ? null : Leer(Formular.VerletzteKoerperteile),
            VerletzungsartId = beinahe ? null : Formular.VerletzungsartId,
            Erstbehandlung = beinahe ? null : Leer(Formular.Erstbehandlung),
            MehrAlsDreiTageArbeitsunfaehig = Formular.Art == UnfallArt.Unfall && Formular.MehrAlsDreiTage,
            Status = UnfallStatus.Erfasst, Fotos = fotos,
        };
        foreach (var name in new[] { Formular.Zeuge, Formular.Zeuge2 }.Select(Leer).OfType<string>())
        {
            unfall.Zeugen.Add(new UnfallZeuge { Name = name });
        }

        db.Unfaelle.Add(unfall);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = unfall.AnzeigeBis(FristTage) is { } bis
            ? $"Gemeldet. Der Kommandant kümmert sich um die Unfallanzeige, die bis {Anzeige.Datum(bis)} bei der Unfallkasse sein muss."
            : "Gemeldet. Kommandant und Stellvertreter sehen die Meldung.";
        return RedirectToPage();
    }

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        FristTage = await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, heute, 3, ct);
        FotosHoechst = await parameter.ZahlAsync(ParameterSchluessel.MeldungFotosHoechstAnzahl, heute, 5, ct);
        Meldungen = await db.Unfaelle.AsNoTracking().Where(u => u.MitgliedId == mitgliedId)
            .OrderByDescending(u => u.Zeitpunkt).Take(20).ToListAsync(ct);
        (Einsaetze, Dienste) = await TeilnahmenAsync(db, planung, mitgliedId, zeit, Formular.EinsatzId, Formular.DienstId, ct);
        Verletzungsarten = await Meldungsauswahl.VerletzungsartenAsync(db, null, ct);
    }

    /// <summary>Eigene Einsätze und betroffene Dienste der letzten 30 Tage für die Auswahl „bei welchem Einsatz oder Dienst“.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="planung">Dienstplanung für die Zielgruppe.</param>
    /// <param name="mitgliedId">Das meldende Mitglied.</param>
    /// <param name="zeit">Zeitquelle.</param>
    /// <param name="einsatzId">Schon gewählter Einsatz.</param>
    /// <param name="dienstId">Schon gewählter Dienst.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die beiden Auswahllisten.</returns>
    internal static async Task<(IReadOnlyList<SelectListItem> Einsaetze, IReadOnlyList<SelectListItem> Dienste)> TeilnahmenAsync(
        AppDbContext db, Dienstplanung planung, Guid mitgliedId, TimeProvider zeit, Guid? einsatzId, Guid? dienstId, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var seit = jetzt.AddDays(-30);
        var einsaetze = (await db.EinsatzTeilnahmen.AsNoTracking()
                .Where(t => t.MitgliedId == mitgliedId && (t.Einsatz!.Alarmiert >= seit || t.EinsatzId == einsatzId))
                .Select(t => new { t.EinsatzId, t.Einsatz!.Alarmiert, t.Einsatz.Einsatznummer, t.Einsatz.Stichwort })
                .Distinct().OrderByDescending(t => t.Alarmiert).ToListAsync(ct))
            .Select(t => new SelectListItem($"{Anzeige.DatumZeit(t.Alarmiert)} · {t.Einsatznummer} {t.Stichwort}", t.EinsatzId.ToString())).ToList();
        var profil = await planung.ProfilAsync(mitgliedId, ct);
        var dienste = (await planung.TermineAsync(seit, jetzt.AddHours(12), profil, ct))
            .Where(d => d.Status != Fevad.Domain.Dienstbetrieb.DienstStatus.Abgesagt || d.Id == dienstId)
            .OrderByDescending(d => d.Beginn)
            .Select(d => new SelectListItem($"{Anzeige.DatumZeit(d.Beginn)} · {d.Titel}", d.Id.ToString())).ToList();
        return (einsaetze, dienste);
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
