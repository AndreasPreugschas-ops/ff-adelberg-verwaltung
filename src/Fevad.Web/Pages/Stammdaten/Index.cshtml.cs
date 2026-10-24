using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>Alle Kataloge und Regeln an einer Stelle; zeigt nur, was die Rolle pflegen darf. Die Seiten selbst bleiben in ihren Bereichen.</summary>
public class IndexModel(IAuthorizationService autorisierung, AppDbContext db) : PageModel
{
    public sealed record Eintrag(string Titel, string Seite, string Policy, string Beschreibung);

    public static readonly IReadOnlyList<Eintrag> Eintraege =
    [
        new("Feuerwehr, Gemeinde und Logo", "/Stammdaten/Feuerwehr", Berechtigungen.StammdatenOrganisation,
            "Anschriften der Feuerwehr und der Gemeinde (verantwortliche Stelle im Datenschutz) und das Logo für die Formulare."),
        new("Abteilungen und Funktionen", "/Stammdaten/Organisation", Berechtigungen.StammdatenOrganisation,
            "Abteilungen mit Anschrift anlegen, umbenennen und abschalten (fünf Einsatzabteilungen, Jugend-, Kinder- und Altersabteilung); Funktionen mit Rechtsgrundlage, Voraussetzungen und Amtszeit."),
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

    /// <summary>Anschriften, die auf den Formularen noch fehlen; nur für Benutzer, die sie pflegen dürfen.</summary>
    public IReadOnlyList<string> FehlendeAnschriften { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Erlaubt = await ErlaubtAsync(autorisierung, User);
        if (Erlaubt.Count == 0)
        {
            return Forbid();
        }

        if (Erlaubt.Any(e => e.Policy == Berechtigungen.StammdatenOrganisation))
        {
            var o = await db.OrganisationAsync(ct);
            var fehlend = new List<string>();
            if (o.FeuerwehrAnschrift is null)
            {
                fehlend.Add("Feuerwehr");
            }

            if (o.GemeindeAnschrift is null)
            {
                fehlend.Add("Gemeinde");
            }

            fehlend.AddRange(await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && (a.Strasse == null || a.Ort == null))
                .OrderBy(a => a.Sortierung).Select(a => a.Bezeichnung).ToListAsync(ct));
            FehlendeAnschriften = fehlend;
        }

        return Page();
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
