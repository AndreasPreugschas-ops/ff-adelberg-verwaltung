using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Infrastructure.Einsaetze;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Einsaetze;

public class IndexModel(AppDbContext db, Einsatzberichte berichte, IAuthorizationService autorisierung) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public int UnverarbeiteteAlarme { get; private set; }
    public bool DarfBearbeiten { get; private set; }

    public sealed record Zeile(Guid Id, string Einsatznummer, DateTimeOffset Alarmiert, string? Stichwort, string? Ort, string? Kategorie, EinsatzStatus Status, int Teilnehmer);

    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded;

        Zeilen = await db.Einsaetze.AsNoTracking()
            .OrderByDescending(e => e.Alarmiert)
            .Take(100)
            .Select(e => new Zeile(e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort, e.Ort, e.StatistikKategorie!.Bezeichnung, e.Status, e.Teilnahmen.Count))
            .ToListAsync(ct);

        UnverarbeiteteAlarme = await db.AlarmImporte.CountAsync(a => !a.Verarbeitet, ct);
    }

    /// <summary>Holt Entwürfe für Alarme nach, bei denen das Anlegen beim Empfang gescheitert ist.</summary>
    public async Task<IActionResult> OnPostAlarmeUebernehmenAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var anzahl = await berichte.OffeneAlarmeVerarbeitenAsync(ct);
        TempData["Meldung"] = $"{anzahl} Alarm(e) übernommen.";
        return RedirectToPage();
    }
}
