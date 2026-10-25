using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Intervalle und Warnfristen der Personalverwaltung; gespeichert über <see cref="Parametereinstellungen"/>.
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class EinstellungenModel(Parametereinstellungen einstellungen, TimeProvider zeit) : PageModel
{
    /// <summary>Alle Einstellungen der Seite mit Vorgabe und Grenzen.</summary>
    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new("Atemschutz", ParameterSchluessel.BelastungsuebungIntervallMonate, "Belastungsübung (Übungsanlage)", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.EinsatzuebungIntervallMonate, "Einsatzübung", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.AtemschutzUnterweisungIntervallMonate, "Atemschutz-Unterweisung", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.VorsorgeIntervallMonate, "Vorsorge G 26.3, Standardintervall", "Monate", 36, 1, 60, "Vorschlag, wenn bei der Vorsorge kein Datum eingetragen wird"),
        new("Atemschutz", ParameterSchluessel.VorsorgeIntervallAbAlterMonate, "Vorsorge G 26.3, kürzeres Intervall", "Monate", 12, 1, 60),
        new("Atemschutz", ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, "Kürzeres Intervall ab Lebensalter", "Jahre", 50, 18, 70),
        new("Warnfristen", ParameterSchluessel.FristWarnungTage, "Lehrgänge und Qualifikationen", "Tage vorher", 90, 0, 365),
        new("Warnfristen", ParameterSchluessel.NachweisWarnungTage, "Wiederkehrende Nachweise und Untersuchungen", "Tage vorher", 30, 0, 365),
        new("Warnfristen", ParameterSchluessel.AtemschutzWarnungTage, "Atemschutz", "Tage vorher", 60, 0, 365),
        new("Erinnerungsmail", ParameterSchluessel.ErinnerungIntervallTage, "Abstand der Mails", "Tage", 7, 1, 90),
        new("Erinnerungsmail", ParameterSchluessel.ErinnerungAbStunde, "Frühestens ab", "Uhr", 7, 0, 23, "24-Stunden-Format, deutsche Ortszeit"),
    ];

    /// <summary>Eingegebene Werte nach Parameterschlüssel.</summary>
    [BindProperty]
    public Dictionary<string, int> Werte { get; set; } = [];

    /// <summary>Lädt die aktuellen Werte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);

    /// <summary>Speichert die Werte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, bei Fehlern die Seite.</returns>
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
