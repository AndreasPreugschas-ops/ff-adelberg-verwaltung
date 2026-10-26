using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitteilungen;

/// <summary>Mitteilungen an mich (aktuelle und ältere) und, für Verfassende, die eigenen Entwürfe und veröffentlichten Mitteilungen mit Lesestand (V42).</summary>
public class IndexModel(AppDbContext db, Schreibrecht schreibrecht, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Mitteilung an mich.</summary>
    /// <param name="Id">Schlüssel.</param>
    /// <param name="Titel">Titel.</param>
    /// <param name="Verfasser">Verfasser.</param>
    /// <param name="Am">Veröffentlicht am.</param>
    /// <param name="Gelesen">Schon geöffnet.</param>
    /// <param name="Offen">Lesebestätigung erbeten und noch nicht gegeben.</param>
    /// <param name="Aktuell">Steht oben.</param>
    public sealed record AnMich(Guid Id, string Titel, string? Verfasser, DateTimeOffset? Am, bool Gelesen, bool Offen, bool Aktuell);

    /// <summary>Eine Mitteilung zur Verwaltung.</summary>
    /// <param name="Id">Schlüssel.</param>
    /// <param name="Titel">Titel.</param>
    /// <param name="Status">Stand.</param>
    /// <param name="Am">Veröffentlicht oder angelegt am.</param>
    /// <param name="Empfaenger">Zahl der Empfänger.</param>
    /// <param name="Gelesen">Zahl der Lesenden.</param>
    /// <param name="Bestaetigt">Zahl der Bestätigungen; null ohne Lesebestätigung.</param>
    public sealed record Verwaltet(Guid Id, string Titel, MitteilungStatus Status, DateTimeOffset Am, int Empfaenger, int Gelesen, int? Bestaetigt);

    /// <summary>Mitteilungen an mich.</summary>
    public IReadOnlyList<AnMich> Eingang { get; private set; } = [];
    /// <summary>Mitteilungen, die ich verwalten darf.</summary>
    public IReadOnlyList<Verwaltet> Verfasst { get; private set; } = [];
    /// <summary>Darf Mitteilungen schreiben.</summary>
    public bool Schreiben { get; private set; }

    /// <summary>Zeigt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        if (benutzer.Id is { } konto)
        {
            var eingang = await db.MitteilungEmpfaenger.AsNoTracking().Where(e => e.KontoId == konto)
                .Join(db.Mitteilungen.Where(m => m.Status == MitteilungStatus.Veroeffentlicht), e => e.MitteilungId, m => m.Id, (e, m) => new { e, m })
                .OrderByDescending(x => x.m.VeroeffentlichtAm).Take(200)
                .ToListAsync(ct);
            var neuester = eingang.FirstOrDefault()?.m.Id;
            Eingang = eingang.Select(x => new AnMich(x.m.Id, x.m.Titel, x.m.Verfasser, x.m.VeroeffentlichtAm, x.e.GelesenAm is not null,
                    x.m.Lesebestaetigung && x.e.BestaetigtAm is null,
                    x.m.AktuellBis is { } bis ? bis >= heute : x.m.Id == neuester || x.e.GelesenAm is null || (x.m.Lesebestaetigung && x.e.BestaetigtAm is null)))
                .ToList();
        }

        Schreiben = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitteilungenVerfassen)).Succeeded;
        if (Schreiben)
        {
            var kandidaten = await db.Mitteilungen.AsNoTracking().OrderByDescending(m => m.VeroeffentlichtAm ?? m.ErstelltAm).Take(100).ToListAsync(ct);
            var ids = kandidaten.Select(k => k.Id).ToList();
            var zaehler = await db.MitteilungEmpfaenger.Where(e => ids.Contains(e.MitteilungId)).GroupBy(e => e.MitteilungId)
                .Select(g => new { g.Key, Alle = g.Count(), Gelesen = g.Count(e => e.GelesenAm != null), Bestaetigt = g.Count(e => e.BestaetigtAm != null) })
                .ToDictionaryAsync(g => g.Key, ct);
            var liste = new List<Verwaltet>();
            foreach (var m in kandidaten)
            {
                if (m.ErstelltVon == benutzer.Id || await schreibrecht.DarfAsync(User, m, ct))
                {
                    var z = zaehler.GetValueOrDefault(m.Id);
                    liste.Add(new(m.Id, m.Titel, m.Status, m.VeroeffentlichtAm ?? m.ErstelltAm, z?.Alle ?? 0, z?.Gelesen ?? 0, m.Lesebestaetigung ? z?.Bestaetigt ?? 0 : null));
                }
            }

            Verfasst = liste;
        }

        return Page();
    }
}
