using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Dienste;
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
    public bool DarfMeldungenBearbeiten { get; private set; }

    public int Zusagen { get; private set; }
    public int Absagen { get; private set; }
    public IReadOnlyList<Meldung> Meldungen { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var karte = await lader.KarteAsync(id, ct);
        if (karte is null)
        {
            return NotFound();
        }

        Karte = karte;
        var dienst = karte.Dienst;
        HatMitglied = await lader.HatZielgruppeAsync(ct);
        DarfBearbeiten = await bereich.DarfDienstBearbeitenAsync(User, dienst.AbteilungIds, ct);
        DarfMeldungenBearbeiten = DarfBearbeiten || await bereich.IstUebungsleiterAsync(User, dienst, ct);
        DarfRueckmeldungenSehen = DarfMeldungenBearbeiten || await bereich.DarfDienstSehenAsync(User, dienst.AbteilungIds, ct);

        var antworten = await db.Rueckmeldungen.AsNoTracking().Where(r => r.DienstId == id).Select(r => r.Antwort).ToListAsync(ct);
        Zusagen = antworten.Count(a => a == RueckmeldungAntwort.Zusage);
        Absagen = antworten.Count(a => a == RueckmeldungAntwort.Absage);

        if (DarfRueckmeldungenSehen)
        {
            Meldungen = await Meldungsliste.LadenAsync(db, planung, bereich, User, dienst, ct);
        }

        return Page();
    }

    /// <summary>
    /// Zu- oder Absage des angemeldeten Mitglieds oder, mit <paramref name="mitgliedId"/>, für ein zugeordnetes Kind (Elternkonto).
    /// Per HTMX kommt die aktualisierte Karte zurück.
    /// </summary>
    public async Task<IActionResult> OnPostRueckmeldenAsync(Guid id, RueckmeldungAntwort antwort, string? begruendung, Guid? mitgliedId, CancellationToken ct)
    {
        var personen = await lader.PersonenAsync(ct);
        var person = mitgliedId is { } gewuenscht ? personen.FirstOrDefault(p => p.MitgliedId == gewuenscht) : personen.FirstOrDefault(p => p.Selbst);
        if (person is null)
        {
            return Forbid();
        }

        mitgliedId = person.MitgliedId;

        var ergebnis = await planung.RueckmeldenAsync(id, mitgliedId.Value, antwort, begruendung, ct);
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
