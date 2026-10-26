using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Werkstatt;

/// <summary>Werkstattaufträge (V61): was gerade in einer fremden Werkstatt ist und was zurückkam.</summary>
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Offene Aufträge zuerst, dann die zurückgebuchten der letzten Zeit.</summary>
    public IReadOnlyList<Zeile> Auftraege { get; private set; } = [];

    /// <summary>Abgeben und zurückbuchen darf, wer TechnikPruefen hat.</summary>
    public bool DarfPruefen { get; private set; }

    /// <summary>So viele zurückgebuchte Aufträge zeigt die Liste.</summary>
    public const int Zurueckgebucht = 30;

    /// <summary>Ein Auftrag in der Liste.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Nummer">Auftragsnummer.</param>
    /// <param name="Werkstatt">Rücklaufweg.</param>
    /// <param name="Werkstattname">Name der Werkstatt.</param>
    /// <param name="AbgegebenAm">Tag der Abgabe.</param>
    /// <param name="ZurueckAm">Tag der Rückgabe.</param>
    /// <param name="Anzahl">Anzahl der Geräte.</param>
    public sealed record Zeile(Guid Id, string Nummer, Ruecklauf Werkstatt, string Werkstattname, DateOnly AbgegebenAm, DateOnly? ZurueckAm, int Anzahl);

    /// <summary>Zeigt die Liste.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfPruefen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        var abfrage = db.Werkstattauftraege.AsNoTracking()
            .Select(w => new Zeile(w.Id, w.Nummer, w.Werkstatt, w.Werkstattname, w.AbgegebenAm, w.ZurueckAm, w.Positionen.Count));
        var offen = await abfrage.Where(w => w.ZurueckAm == null).OrderBy(w => w.AbgegebenAm).ThenBy(w => w.Nummer).ToListAsync(ct);
        var zurueck = await abfrage.Where(w => w.ZurueckAm != null).OrderByDescending(w => w.ZurueckAm).ThenByDescending(w => w.Nummer).Take(Zurueckgebucht).ToListAsync(ct);
        Auftraege = [.. offen, .. zurueck];
    }
}
