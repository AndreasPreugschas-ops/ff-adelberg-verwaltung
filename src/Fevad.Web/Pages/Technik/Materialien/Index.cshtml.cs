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
    /// <summary>Suchtext.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }

    /// <summary>Die gefundenen Materialstämme.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Ab diesem Stückwert in Euro wird inventarisiert (Parameter).</summary>
    public int InventarisierungAbWert { get; private set; }
    /// <summary>Bis zu diesem Stückwert in Euro wird direkt abgeschrieben (Parameter).</summary>
    public int DirektabschreibungBisWert { get; private set; }

    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Hersteller">Hersteller.</param>
    /// <param name="Typ">Typ oder Modell.</param>
    /// <param name="Teilenummer">Teilenummer des Herstellers.</param>
    /// <param name="Geraeteart">Bezeichnung der Geräteart.</param>
    /// <param name="Stueckwert">Stückwert netto in Euro.</param>
    /// <param name="Abschreibung">Abschreibung nach dem Stückwert.</param>
    /// <param name="Inventarisierungspflichtig">Jedes Stück bekommt eine Inventarnummer.</param>
    /// <param name="Inventurpflichtig">Stücke werden bei Inventuren gezählt.</param>
    /// <param name="Bestand">Zahl der Geräte im Bestand.</param>
    /// <param name="Aktiv">In Auswahllisten angeboten.</param>
    public sealed record Zeile(Guid Id, string Bezeichnung, string? Hersteller, string? Typ, string? Teilenummer, string Geraeteart,
        decimal? Stueckwert, Abschreibungsart Abschreibung, bool Inventarisierungspflichtig, bool Inventurpflichtig, int Bestand, bool Aktiv);

    /// <summary>Lädt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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
