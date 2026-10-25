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
    /// <summary>Der Termin aus Sicht des Kontos.</summary>
    public TerminKarte Karte { get; private set; } = null!;
    /// <summary>Das Konto ist mit einem Mitglied oder Kindern einer Zielgruppe verknüpft.</summary>
    public bool HatMitglied { get; private set; }
    /// <summary>Der Benutzer darf die Rückmeldungen aller sehen.</summary>
    public bool DarfRueckmeldungenSehen { get; private set; }
    /// <summary>Der Benutzer darf den Dienst bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Der Benutzer darf die Meldungen anderer ändern.</summary>
    public bool DarfMeldungenBearbeiten { get; private set; }

    /// <summary>Anzahl der Zusagen.</summary>
    public int Zusagen { get; private set; }
    /// <summary>Anzahl der Absagen.</summary>
    public int Absagen { get; private set; }
    /// <summary>Die einzelnen Meldungen, falls sichtbar.</summary>
    public IReadOnlyList<Meldung> Meldungen { get; private set; } = [];

    /// <summary>Zeigt den Termin.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
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
