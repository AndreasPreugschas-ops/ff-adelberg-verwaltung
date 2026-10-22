using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

public class IndexModel(AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Suche { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Abteilung { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Personalnummer, string Name, int Alter, string? Abteilung, string? Status, string? Dienstgrad, string? Mobil);

    public async Task OnGetAsync(CancellationToken ct) => Zeilen = await LadenAsync(ct);

    /// <summary>HTMX: liefert nur die Tabelle für die Live-Suche.</summary>
    public async Task<PartialViewResult> OnGetTabelleAsync(CancellationToken ct)
    {
        Zeilen = await LadenAsync(ct);
        return Partial("_Tabelle", this);
    }

    private async Task<IReadOnlyList<Zeile>> LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var abfrage = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute);

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var muster = $"%{Suche.Trim()}%";
            abfrage = abfrage.Where(m =>
                EF.Functions.ILike(m.Nachname, muster)
                || EF.Functions.ILike(m.Vorname, muster)
                || EF.Functions.ILike(m.Personalnummer, muster));
        }

        if (!string.IsNullOrWhiteSpace(Abteilung))
        {
            abfrage = abfrage.Where(m => m.Mitgliedschaften.Any(s =>
                s.Abteilung!.Code == Abteilung && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)));
        }

        var daten = await abfrage
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new
            {
                m.Id,
                m.Personalnummer,
                m.Nachname,
                m.Vorname,
                m.Geburtsdatum,
                m.TelefonMobil,
                Mitgliedschaft = m.Mitgliedschaften
                    .Where(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
                    .OrderByDescending(s => s.GueltigAb)
                    .Select(s => new { s.Abteilung!.Bezeichnung, s.Status })
                    .FirstOrDefault(),
                Dienstgrad = m.Dienstgrade
                    .Where(d => d.Ab <= heute)
                    .OrderByDescending(d => d.Ab)
                    .Select(d => d.Dienstgrad!.Kurzbezeichnung)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return daten
            .Select(m => new Zeile(m.Id, m.Personalnummer, $"{m.Nachname}, {m.Vorname}", Mitglied.Alter(m.Geburtsdatum, heute),
                m.Mitgliedschaft?.Bezeichnung, m.Mitgliedschaft is null ? null : Anzeige.Text(m.Mitgliedschaft.Status), m.Dienstgrad, m.TelefonMobil))
            .ToList();
    }
}
