using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

public class IndexModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? FahrzeugId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? GeraeteartId { get; set; }
    [BindProperty(SupportsGet = true)] public bool NurFaellige { get; set; }
    [BindProperty(SupportsGet = true)] public bool Ausgesonderte { get; set; }

    public IReadOnlyList<Pruefstand> Zeilen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Fahrzeuge = await db.Fahrzeuge.OrderBy(f => f.Funkrufname).Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString())).ToListAsync(ct);
        Geraetearten = await db.Geraetearten.Where(g => !g.FuerFahrzeuge).OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct);
        Zeilen = await LadenAsync(ct);
    }

    /// <summary>HTMX: liefert nur die Tabelle für die Live-Suche.</summary>
    public async Task<PartialViewResult> OnGetTabelleAsync(CancellationToken ct) =>
        Partial("_Tabelle", await LadenAsync(ct));

    private async Task<IReadOnlyList<Pruefstand>> LadenAsync(CancellationToken ct)
    {
        var ids = (await Filtern(db.Geraete, Suche, FahrzeugId, GeraeteartId).Select(g => g.Id).ToListAsync(ct)).ToHashSet();
        var staende = await pruefstaende.GeraeteAsync(Anzeige.Heute(zeit), fahrzeugId: FahrzeugId, mitAusgesonderten: Ausgesonderte, ct: ct);
        return staende
            .Where(s => ids.Contains(s.Id))
            .Where(s => !NurFaellige || s.Ampel != Fristampel.Gruen)
            .ToList();
    }

    /// <summary>Gleiche Auswahl wie die Tabelle, auch für den Etikettendruck.</summary>
    public static IQueryable<Geraet> Filtern(IQueryable<Geraet> abfrage, string? suche, Guid? fahrzeugId, Guid? geraeteartId)
    {
        if (!string.IsNullOrWhiteSpace(suche))
        {
            var muster = $"%{suche.Trim()}%";
            abfrage = abfrage.Where(g =>
                EF.Functions.ILike(g.Inventarnummer, muster)
                || EF.Functions.ILike(g.Bezeichnung, muster)
                || (g.Seriennummer != null && EF.Functions.ILike(g.Seriennummer, muster)));
        }

        if (fahrzeugId is not null)
        {
            abfrage = abfrage.Where(g => g.FahrzeugId == fahrzeugId);
        }

        if (geraeteartId is not null)
        {
            abfrage = abfrage.Where(g => g.GeraeteartId == geraeteartId);
        }

        return abfrage;
    }
}
