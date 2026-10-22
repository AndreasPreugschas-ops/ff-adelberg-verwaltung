using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

public class IndexModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zeile(Pruefstand Stand, string? Kennzeichen, int? Baujahr, int Geraete);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var staende = await pruefstaende.FahrzeugeAsync(heute, ct: ct);
        var daten = await db.Fahrzeuge.AsNoTracking()
            .Select(f => new { f.Id, f.Kennzeichen, f.Baujahr, Geraete = f.Geraete.Count(g => g.AusgesondertAm == null) })
            .ToDictionaryAsync(f => f.Id, ct);

        Zeilen = staende
            .Select(s => new Zeile(s, daten[s.Id].Kennzeichen, daten[s.Id].Baujahr, daten[s.Id].Geraete))
            .ToList();
    }
}
