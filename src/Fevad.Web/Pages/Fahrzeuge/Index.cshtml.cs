using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Fahrzeuge;

public class IndexModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zeile(string Funkrufname, string Typ, int? Baujahr, string? Kennzeichen, int Geraete, DateOnly? NaechstePruefung, Fristampel Ampel);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.PruefungWarnungTage, heute, 30, ct);

        var fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => f.AusserDienstAm == null)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new { f.Id, f.Funkrufname, f.Typ, f.Baujahr, f.Kennzeichen, Geraete = f.Geraete.Count })
            .ToListAsync(ct);

        var pruefungen = await db.Pruefungen.AsNoTracking()
            .Where(p => p.FahrzeugId != null || (p.Geraet != null && p.Geraet.FahrzeugId != null))
            .Select(p => new
            {
                p.GeraetId,
                p.FahrzeugId,
                ZugehoerigesFahrzeug = p.FahrzeugId ?? p.Geraet!.FahrzeugId,
                p.Pruefart,
                p.Datum,
                p.NaechsteFaellig,
            })
            .ToListAsync(ct);

        // Je Prüfgegenstand und Prüfart zählt nur die jüngste Prüfung, sonst halten alte Fälligkeiten die Ampel rot.
        var naechste = pruefungen
            .GroupBy(p => (p.GeraetId, p.FahrzeugId, p.Pruefart))
            .Select(g => g.OrderByDescending(p => p.Datum).First())
            .Where(p => p.NaechsteFaellig is not null)
            .GroupBy(p => p.ZugehoerigesFahrzeug!.Value)
            .ToDictionary(g => g.Key, g => g.Min(p => p.NaechsteFaellig));

        Zeilen = fahrzeuge
            .Select(f =>
            {
                var faellig = naechste.GetValueOrDefault(f.Id);
                return new Zeile(f.Funkrufname, f.Typ, f.Baujahr, f.Kennzeichen, f.Geraete, faellig, Frist.Bewerten(faellig, heute, warnungTage));
            })
            .ToList();
    }
}
