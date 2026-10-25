using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// „Meine Akte“ (V38): die eigene Akte lesbar statt als Rohtabellen. Mitgliedschaft, Dienstgrad, Funktionen, Lehrgänge und Fristen
/// mit Ampel, Ehrungen, Schutzausrüstung, Teilnahme und Stunden je Jahr. Atemschutz erscheint nur als Frist, ohne Befunde.
/// </summary>
public class AkteModel(
    AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, Personalfristenabfrage fristen, Kleiderkammerstand kleiderkammer,
    TimeProvider zeit) : PageModel
{
    /// <summary>Teilnahme eines Kalenderjahres.</summary>
    /// <param name="Jahr">Kalenderjahr.</param>
    /// <param name="Anwesend">Dienste, bei denen das Mitglied anwesend war.</param>
    /// <param name="Entschuldigt">Entschuldigt gefehlt.</param>
    /// <param name="Unentschuldigt">Unentschuldigt gefehlt.</param>
    /// <param name="Stunden">Anwesende Stunden bei Diensten.</param>
    /// <param name="FortbildungStunden">Davon Stunden, die für die Standortfortbildung zählen.</param>
    /// <param name="Einsaetze">Zahl der Einsätze.</param>
    /// <param name="EinsatzStunden">Stunden im Einsatz, soweit erfasst.</param>
    public sealed record Jahreszeile(int Jahr, int Anwesend, int Entschuldigt, int Unentschuldigt, decimal Stunden, decimal FortbildungStunden,
        int Einsaetze, decimal EinsatzStunden);

    /// <summary>Ein Stück Schutzausrüstung beim Mitglied.</summary>
    /// <param name="Artikel">Bezeichnung des Artikels.</param>
    /// <param name="Groesse">Größe.</param>
    /// <param name="Menge">Menge.</param>
    /// <param name="AusgegebenAm">Tag der Ausgabe.</param>
    /// <param name="NaechstePruefung">Nächste Prüfung bei Einzelstücken.</param>
    /// <param name="Ampel">Ampel aus Prüfung und Lebensdauer; leer bei Mengenartikeln.</param>
    public sealed record Ausruestung(string Artikel, string Groesse, int Menge, DateOnly AusgegebenAm, DateOnly? NaechstePruefung, Fristampel? Ampel);

    /// <summary>Das eigene Mitglied mit Mitgliedschaften, Dienstgraden und Funktionen.</summary>
    public Mitglied Mitglied { get; private set; } = null!;
    /// <summary>Heutiger Tag in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Aktueller Dienstgrad, falls vergeben.</summary>
    public DienstgradZuordnung? Dienstgrad { get; private set; }
    /// <summary>Eigene Fristen mit Ampel, rote zuerst.</summary>
    public IReadOnlyList<Fristeintrag> Fristen { get; private set; } = [];
    /// <summary>Erworbene Qualifikationen, neueste zuerst.</summary>
    public IReadOnlyList<QualifikationNachweis> Qualifikationen { get; private set; } = [];
    /// <summary>Lehrgänge, die noch laufen oder auf die das Mitglied wartet.</summary>
    public IReadOnlyList<Lehrgangsteilnahme> OffeneLehrgaenge { get; private set; } = [];
    /// <summary>Verliehene Ehrungen, neueste zuerst.</summary>
    public IReadOnlyList<(string Bezeichnung, DateOnly VerliehenAm)> Ehrungen { get; private set; } = [];
    /// <summary>Ausgegebene Schutzausrüstung.</summary>
    public IReadOnlyList<Ausruestung> Ausstattung { get; private set; } = [];
    /// <summary>Teilnahme je Jahr, neuestes zuerst.</summary>
    public IReadOnlyList<Jahreszeile> Jahre { get; private set; } = [];

    /// <summary>Zeigt die Akte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } id)
        {
            return NotFound();
        }

        Heute = Anzeige.Heute(zeit);
        var mitglied = await db.Mitglieder.AsNoTracking()
            .Include(m => m.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .Include(m => m.Funktionen).ThenInclude(f => f.Funktion)
            .Include(m => m.Dienstgrade).ThenInclude(d => d.Dienstgrad)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        Mitglied = mitglied;
        Dienstgrad = mitglied.Dienstgrade.Where(d => d.Ab <= Heute).OrderByDescending(d => d.Ab).FirstOrDefault();
        Fristen = (await fristen.LadenAsync(Heute, id, ct)).OrderByDescending(f => f.Ampel).ThenBy(f => f.FaelligAm).ToList();
        Qualifikationen = await db.QualifikationNachweise.AsNoTracking().Include(q => q.Qualifikation)
            .Where(q => q.MitgliedId == id).OrderByDescending(q => q.ErworbenAm).ToListAsync(ct);
        OffeneLehrgaenge = await db.Lehrgangsteilnahmen.AsNoTracking().Include(l => l.Qualifikation)
            .Where(l => l.MitgliedId == id && !l.Abgeschlossen).OrderBy(l => l.Von).ToListAsync(ct);
        Ehrungen = (await db.Ehrungen.AsNoTracking().Where(e => e.MitgliedId == id && e.VerliehenAm != null)
                .OrderByDescending(e => e.VerliehenAm).Select(e => new { e.Ehrungsart!.Bezeichnung, e.VerliehenAm }).ToListAsync(ct))
            .Select(e => (e.Bezeichnung, e.VerliehenAm!.Value)).ToList();

        var psa = (await kleiderkammer.PsaAsync(Heute, ct)).Where(p => p.MitgliedId == id).ToDictionary(p => p.Stand.Id);
        Ausstattung = (await db.Ausstattungen.AsNoTracking()
                .Where(a => a.MitgliedId == id && a.ZurueckAm == null)
                .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.AusgegebenAm)
                .Select(a => new { Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.AusgegebenAm, a.GeraetId })
                .ToListAsync(ct))
            .Select(a =>
            {
                var p = a.GeraetId is { } g ? psa.GetValueOrDefault(g) : null;
                return new Ausruestung(a.Artikel, a.Groesse, a.Menge, a.AusgegebenAm, p?.Stand.NaechsterTermin, p?.Stand.Ampel);
            })
            .ToList();

        Jahre = await JahreAsync(id, ct);
        return Page();
    }

    /// <summary>Teilnahme je Jahr aus den Anwesenheiten und den schon verdichteten Jahressummen (L5), dazu die Einsätze.</summary>
    private async Task<IReadOnlyList<Jahreszeile>> JahreAsync(Guid id, CancellationToken ct)
    {
        var anwesenheiten = await db.Anwesenheiten.AsNoTracking().Where(a => a.MitgliedId == id)
            .Select(a => new { a.Status, a.Von, a.Bis, a.Dienst!.Beginn, a.Dienst.Ende, a.Dienst.ZaehltFuerFortbildung })
            .ToListAsync(ct);
        var werte = anwesenheiten.Select(a => new Anwesenheitsjahressumme.Einzelwert(
            id, Ortszeit.Datum(a.Beginn).Year, a.Status,
            a.Status == AnwesenheitStatus.Anwesend ? (a.Bis ?? a.Ende) - (a.Von ?? a.Beginn) : TimeSpan.Zero, a.ZaehltFuerFortbildung));
        var verdichtet = await db.Anwesenheitsjahressummen.AsNoTracking().Where(s => s.MitgliedId == id).ToListAsync(ct);
        var dienste = Anwesenheitsjahressumme.Verdichten(werte, verdichtet).ToDictionary(s => s.Jahr);

        var einsaetze = (await db.EinsatzTeilnahmen.AsNoTracking().Where(t => t.MitgliedId == id)
                .Select(t => new { t.Einsatz!.Alarmiert, t.Einsatz.Einsatzende, t.Von, t.Bis })
                .ToListAsync(ct))
            .GroupBy(t => Ortszeit.Datum(t.Alarmiert).Year)
            .ToDictionary(g => g.Key, g => (Anzahl: g.Count(), Stunden: g.Sum(t =>
                (t.Bis ?? t.Einsatzende) is { } bis ? (decimal)Math.Max(0, (bis - (t.Von ?? t.Alarmiert)).TotalHours) : 0m)));

        return dienste.Keys.Union(einsaetze.Keys).OrderDescending()
            .Select(jahr =>
            {
                var d = dienste.GetValueOrDefault(jahr);
                var e = einsaetze.GetValueOrDefault(jahr);
                return new Jahreszeile(jahr, d?.Anwesend ?? 0, d?.Entschuldigt ?? 0, d?.Unentschuldigt ?? 0,
                    Math.Round((d?.Minuten ?? 0) / 60m, 1), Math.Round((d?.FortbildungMinuten ?? 0) / 60m, 1), e.Anzahl, Math.Round(e.Stunden, 1));
            })
            .ToList();
    }
}
