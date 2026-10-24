using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Materialien;

/// <summary>Materialstamm mit Suche über Bezeichnung, Hersteller, Typ und Herstellerteilenummer.</summary>
public class IndexModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public int InventarisierungAbWert { get; private set; }
    public int DirektabschreibungBisWert { get; private set; }

    public sealed record Zeile(Guid Id, string Bezeichnung, string? Hersteller, string? Typ, string? Teilenummer, string Geraeteart,
        decimal? Stueckwert, Abschreibungsart Abschreibung, bool Inventarisierungspflichtig, bool Inventurpflichtig, int Bestand, bool Aktiv);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        InventarisierungAbWert = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, heute, 150, ct);
        DirektabschreibungBisWert = await parameter.ZahlAsync(ParameterSchluessel.DirektabschreibungBisWertEuro, heute, 1000, ct);

        var abfrage = db.Materialien.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var muster = $"%{Suche.Trim()}%";
            abfrage = abfrage.Where(m => EF.Functions.ILike(m.Bezeichnung, muster)
                || (m.Hersteller != null && EF.Functions.ILike(m.Hersteller, muster))
                || (m.Typ != null && EF.Functions.ILike(m.Typ, muster))
                || (m.Herstellerteilenummer != null && EF.Functions.ILike(m.Herstellerteilenummer, muster)));
        }

        var materialien = await abfrage
            .OrderBy(m => m.Geraeteart!.Sortierung).ThenBy(m => m.Bezeichnung).ThenBy(m => m.Hersteller)
            .Select(m => new
            {
                m.Id, m.Bezeichnung, m.Hersteller, m.Typ, m.Herstellerteilenummer, Art = m.Geraeteart!.Bezeichnung, m.Stueckwert,
                m.Inventarisierungspflichtig, m.Inventurpflichtig, m.Aktiv,
                Bestand = m.Geraete.Count(g => g.AusgesondertAm == null),
            })
            .Take(500)
            .ToListAsync(ct);
        Zeilen = materialien
            .Select(m => new Zeile(m.Id, m.Bezeichnung, m.Hersteller, m.Typ, m.Herstellerteilenummer, m.Art, m.Stueckwert,
                Domain.Technik.Material.Abschreibung(m.Stueckwert, DirektabschreibungBisWert), m.Inventarisierungspflichtig, m.Inventurpflichtig, m.Bestand, m.Aktiv))
            .ToList();
    }
}
