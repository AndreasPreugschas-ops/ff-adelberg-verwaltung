using Fevad.Domain.Ausbildung;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

public class IndexModel(AppDbContext db, Personalfristenabfrage fristen, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public int AktiveMitglieder { get; private set; }
    public int DiensteDiesesJahr { get; private set; }
    public int EinsaetzeDiesesJahr { get; private set; }
    public int OffeneEinsatzberichte { get; private set; }
    /// <summary>Fristen anderer Mitglieder nur mit Leserecht auf die Personalverwaltung.</summary>
    public bool FristenSichtbar { get; private set; }
    public int FristenGesamt { get; private set; }
    public IReadOnlyList<Fristeintrag> Faellig { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var (jahresbeginn, jahresende) = Anzeige.Jahr(heute.Year);

        AktiveMitglieder = await db.Mitglieder
            .CountAsync(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)), ct);
        DiensteDiesesJahr = await db.Dienste.CountAsync(d => d.Beginn >= jahresbeginn && d.Beginn < jahresende, ct);
        EinsaetzeDiesesJahr = await db.Einsaetze.CountAsync(e => e.Alarmiert >= jahresbeginn && e.Alarmiert < jahresende, ct);
        OffeneEinsatzberichte = await db.Einsaetze.CountAsync(e => e.Status != EinsatzStatus.Freigegeben, ct);

        FristenSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungLesen)).Succeeded;
        if (!FristenSichtbar)
        {
            return;
        }

        var gesundheit = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AtemschutzLesen)).Succeeded;
        var offen = (await fristen.LadenAsync(heute, ct: ct))
            .Where(f => f.Ampel != Fristampel.Gruen && (gesundheit || !f.Gesundheitsbezogen))
            .OrderByDescending(f => f.Ampel).ThenBy(f => f.FaelligAm ?? DateOnly.MinValue).ThenBy(f => f.Mitglied)
            .ToList();
        FristenGesamt = offen.Count;
        Faellig = offen.Take(10).ToList();
    }
}
