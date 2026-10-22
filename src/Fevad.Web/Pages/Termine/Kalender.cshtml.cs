using System.Globalization;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Termine;

/// <summary>Monatskalender. Mitglieder sehen ihre Zielgruppe, Führungskräfte auf Wunsch alle Termine.</summary>
public class KalenderModel(Terminlader lader, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    public DateOnly Erster { get; private set; }
    public DateOnly Heute { get; private set; }
    public bool DarfAlleSehen { get; private set; }
    public bool HatMitglied { get; private set; }

    /// <summary>Volle Wochen von Montag bis Sonntag, die den Monat abdecken.</summary>
    public IReadOnlyList<DateOnly> Tage { get; private set; } = [];
    public ILookup<DateOnly, TerminKarte> Termine { get; private set; } = Array.Empty<TerminKarte>().ToLookup(t => default(DateOnly));

    public string MonatText(DateOnly tag) => tag.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Erster = DateOnly.TryParseExact(Monat, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monat)
            ? monat
            : new DateOnly(Heute.Year, Heute.Month, 1);

        HatMitglied = await lader.MitgliedIdAsync(ct) is not null;
        DarfAlleSehen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AnwesenheitLesen)).Succeeded;
        var alle = !HatMitglied || (Alle && DarfAlleSehen);
        Alle = alle && HatMitglied;

        var start = Erster.AddDays(-(((int)Erster.DayOfWeek + 6) % 7));
        var letzter = Erster.AddMonths(1).AddDays(-1);
        var ende = letzter.AddDays(6 - (((int)letzter.DayOfWeek + 6) % 7));
        Tage = Enumerable.Range(0, ende.DayNumber - start.DayNumber + 1).Select(start.AddDays).ToList();

        var karten = await lader.KartenAsync(Anzeige.AlsUtc(start, TimeOnly.MinValue), Anzeige.AlsUtc(ende.AddDays(1), TimeOnly.MinValue), alle, ct);
        Termine = karten.ToLookup(k => Anzeige.LokalesDatum(k.Dienst.Beginn));
    }
}
