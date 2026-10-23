using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>Alle Kataloge und Regeln an einer Stelle; zeigt nur, was die Rolle pflegen darf. Die Seiten selbst bleiben in ihren Bereichen.</summary>
public class IndexModel(IAuthorizationService autorisierung) : PageModel
{
    public sealed record Eintrag(string Titel, string Seite, string Policy, string Beschreibung);

    public static readonly IReadOnlyList<Eintrag> Eintraege =
    [
        new("Abteilungen und Funktionen", "/Stammdaten/Organisation", Berechtigungen.StammdatenOrganisation,
            "Abteilungen anlegen, umbenennen und abschalten (fünf Einsatzabteilungen, Jugend-, Kinder- und Altersabteilung); Funktionen mit Rechtsgrundlage, Voraussetzungen und Amtszeit."),
        new("Einstellungen der Abteilungen", "/Stammdaten/Abteilungsregeln", Berechtigungen.LaufbahnRegeln,
            "Probezeit, Aufnahme- und Altersgrenzen der Einsatz-, Jugend- und Kinderfeuerwehr, Vorlauf der Hinweise, Prüfung beim Start."),
        new("Züge und Gruppen", "/Mitglieder/Gruppen", Berechtigungen.MitgliederBearbeiten,
            "Untergliederung der Abteilungen, auch als Zielgruppe im Dienstplan."),
        new("Lehrgänge, Abzeichen und Nachweise", "/Personal/Kataloge", Berechtigungen.AusbildungKataloge,
            "Qualifikationen mit Voraussetzungen und Mindestalter, wiederkehrende Nachweise und Untersuchungen mit Intervall."),
        new("Fristen und Erinnerungen", "/Personal/Einstellungen", Berechtigungen.AusbildungKataloge,
            "Intervalle der Atemschutz-Nachweise und Warnfristen für Lehrgänge und Untersuchungen."),
        new("Dienstgrade, Beförderung und Ehrungen", "/Laufbahn/Regeln", Berechtigungen.LaufbahnRegeln,
            "Wartezeiten und Beförderungswege je Dienstgrad, Ehrungsarten und Altersgrenzen der Abteilungen."),
        new("Entschädigungssätze", "/Auswertungen/Entschaedigung/Saetze", Berechtigungen.EntschaedigungSaetze,
            "Stundensätze und Pauschalen nach der Entschädigungssatzung."),
        new("Gerätearten und Prüfintervalle", "/Technik/Geraetearten/Index", Berechtigungen.TechnikBearbeiten,
            "Gerätearten mit Prüfplänen und Intervallen."),
        new("Kleiderkammer-Artikel", "/Kleiderkammer/Artikel", Berechtigungen.KleiderkammerBearbeiten,
            "Artikel der persönlichen Schutzausrüstung mit Größen und Fristen."),
    ];

    public IReadOnlyList<Eintrag> Erlaubt { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Erlaubt = await ErlaubtAsync(autorisierung, User);
        return Erlaubt.Count > 0 ? Page() : Forbid();
    }

    public static async Task<IReadOnlyList<Eintrag>> ErlaubtAsync(IAuthorizationService autorisierung, System.Security.Claims.ClaimsPrincipal benutzer)
    {
        var erlaubt = new List<Eintrag>();
        foreach (var eintrag in Eintraege)
        {
            if ((await autorisierung.AuthorizeAsync(benutzer, eintrag.Policy)).Succeeded)
            {
                erlaubt.Add(eintrag);
            }
        }

        return erlaubt;
    }
}
