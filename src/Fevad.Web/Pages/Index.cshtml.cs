using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

public class IndexModel(AppDbContext db, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public int AktiveMitglieder { get; private set; }
    public int DiensteDiesesJahr { get; private set; }
    public int EinsaetzeDiesesJahr { get; private set; }
    public int OffeneEinsatzberichte { get; private set; }
    public int WarnungTage { get; private set; }

    /// <summary>Fristen anderer Mitglieder nur mit Leserecht auf Mitglieder.</summary>
    public bool FristenSichtbar { get; private set; }
    public IReadOnlyList<FaelligeQualifikation> Faellig { get; private set; } = [];

    public sealed record FaelligeQualifikation(string Mitglied, string Qualifikation, DateOnly GueltigBis, Fristampel Ampel);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var (jahresbeginn, jahresende) = Anzeige.Jahr(heute.Year);
        WarnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, heute, 90, ct);

        AktiveMitglieder = await db.Mitglieder
            .CountAsync(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)), ct);
        DiensteDiesesJahr = await db.Dienste.CountAsync(d => d.Beginn >= jahresbeginn && d.Beginn < jahresende, ct);
        EinsaetzeDiesesJahr = await db.Einsaetze.CountAsync(e => e.Alarmiert >= jahresbeginn && e.Alarmiert < jahresende, ct);
        OffeneEinsatzberichte = await db.Einsaetze.CountAsync(e => e.Status != EinsatzStatus.Freigegeben, ct);

        FristenSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederLesen)).Succeeded;
        if (!FristenSichtbar)
        {
            return;
        }

        // Maßgeblich ist je Mitglied und Qualifikation nur der jüngste Nachweis; ältere, erneuerte Nachweise zählen nicht.
        var nachweise = await db.QualifikationNachweise
            .Select(q => new { q.MitgliedId, q.QualifikationId, q.ErworbenAm, q.GueltigBis, q.Mitglied!.Nachname, q.Mitglied.Vorname, q.Qualifikation!.Bezeichnung })
            .ToListAsync(ct);

        var grenze = heute.AddDays(WarnungTage);
        Faellig = nachweise
            .GroupBy(n => (n.MitgliedId, n.QualifikationId))
            .Select(g => g.MaxBy(n => n.ErworbenAm)!)
            .Where(n => n.GueltigBis is not null && n.GueltigBis <= grenze)
            .OrderBy(n => n.GueltigBis)
            .Take(20)
            .Select(n => new FaelligeQualifikation(
                $"{n.Nachname}, {n.Vorname}", n.Bezeichnung, n.GueltigBis!.Value, Frist.Bewerten(n.GueltigBis, heute, WarnungTage)))
            .ToList();
    }
}
