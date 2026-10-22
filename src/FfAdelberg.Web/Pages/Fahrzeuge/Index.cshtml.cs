using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Fahrzeuge;

public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zeile(string Funkrufname, string Typ, int? Baujahr, string? Kennzeichen, int Geraete, DateOnly? NaechstePruefung, Fristampel Ampel);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var daten = await db.Fahrzeuge.AsNoTracking()
            .Where(f => f.AusserDienstAm == null)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new
            {
                f.Funkrufname,
                f.Typ,
                f.Baujahr,
                f.Kennzeichen,
                Geraete = f.Geraete.Count,
                NaechstePruefung = db.Pruefungen
                    .Where(p => p.FahrzeugId == f.Id || (p.Geraet != null && p.Geraet.FahrzeugId == f.Id))
                    .Min(p => p.NaechsteFaellig),
            })
            .ToListAsync(ct);

        Zeilen = daten
            .Select(f => new Zeile(f.Funkrufname, f.Typ, f.Baujahr, f.Kennzeichen, f.Geraete, f.NaechstePruefung, Frist.Bewerten(f.NaechstePruefung, heute, 30)))
            .ToList();
    }
}
