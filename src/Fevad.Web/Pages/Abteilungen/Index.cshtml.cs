using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Abteilungen;

/// <summary>
/// Sicht je Abteilung mit den Daten, die dort zählen: Jugendfeuerwehr mit Übertrittsterminen, Einsatzabteilung mit
/// Probezeit und Altersgrenze, Altersabteilung mit Dienstjahren. Der Jugendwart sieht nur die Jugendfeuerwehr.
/// </summary>
public class IndexModel(AppDbContext db, Laufbahnabfrage laufbahn, Datenbereich datenbereich, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Code { get; set; }

    public DateOnly Heute { get; private set; }
    public Laufbahnregeln Regeln { get; private set; } = Laufbahnregeln.Satzung;
    public string Bezeichnung { get; private set; } = "";
    public bool NurJugend { get; private set; }
    public bool LaufbahnSichtbar { get; private set; }
    public IReadOnlyList<(string Code, string Bezeichnung, int Anzahl)> Reiter { get; private set; } = [];
    public IReadOnlyList<Laufbahnstand> Mitglieder { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        NurJugend = await datenbereich.NurJugendAsync(User);
        LaufbahnSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnLesen)).Succeeded;
        if (NurJugend)
        {
            Code = AbteilungCode.Jugend;
        }

        Code ??= AbteilungCode.Einsatz;
        var abteilung = await db.Abteilungen.AsNoTracking().SingleOrDefaultAsync(a => a.Code == Code, ct);
        if (abteilung is null)
        {
            return NotFound();
        }

        Bezeichnung = abteilung.Bezeichnung;
        Regeln = await laufbahn.RegelnAsync(Heute, ct);

        var heute = Heute;
        var sichtbar = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute);
        var offen = db.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute));
        Reiter = (await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && (!NurJugend || a.Code == AbteilungCode.Jugend)).OrderBy(a => a.Sortierung)
                .Select(a => new { a.Code, a.Bezeichnung, Anzahl = offen.Count(s => s.AbteilungId == a.Id && sichtbar.Any(m => m.Id == s.MitgliedId)) })
                .ToListAsync(ct))
            .Select(a => (a.Code, a.Bezeichnung, a.Anzahl)).ToList();

        var ids = await sichtbar.Where(m => offen.Any(s => s.MitgliedId == m.Id && s.AbteilungId == abteilung.Id)).Select(m => m.Id).ToListAsync(ct);
        Mitglieder = await laufbahn.LadenAsync(heute, ids, ct);
        return Page();
    }

    /// <summary>Der am Stichtag offene Abschnitt in dieser Abteilung.</summary>
    public Abschnitt? Abschnitt(Laufbahnstand stand) =>
        stand.OffeneAbschnitte(Heute).Where(a => a.AbteilungCode == Code).MaxBy(a => a.Von);

    /// <summary>Erster Tag der ununterbrochenen Zugehörigkeit zur Abteilung (Probezeit und aktiv zusammen).</summary>
    public DateOnly? Seit(Laufbahnstand stand)
    {
        var abschnitte = stand.Abschnitte.Where(a => !a.Vordienstzeit && a.AbteilungCode == Code).OrderByDescending(a => a.Von).ToList();
        if (abschnitte.Count == 0)
        {
            return null;
        }

        var seit = abschnitte[0].Von;
        foreach (var a in abschnitte.Skip(1))
        {
            if (a.Bis is { } bis && bis.AddDays(1) >= seit)
            {
                seit = a.Von;
            }
            else
            {
                break;
            }
        }

        return seit;
    }
}
