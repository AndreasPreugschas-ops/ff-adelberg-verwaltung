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

    /// <summary>Die einstellbaren Parameter des Dienstplans mit Vorgabe und Grenzen.</summary>
    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new(Rueckmeldung, ParameterSchluessel.DienstRueckmeldungSchlussStunden, "Selbst zu- oder absagen bis", "Stunden vor Beginn", 0, 0, 336,
            "0 = bis zum Beginn. Danach ändern nur noch Bearbeiter und der Übungsleiter."),
        new(Rueckmeldung, ParameterSchluessel.DienstEntschuldigungNachfristTage, "Nachträglich entschuldigen bis", "Tage nach dem Dienst", 1, 0, 14,
            "Satzung § 5 Abs. 6: bis zum Folgetag. 0 = nur am Diensttag selbst."),
        new(Rueckmeldung, ParameterSchluessel.AbwesenheitAnzeigeAbTagen, "Abwesenheit dem Kommandanten anzeigen ab", "Tagen", 14, 0, 365,
            "Satzung § 5 Abs. 6: ab zwei Wochen. 0 = nie."),
        new(Plan, ParameterSchluessel.DienstSerieMaxMonate, "Serien höchstens", "Monate im Voraus", 12, 1, 24),
        new(Plan, ParameterSchluessel.KalenderVorschauMonate, "Kalender-Abo zeigt voraus", "Monate", 12, 1, 24),
        new(Plan, ParameterSchluessel.KalenderRueckblickMonate, "Kalender-Abo zeigt zurück", "Monate", 3, 0, 24),
    ];

    /// <summary>Eingestellte Werte je Parameterschlüssel.</summary>
    [BindProperty]
    public Dictionary<string, int> Werte { get; set; } = [];

    /// <summary>Lädt die heute gültigen Werte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);

    /// <summary>Speichert die geänderten Werte ab heute.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
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
