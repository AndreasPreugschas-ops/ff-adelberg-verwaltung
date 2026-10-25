using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Unfaelle;

/// <summary>Dienstunfälle eines Jahres mit Anzeigefrist. Zeigt nur Name, Datum, Anlass und Status, keine Verletzungsangaben.</summary>
[Authorize(Policy = Berechtigungen.UnfaelleLesen)]
public class IndexModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    public sealed record Zeile(Guid Id, DateTimeOffset Zeitpunkt, string Name, UnfallAnlass Anlass, UnfallStatus Status, DateOnly? AnzeigeBis, Fristampel Ampel);

    /// <summary>Angezeigtes Jahr; ohne Angabe das laufende.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    public IReadOnlyList<int> Jahre { get; private set; } = [];
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Offene, anzeigepflichtige Unfälle aller Jahre, damit nichts über den Jahreswechsel verloren geht.</summary>
    public IReadOnlyList<Zeile> Offen { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var frist = await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, heute, 3, ct);
        var aeltester = await db.Unfaelle.MinAsync(u => (DateTimeOffset?)u.Zeitpunkt, ct);
        var erstesJahr = aeltester is { } a ? Anzeige.LokalesDatum(a).Year : heute.Year;
        Jahre = Enumerable.Range(erstesJahr, heute.Year - erstesJahr + 1).Reverse().ToList();
        Jahr = Jahr is { } j && Jahre.Contains(j) ? j : heute.Year;
        var (von, bis) = Anzeige.Jahr(Jahr.Value);

        var unfaelle = await db.Unfaelle.AsNoTracking().Include(u => u.Mitglied)
            .Where(u => (u.Zeitpunkt >= von && u.Zeitpunkt < bis) || u.Status == UnfallStatus.Erfasst)
            .OrderByDescending(u => u.Zeitpunkt).ToListAsync(ct);
        Zeile Als(Unfall u) => new(u.Id, u.Zeitpunkt, $"{u.Mitglied!.Nachname}, {u.Mitglied.Vorname}", u.Anlass, u.Status, u.AnzeigeBis(frist), u.AnzeigeAmpel(heute, frist));
        Zeilen = unfaelle.Where(u => u.Zeitpunkt >= von && u.Zeitpunkt < bis).Select(Als).ToList();
        Offen = unfaelle.Where(u => u.Status == UnfallStatus.Erfasst && u.Anzeigepflichtig && (u.Zeitpunkt < von || u.Zeitpunkt >= bis)).Select(Als).ToList();
    }
}
