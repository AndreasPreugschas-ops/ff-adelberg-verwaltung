using FfAdelberg.Domain.Ausbildung;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages;

public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    public int AktiveMitglieder { get; private set; }
    public int DiensteDiesesJahr { get; private set; }
    public int EinsaetzeDiesesJahr { get; private set; }
    public int OffeneEinsatzberichte { get; private set; }
    public IReadOnlyList<FaelligeQualifikation> Faellig { get; private set; } = [];

    public sealed record FaelligeQualifikation(string Mitglied, string Qualifikation, DateOnly GueltigBis, Fristampel Ampel);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var jahresbeginn = new DateTimeOffset(heute.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var warnungTage = 90;
        var grenze = heute.AddDays(warnungTage);

        AktiveMitglieder = await db.Mitglieder
            .CountAsync(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)), ct);
        DiensteDiesesJahr = await db.Dienste.CountAsync(d => d.Beginn >= jahresbeginn, ct);
        EinsaetzeDiesesJahr = await db.Einsaetze.CountAsync(e => e.Alarmiert >= jahresbeginn, ct);
        OffeneEinsatzberichte = await db.Einsaetze.CountAsync(e => e.Status != Domain.Einsaetze.EinsatzStatus.Freigegeben, ct);

        var nachweise = await db.QualifikationNachweise
            .Where(q => q.GueltigBis != null && q.GueltigBis <= grenze)
            .OrderBy(q => q.GueltigBis)
            .Select(q => new { q.Mitglied!.Nachname, q.Mitglied.Vorname, q.Qualifikation!.Bezeichnung, q.GueltigBis })
            .Take(20)
            .ToListAsync(ct);

        Faellig = nachweise
            .Select(n => new FaelligeQualifikation(
                $"{n.Nachname}, {n.Vorname}", n.Bezeichnung, n.GueltigBis!.Value, Frist.Bewerten(n.GueltigBis, heute, warnungTage)))
            .ToList();
    }
}
