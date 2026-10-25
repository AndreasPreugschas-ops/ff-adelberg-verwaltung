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
    /// <summary>Angezeigter Monat als „JJJJ-MM“; ohne Angabe der aktuelle.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    /// <summary>Alle Termine statt nur der eigenen Zielgruppe zeigen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    /// <summary>Erster Tag des angezeigten Monats.</summary>
    public DateOnly Erster { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Der Benutzer darf alle Termine sehen.</summary>
    public bool DarfAlleSehen { get; private set; }
    /// <summary>Das Konto ist mit einem Mitglied oder Kindern einer Zielgruppe verknüpft.</summary>
    public bool HatMitglied { get; private set; }

    /// <summary>Volle Wochen von Montag bis Sonntag, die den Monat abdecken.</summary>
    public IReadOnlyList<DateOnly> Tage { get; private set; } = [];
    /// <summary>Termine je Kalendertag.</summary>
    public ILookup<DateOnly, TerminKarte> Termine { get; private set; } = Array.Empty<TerminKarte>().ToLookup(t => default(DateOnly));

    /// <summary>Monatsangabe für Links im Format „JJJJ-MM“.</summary>
    /// <param name="tag">Ein Tag im gewünschten Monat.</param>
    /// <returns>Der Monat als Text.</returns>
    public string MonatText(DateOnly tag) => tag.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Lädt die Termine des Monats.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Erster = DateOnly.TryParseExact(Monat, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monat)
            ? monat
            : new DateOnly(Heute.Year, Heute.Month, 1);

        HatMitglied = await lader.HatZielgruppeAsync(ct);
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
