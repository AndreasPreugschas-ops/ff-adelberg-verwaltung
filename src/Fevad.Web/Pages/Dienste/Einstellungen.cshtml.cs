using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Einstellungen des Dienstplans: bis wann Mitglieder selbst zu- oder absagen können, wie weit Serien reichen und was das
/// Kalender-Abo zeigt. Bearbeiter und der Übungsleiter können An- und Abmeldungen auch nach dem Schluss noch ändern.
/// </summary>
[Authorize(Policy = Berechtigungen.DiensteEinstellungen)]
public class EinstellungenModel(Parametereinstellungen einstellungen, TimeProvider zeit) : PageModel
{
    private const string Rueckmeldung = "An- und Abmeldung";
    private const string Plan = "Serien und Kalender";

    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new(Rueckmeldung, ParameterSchluessel.DienstRueckmeldungSchlussStunden, "Selbst zu- oder absagen bis", "Stunden vor Beginn", 0, 0, 336,
            "0 = bis zum Beginn. Danach ändern nur noch Bearbeiter und der Übungsleiter."),
        new(Plan, ParameterSchluessel.DienstSerieMaxMonate, "Serien höchstens", "Monate im Voraus", 12, 1, 24),
        new(Plan, ParameterSchluessel.KalenderVorschauMonate, "Kalender-Abo zeigt voraus", "Monate", 12, 1, 24),
        new(Plan, ParameterSchluessel.KalenderRueckblickMonate, "Kalender-Abo zeigt zurück", "Monate", 3, 0, 24),
    ];

    [BindProperty]
    public Dictionary<string, int> Werte { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await einstellungen.SpeichernAsync(Alle, Werte, ModelState, Anzeige.Heute(zeit), ct) is not { } geaendert)
        {
            return Page();
        }

        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : $"{geaendert} Einstellungen gespeichert, gültig ab heute.";
        return RedirectToPage();
    }
}
