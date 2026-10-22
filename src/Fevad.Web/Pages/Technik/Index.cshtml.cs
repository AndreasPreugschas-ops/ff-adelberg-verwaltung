using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

public class IndexModel(AppDbContext db, Pruefstaende pruefstaende, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Vorschau in Tagen; ohne Angabe die Warnfrist aus dem Parameter.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Tage { get; set; }

    public int Geraete { get; private set; }
    public int Ueberfaellig { get; private set; }
    public int AusserBetrieb { get; private set; }
    public int OffeneMaengel { get; private set; }
    public IReadOnlyList<Zeile> Faellig { get; private set; } = [];
    public IReadOnlyList<Pruefstand> Aussonderung { get; private set; } = [];

    public sealed record Zeile(Pruefstand Stand, Faelligkeit Faelligkeit);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Tage ??= await parameter.ZahlAsync(ParameterSchluessel.PruefungWarnungTage, heute, 30, ct);
        var grenze = heute.AddDays(Math.Clamp(Tage.Value, 0, 730));

        var fahrzeuge = await pruefstaende.FahrzeugeAsync(heute, ct: ct);
        var geraete = await pruefstaende.GeraeteAsync(heute, ct: ct);
        var alle = fahrzeuge.Concat(geraete).ToList();

        Geraete = geraete.Count;
        Ueberfaellig = alle.Count(s => s.Faelligkeiten.Any(f => f.Ampel == Fristampel.Rot));
        AusserBetrieb = alle.Count(s => s.Zustand == GeraetZustand.AusserBetrieb);
        OffeneMaengel = await db.Maengel.CountAsync(m => m.Status != MangelStatus.Behoben, ct);

        // Fahrzeuge zuerst, dann nach Termin; nie Geprüftes ganz oben.
        Faellig = alle
            .SelectMany(s => s.Faelligkeiten.Select(f => new Zeile(s, f)))
            .Where(z => z.Faelligkeit.NieGeprueft || z.Faelligkeit.FaelligAm <= grenze)
            .OrderBy(z => z.Faelligkeit.FaelligAm ?? DateOnly.MinValue)
            .ThenBy(z => !z.Stand.IstFahrzeug)
            .ThenBy(z => z.Stand.Nummer)
            .ToList();

        Aussonderung = geraete
            .Where(s => s.AussonderungFaellig <= grenze)
            .OrderBy(s => s.AussonderungFaellig)
            .ToList();
    }
}
