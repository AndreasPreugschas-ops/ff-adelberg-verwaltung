using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>
/// Entschädigungsanträge der Mitglieder prüfen (V32): Lehrgänge und Einsätze über mehr als zwei Tage. Wer abrechnet, sieht die Angaben,
/// die Belege und die Berechnung nach den heutigen Sätzen, genehmigt oder lehnt mit Begründung ab. Genehmigte Anträge gehen in die
/// nächste Abrechnung ein. Die Gemeinde sieht nur die Summen in der Abrechnung, nicht die Anträge mit Verdienstangaben.
/// </summary>
[Authorize(Policy = Berechtigungen.EntschaedigungAbrechnen)]
public class AntraegeModel(AppDbContext db, Abrechnungen abrechnungen, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Antrag mit Berechnung.</summary>
    /// <param name="Antrag">Der Antrag mit Mitglied und Tagen.</param>
    /// <param name="Berechnung">Berechnung nach den heutigen Sätzen.</param>
    /// <param name="Belege">Belege ohne Inhalt.</param>
    /// <param name="Spaet">Der Antrag kam später als die Antragsfrist.</param>
    /// <param name="Abgerechnet">Der Antrag steht in einer abgeschlossenen Abrechnung.</param>
    public sealed record Eintrag(Entschaedigungsantrag Antrag, Antragsberechnung.Ergebnis Berechnung, IReadOnlyList<(Guid Id, string Dateiname)> Belege, bool Spaet, bool Abgerechnet);

    /// <summary>Offene Anträge, älteste zuerst.</summary>
    public IReadOnlyList<Eintrag> Offen { get; private set; } = [];

    /// <summary>Zuletzt entschiedene Anträge.</summary>
    public IReadOnlyList<Eintrag> Entschieden { get; private set; } = [];

    /// <summary>Zeigt offene und zuletzt entschiedene Anträge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var antraege = await db.Entschaedigungsantraege.AsNoTracking().Include(a => a.Tage).Include(a => a.Mitglied)
            .Where(a => a.Stand == AntragStand.Eingereicht || a.Stand == AntragStand.Genehmigt || a.Stand == AntragStand.Abgelehnt)
            .OrderByDescending(a => a.EingereichtAm).Take(200)
            .ToListAsync(ct);
        var ids = antraege.Select(a => a.Id).ToList();
        var belege = await db.AntragBelege.AsNoTracking().Where(b => ids.Contains(b.EntschaedigungsantragId))
            .Select(b => new { b.Id, b.EntschaedigungsantragId, b.Dateiname }).ToListAsync(ct);
        var abgerechnet = (await db.AbrechnungsPosten.AsNoTracking()
            .Where(p => p.EntschaedigungsantragId != null && ids.Contains(p.EntschaedigungsantragId.Value) && p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen)
            .Select(p => p.EntschaedigungsantragId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        var saetze = await abrechnungen.SaetzeAsync(ct);
        var heute = Anzeige.Heute(zeit);
        var frist = (int)(saetze.Zahl(Entschaedigungssaetze.AntragsfristMonate, heute) ?? 0);
        var eintraege = antraege.Select(a => new Eintrag(
            a,
            Antragsberechnung.Berechnen(a, saetze),
            belege.Where(b => b.EntschaedigungsantragId == a.Id).Select(b => (b.Id, b.Dateiname)).ToList(),
            frist > 0 && a.ErsterTag is { } erster && a.EingereichtAm > erster.AddMonths(frist),
            abgerechnet.Contains(a.Id))).ToList();
        Offen = eintraege.Where(e => e.Antrag.Stand == AntragStand.Eingereicht).OrderBy(e => e.Antrag.EingereichtAm).ToList();
        Entschieden = eintraege.Where(e => e.Antrag.Stand != AntragStand.Eingereicht).Take(50).ToList();
    }

    /// <summary>Genehmigt einen eingereichten Antrag.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="vermerk">Optionaler Hinweis.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public Task<IActionResult> OnPostGenehmigenAsync(Guid id, string? vermerk, CancellationToken ct) =>
        EntscheidenAsync(id, AntragStand.Genehmigt, vermerk, ct);

    /// <summary>Lehnt einen eingereichten Antrag mit Begründung ab.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="vermerk">Begründung; Pflicht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostAblehnenAsync(Guid id, string? vermerk, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vermerk))
        {
            TempData["Fehler"] = "Bitte die Ablehnung begründen; das Mitglied sieht die Begründung.";
            return RedirectToPage();
        }

        return await EntscheidenAsync(id, AntragStand.Abgelehnt, vermerk, ct);
    }

    /// <summary>Setzt eine Entscheidung zurück, solange der Antrag in keiner abgeschlossenen Abrechnung steht.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZuruecksetzenAsync(Guid id, CancellationToken ct)
    {
        var antrag = await db.Entschaedigungsantraege.FirstOrDefaultAsync(a => a.Id == id && (a.Stand == AntragStand.Genehmigt || a.Stand == AntragStand.Abgelehnt), ct);
        if (antrag is null)
        {
            return NotFound();
        }

        if (await db.AbrechnungsPosten.AnyAsync(p => p.EntschaedigungsantragId == id && p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen, ct))
        {
            TempData["Fehler"] = "Der Antrag ist schon abgerechnet und kann nicht mehr zurückgesetzt werden.";
            return RedirectToPage();
        }

        (antrag.Stand, antrag.EntschiedenAm, antrag.EntschiedenVon, antrag.Entscheidungsvermerk) = (AntragStand.Eingereicht, null, null, null);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entscheidung zurückgesetzt. Offene Abrechnungsentwürfe bitte neu berechnen.";
        return RedirectToPage();
    }

    /// <summary>Liefert einen Beleg aus.</summary>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetBelegAsync(Guid belegId, CancellationToken ct)
    {
        var beleg = await db.AntragBelege.AsNoTracking().FirstOrDefaultAsync(b => b.Id == belegId, ct);
        return beleg is null ? NotFound() : File(beleg.Inhalt, beleg.Inhaltstyp, beleg.Dateiname);
    }

    private async Task<IActionResult> EntscheidenAsync(Guid id, AntragStand stand, string? vermerk, CancellationToken ct)
    {
        var antrag = await db.Entschaedigungsantraege.FirstOrDefaultAsync(a => a.Id == id && a.Stand == AntragStand.Eingereicht, ct);
        if (antrag is null)
        {
            return NotFound();
        }

        antrag.Stand = stand;
        antrag.EntschiedenAm = Anzeige.Heute(zeit);
        antrag.EntschiedenVon = benutzer.Id;
        antrag.Entscheidungsvermerk = string.IsNullOrWhiteSpace(vermerk) ? null : vermerk.Trim()[..Math.Min(vermerk.Trim().Length, 500)];
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = stand == AntragStand.Genehmigt
            ? $"Genehmigt: {antrag.Bezeichnung}. Der Antrag geht in die nächste Abrechnung ein."
            : $"Abgelehnt: {antrag.Bezeichnung}.";
        return RedirectToPage();
    }
}
