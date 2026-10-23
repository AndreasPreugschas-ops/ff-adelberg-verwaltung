using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>
/// Einstellungen der Einsatzabteilung und der Nachwuchsabteilungen: Probezeit, Aufnahme- und Altersgrenzen, Vorlauf der Hinweise
/// und der Gesamtlauf der Personalprüfung. Grundlage ist die Feuerwehrsatzung; eine Satzungsänderung gilt ab dem Tag der Änderung.
/// </summary>
[Authorize(Policy = Berechtigungen.LaufbahnRegeln)]
public class AbteilungsregelnModel(Parametereinstellungen einstellungen, TimeProvider zeit) : PageModel
{
    private const string Einsatz = "Einsatzabteilung";
    private const string Nachwuchs = "Jugend- und Kinderfeuerwehr";
    private const string Pruefung = "Hinweise und Prüfung";

    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new(Einsatz, ParameterSchluessel.ProbezeitMonate, "Probezeit", "Monate", 12, 0, 36, "Satzung: Verkürzen oder Verzicht im Einzelfall beim Übertritt"),
        new(Einsatz, LaufbahnParameter.AufnahmeEinsatzAbAlter, "Aufnahme ab", "Jahre", 17, 16, 18, "Bis zum 18. Geburtstag mit Zustimmung der Erziehungsberechtigten"),
        new(Einsatz, LaufbahnParameter.EinsatzdienstAbAlter, "Einsatzdienst ab", "Jahre", 18, 16, 18),
        new(Einsatz, ParameterSchluessel.AltersgrenzeEinsatzdienst, "Ende des Einsatzdienstes", "Jahre", 65, 50, 70, "FwG BW und Satzung"),
        new(Einsatz, ParameterSchluessel.AltersabteilungAbAlter, "Übertritt in die Altersabteilung ab", "Jahre", 55, 40, 70, "auf Antrag"),
        new(Nachwuchs, LaufbahnParameter.JugendAufnahmeAbAlter, "Jugendfeuerwehr: Aufnahme ab", "Jahre", 10, 6, 17),
        new(Nachwuchs, LaufbahnParameter.JugendAufnahmeBisAlter, "Jugendfeuerwehr: Aufnahme bis", "Jahre", 17, 10, 18),
        new(Nachwuchs, LaufbahnParameter.JugendEndeAlter, "Jugendfeuerwehr endet mit", "Jahren", 18, 16, 21),
        new(Nachwuchs, LaufbahnParameter.KinderAufnahmeAbAlter, "Kinderfeuerwehr: Aufnahme ab", "Jahre", 6, 3, 10),
        new(Nachwuchs, LaufbahnParameter.KinderEndeAlter, "Kinderfeuerwehr endet mit", "Jahren", 10, 0, 14, "0 = keine Grenze"),
        new(Pruefung, LaufbahnParameter.VorlaufMonate, "Hinweise so lange vorher", "Monate", 3, 0, 24, "Übertritte, Probezeit, Altersgrenzen, Amtszeiten"),
        new(Pruefung, ParameterSchluessel.PruefungGesamtlaufBeimStart, "Beim Start alle Mitglieder prüfen", "", 1, 0, 1,
            "Sonst nur nach Änderungen und einmal täglich", JaNein: true),
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

        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : $"{geaendert} Einstellungen gespeichert, gültig ab heute. Die Prüfung läuft gleich für alle neu.";
        return RedirectToPage();
    }
}
