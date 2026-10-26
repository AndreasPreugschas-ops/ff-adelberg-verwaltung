using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Jahresausbildungsplan, Stundenkonten und Dienstbuch (V53). Grundlage sind die Dienste der Ausbildungsarten (Übung, Unterricht,
/// Unterweisung, Dienstsport) und ihre Anwesenheiten; Jugend- und Kinderfeuerwehr bleiben außen vor.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="parameter">Parameter für Sollstunden und Zeiträume.</param>
public sealed class Ausbildungsplaene(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Ein Dienst im Plan.</summary>
    /// <param name="Id">Kennung des Dienstes.</param>
    /// <param name="Beginn">Beginn.</param>
    /// <param name="Ende">Ende.</param>
    /// <param name="Art">Dienstart.</param>
    /// <param name="Titel">Titel.</param>
    /// <param name="Thema">Ausbildungsthema oder freies Thema.</param>
    /// <param name="Leiter">Übungsleiter.</param>
    /// <param name="Status">Durchführungsstand.</param>
    /// <param name="Anwesend">Zahl der Anwesenden.</param>
    /// <param name="Personenstunden">Stunden aller Anwesenden.</param>
    public sealed record Planzeile(Guid Id, DateTimeOffset Beginn, DateTimeOffset Ende, DienstArt Art, string Titel, string? Thema, string? Leiter,
        DienstStatus Status, int Anwesend, decimal Personenstunden);

    /// <summary>Stand eines Pflichtthemas.</summary>
    /// <param name="Thema">Das Thema.</param>
    /// <param name="Stand">Offen, geplant oder durchgeführt.</param>
    /// <param name="Anzahl">Nicht abgesagte Dienste mit dem Thema.</param>
    public sealed record Themenstand(Ausbildungsthema Thema, PlanStand Stand, int Anzahl);

    /// <summary>Stundenkonto einer Person.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">Nachname, Vorname.</param>
    /// <param name="Fortbildung">Stunden der Standortfortbildung im Jahr.</param>
    /// <param name="Truppmann2">Konto Truppmann Teil 2, nur nach Truppmann Teil 1 und vor Truppmann Teil 2.</param>
    public sealed record Konto(Guid MitgliedId, string Name, decimal Fortbildung, Ausbildungsplanung.Stundenkonto? Truppmann2);

    /// <summary>Plan eines Jahres.</summary>
    /// <param name="Dienste">Dienste der Ausbildungsarten in zeitlicher Reihenfolge, ohne abgesagte.</param>
    /// <param name="Pflichtthemen">Stand der Pflichtthemen.</param>
    public sealed record Plan(IReadOnlyList<Planzeile> Dienste, IReadOnlyList<Themenstand> Pflichtthemen);

    /// <summary>Lädt den Jahresausbildungsplan.</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Dienste und Stand der Pflichtthemen.</returns>
    public async Task<Plan> PlanAsync(int jahr, CancellationToken ct = default)
    {
        var zeilen = await DiensteAsync(jahr, nurDurchgefuehrt: false, ct);
        var themen = await db.Ausbildungsthemen.AsNoTracking().Where(t => t.Aktiv && t.Pflicht).OrderBy(t => t.Sortierung).ToListAsync(ct);
        var jeThema = await db.Dienste.AsNoTracking()
            .Where(d => d.AusbildungsthemaId != null && d.Beginn >= Jahresbeginn(jahr) && d.Beginn < Jahresbeginn(jahr + 1))
            .Select(d => new { d.AusbildungsthemaId, d.Status }).ToListAsync(ct);
        var stand = themen.Select(t =>
        {
            var status = jeThema.Where(d => d.AusbildungsthemaId == t.Id).Select(d => d.Status).ToList();
            return new Themenstand(t, Ausbildungsplanung.Stand(status), status.Count(s => s != DienstStatus.Abgesagt));
        }).ToList();
        return new(zeilen, stand);
    }

    /// <summary>Durchgeführte Ausbildungsdienste eines Jahres für das Dienstbuch.</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Dienste in zeitlicher Reihenfolge.</returns>
    public Task<IReadOnlyList<Planzeile>> DienstbuchAsync(int jahr, CancellationToken ct = default) => DiensteAsync(jahr, nurDurchgefuehrt: true, ct);

    /// <summary>
    /// Stundenkonten der Einsatzabteilung am Stichtag: Standortfortbildung im Jahr (Dienste, die für die Fortbildung zählen) und
    /// Truppmann Teil 2 für alle mit Truppmann Teil 1, aber ohne Truppmann Teil 2.
    /// </summary>
    /// <param name="stichtag">Stichtag; das Jahr des Stichtags zählt für die Fortbildung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Je Person ein Konto, nach Namen sortiert.</returns>
    public async Task<IReadOnlyList<Konto>> StundenkontenAsync(DateOnly stichtag, CancellationToken ct = default)
    {
        var soll = await parameter.ZahlAsync(ParameterSchluessel.Truppmann2Stunden, stichtag, 80, ct);
        var monate = await parameter.ZahlAsync(ParameterSchluessel.Truppmann2Monate, stichtag, 24, ct);
        var personen = await db.Mitgliedschaften.AsNoTracking()
            .Where(m => m.Abteilung!.Art == AbteilungArt.Einsatz && m.GueltigAb <= stichtag && (m.GueltigBis == null || m.GueltigBis >= stichtag)
                && m.Status != MitgliedStatus.Ausgeschieden)
            .Select(m => new { m.MitgliedId, m.Mitglied!.Nachname, m.Mitglied.Vorname }).Distinct().ToListAsync(ct);
        var ids = personen.Select(p => p.MitgliedId).ToList();
        var lehrgaenge = await db.QualifikationNachweise.AsNoTracking()
            .Where(n => ids.Contains(n.MitgliedId) && (n.Qualifikation!.Code == "TM1" || n.Qualifikation.Code == "TM2"))
            .Select(n => new { n.MitgliedId, n.Qualifikation!.Code, n.ErworbenAm }).ToListAsync(ct);

        var fruehestens = lehrgaenge.Where(l => l.Code == "TM1").Select(l => l.ErworbenAm).DefaultIfEmpty(new DateOnly(stichtag.Year, 1, 1)).Min();
        var von = Jahresbeginn(Math.Min(fruehestens.Year, stichtag.Year));
        var bis = Ortszeit.TagesbeginnUtc(stichtag.AddDays(1));
        var anwesenheiten = await db.Anwesenheiten.AsNoTracking()
            .Where(a => ids.Contains(a.MitgliedId) && a.Status == AnwesenheitStatus.Anwesend && a.Dienst!.ZaehltFuerFortbildung
                && a.Dienst.Status == DienstStatus.Durchgefuehrt && a.Dienst.Beginn >= von && a.Dienst.Beginn < bis)
            .Select(a => new { a.MitgliedId, Von = a.Von ?? a.Dienst!.Beginn, Bis = a.Bis ?? a.Dienst!.Ende, a.Dienst!.Beginn })
            .ToListAsync(ct);
        var jeMitglied = anwesenheiten.ToLookup(a => a.MitgliedId,
            a => (Datum: Ortszeit.Datum(a.Beginn), Stunden: a.Bis > a.Von ? (decimal)(a.Bis - a.Von).TotalHours : 0m));

        return personen.OrderBy(p => p.Nachname).ThenBy(p => p.Vorname).Select(p =>
        {
            var eigene = jeMitglied[p.MitgliedId].ToList();
            var fortbildung = Math.Round(eigene.Where(e => e.Datum.Year == stichtag.Year).Sum(e => e.Stunden), 1);
            var tm1 = lehrgaenge.Where(l => l.MitgliedId == p.MitgliedId && l.Code == "TM1").Select(l => (DateOnly?)l.ErworbenAm).Min();
            var hatTm2 = lehrgaenge.Any(l => l.MitgliedId == p.MitgliedId && l.Code == "TM2");
            var konto = tm1 is { } t && !hatTm2 ? Ausbildungsplanung.Truppmann2(t, eigene, soll, monate) : null;
            return new Konto(p.MitgliedId, $"{p.Nachname}, {p.Vorname}", fortbildung, konto);
        }).ToList();
    }

    private static DateTimeOffset Jahresbeginn(int jahr) => Ortszeit.TagesbeginnUtc(new DateOnly(jahr, 1, 1));

    private async Task<IReadOnlyList<Planzeile>> DiensteAsync(int jahr, bool nurDurchgefuehrt, CancellationToken ct)
    {
        var arten = Ausbildungsplanung.Ausbildungsarten.ToList();
        var von = Jahresbeginn(jahr);
        var bis = Jahresbeginn(jahr + 1);
        var dienste = await db.Dienste.AsNoTracking()
            .Where(d => arten.Contains(d.Art) && d.Beginn >= von && d.Beginn < bis && d.Status != DienstStatus.Abgesagt
                && (!nurDurchgefuehrt || d.Status == DienstStatus.Durchgefuehrt)
                && !d.Abteilungen.Any(a => a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder))
            .OrderBy(d => d.Beginn)
            .Select(d => new
            {
                d.Id, d.Beginn, d.Ende, d.Art, d.Titel, d.Status,
                Thema = d.Ausbildungsthema != null ? d.Ausbildungsthema.Bezeichnung : d.Thema,
                Leiter = d.Verantwortlicher != null ? d.Verantwortlicher.Vorname + " " + d.Verantwortlicher.Nachname : null,
                Anwesend = d.Anwesenheiten.Where(a => a.Status == AnwesenheitStatus.Anwesend).Select(a => new { Von = a.Von ?? d.Beginn, Bis = a.Bis ?? d.Ende }).ToList(),
            })
            .ToListAsync(ct);
        return dienste.Select(d => new Planzeile(d.Id, d.Beginn, d.Ende, d.Art, d.Titel, d.Thema, d.Leiter, d.Status, d.Anwesend.Count,
            Math.Round(d.Anwesend.Sum(a => a.Bis > a.Von ? (decimal)(a.Bis - a.Von).TotalHours : 0m), 1))).ToList();
    }
}
