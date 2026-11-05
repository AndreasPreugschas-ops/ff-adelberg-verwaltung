using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>
/// Ausrüstung der Mitglieder für Kleiderwart und Gerätewart: je Mitglied Stückzahl, offene Fristen der Einzelstücke und offene Quittungen.
/// Nur Name und Abteilung, keine Kontaktdaten.
/// </summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class MitgliederModel(AppDbContext db, Kleiderkammerstand stand, TimeProvider zeit) : PageModel
{
    /// <summary>Suchtext für Name oder Personalnummer.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Suche { get; set; }

    /// <summary>Auch ausgeschiedene Mitglieder ohne Ausstattung zeigen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool AuchEhemalige { get; set; }

    /// <summary>Nur Mitglieder mit fälliger Prüfung, ablaufender Lebensdauer oder offener Quittung.</summary>
    [BindProperty(SupportsGet = true)]
    public bool NurOffene { get; set; }

    /// <summary>Die gefundenen Mitglieder.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Ein Mitglied in der Liste.</summary>
    /// <param name="Id">Kennung des Mitglieds.</param>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Name">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Abteilung">Aktuelle Abteilung.</param>
    /// <param name="Stuecke">Anzahl der ausgegebenen, nicht zurückgegebenen Stücke.</param>
    /// <param name="GroessenErfasst">Das Größenprofil ist ausgefüllt.</param>
    /// <param name="Ampel">Dringlichste Frist der Einzelstücke, null ohne Einzelstücke.</param>
    /// <param name="OffeneQuittungen">Ausgaben und Rücknahmen ohne Unterschrift.</param>
    public sealed record Zeile(Guid Id, string Personalnummer, string Name, string? Abteilung, int Stuecke, bool GroessenErfasst, Fristampel? Ampel, int OffeneQuittungen)
    {
        /// <summary>Es gibt etwas zu tun.</summary>
        public bool Offen => Ampel is Fristampel.Rot or Fristampel.Gelb || OffeneQuittungen > 0;
    }

    /// <summary>Lädt die Mitglieder nach Filter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var abfrage = db.Mitglieder.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var muster = $"%{Suche.Trim()}%";
            abfrage = abfrage.Where(m => EF.Functions.ILike(m.Nachname, muster) || EF.Functions.ILike(m.Vorname, muster) || EF.Functions.ILike(m.Personalnummer, muster));
        }

        var daten = await abfrage
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new
            {
                m.Id,
                m.Personalnummer,
                m.Nachname,
                m.Vorname,
                Abteilung = m.Mitgliedschaften
                    .Where(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
                    .OrderByDescending(s => s.GueltigAb)
                    .Select(s => s.Abteilung!.Bezeichnung)
                    .FirstOrDefault(),
                Stuecke = db.Ausstattungen.Where(a => a.MitgliedId == m.Id && a.ZurueckAm == null).Sum(a => (int?)a.Menge) ?? 0,
                Groessen = db.Groessenangaben.Any(g => g.MitgliedId == m.Id),
                OffeneQuittungen = db.Kleiderkammervorgaenge.Count(v => v.MitgliedId == m.Id && v.QuittiertAm == null),
            })
            .ToListAsync(ct);

        var ampeln = (await stand.PsaAsync(heute, ct))
            .Where(p => p.MitgliedId is not null)
            .GroupBy(p => p.MitgliedId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(p => p.Stand.Ampel));

        // Ehemalige nur, wenn sie noch Ausstattung haben oder ausdrücklich gewünscht.
        Zeilen = daten
            .Where(m => AuchEhemalige || m.Abteilung is not null || m.Stuecke > 0)
            .Select(m => new Zeile(m.Id, m.Personalnummer, $"{m.Nachname}, {m.Vorname}", m.Abteilung, m.Stuecke, m.Groessen,
                ampeln.TryGetValue(m.Id, out var ampel) ? ampel : null, m.OffeneQuittungen))
            .Where(z => !NurOffene || z.Offen)
            .ToList();
    }
}
