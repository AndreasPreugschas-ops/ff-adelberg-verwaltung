using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Wirtschaftsplan einer Kasse (V01): Ansätze nach der LFV-Gliederung, Plan und Ist mit Vorjahr, Deckung, Ausgleich und der Weg vom
/// Entwurf über den Ausschussbeschluss zur Zustimmung des Bürgermeisters.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class PlanModel(AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Position im Plan.</summary>
    /// <param name="Position">Die Position.</param>
    /// <param name="Ansatz">Geplanter Betrag.</param>
    /// <param name="Erlaeuterung">Erläuterung zum Ansatz.</param>
    /// <param name="Ist">Ist des Jahres.</param>
    /// <param name="IstVorjahr">Ist des Vorjahres.</param>
    /// <param name="Verfuegbar">Bei Ausgaben: noch verfügbar unter Berücksichtigung der Deckungsfähigkeit.</param>
    public sealed record Zeile(Planposition Position, decimal Ansatz, string? Erlaeuterung, decimal Ist, decimal IstVorjahr, decimal? Verfuegbar);

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Haushaltsjahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Der Plan; null, solange er nicht aufgestellt ist.</summary>
    public Wirtschaftsplan? Plan { get; private set; }

    /// <summary>Positionen mit Plan und Ist.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Bestand aller Konten zu Jahresbeginn.</summary>
    public decimal Anfangsbestand { get; private set; }

    /// <summary>Der Benutzer darf den Plan bearbeiten.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Beschlüsse für den Verweis.</summary>
    public IReadOnlyList<SelectListItem> Beschluesse { get; private set; } = [];

    /// <summary>Ansätze je Position (Eingabe).</summary>
    [BindProperty] public Dictionary<Guid, decimal?> Betraege { get; set; } = [];

    /// <summary>Erläuterungen je Position (Eingabe).</summary>
    [BindProperty] public Dictionary<Guid, string?> Erlaeuterungen { get; set; } = [];

    /// <summary>Neuer Stand des Plans.</summary>
    [BindProperty] public WirtschaftsplanStatus NeuerStatus { get; set; }

    /// <summary>Tag des Beschlusses oder der Zustimmung.</summary>
    [BindProperty] public DateOnly? StatusDatum { get; set; }

    /// <summary>Beschluss des Ausschusses.</summary>
    [BindProperty] public Guid? BeschlussId { get; set; }

    /// <summary>Bemerkung, z. B. Aktenzeichen der Zustimmung.</summary>
    [BindProperty] public string? Bemerkung { get; set; }

    /// <summary>Zeigt den Plan.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) => await LadenAsync(kasse, ct) ? Page() : NotFound();

    /// <summary>Stellt den Plan als Entwurf auf, mit den Ansätzen des Vorjahres.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostAnlegenAsync(Guid kasse, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseBuchen, kasse, ct) is null)
        {
            return Forbid();
        }

        await kassen.PlanAnlegenAsync(kasse, Jahr, ct);
        TempData["Meldung"] = $"Wirtschaftsplan {Jahr} als Entwurf aufgestellt.";
        return RedirectToPage(new { kasse, Jahr });
    }

    /// <summary>Speichert die Ansätze eines änderbaren Plans.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAnsaetzeAsync(Guid kasse, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseBuchen, kasse, ct) is null)
        {
            return Forbid();
        }

        var plan = await db.Wirtschaftsplaene.Include(w => w.Ansaetze).FirstOrDefaultAsync(w => w.KasseId == kasse && w.Jahr == Jahr, ct);
        if (plan is null)
        {
            return NotFound();
        }

        if (!plan.Aenderbar)
        {
            TempData["Meldung"] = "Der Plan ist beschlossen. Für Änderungen zuerst einen Nachtrag öffnen.";
            return RedirectToPage(new { kasse, Jahr });
        }

        var positionen = await db.Planpositionen.Where(p => p.KasseId == kasse).Select(p => p.Id).ToListAsync(ct);
        foreach (var (positionId, betrag) in Betraege.Where(b => positionen.Contains(b.Key)))
        {
            var ansatz = plan.Ansaetze.FirstOrDefault(a => a.PlanpositionId == positionId);
            if (ansatz is null)
            {
                ansatz = new Planansatz { PlanpositionId = positionId };
                plan.Ansaetze.Add(ansatz);
            }

            ansatz.Betrag = Math.Max(0, Math.Round(betrag ?? 0, 2));
            ansatz.Erlaeuterung = Erlaeuterungen.GetValueOrDefault(positionId) is { } e && !string.IsNullOrWhiteSpace(e) ? e.Trim() : null;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Ansätze gespeichert.";
        return RedirectToPage(new { kasse, Jahr });
    }

    /// <summary>Setzt den Stand des Plans: beschlossen, zugestimmt, verweigert oder zurück zum Entwurf (Nachtrag).</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostStatusAsync(Guid kasse, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseBuchen, kasse, ct) is null)
        {
            return Forbid();
        }

        var plan = await db.Wirtschaftsplaene.FirstOrDefaultAsync(w => w.KasseId == kasse && w.Jahr == Jahr, ct);
        if (plan is null)
        {
            return NotFound();
        }

        var tag = StatusDatum ?? Anzeige.Heute(zeit);
        switch (NeuerStatus)
        {
            case WirtschaftsplanStatus.Beschlossen:
                plan.BeschlussId = BeschlussId is { } b && await db.Beschluesse.AnyAsync(x => x.Id == b, ct) ? b : null;
                plan.BeschlossenAm = tag;
                plan.ZustimmungAm = null;
                break;
            case WirtschaftsplanStatus.Zugestimmt or WirtschaftsplanStatus.Verweigert:
                if (plan.Status != WirtschaftsplanStatus.Beschlossen)
                {
                    TempData["Meldung"] = "Der Bürgermeister stimmt einem vom Ausschuss beschlossenen Plan zu.";
                    return RedirectToPage(new { kasse, Jahr });
                }

                plan.ZustimmungAm = tag;
                break;
            case WirtschaftsplanStatus.Entwurf:
                if (plan.Status != WirtschaftsplanStatus.Entwurf)
                {
                    plan.Nachtrag++;
                }

                plan.ZustimmungAm = null;
                break;
        }

        plan.Status = NeuerStatus;
        if (!string.IsNullOrWhiteSpace(Bemerkung))
        {
            plan.Bemerkung = Bemerkung.Trim();
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Wirtschaftsplan {Jahr}: {Anzeige.Text(NeuerStatus)}.";
        return RedirectToPage(new { kasse, Jahr });
    }

    private async Task<bool> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return false;
        }

        Kasse = kasse;
        var heute = Anzeige.Heute(zeit);
        if (Jahr == 0)
        {
            Jahr = heute.Year;
        }

        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        Plan = await db.Wirtschaftsplaene.AsNoTracking().Include(w => w.Ansaetze).Include(w => w.Beschluss)
            .FirstOrDefaultAsync(w => w.KasseId == kasseId && w.Jahr == Jahr, ct);
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasseId).ToListAsync(ct);
        var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId && (b.Jahr == Jahr || b.Jahr == Jahr - 1)).ToListAsync(ct);
        var ist = Kassenregeln.IstJePosition(buchungen.Where(b => b.Jahr == Jahr));
        var istVorjahr = Kassenregeln.IstJePosition(buchungen.Where(b => b.Jahr == Jahr - 1));
        var ansaetze = Plan?.Ansaetze.ToDictionary(a => a.PlanpositionId, a => a.Betrag) ?? new Dictionary<Guid, decimal>();
        Zeilen = Sichtbare(positionen, ansaetze, ist, istVorjahr)
            .Select(p => new Zeile(p, ansaetze.GetValueOrDefault(p.Id), Plan?.Ansaetze.FirstOrDefault(a => a.PlanpositionId == p.Id)?.Erlaeuterung,
                ist.GetValueOrDefault(p.Id), istVorjahr.GetValueOrDefault(p.Id),
                p.Richtung == Planrichtung.Ausgabe && Plan is not null ? Kassenregeln.Verfuegbar(p, positionen, ansaetze, ist) : null))
            .ToList();
        Anfangsbestand = (await kassen.KontostaendeAsync(kasseId, new DateOnly(Jahr, 1, 1).AddDays(-1), ct)).Sum(k => k.Bestand);
        Beschluesse = await Kassenauswahl.BeschluesseAsync(db, heute, Plan?.BeschlussId, ct);
        return true;

        static IEnumerable<Planposition> Sichtbare(
            List<Planposition> alle, Dictionary<Guid, decimal> ansaetze, IReadOnlyDictionary<Guid, decimal> ist, IReadOnlyDictionary<Guid, decimal> vorjahr) =>
            alle.Where(p => p.Aktiv || ansaetze.ContainsKey(p.Id) || ist.ContainsKey(p.Id) || vorjahr.ContainsKey(p.Id))
                .OrderBy(p => p.Richtung).ThenBy(p => p.Nummer, StringComparer.Ordinal);
    }
}
