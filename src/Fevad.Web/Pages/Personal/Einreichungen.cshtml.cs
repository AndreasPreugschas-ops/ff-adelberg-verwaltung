using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Von Mitgliedern eingereichte Nachweise prüfen (V44): Datei ansehen, übernehmen oder mit Antwort ablehnen. Übernehmen trägt einen
/// wiederkehrenden Nachweis bzw. (mit Ausbildung.Bearbeiten) die Qualifikation in die Akte ein; die Datei wird mit jeder Entscheidung gelöscht.
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungNachweise)]
public class EinreichungenModel(AppDbContext db, Benachrichtigungsdienst benachrichtigungen, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Einreichungen, älteste zuerst, danach die der letzten 90 Tage.</summary>
    public IReadOnlyList<Nachweiseinreichung> Einreichungen { get; private set; } = [];

    /// <summary>Darf Lehrgänge und Qualifikationen eintragen.</summary>
    public bool DarfQualifikation { get; private set; }

    /// <summary>Zeigt die Einreichungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        DarfQualifikation = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungBearbeiten)).Succeeded;
        var seit = zeit.GetUtcNow().AddDays(-90);
        Einreichungen = await db.Nachweiseinreichungen.AsNoTracking()
            .Include(e => e.Mitglied).Include(e => e.Nachweisart).Include(e => e.Qualifikation)
            .Where(e => e.Status == EinreichungStatus.Eingereicht || e.GeprueftAm >= seit)
            .OrderBy(e => e.Status).ThenBy(e => e.ErstelltAm)
            .Select(e => new Nachweiseinreichung
            {
                Id = e.Id, MitgliedId = e.MitgliedId, Mitglied = e.Mitglied, NachweisartId = e.NachweisartId, Nachweisart = e.Nachweisart,
                QualifikationId = e.QualifikationId, Qualifikation = e.Qualifikation, Bezeichnung = e.Bezeichnung, Datum = e.Datum, GueltigBis = e.GueltigBis,
                Aussteller = e.Aussteller, Bemerkung = e.Bemerkung, Dateiname = e.Dateiname, Status = e.Status, GeprueftAm = e.GeprueftAm, Antwort = e.Antwort,
                ErstelltAm = e.ErstelltAm,
            })
            .ToListAsync(ct);
    }

    /// <summary>Liefert die Datei einer offenen Einreichung.</summary>
    /// <param name="id">Die Einreichung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetDateiAsync(Guid id, CancellationToken ct)
    {
        var e = await db.Nachweiseinreichungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Inhalt != null, ct);
        return e is null ? NotFound() : File(e.Inhalt!, e.Inhaltstyp ?? "application/octet-stream", e.Dateiname);
    }

    /// <summary>Übernimmt eine Einreichung in die Akte oder lehnt sie ab; die Datei wird gelöscht und das Mitglied benachrichtigt.</summary>
    /// <param name="id">Die Einreichung.</param>
    /// <param name="uebernehmen">Übernehmen statt ablehnen.</param>
    /// <param name="antwort">Antwort an das Mitglied.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, bool uebernehmen, string? antwort, CancellationToken ct)
    {
        var e = await db.Nachweiseinreichungen.Include(x => x.Nachweisart).Include(x => x.Qualifikation)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == EinreichungStatus.Eingereicht, ct);
        if (e is null)
        {
            return NotFound();
        }

        var text = string.IsNullOrWhiteSpace(antwort) ? null : antwort.Trim()[..Math.Min(antwort.Trim().Length, 500)];
        string meldung;
        if (!uebernehmen)
        {
            meldung = "Einreichung abgelehnt.";
        }
        else if (e.Nachweisart is { } art)
        {
            db.Nachweise.Add(new Nachweis
            {
                MitgliedId = e.MitgliedId, NachweisartId = art.Id, Datum = e.Datum, GueltigBis = e.GueltigBis ?? Nachweis.GueltigkeitAus(e.Datum, art),
                DurchgefuehrtVon = e.Aussteller, Bemerkung = "Vom Mitglied eingereicht und geprüft",
            });
            meldung = $"{art.Bezeichnung} in die Akte übernommen.";
        }
        else if (e.Qualifikation is { } q)
        {
            if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungBearbeiten)).Succeeded)
            {
                return Forbid();
            }

            var nachweis = new QualifikationNachweis
            {
                MitgliedId = e.MitgliedId, QualifikationId = q.Id, ErworbenAm = e.Datum, GueltigBis = e.GueltigBis, Lehrgangsort = e.Aussteller,
                Bemerkung = "Vom Mitglied eingereicht und geprüft",
            };
            nachweis.GueltigkeitVorbelegen(q);
            db.QualifikationNachweise.Add(nachweis);
            meldung = $"{q.Bezeichnung} in die Akte übernommen.";
        }
        else
        {
            meldung = "Als geprüft vermerkt. Bitte den Nachweis von Hand in der Akte eintragen.";
        }

        e.Status = uebernehmen ? EinreichungStatus.Uebernommen : EinreichungStatus.Abgelehnt;
        e.GeprueftAm = zeit.GetUtcNow();
        e.Antwort = text;
        (e.Dateiname, e.Inhaltstyp, e.Inhalt) = (null, null, null);
        await db.SaveChangesAsync(ct);
        await benachrichtigungen.AnMitgliederAsync([e.MitgliedId],
            new(Benachrichtigungsart.Hinweis, $"Nachweis {e.Anzeigename}: {Anzeige.Text(e.Status)}", "/Konto/Nachweise", e.Id, text), ct);
        TempData["Meldung"] = meldung;
        return RedirectToPage();
    }
}
