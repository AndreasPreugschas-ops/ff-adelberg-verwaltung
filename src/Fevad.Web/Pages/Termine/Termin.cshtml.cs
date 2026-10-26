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
public class TerminModel(AppDbContext db, Dienstplanung planung, Terminlader lader, Datenbereich bereich, Jugendveranstaltungen jugend, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Kind des Elternkontos in der Zielgruppe einer Jugendveranstaltung.</summary>
    /// <param name="MitgliedId">Das Kind.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Anmeldung">Die Anmeldung, falls schon angemeldet.</param>
    public sealed record Kind(Guid MitgliedId, string Vorname, Veranstaltungsanmeldung? Anmeldung);

    /// <summary>Angaben zur Jugendveranstaltung (V48), falls der Termin eine ist.</summary>
    public Jugendveranstaltung? Jugendveranstaltung { get; private set; }
    /// <summary>Kinder des Elternkontos, die zur Zielgruppe gehören.</summary>
    public IReadOnlyList<Kind> Kinder { get; private set; } = [];
    /// <summary>Eltern können noch selbst an- oder abmelden.</summary>
    public bool AnmeldungOffen { get; private set; }

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

        await JugendLadenAsync(id, ct);
        return Page();
    }

    /// <summary>Eltern melden ihr Kind zu einer Jugendveranstaltung an und willigen in die Teilnahme ein (V48).</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="einwilligung">Die Eltern willigen ein.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAnmeldenAsync(Guid id, Guid mitgliedId, bool einwilligung, CancellationToken ct)
    {
        if (!await JugendLadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Kinder.All(k => k.MitgliedId != mitgliedId))
        {
            return Forbid();
        }

        var ergebnis = await jugend.AnmeldenAsync(Jugendveranstaltung!, mitgliedId, Anmeldeweg.Elternkonto, User.Identity?.Name, einwilligung, null, Anzeige.Heute(zeit), ct);
        switch (ergebnis)
        {
            case Jugendveranstaltungen.Anmeldeergebnis.Geschlossen:
                TempData["Fehler"] = "Die Anmeldung ist geschlossen. Bitte beim Jugendwart melden.";
                break;
            case Jugendveranstaltungen.Anmeldeergebnis.EinwilligungFehlt:
                TempData["Fehler"] = "Bitte bestätigen, dass ihr in die Teilnahme einwilligt.";
                break;
            case Jugendveranstaltungen.Anmeldeergebnis.NichtZielgruppe:
                return Forbid();
            default:
                TempData["Meldung"] = "Angemeldet.";
                break;
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Eltern nehmen die Anmeldung ihres Kindes bis zum Anmeldeschluss zurück.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAbmeldenAsync(Guid id, Guid mitgliedId, CancellationToken ct)
    {
        if (!await JugendLadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Kinder.All(k => k.MitgliedId != mitgliedId))
        {
            return Forbid();
        }

        if (!AnmeldungOffen)
        {
            TempData["Fehler"] = "Der Anmeldeschluss ist vorbei. Bitte beim Jugendwart abmelden.";
        }
        else if (await jugend.AbmeldenAsync(Jugendveranstaltung!, mitgliedId, ct))
        {
            TempData["Meldung"] = "Abgemeldet.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Lädt die Jugendveranstaltung und die Kinder des Elternkontos in ihrer Zielgruppe.</summary>
    private async Task<bool> JugendLadenAsync(Guid id, CancellationToken ct)
    {
        Jugendveranstaltung = await jugend.LadenAsync(id, ct);
        if (Jugendveranstaltung is not { Dienst: { } dienst } v)
        {
            return false;
        }

        var tag = Jugendveranstaltungen.ErsterTag(dienst);
        AnmeldungOffen = v.AnmeldungOffen(Anzeige.Heute(zeit), tag);
        var kinder = new List<Kind>();
        foreach (var person in (await lader.PersonenAsync(ct)).Where(p => !p.Selbst))
        {
            if ((await planung.ProfilAsync(person.MitgliedId, ct)).Betrifft(dienst, tag))
            {
                kinder.Add(new Kind(person.MitgliedId, person.Vorname, v.Anmeldungen.FirstOrDefault(a => a.MitgliedId == person.MitgliedId)));
            }
        }

        Kinder = kinder;
        return true;
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
