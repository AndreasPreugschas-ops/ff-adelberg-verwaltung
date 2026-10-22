using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Termine;

/// <summary>Ein Termin: Details, eigene Zu-/Absage und für Führungskräfte die Übersicht der Rückmeldungen.</summary>
public class TerminModel(AppDbContext db, Dienstplanung planung, Terminlader lader, Datenbereich bereich) : PageModel
{
    public TerminKarte Karte { get; private set; } = null!;
    public bool HatMitglied { get; private set; }
    public bool DarfRueckmeldungenSehen { get; private set; }
    public bool DarfBearbeiten { get; private set; }

    public int Zusagen { get; private set; }
    public int Absagen { get; private set; }
    public IReadOnlyList<Person> Zugesagt { get; private set; } = [];
    public IReadOnlyList<Person> Abgesagt { get; private set; } = [];
    public IReadOnlyList<Person> Offen { get; private set; } = [];

    public sealed record Person(string Name, string? Abteilung, string? Begruendung, DateTimeOffset? GemeldetAm);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var karte = await lader.KarteAsync(id, ct);
        if (karte is null)
        {
            return NotFound();
        }

        Karte = karte;
        HatMitglied = await lader.MitgliedIdAsync(ct) is not null;
        DarfRueckmeldungenSehen = await bereich.DarfDienstSehenAsync(User, karte.Dienst.AbteilungId, ct);
        DarfBearbeiten = await bereich.DarfDienstBearbeitenAsync(User, karte.Dienst.AbteilungId, ct);

        var rueckmeldungen = await db.Rueckmeldungen.AsNoTracking()
            .Where(r => r.DienstId == id)
            .Select(r => new { r.MitgliedId, r.Antwort, r.Begruendung, r.GemeldetAm, r.Mitglied!.Nachname, r.Mitglied.Vorname })
            .ToListAsync(ct);
        Zusagen = rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Zusage);
        Absagen = rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Absage);

        if (DarfRueckmeldungenSehen)
        {
            var kreis = await planung.TeilnehmerkreisAsync(karte.Dienst, ct);
            var abteilung = kreis.ToDictionary(t => t.MitgliedId, t => t.Abteilung);
            Zugesagt = rueckmeldungen.Where(r => r.Antwort == RueckmeldungAntwort.Zusage).OrderBy(r => r.Nachname).ThenBy(r => r.Vorname)
                .Select(r => new Person($"{r.Nachname}, {r.Vorname}", abteilung.GetValueOrDefault(r.MitgliedId), null, r.GemeldetAm)).ToList();
            Abgesagt = rueckmeldungen.Where(r => r.Antwort == RueckmeldungAntwort.Absage).OrderBy(r => r.Nachname).ThenBy(r => r.Vorname)
                .Select(r => new Person($"{r.Nachname}, {r.Vorname}", abteilung.GetValueOrDefault(r.MitgliedId), r.Begruendung, r.GemeldetAm)).ToList();
            var gemeldet = rueckmeldungen.Select(r => r.MitgliedId).ToHashSet();
            Offen = kreis.Where(t => !gemeldet.Contains(t.MitgliedId))
                .Select(t => new Person($"{t.Nachname}, {t.Vorname}", t.Abteilung, null, null)).ToList();
        }

        return Page();
    }

    /// <summary>Zu- oder Absage des angemeldeten Mitglieds. Per HTMX kommt die aktualisierte Karte zurück.</summary>
    public async Task<IActionResult> OnPostRueckmeldenAsync(Guid id, RueckmeldungAntwort antwort, string? begruendung, CancellationToken ct)
    {
        if (await lader.MitgliedIdAsync(ct) is not { } mitgliedId)
        {
            return Forbid();
        }

        var ergebnis = await planung.RueckmeldenAsync(id, mitgliedId, antwort, begruendung, ct);
        switch (ergebnis)
        {
            case Dienstplanung.RueckmeldungErgebnis.NichtGefunden:
                return NotFound();
            case Dienstplanung.RueckmeldungErgebnis.NichtZielgruppe:
                return Forbid();
        }

        var istHtmx = Request.Headers.ContainsKey("HX-Request");
        if (ergebnis == Dienstplanung.RueckmeldungErgebnis.Geschlossen && !istHtmx)
        {
            TempData["Fehler"] = "Für diesen Termin ist keine Rückmeldung mehr möglich.";
        }

        if (istHtmx)
        {
            return Partial("_TerminKarte", (await lader.KarteAsync(id, ct))!);
        }

        return RedirectToPage(new { id });
    }
}
