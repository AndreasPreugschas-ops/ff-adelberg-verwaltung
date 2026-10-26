using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Terminzettel auf Papier (V41) für Mitglieder ohne Smartphone, z. B. in der Altersabteilung: die Termine, die das Mitglied betreffen,
/// für die nächsten Monate als PDF zum Ausdrucken oder Mitgeben. Wer Dienste planen darf, druckt für die Mitglieder seiner Abteilungen.
/// </summary>
public class TerminzettelModel(AppDbContext db, Dienstplanung planung, Datenbereich bereich, Kassenberichte berichte, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Mitglied in der Auswahl.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Abteilung">Abteilung(en) am Stichtag.</param>
    /// <param name="OhneKonto">Hat kein aktives Konto in FEVAD.</param>
    public sealed record Person(Guid Id, string Name, string Abteilung, bool OhneKonto);

    /// <summary>Mitglieder, für die gedruckt werden darf; ohne Konto zuerst.</summary>
    public IReadOnlyList<Person> Personen { get; private set; } = [];

    /// <summary>Zeigt die Auswahl.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!(await bereich.FreigabeAsync(User, Berechtigungen.DiensteBearbeiten)).Irgendwo)
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        var mitglieder = await bereich.FilternAsync(db.Mitglieder.AsNoTracking(), User, Berechtigungen.DiensteBearbeiten, heute, ct);
        var konten = (await db.Users.Where(u => u.MitgliedId != null && u.DeaktiviertAm == null).Select(u => u.MitgliedId!.Value).ToListAsync(ct)).ToHashSet();
        Personen = (await mitglieder
                .Where(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)))
                .Select(m => new
                {
                    m.Id, m.Nachname, m.Vorname,
                    Abteilungen = m.Mitgliedschaften.Where(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
                        .Select(s => s.Abteilung!.Bezeichnung).ToList(),
                })
                .ToListAsync(ct))
            .Select(m => new Person(m.Id, $"{m.Nachname}, {m.Vorname}", string.Join(", ", m.Abteilungen), !konten.Contains(m.Id)))
            .OrderByDescending(p => p.OhneKonto).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Page();
    }

    /// <summary>Erzeugt den Terminzettel eines Mitglieds.</summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="monate">Zeitraum in Monaten ab heute (1 bis 12).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid mitgliedId, int monate, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var mitglieder = await bereich.FilternAsync(db.Mitglieder.AsNoTracking(), User, Berechtigungen.DiensteBearbeiten, heute, ct);
        if (await mitglieder.Where(m => m.Id == mitgliedId).Select(m => new { m.Vorname, m.Nachname }).FirstOrDefaultAsync(ct) is not { } m)
        {
            return (await bereich.FreigabeAsync(User, Berechtigungen.DiensteBearbeiten)).Irgendwo ? NotFound() : Forbid();
        }

        monate = Math.Clamp(monate, 1, 12);
        var bis = heute.AddMonths(monate);
        var termine = await planung.TermineAsync(Anzeige.AlsUtc(heute, TimeOnly.MinValue), Anzeige.AlsUtc(bis.AddDays(1), TimeOnly.MinValue), await planung.ProfilAsync(mitgliedId, ct), ct);
        var pdf = TerminzettelPdf.Erzeugen(await berichte.KopfAsync(ct), $"{m.Vorname} {m.Nachname}",
            $"{Anzeige.Datum(heute)} bis {Anzeige.Datum(bis)}", termine,
            "Zu- oder Absagen bitte wie gewohnt bei der Abteilungsleitung. Änderungen nach dem Druck stehen in FEVAD.",
            Anzeige.DatumZeit(zeit.GetUtcNow()));
        return File(pdf, "application/pdf", $"Termine_{m.Nachname}_{heute:yyyy-MM-dd}.pdf");
    }
}
