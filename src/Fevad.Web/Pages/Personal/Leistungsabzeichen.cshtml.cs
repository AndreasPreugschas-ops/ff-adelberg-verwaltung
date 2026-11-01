using System.Text.Json;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Planungshilfe für eine Abnahme des Leistungsabzeichens: Wer erfüllt die Voraussetzungen der Stufe und
/// welche Funktion kann er am Abnahmetag übernehmen? Die Atemschutz-Spalte sieht nur, wer Atemschutz lesen darf.
/// </summary>
public class LeistungsabzeichenModel(AppDbContext db, Abzeichenabfrage abzeichen, TimeProvider zeit, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Beim ATLAS-Export E-Mail und Mobilnummer mitgeben (nur mit dem Recht, Mitglieder zu lesen).</summary>
    [BindProperty(SupportsGet = true)] public bool MitKontakt { get; set; }
    /// <summary>Der Benutzer darf Kontaktdaten mitgeben; dann wird das Häkchen gezeigt.</summary>
    public bool DarfKontakt { get; private set; }
    /// <summary>Gewählte Stufe des Abzeichens.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Stufe { get; set; }
    /// <summary>Tag der Abnahme; ohne Angabe heute.</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? Abnahmetag { get; set; }
    /// <summary>Nur Personen zeigen, die die Voraussetzungen erfüllen.</summary>
    [BindProperty(SupportsGet = true)] public bool NurMoegliche { get; set; }

    /// <summary>Wählbare Stufen.</summary>
    public IReadOnlyList<Qualifikation> Stufen { get; private set; } = [];
    /// <summary>Die geplante Stufe.</summary>
    public Qualifikation? Ziel { get; private set; }
    /// <summary>Die angezeigten Personen.</summary>
    public IReadOnlyList<Abzeichenabfrage.Zeile> Zeilen { get; private set; } = [];
    /// <summary>Zahl der Personen, die die Voraussetzungen erfüllen.</summary>
    public int Moegliche { get; private set; }
    /// <summary>Die Atemschutz-Spalte wird gezeigt.</summary>
    public bool MitAtemschutz { get; private set; }

    /// <summary>Lädt die Planung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Abnahmetag ??= Anzeige.Heute(zeit);
        MitAtemschutz = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AtemschutzLesen)).Succeeded;
        DarfKontakt = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederLesen)).Succeeded;

        // Abzeichen der Einsatzabteilung: alles ohne Jugendbezug und ohne Altersgrenze.
        Stufen = await db.Qualifikationen.AsNoTracking().Include(q => q.Vorstufe)
            .Where(q => q.Aktiv && q.Art == QualifikationArt.Abzeichen && q.MindestJugendMonate == null && q.MindestalterJahre == null)
            .OrderBy(q => q.Sortierung)
            .ToListAsync(ct);
        Ziel = Stufen.FirstOrDefault(q => q.Id == Stufe) ?? Stufen.FirstOrDefault();
        if (Ziel is null)
        {
            return;
        }

        Stufe = Ziel.Id;
        var zeilen = await abzeichen.PlanenAsync(Ziel, Abnahmetag.Value, MitAtemschutz, ct);
        Moegliche = zeilen.Count(z => z.Erfuellt);
        Zeilen = NurMoegliche ? zeilen.Where(z => z.Erfuellt).ToList() : zeilen;
    }

    /// <summary>
    /// Schnittstelle zu ATLAS: die angezeigten Personen als CSV für den ATLAS-Personenimport.
    /// E-Mail und Mobilnummer nur auf ausdrücklichen Wunsch und mit dem Recht, Mitglieder zu lesen. Jeder Export wird protokolliert.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task<IActionResult> OnGetAtlasAsync(CancellationToken ct)
    {
        await OnGetAsync(ct);
        var ids = Zeilen.Select(z => z.MitgliedId).ToList();
        var mitglieder = await db.Mitglieder.AsNoTracking().Where(m => ids.Contains(m.Id))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname).ToListAsync(ct);
        var mitKontakt = MitKontakt && DarfKontakt;
        db.AuditEintraege.Add(new AuditEintrag
        {
            Zeitpunkt = zeit.GetUtcNow(), BenutzerId = benutzer.Id, Tabelle = AtlasExport.Protokolltabelle, DatensatzId = Guid.CreateVersion7(),
            Aktion = AuditAktion.Gelesen, Aenderungen = JsonSerializer.Serialize(new { personen = mitglieder.Count, kontaktdaten = mitKontakt }),
        });
        await db.SaveChangesAsync(ct);
        return File(AtlasExport.Csv(mitglieder, mitKontakt), "text/csv; charset=utf-8", AtlasExport.Dateiname(Anzeige.Heute(zeit)));
    }
}
