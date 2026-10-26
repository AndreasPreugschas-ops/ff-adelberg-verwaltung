using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Unterweisungen mit Pflichtthemen (V53, F22): trägt beim Abschluss die Nachweise der Anwesenden ein und prüft, welche Pflichtthemen im
/// Kalenderjahr noch fehlen.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
public sealed class Unterweisungen(AppDbContext db)
{
    /// <summary>Zeile der Deckungsprüfung je Thema.</summary>
    /// <param name="Thema">Das Thema.</param>
    /// <param name="Ergebnis">Zielgruppe, Unterwiesene und offene Personen.</param>
    /// <param name="Offen">Namen der offenen Personen (Nachname, Vorname), sortiert.</param>
    /// <param name="Termine">Unterweisungen des Jahres mit diesem Thema (geplant oder durchgeführt).</param>
    public sealed record Deckungszeile(Unterweisungsthema Thema, Unterweisungsdeckung.Ergebnis Ergebnis, IReadOnlyList<string> Offen, int Termine);

    /// <summary>Ergebnis des Eintragens.</summary>
    /// <param name="Nachweise">Neu eingetragene wiederkehrende Nachweise.</param>
    /// <param name="Atemschutz">Neu angelegte Atemschutz-Unterweisungen (warten auf Bestätigung).</param>
    public sealed record Eintragung(int Nachweise, int Atemschutz);

