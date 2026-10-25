using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Termine;

/// <summary>
/// Abwesenheit melden (V37): Zeitraum und Art ohne Diagnose, für sich selbst oder ein zugeordnetes Kind. Alle Dienste im
/// Zeitraum gelten als entschuldigt; ab der Mindestdauer sieht der Kommandant die Anzeige (Satzung § 5 Abs. 6).
/// </summary>
public class AbwesenheitModel(AppDbContext db, Dienstplanung planung, Terminlader lader, TimeProvider zeit) : PageModel
{
    /// <summary>Die Personen des Kontos, für die gemeldet werden kann.</summary>
    public IReadOnlyList<Dienstplanung.Person> Personen { get; private set; } = [];
    /// <summary>Laufende und künftige Abwesenheiten dieser Personen.</summary>
    public IReadOnlyList<Abwesenheit> Abwesenheiten { get; private set; } = [];
    /// <summary>Heutiger Tag in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Nachfrist für Entschuldigungen in Tagen.</summary>
    public int NachfristTage { get; private set; }
    /// <summary>Ab so vielen Tagen erfährt der Kommandant von der Abwesenheit; 0 = nie.</summary>
    public int AnzeigeAbTagen { get; private set; }

    /// <summary>Formular einer neuen Abwesenheit.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Eingaben einer Abwesenheit.</summary>
    public sealed class Eingabe
    {
        /// <summary>Für wen gemeldet wird; leer = das eigene Mitglied.</summary>
        public Guid? MitgliedId { get; set; }
        /// <summary>Erster Tag.</summary>
        [Display(Name = "Von"), Required(ErrorMessage = "Beginn fehlt.")]
        public DateOnly? Von { get; set; }
        /// <summary>Letzter Tag.</summary>
        [Display(Name = "Bis"), Required(ErrorMessage = "Ende fehlt.")]
        public DateOnly? Bis { get; set; }
        /// <summary>Grund ohne Diagnose.</summary>
        [Display(Name = "Grund")]
        public AbwesenheitArt Art { get; set; } = AbwesenheitArt.Urlaub;
        /// <summary>Freiwillige Bemerkung.</summary>
        [Display(Name = "Bemerkung (freiwillig)"), StringLength(500)]
        public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt Formular und eigene Abwesenheiten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        Formular.Von = Formular.Bis = Heute;
        return Page();
    }

    /// <summary>Speichert die Abwesenheit und sagt die betroffenen Dienste ab.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        var person = Formular.MitgliedId is { } gewuenscht ? Personen.FirstOrDefault(p => p.MitgliedId == gewuenscht) : Personen.FirstOrDefault(p => p.Selbst);
        if (person is null)
        {
            return Forbid();
        }

        if (ModelState.IsValid && Abwesenheit.Pruefen(Formular.Von!.Value, Formular.Bis!.Value, Heute, NachfristTage) is { } fehler)
        {
            ModelState.AddModelError($"{nameof(Formular)}.{fehler.Feld}", fehler.Meldung);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var abwesenheit = new Abwesenheit
        {
            MitgliedId = person.MitgliedId, Von = Formular.Von!.Value, Bis = Formular.Bis!.Value, Art = Formular.Art, Bemerkung = Formular.Bemerkung,
        };
        var abgesagt = await planung.AbwesenheitMeldenAsync(abwesenheit, ct);
        var fuer = person.Selbst ? "" : $" für {person.Vorname}";
        TempData["Meldung"] = $"Abwesenheit{fuer} vom {Anzeige.Datum(abwesenheit.Von)} bis {Anzeige.Datum(abwesenheit.Bis)} gemeldet"
            + (abgesagt > 0 ? $", {abgesagt} Termin(e) abgesagt." : ".")
            + (abwesenheit.Anzeigepflichtig(AnzeigeAbTagen) ? " Der Kommandant ist informiert." : "");
        return RedirectToPage();
    }

    /// <summary>Nimmt eine Abwesenheit zurück oder beendet sie vorzeitig.</summary>
    /// <param name="abwesenheitId">Kennung der Abwesenheit.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 404.</returns>
    public async Task<IActionResult> OnPostZuruecknehmenAsync(Guid abwesenheitId, CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        var ids = Personen.Select(p => p.MitgliedId).ToList();
        var abwesenheit = await db.Abwesenheiten.FirstOrDefaultAsync(a => a.Id == abwesenheitId && ids.Contains(a.MitgliedId) && a.Bis >= Heute, ct);
        if (abwesenheit is null)
        {
            return NotFound();
        }

        var zurueck = await planung.AbwesenheitZuruecknehmenAsync(abwesenheit, ct);
        TempData["Meldung"] = (abwesenheit.Von >= Heute ? "Abwesenheit zurückgenommen" : "Abwesenheit endet gestern")
            + (zurueck > 0 ? $", {zurueck} Absage(n) zurückgenommen. Bitte sag bei diesen Terminen neu zu oder ab." : ".");
        return RedirectToPage();
    }

    private async Task<bool> LadenAsync(CancellationToken ct)
    {
        Personen = await lader.PersonenAsync(ct);
        if (Personen.Count == 0)
        {
            return false;
        }

        Heute = Anzeige.Heute(zeit);
        NachfristTage = await planung.NachfristTageAsync(ct);
        AnzeigeAbTagen = await planung.AnzeigeAbTagenAsync(ct);
        var ids = Personen.Select(p => p.MitgliedId).ToList();
        Abwesenheiten = await db.Abwesenheiten.AsNoTracking()
            .Where(a => ids.Contains(a.MitgliedId) && a.Bis >= Heute)
            .OrderBy(a => a.Von)
            .ToListAsync(ct);
        return true;
    }
}
