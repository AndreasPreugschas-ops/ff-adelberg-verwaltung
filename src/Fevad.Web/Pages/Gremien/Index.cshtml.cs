using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Übersicht der Sitzungen (Hauptversammlung, Feuerwehrausschuss) mit offenen Beschlüssen zur Wiedervorlage.</summary>
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Filter auf eine Sitzungsart; <c>null</c> zeigt alle.</summary>
    [BindProperty(SupportsGet = true)] public SitzungArt? Art { get; set; }

    /// <summary>Die Sitzungen, neueste zuerst.</summary>
    public IReadOnlyList<Zeile> Sitzungen { get; private set; } = [];
    /// <summary>Beschlüsse mit Umsetzungsfrist, die noch nicht erledigt sind.</summary>
    public IReadOnlyList<OffenerBeschluss> Wiedervorlagen { get; private set; } = [];
    /// <summary>Der Benutzer darf Sitzungen anlegen und bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit, für die Fristanzeige.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Eine Sitzung in der Liste.</summary>
    /// <param name="Id">Kennung der Sitzung.</param>
    /// <param name="Art">Art der Sitzung.</param>
    /// <param name="Titel">Titel der Sitzung.</param>
    /// <param name="Beginn">Beginn (UTC).</param>
    /// <param name="Status">Bearbeitungsstand der Sitzung.</param>
    /// <param name="Beschluesse">Anzahl der gefassten Beschlüsse.</param>
    /// <param name="Wahlen">Anzahl der durchgeführten Wahlen.</param>
    public sealed record Zeile(Guid Id, SitzungArt Art, string Titel, DateTimeOffset Beginn, SitzungStatus Status, int Beschluesse, int Wahlen);
    /// <summary>Ein noch nicht erledigter Beschluss mit Umsetzungsfrist.</summary>
    /// <param name="SitzungId">Sitzung, in der der Beschluss gefasst wurde.</param>
    /// <param name="Jahr">Jahr der Beschlussnummer.</param>
    /// <param name="Nummer">Laufende Beschlussnummer im Jahr.</param>
    /// <param name="Gegenstand">Text des Beschlusses.</param>
    /// <param name="UmsetzenBis">Frist für die Umsetzung.</param>
    public sealed record OffenerBeschluss(Guid SitzungId, int Jahr, int Nummer, string Gegenstand, DateOnly UmsetzenBis);

    /// <summary>Lädt Sitzungen und Wiedervorlagen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GremienBearbeiten)).Succeeded;

        var abfrage = db.Sitzungen.AsNoTracking();
        if (Art is { } art)
        {
            abfrage = abfrage.Where(s => s.Art == art);
        }

        Sitzungen = await abfrage.OrderByDescending(s => s.Beginn).Take(200)
            .Select(s => new Zeile(s.Id, s.Art, s.Titel, s.Beginn, s.Status, s.Beschluesse.Count, s.Wahlen.Count))
            .ToListAsync(ct);

        Heute = Anzeige.Heute(zeit);
        Wiedervorlagen = await db.Beschluesse.AsNoTracking()
            .Where(b => b.UmsetzenBis != null && b.ErledigtAm == null && b.Ergebnis == BeschlussErgebnis.Angenommen)
            .OrderBy(b => b.UmsetzenBis)
            .Select(b => new OffenerBeschluss(b.SitzungId, b.Jahr, b.Nummer, b.Gegenstand, b.UmsetzenBis!.Value))
            .ToListAsync(ct);
    }
}
