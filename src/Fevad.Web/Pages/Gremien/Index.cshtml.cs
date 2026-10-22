using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

public class IndexModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    [BindProperty(SupportsGet = true)] public SitzungArt? Art { get; set; }

    public IReadOnlyList<Zeile> Sitzungen { get; private set; } = [];
    public IReadOnlyList<OffenerBeschluss> Wiedervorlagen { get; private set; } = [];
    public bool DarfBearbeiten { get; private set; }
    public DateOnly Heute { get; private set; }

    public sealed record Zeile(Guid Id, SitzungArt Art, string Titel, DateTimeOffset Beginn, SitzungStatus Status, int Beschluesse, int Wahlen);
    public sealed record OffenerBeschluss(Guid SitzungId, int Jahr, int Nummer, string Gegenstand, DateOnly UmsetzenBis);

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
