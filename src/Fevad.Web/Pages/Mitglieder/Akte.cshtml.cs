using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Übersicht der Mitgliederakte: alles zu einem Mitglied auf einen Blick, je Bereich nur mit dem passenden Recht.
/// Die Reiter führen zu den Seiten, auf denen die Daten gepflegt werden.
/// </summary>
public class AkteModel(
    AppDbContext db,
    Datenbereich datenbereich,
    Laufbahnabfrage laufbahn,
    Personalfristenabfrage fristen,
    ParameterLeser parameter,
    IAuthorizationService autorisierung,
    TimeProvider zeit) : PageModel
{
    /// <summary>Eckdaten der Jugendfeuerwehr; Eintritt null heißt: war nicht in der Jugend- oder Kinderfeuerwehr.</summary>
    public sealed record Jugendzeit(DateOnly? Eintritt, DateOnly? Austritt, Austrittsgrund? Grund, bool NochDabei, DateOnly? Leistungsspange);

    public Mitglied Mitglied { get; private set; } = null!;
    public DateOnly Heute { get; private set; }
    public Jugendzeit Jugend { get; private set; } = new(null, null, null, false, null);
    public IReadOnlyList<string> Gruppen { get; private set; } = [];
    public bool ArbeitgeberSichtbar { get; private set; }

    public Laufbahnstand? Laufbahn { get; private set; }
    public IReadOnlyList<Ehrung> Ehrungen { get; private set; } = [];

    /// <summary>Funktionen ohne Voraussetzung oder mit auslaufender Amtszeit; dieselbe Prüfung wie im Hintergrund.</summary>
    public IReadOnlyList<Pruefanlass> Funktionshinweise { get; private set; } = [];

    /// <summary>Erklärung bei Volljährigkeit fällig (wer minderjährig in die Einsatzabteilung kam).</summary>
    public Pruefanlass? Volljaehrigkeit { get; private set; }
    public IReadOnlyList<Fristeintrag> Fristen { get; private set; } = [];
    public IReadOnlyList<QualifikationNachweis> Qualifikationen { get; private set; } = [];
    public AtemschutzVorsorge? Vorsorge { get; private set; }
    public bool AtemschutzSichtbar { get; private set; }

    /// <summary>Anwesend bei Diensten im laufenden und im Vorjahr; null ohne Recht.</summary>
    public (int DiesesJahr, int Vorjahr)? Dienste { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var sichtbar = await datenbereich.Filtern(db.Mitglieder, User, Heute);
        var mitglied = await sichtbar.AsNoTracking()
            .Include(m => m.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .Include(m => m.Funktionen).ThenInclude(f => f.Funktion)
            .Include(m => m.Funktionen).ThenInclude(f => f.Abteilung)
            .Include(m => m.Dienstgrade).ThenInclude(d => d.Dienstgrad)
            .Include(m => m.Erziehungsberechtigte)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        Mitglied = mitglied;
        ArbeitgeberSichtbar = await DarfAsync(Berechtigungen.MitgliederArbeitgeber);
        Gruppen = await db.GruppenMitgliedschaften.AsNoTracking().Where(g => g.MitgliedId == id && g.Gruppe!.Aktiv)
            .Select(g => g.Gruppe!.Bezeichnung).ToListAsync(ct);

        var jugend = mitglied.Mitgliedschaften.Where(s => s.Abteilung!.Art is AbteilungArt.Jugend or AbteilungArt.Kinder).ToList();
        var spange = await db.QualifikationNachweise.AsNoTracking()
            .Where(n => n.MitgliedId == id && n.Qualifikation!.Code == Qualifikation.CodeLeistungsspange)
            .Select(n => (DateOnly?)n.ErworbenAm).MinAsync(ct);
        var letzte = jugend.OrderByDescending(s => s.GueltigBis ?? DateOnly.MaxValue).FirstOrDefault();
        Jugend = new Jugendzeit(
            jugend.Min(s => (DateOnly?)s.GueltigAb),
            letzte?.GueltigBis,
            letzte?.Austrittsgrund,
            jugend.Any(s => s.GueltigBis is null || s.GueltigBis >= Heute),
            spange);

        if (await DarfAsync(Berechtigungen.LaufbahnLesen))
        {
            Laufbahn = (await laufbahn.LadenAsync(Heute, [id], ct)).FirstOrDefault();
            Ehrungen = await db.Ehrungen.AsNoTracking().Include(e => e.Ehrungsart).Where(e => e.MitgliedId == id)
                .OrderByDescending(e => e.VerliehenAm).ToListAsync(ct);
            var vorhanden = await db.QualifikationNachweise.AsNoTracking()
                .Where(q => q.MitgliedId == id && q.ErworbenAm <= Heute && (q.GueltigBis == null || q.GueltigBis >= Heute))
                .Select(q => q.Qualifikation!.Code).ToListAsync(ct);
            Funktionshinweise = Funktionsanlaesse.Pruefen(
                Mitglied.Funktionen.Select(f => new LaufendeFunktion(
                    f.Id, f.Funktion!.Bezeichnung, f.Funktion.Voraussetzungen, f.Art, f.GueltigAb, f.GueltigBis, f.Funktion.AmtszeitJahre)),
                vorhanden.ToHashSet(StringComparer.OrdinalIgnoreCase), Heute, (await laufbahn.RegelnAsync(Heute, ct)).VorlaufMonate,
                await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Code, q => q.Bezeichnung, ct));
            if (Laufbahn is not null && !await db.Antraege.AnyAsync(
                    a => a.MitgliedId == id && a.Art == AntragArt.Volljaehrigkeit && a.Status == AntragStatus.Angenommen, ct))
            {
                Volljaehrigkeit = Personalpruefung.Volljaehrigkeitsanlass(Laufbahn, Heute,
                    await parameter.ZahlAsync(Fevad.Domain.Gemeinsam.ParameterSchluessel.VolljaehrigkeitAlter, Heute, 18, ct),
                    (await laufbahn.RegelnAsync(Heute, ct)).VorlaufMonate,
                    await parameter.ZahlAsync(Fevad.Domain.Gemeinsam.ParameterSchluessel.VolljaehrigkeitErinnernMonate, Heute, 12, ct));
            }
        }

        AtemschutzSichtbar = await DarfAsync(Berechtigungen.AtemschutzLesen);
        if (await DarfAsync(Berechtigungen.AusbildungLesen))
        {
            Fristen = (await fristen.LadenAsync(Heute, id, ct))
                .Where(f => AtemschutzSichtbar || !f.Gesundheitsbezogen)
                .OrderBy(f => f.Ampel).ThenBy(f => f.FaelligAm)
                .ToList();
            Qualifikationen = await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation)
                .Where(n => n.MitgliedId == id).OrderBy(n => n.Qualifikation!.Art).ThenBy(n => n.Qualifikation!.Sortierung).ToListAsync(ct);
        }

        if (AtemschutzSichtbar)
        {
            Vorsorge = await db.AtemschutzVorsorgen.AsNoTracking().Where(v => v.MitgliedId == id)
                .OrderByDescending(v => v.UntersuchtAm).FirstOrDefaultAsync(ct);
        }

        if ((await datenbereich.FreigabeAsync(User, Berechtigungen.AnwesenheitLesen)).Irgendwo)
        {
            // Ab dem 31.12. des Vorvorjahres laden und nach Ortszeit zählen, damit Silvesterdienste richtig zugeordnet sind.
            var ab = new DateTimeOffset(Heute.Year - 2, 12, 31, 0, 0, 0, TimeSpan.Zero);
            var jahre = (await db.Anwesenheiten.AsNoTracking()
                    .Where(a => a.MitgliedId == id && a.Status == AnwesenheitStatus.Anwesend && a.Dienst!.Beginn >= ab)
                    .Select(a => a.Dienst!.Beginn).ToListAsync(ct))
                .Select(b => Anzeige.LokalesDatum(b).Year).ToList();
            Dienste = (jahre.Count(j => j == Heute.Year), jahre.Count(j => j == Heute.Year - 1));
        }

        return Page();
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
