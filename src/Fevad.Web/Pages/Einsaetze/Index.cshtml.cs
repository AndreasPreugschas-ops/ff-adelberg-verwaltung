using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>Einsatzberichte eines Jahres mit Hinweis auf nicht übernommene Alarme.</summary>
public class IndexModel(AppDbContext db, Einsatzberichte berichte, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Angezeigtes Einsatzjahr; ohne Angabe das jüngste Jahr mit Einsätzen.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    /// <summary>Wählbare Jahre vom ältesten Einsatz bis heute, jüngstes zuerst.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];
    /// <summary>Einsätze des angezeigten Jahres, jüngster zuerst.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Zahl der Alarme, aus denen noch kein Bericht angelegt wurde.</summary>
    public int UnverarbeiteteAlarme { get; private set; }
    /// <summary>Darf Berichte anlegen und bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Eine Zeile der Einsatzliste.</summary>
    /// <param name="Id">Kennung des Einsatzes.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    /// <param name="Alarmiert">Alarmzeit (UTC).</param>
    /// <param name="Stichwort">Einsatzstichwort.</param>
    /// <param name="Ort">Einsatzort.</param>
    /// <param name="Kategorie">Statistikkategorie.</param>
    /// <param name="Status">Bearbeitungsstand des Berichts.</param>
    /// <param name="Teilnehmer">Zahl der Teilnehmenden.</param>
    public sealed record Zeile(Guid Id, string Einsatznummer, DateTimeOffset Alarmiert, string? Stichwort, string? Ort, string? Kategorie, EinsatzStatus Status, int Teilnehmer);

    /// <summary>Lädt die Einsätze des gewählten Jahres.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded;

        var heute = Ortszeit.Datum(DateTimeOffset.UtcNow);
        var aeltester = await db.Einsaetze.AsNoTracking().OrderBy(e => e.Alarmiert).Select(e => (DateTimeOffset?)e.Alarmiert).FirstOrDefaultAsync(ct);
        var erstesJahr = aeltester is { } a ? Ortszeit.Datum(a).Year : heute.Year;
        Jahre = Enumerable.Range(erstesJahr, heute.Year - erstesJahr + 1).Reverse().ToList();
        Jahr = Jahr is { } j && Jahre.Contains(j) ? j : heute.Year;
        var von = Ortszeit.TagesbeginnUtc(new DateOnly(Jahr.Value, 1, 1));
        var bis = Ortszeit.TagesbeginnUtc(new DateOnly(Jahr.Value + 1, 1, 1));

        // Ein Jahr auf einmal: mit zehn Jahren Betrieb sind das rund 50 Zeilen, ohne dass ältere Berichte unerreichbar werden.
        Zeilen = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Alarmiert >= von && e.Alarmiert < bis)
            .OrderByDescending(e => e.Alarmiert)
            .Select(e => new Zeile(e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort, e.Ort, e.StatistikKategorie!.Bezeichnung, e.Status, e.Teilnahmen.Count))
            .ToListAsync(ct);

        UnverarbeiteteAlarme = await db.AlarmImporte.CountAsync(a => !a.Verarbeitet, ct);
    }

    /// <summary>Holt Entwürfe für Alarme nach, bei denen das Anlegen beim Empfang gescheitert ist.</summary>
    public async Task<IActionResult> OnPostAlarmeUebernehmenAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var anzahl = await berichte.OffeneAlarmeVerarbeitenAsync(ct);
        TempData["Meldung"] = $"{anzahl} Alarm(e) übernommen.";
        return RedirectToPage();
    }
}
