using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Personalübersicht, strikt getrennt: Feuerwehr (Einsatz-, Alters- und weitere Abteilungen) und Jugendfeuerwehr samt
/// Kinderfeuerwehr. Den Reiter Jugendfeuerwehr sieht nur, wer Zugang zu ihr hat.
/// </summary>
public class IndexModel(AppDbContext db, Datenbereich datenbereich, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Reiter Jugendfeuerwehr statt Feuerwehr.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Jugend { get; set; }

    public bool JugendSichtbar { get; private set; }
    public bool FeuerwehrSichtbar { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Suche { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Abteilung { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<(string Code, string Bezeichnung)> Abteilungen { get; private set; } = [];

    public sealed record Zeile(Guid Id, string Personalnummer, string Name, int Alter, string? Abteilung, string? Status, string? Dienstgrad, string? Mobil);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await BereichPruefenAsync())
        {
            return Forbid();
        }

        var jugend = Jugend;
        Abteilungen = (await db.Abteilungen.AsNoTracking()
                .Where(a => a.Aktiv && (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder) == jugend)
                .OrderBy(a => a.Sortierung)
                .Select(a => new { a.Code, a.Bezeichnung }).ToListAsync(ct))
            .Select(a => (a.Code, a.Bezeichnung)).ToList();
        Zeilen = await LadenAsync(ct);
        return Page();
    }

    /// <summary>HTMX: liefert nur die Tabelle für die Live-Suche.</summary>
    public async Task<IActionResult> OnGetTabelleAsync(CancellationToken ct)
    {
        if (!await BereichPruefenAsync())
        {
            return Forbid();
        }

        Zeilen = await LadenAsync(ct);
        return Partial("_Tabelle", this);
    }

    /// <summary>Wer nur die Jugendfeuerwehr sieht (Jugendwart), landet dort; ohne Zugang kein Reiter Jugendfeuerwehr.</summary>
    private async Task<bool> BereichPruefenAsync()
    {
        JugendSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendLesen)).Succeeded;
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.MitgliederLesen);
        var jugendabteilungen = await db.Abteilungen.Where(a => a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder).Select(a => a.Id).ToListAsync();
        FeuerwehrSichtbar = freigabe.Alle || freigabe.OhneJugend is not null || freigabe.Abteilungen.Except(jugendabteilungen).Any();
        if (!FeuerwehrSichtbar)
        {
            Jugend = true;
        }

        return !Jugend || JugendSichtbar;
    }

    private async Task<IReadOnlyList<Zeile>> LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var sichtbar = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute);
        var abfrage = Jugend ? Datenbereich.NurJugend(sichtbar, heute) : Datenbereich.OhneJugendpersonal(sichtbar, heute);

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
