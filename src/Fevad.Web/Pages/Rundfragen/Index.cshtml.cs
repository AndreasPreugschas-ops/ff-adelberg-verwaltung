using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Mitteilungen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Rundfragen;

/// <summary>Rundfragen an mich und, für Verfassende, die eigenen Entwürfe und verschickten Rundfragen mit Rücklauf (V42).</summary>
public class IndexModel(AppDbContext db, Schreibrecht schreibrecht, IAktuellerBenutzer benutzer, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Rundfrage an mich.</summary>
    /// <param name="Id">Schlüssel.</param>
    /// <param name="Titel">Frage.</param>
    /// <param name="Verfasser">Verfasser.</param>
    /// <param name="Tag">Tag, um den es geht.</param>
    /// <param name="AntwortBis">Frist.</param>
    /// <param name="Beantwortet">Schon geantwortet.</param>
    /// <param name="Offen">Antworten noch möglich.</param>
    public sealed record AnMich(Guid Id, string Titel, string? Verfasser, DateOnly? Tag, DateOnly? AntwortBis, bool Beantwortet, bool Offen);

    /// <summary>Eine Rundfrage zur Verwaltung.</summary>
    /// <param name="Id">Schlüssel.</param>
    /// <param name="Titel">Frage.</param>
    /// <param name="Status">Stand.</param>
    /// <param name="Am">Verschickt oder angelegt am.</param>
    /// <param name="Empfaenger">Zahl der Empfänger.</param>
    /// <param name="Geantwortet">Zahl der Antwortenden.</param>
    public sealed record Verwaltet(Guid Id, string Titel, RundfrageStatus Status, DateTimeOffset Am, int Empfaenger, int Geantwortet);

    /// <summary>Rundfragen an mich.</summary>
    public IReadOnlyList<AnMich> Eingang { get; private set; } = [];
    /// <summary>Rundfragen, die ich verwalten darf.</summary>
    public IReadOnlyList<Verwaltet> Verfasst { get; private set; } = [];
    /// <summary>Darf Rundfragen anlegen.</summary>
    public bool Schreiben { get; private set; }

    /// <summary>Zeigt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        if (benutzer.Id is { } konto)
        {
            var eingang = await db.RundfrageEmpfaenger.AsNoTracking().Where(e => e.KontoId == konto)
                .Join(db.Rundfragen.Where(r => r.Status != RundfrageStatus.Entwurf), e => e.RundfrageId, r => r.Id, (e, r) => new { e, r })
                .OrderByDescending(x => x.r.VerschicktAm).Take(100)
                .ToListAsync(ct);
            Eingang = eingang.Select(x => new AnMich(x.r.Id, x.r.Titel, x.r.Verfasser, x.r.Tag, x.r.AntwortBis, x.e.BeantwortetAm is not null, x.r.AntwortenMoeglich(heute)))
                .ToList();
        }

        Schreiben = (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitteilungenVerfassen)).Succeeded;
        if (Schreiben)
        {
            var kandidaten = await db.Rundfragen.AsNoTracking().OrderByDescending(r => r.VerschicktAm ?? r.ErstelltAm).Take(100).ToListAsync(ct);
            var ids = kandidaten.Select(k => k.Id).ToList();
            var zaehler = await db.RundfrageEmpfaenger.Where(e => ids.Contains(e.RundfrageId)).GroupBy(e => e.RundfrageId)
                .Select(g => new { g.Key, Alle = g.Count(), Geantwortet = g.Count(e => e.BeantwortetAm != null) })
                .ToDictionaryAsync(g => g.Key, ct);
            var liste = new List<Verwaltet>();
            foreach (var r in kandidaten)
            {
                if (r.ErstelltVon == benutzer.Id || await schreibrecht.DarfAsync(User, r.Ziel, r.Abteilungen, r.JugendveranstaltungId, ct))
                {
                    var z = zaehler.GetValueOrDefault(r.Id);
                    liste.Add(new(r.Id, r.Titel, r.Status, r.VerschicktAm ?? r.ErstelltAm, z?.Alle ?? 0, z?.Geantwortet ?? 0));
                }
            }

            Verfasst = liste;
        }

        return Page();
    }
}