    /// <summary>
    /// Trägt für eine durchgeführte Unterweisung je anwesender Person und Thema die verknüpfte Nachweisart ein und legt für anwesende Inhaber
    /// der Zielgruppen-Qualifikation eines Atemschutz-Themas eine Atemschutz-Unterweisung an. Wiederholbar: vorhandene Einträge bleiben.
    /// </summary>
    /// <param name="dienstId">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Anzahl der neuen Einträge; gespeichert ist bereits.</returns>
    public async Task<Eintragung> NachweiseEintragenAsync(Guid dienstId, CancellationToken ct = default)
    {
        var dienst = await db.Dienste.AsNoTracking()
            .Include(d => d.Unterweisungsthemen).ThenInclude(t => t.Nachweisart)
            .Include(d => d.Anwesenheiten)
            .FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        if (dienst is null || dienst.Art != DienstArt.Unterweisung || dienst.Status != DienstStatus.Durchgefuehrt)
        {
            return new(0, 0);
        }

        var tag = Ortszeit.Datum(dienst.Beginn);
        var anwesend = dienst.Anwesenheiten.Where(a => a.Status == AnwesenheitStatus.Anwesend).Select(a => a.MitgliedId).Distinct().ToList();
        var qualifiziert = await QualifiziertAsync(dienst.Unterweisungsthemen, anwesend, tag, ct);
        var nachweise = 0;
        var atemschutz = 0;
        foreach (var thema in dienst.Unterweisungsthemen)
        {
            var personen = thema.QualifikationId is { } q ? anwesend.Where(m => qualifiziert.Contains((m, q))).ToList() : anwesend;
            if (thema.Nachweisart is { } art)
            {
                var vorhanden = await db.Nachweise.Where(n => n.NachweisartId == art.Id && n.Datum == tag && personen.Contains(n.MitgliedId))
                    .Select(n => n.MitgliedId).ToListAsync(ct);
                foreach (var mitglied in personen.Except(vorhanden))
                {
                    db.Nachweise.Add(new Nachweis
                    {
                        MitgliedId = mitglied, NachweisartId = art.Id, Datum = tag, GueltigBis = Nachweis.GueltigkeitAus(tag, art),
                        DurchgefuehrtVon = dienst.Titel, Bemerkung = $"Unterweisung „{thema.Bezeichnung}“",
                    });
                    nachweise++;
                }
            }

            if (thema.Atemschutzunterweisung)
            {
                var vorhanden = await db.AtemschutzNachweise
                    .Where(n => n.DienstId == dienst.Id && n.Art == AtemschutzNachweisArt.Unterweisung && personen.Contains(n.MitgliedId))
                    .Select(n => n.MitgliedId).ToListAsync(ct);
                foreach (var mitglied in personen.Except(vorhanden))
                {
                    db.AtemschutzNachweise.Add(new AtemschutzNachweis
                    {
                        MitgliedId = mitglied, Datum = tag, Art = AtemschutzNachweisArt.Unterweisung, DienstId = dienst.Id, Ort = dienst.Ort,
                        Taetigkeit = dienst.Titel,
                    });
                    atemschutz++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new(nachweise, atemschutz);
    }

    /// <summary>
    /// Deckungsprüfung der aktiven Pflichtthemen für ein Kalenderjahr. Zielgruppe sind die Angehörigen der Einsatzabteilung am Stichtag,
    /// bei Themen mit Qualifikation nur deren Inhaber; gezählt wird die Anwesenheit bei durchgeführten Unterweisungen des Jahres.
    /// </summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="stichtag">Stichtag der Zielgruppe, z. B. heute oder der 31.12. eines vergangenen Jahres.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Je Pflichtthema eine Zeile in Katalogreihenfolge.</returns>
    public async Task<IReadOnlyList<Deckungszeile>> DeckungAsync(int jahr, DateOnly stichtag, CancellationToken ct = default)
    {
        var themen = await db.Unterweisungsthemen.AsNoTracking().Include(t => t.Qualifikation)
            .Where(t => t.Aktiv && t.Pflicht).OrderBy(t => t.Sortierung).ThenBy(t => t.Bezeichnung).ToListAsync(ct);
        var einsatzkraefte = await db.Mitgliedschaften.AsNoTracking()
            .Where(m => m.Abteilung!.Art == AbteilungArt.Einsatz && m.GueltigAb <= stichtag && (m.GueltigBis == null || m.GueltigBis >= stichtag)
                && m.Status != MitgliedStatus.Ausgeschieden)
            .Select(m => m.MitgliedId).Distinct().ToListAsync(ct);
        var qualifiziert = await QualifiziertAsync(themen, einsatzkraefte, stichtag, ct);

        var von = Ortszeit.TagesbeginnUtc(new DateOnly(jahr, 1, 1));
        var bis = Ortszeit.TagesbeginnUtc(new DateOnly(jahr + 1, 1, 1));
        var termine = await db.Dienste.AsNoTracking()
            .Where(d => d.Art == DienstArt.Unterweisung && d.Status != DienstStatus.Abgesagt && d.Beginn >= von && d.Beginn < bis)
            .Select(d => new
            {
                d.Status,
                Themen = d.Unterweisungsthemen.Select(t => t.Id).ToList(),
                Anwesend = d.Anwesenheiten.Where(a => a.Status == AnwesenheitStatus.Anwesend).Select(a => a.MitgliedId).ToList(),
            })
            .ToListAsync(ct);
        var namen = await db.Mitglieder.AsNoTracking().Where(m => einsatzkraefte.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Nachname + ", " + m.Vorname, ct);

        return themen.Select(t =>
        {
            var ziel = t.QualifikationId is { } q ? einsatzkraefte.Where(m => qualifiziert.Contains((m, q))) : einsatzkraefte;
            var dabei = termine.Where(d => d.Status == DienstStatus.Durchgefuehrt && d.Themen.Contains(t.Id)).SelectMany(d => d.Anwesend);
            var ergebnis = Unterweisungsdeckung.Berechnen(t.Id, ziel, dabei);
            var offen = ergebnis.Offen.Select(id => namen.GetValueOrDefault(id, "?")).Order(StringComparer.CurrentCulture).ToList();
            return new Deckungszeile(t, ergebnis, offen, termine.Count(d => d.Themen.Contains(t.Id)));
        }).ToList();
    }

    /// <summary>Paare aus Mitglied und Qualifikation, die am Stichtag gültig erworben sind, für die Qualifikationen der Themen.</summary>
    private async Task<HashSet<(Guid Mitglied, Guid Qualifikation)>> QualifiziertAsync(IEnumerable<Unterweisungsthema> themen, IReadOnlyCollection<Guid> personen,
        DateOnly stichtag, CancellationToken ct)
    {
        var qualifikationen = themen.Where(t => t.QualifikationId is not null).Select(t => t.QualifikationId!.Value).Distinct().ToList();
        if (qualifikationen.Count == 0 || personen.Count == 0)
        {
            return [];
        }

        var paare = await db.QualifikationNachweise.AsNoTracking()
            .Where(n => qualifikationen.Contains(n.QualifikationId) && personen.Contains(n.MitgliedId) && n.ErworbenAm <= stichtag
                && (n.GueltigBis == null || n.GueltigBis >= stichtag))
            .Select(n => new { n.MitgliedId, n.QualifikationId }).ToListAsync(ct);
        return paare.Select(p => (p.MitgliedId, p.QualifikationId)).ToHashSet();
    }
}
