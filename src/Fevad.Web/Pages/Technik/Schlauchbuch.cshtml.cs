using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>
/// Schlauchbuch (V63): alle Schläuche, deren Geräteart über die Schlauchwerkstatt zurückläuft, mit Größe und Länge aus der
/// Typangabe, Prüfstand und den Läufen durch die Schlauchwerkstatt (Wäsche, Prüfung, Reparatur laut Lieferschein).
/// </summary>
public class SchlauchbuchModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    /// <summary>Schläuche, nach Größe und Nummer.</summary>
    public IReadOnlyList<Zeile> Schlaeuche { get; private set; } = [];

    /// <summary>Vorhandene Größen für den Filter.</summary>
    public IReadOnlyList<string> Groessen { get; private set; } = [];

    /// <summary>Gewählte Größe; leer heißt alle.</summary>
    public string? Groesse { get; private set; }

    /// <summary>Summe der Längen je Größe (nicht ausgesondert).</summary>
    public IReadOnlyList<(string Groesse, int Anzahl, int Meter)> Summen { get; private set; } = [];

    /// <summary>Ein Schlauch im Schlauchbuch.</summary>
    /// <param name="Stand">Prüfstand mit Zustand, Standort und Fälligkeiten.</param>
    /// <param name="Typ">Größe und Länge; leer, wenn die Typangabe nicht lesbar ist.</param>
    /// <param name="Baujahr">Baujahr.</param>
    /// <param name="Werkstattlaeufe">Zurückgekommene Läufe durch die Schlauchwerkstatt.</param>
    /// <param name="LetzterLauf">Rückgabe des letzten Laufs.</param>
    /// <param name="Leistungen">Leistungen des letzten Laufs.</param>
    /// <param name="ImUmlauf">Gerade in der Werkstatt.</param>
    /// <param name="Pflege">Einträge im Pflege-Lebenslauf (z. B. Reparatur, Einband).</param>
    public sealed record Zeile(Pruefstand Stand, Schlauchtyp? Typ, int? Baujahr, int Werkstattlaeufe, DateOnly? LetzterLauf, Werkstattleistung? Leistungen, bool ImUmlauf, int Pflege);

    /// <summary>Zeigt das Schlauchbuch.</summary>
    /// <param name="groesse">Nur diese Größe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(string? groesse, CancellationToken ct)
    {
        var schlaeuche = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && g.Geraeteart!.Ruecklauf == Ruecklauf.Schlauchwerkstatt)
            .Select(g => new { g.Id, g.Material!.Typ, g.Baujahr })
            .ToListAsync(ct);
        var ids = schlaeuche.Select(s => s.Id).ToList();
        var staende = (await pruefstaende.GeraeteAsync(Anzeige.Heute(zeit), ct: ct)).Where(s => ids.Contains(s.Id)).ToDictionary(s => s.Id);
        var laeufe = (await db.Werkstattpositionen.AsNoTracking()
                .Where(p => ids.Contains(p.GeraetId))
                .Select(p => new { p.GeraetId, p.Werkstattauftrag!.ZurueckAm, p.Leistungen })
                .ToListAsync(ct))
            .ToLookup(p => p.GeraetId);
        var pflege = await db.Pflegeeintraege.AsNoTracking().Where(p => ids.Contains(p.GeraetId))
            .GroupBy(p => p.GeraetId).Select(g => new { g.Key, Anzahl = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Anzahl, ct);

        var alle = schlaeuche.Where(s => staende.ContainsKey(s.Id)).Select(s =>
        {
            var zurueck = laeufe[s.Id].Where(l => l.ZurueckAm != null).OrderByDescending(l => l.ZurueckAm).ToList();
            return new Zeile(staende[s.Id], Schlauchtyp.Lesen(s.Typ), s.Baujahr, zurueck.Count, zurueck.FirstOrDefault()?.ZurueckAm,
                zurueck.FirstOrDefault()?.Leistungen, laeufe[s.Id].Any(l => l.ZurueckAm == null), pflege.GetValueOrDefault(s.Id));
        }).ToList();

        Groessen = alle.Select(z => z.Typ?.Groesse ?? "ohne Typ").Distinct().Order().ToList();
        Summen = alle.Where(z => z.Typ is not null).GroupBy(z => z.Typ!.Groesse).OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Count(), g.Sum(z => z.Typ!.LaengeM))).ToList();
        Groesse = string.IsNullOrWhiteSpace(groesse) ? null : groesse;
        Schlaeuche = alle.Where(z => Groesse is null || (z.Typ?.Groesse ?? "ohne Typ") == Groesse)
            .OrderBy(z => z.Typ?.Groesse ?? "~").ThenBy(z => z.Stand.Nummer).ToList();
    }
}
