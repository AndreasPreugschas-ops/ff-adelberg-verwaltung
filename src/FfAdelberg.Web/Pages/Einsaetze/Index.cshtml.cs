using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Einsaetze;

public class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public int UnverarbeiteteAlarme { get; private set; }

    public sealed record Zeile(string Einsatznummer, DateTimeOffset Alarmiert, string? Stichwort, string? Ort, string? Kategorie, EinsatzStatus Status, int Teilnehmer);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Zeilen = await db.Einsaetze.AsNoTracking()
            .OrderByDescending(e => e.Alarmiert)
            .Take(100)
            .Select(e => new Zeile(e.Einsatznummer, e.Alarmiert, e.Stichwort, e.Ort, e.StatistikKategorie!.Bezeichnung, e.Status, e.Teilnahmen.Count))
            .ToListAsync(ct);

        UnverarbeiteteAlarme = await db.AlarmImporte.CountAsync(a => !a.Verarbeitet, ct);
    }
}
