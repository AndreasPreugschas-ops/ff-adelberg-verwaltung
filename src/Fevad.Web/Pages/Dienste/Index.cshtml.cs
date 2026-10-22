using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Dienstplan: Übungen und Dienste planen (einmalig oder als Serie) und kommende sowie vergangene Dienste.</summary>
public class IndexModel(AppDbContext db, Dienstplanung planung, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    public IReadOnlyList<Zeile> Kommende { get; private set; } = [];
    public IReadOnlyList<Zeile> Vergangene { get; private set; } = [];
    public Zielgruppenlisten Listen { get; private set; } = null!;

    [BindProperty]
    public Planungsformular Neu { get; set; } = new();

    [BindProperty]
    public Wiederholung? Wiederholung { get; set; }

    [BindProperty]
    public DateOnly? WiederholenBis { get; set; }

    public sealed record Zeile(Dienst Dienst, int Anwesend, int Zusagen, int Absagen);

    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        Neu.ZeitenLesen(ModelState, nameof(Neu), out var beginn, out var ende);
        if (Wiederholung is not null)
        {
            if (WiederholenBis is null)
            {
                ModelState.AddModelError(nameof(WiederholenBis), "Bis wann soll wiederholt werden?");
            }
            else if (Neu.Datum is { } datum && WiederholenBis < datum)
            {
                ModelState.AddModelError(nameof(WiederholenBis), "Das Enddatum liegt vor dem ersten Termin.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var dienst = new Dienst { Titel = "", Status = DienstStatus.Geplant };
        Neu.Uebertragen(dienst, beginn, ende);

        if (Wiederholung is { } rhythmus)
        {
            var spaetestens = await planung.SerieSpaetestensAsync(Neu.Datum!.Value, ct);
            var termine = await planung.SerieAnlegenAsync(dienst, rhythmus, WiederholenBis!.Value, ct);
            TempData["Meldung"] = WiederholenBis > spaetestens
                ? $"{termine.Count} Termine angelegt, bis {Anzeige.Datum(spaetestens)} (längere Serien bitte später verlängern)."
                : $"{termine.Count} Termine angelegt.";
            return RedirectToPage();
        }

        db.Dienste.Add(dienst);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Dienst angelegt.";
        return RedirectToPage("/Termine/Termin", new { id = dienst.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Listen = await Zielgruppenlisten.LadenAsync(db, ct);
        var basis = db.Dienste.AsNoTracking()
            .Include(d => d.Abteilung).Include(d => d.Gruppe).Include(d => d.Qualifikation);

        // Heute laufende Dienste zählen noch als kommend, damit die Anwesenheit leicht zu finden ist.
        var heute = Anzeige.AlsUtc(Anzeige.Heute(zeit), TimeOnly.MinValue);
        Kommende = await Zeilen(basis.Where(d => d.Beginn >= heute).OrderBy(d => d.Beginn).Take(50), ct);
        Vergangene = await Zeilen(basis.Where(d => d.Beginn < heute).OrderByDescending(d => d.Beginn).Take(50), ct);
    }

    private static Task<List<Zeile>> Zeilen(IQueryable<Dienst> abfrage, CancellationToken ct) =>
        abfrage.Select(d => new Zeile(d,
                d.Anwesenheiten.Count(a => a.Status == AnwesenheitStatus.Anwesend),
                d.Rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Zusage),
                d.Rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Absage)))
            .ToListAsync(ct);
}
