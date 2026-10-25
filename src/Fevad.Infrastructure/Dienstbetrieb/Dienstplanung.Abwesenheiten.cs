using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>Gemeldete Abwesenheiten (V37): Absagen für alle Dienste im Zeitraum, Rücknahme und Anzeige an den Kommandanten.</summary>
public sealed partial class Dienstplanung
{
    /// <summary>Begründung, die eine aus einer Abwesenheit erzeugte Absage trägt; der Grund selbst bleibt beim Kommandanten.</summary>
    public const string AbwesenheitBegruendung = "Abwesenheit gemeldet";

    /// <summary>Nachfrist für Entschuldigungen in Tagen nach dem Diensttag aus dem Parameter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Tage; 0 heißt nur bis zum Diensttag.</returns>
    public async Task<int> NachfristTageAsync(CancellationToken ct = default) =>
        await parameter.ZahlAsync(ParameterSchluessel.DienstEntschuldigungNachfristTage, LokalesDatum(zeit.GetUtcNow()), 1, ct);

    /// <summary>Ab so vielen Tagen ist eine Abwesenheit dem Kommandanten anzuzeigen (Parameter).</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Tage; 0 heißt keine Anzeige.</returns>
    public async Task<int> AnzeigeAbTagenAsync(CancellationToken ct = default) =>
        await parameter.ZahlAsync(ParameterSchluessel.AbwesenheitAnzeigeAbTagen, LokalesDatum(zeit.GetUtcNow()), 14, ct);

    /// <summary>Prüft, ob sich ein Mitglied für den Dienst nach dem Rückmeldeschluss noch entschuldigen kann.</summary>
    /// <param name="dienst">Der Dienst.</param>
    /// <param name="jetzt">Aktueller Zeitpunkt.</param>
    /// <param name="schlussStunden">Rückmeldeschluss in Stunden vor Beginn.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true innerhalb der Nachfrist.</returns>
    public async Task<bool> EntschuldigungMoeglichAsync(Dienst dienst, DateTimeOffset jetzt, int schlussStunden, CancellationToken ct = default) =>
        dienst.EntschuldigungMoeglich(jetzt, schlussStunden, LokalesDatum(jetzt), LokalesDatum(dienst.Beginn), await NachfristTageAsync(ct));

    /// <summary>
    /// Speichert eine Abwesenheit und sagt alle Dienste im Zeitraum ab, die das Mitglied betreffen; eine frühere Zusage wird zur Absage.
    /// Prüft die Eingaben vorher mit <see cref="Abwesenheit.Pruefen"/>.
    /// </summary>
    /// <param name="abwesenheit">Die neue Abwesenheit mit Mitglied, Zeitraum und Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl der abgesagten Dienste.</returns>
    public async Task<int> AbwesenheitMeldenAsync(Abwesenheit abwesenheit, CancellationToken ct = default)
    {
        abwesenheit.GemeldetAm = zeit.GetUtcNow();
        abwesenheit.Bemerkung = string.IsNullOrWhiteSpace(abwesenheit.Bemerkung) ? null : abwesenheit.Bemerkung.Trim();
        db.Abwesenheiten.Add(abwesenheit);
        await db.SaveChangesAsync(ct);
        return await AbwesenheitAnwendenAsync(abwesenheit, ct);
    }

    /// <summary>
    /// Nimmt eine Abwesenheit zurück: Hat sie noch nicht begonnen, fällt sie ganz weg, sonst endet sie gestern. Die daraus
    /// erzeugten Absagen ab heute fallen weg; die Anwesenheit vergangener Dienste bleibt entschuldigt.
    /// </summary>
    /// <param name="abwesenheit">Die Abwesenheit (verfolgt).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl der zurückgenommenen Absagen.</returns>
    public async Task<int> AbwesenheitZuruecknehmenAsync(Abwesenheit abwesenheit, CancellationToken ct = default)
    {
        var heute = LokalesDatum(zeit.GetUtcNow());
        var ab = AlsUtc(heute, TimeOnly.MinValue);
        var absagen = await db.Rueckmeldungen.Include(r => r.Dienst)
            .Where(r => r.AbwesenheitId == abwesenheit.Id && r.Dienst!.Beginn >= ab)
            .ToListAsync(ct);
        foreach (var absage in absagen)
        {
            await AbsageEntfernenAsync(absage, ct);
        }

        if (abwesenheit.Von >= heute)
        {
            db.Abwesenheiten.Remove(abwesenheit);
        }
        else if (abwesenheit.Bis >= heute)
        {
            abwesenheit.Bis = heute.AddDays(-1);
        }

        await db.SaveChangesAsync(ct);
        return absagen.Count;
    }

    /// <summary>
    /// Nach dem Anlegen oder Ändern eines Dienstes: Absagen aus Abwesenheiten, die den neuen Tag nicht mehr umfassen, fallen weg;
    /// Mitglieder der Zielgruppe mit einer Abwesenheit am Diensttag und ohne eigene Rückmeldung bekommen eine Absage.
    /// </summary>
    /// <param name="dienst">Der gespeicherte Dienst.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task AbwesenheitenAbgleichenAsync(Dienst dienst, CancellationToken ct = default)
    {
        var tag = LokalesDatum(dienst.Beginn);
        var veraltet = await db.Rueckmeldungen.Include(r => r.Abwesenheit)
            .Where(r => r.DienstId == dienst.Id && r.AbwesenheitId != null)
            .ToListAsync(ct);
        foreach (var absage in veraltet.Where(r => dienst.Status == DienstStatus.Abgesagt || !r.Abwesenheit!.Umfasst(tag)))
        {
            await AbsageEntfernenAsync(absage, ct);
        }

        if (dienst.Status != DienstStatus.Abgesagt && (dienst.AlsDienst || dienst.RueckmeldungErbeten))
        {
            var geladen = await db.Dienste.AsNoTracking().MitZielgruppe().FirstAsync(d => d.Id == dienst.Id, ct);
            var gemeldet = await db.Rueckmeldungen.Where(r => r.DienstId == dienst.Id).Select(r => r.MitgliedId).ToListAsync(ct);
            var abwesende = await db.Abwesenheiten.AsNoTracking()
                .Where(a => a.Von <= tag && a.Bis >= tag && !gemeldet.Contains(a.MitgliedId))
                .ToListAsync(ct);
            var jetzt = zeit.GetUtcNow();
            foreach (var abwesenheit in abwesende.DistinctBy(a => a.MitgliedId))
            {
                if ((await ProfilAsync(abwesenheit.MitgliedId, ct)).Betrifft(geladen, tag))
                {
                    await MeldungSpeichernAsync(geladen, abwesenheit.MitgliedId, RueckmeldungAntwort.Absage, AbwesenheitBegruendung, jetzt, ct,
                        unentschuldigtAufheben: true, abwesenheitId: abwesenheit.Id);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Anzeigepflichtige Abwesenheiten, die noch nicht zur Kenntnis genommen sind und noch nicht vorbei sind.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Abwesenheiten samt Mitglied, nach Beginn sortiert.</returns>
    public async Task<List<Abwesenheit>> OffeneAnzeigenAsync(CancellationToken ct = default)
    {
        var abTagen = await AnzeigeAbTagenAsync(ct);
        if (abTagen <= 0)
        {
            return [];
        }

        var heute = LokalesDatum(zeit.GetUtcNow());
        var offen = await db.Abwesenheiten.AsNoTracking().Include(a => a.Mitglied)
            .Where(a => a.KenntnisGenommenAm == null && a.Bis >= heute)
            .OrderBy(a => a.Von)
            .ToListAsync(ct);
        return offen.Where(a => a.Anzeigepflichtig(abTagen)).ToList();
    }

    /// <summary>Sagt alle Dienste im Zeitraum der Abwesenheit ab, die das Mitglied betreffen und nicht abgesagt sind.</summary>
    private async Task<int> AbwesenheitAnwendenAsync(Abwesenheit abwesenheit, CancellationToken ct)
    {
        var profil = await ProfilAsync(abwesenheit.MitgliedId, ct);
        var dienste = await TermineAsync(AlsUtc(abwesenheit.Von, TimeOnly.MinValue), AlsUtc(abwesenheit.Bis.AddDays(1), TimeOnly.MinValue), profil, ct);
        var jetzt = zeit.GetUtcNow();
        var anzahl = 0;
        foreach (var dienst in dienste.Where(d => d.Status != DienstStatus.Abgesagt && (d.AlsDienst || d.RueckmeldungErbeten)))
        {
            var bisher = await db.Rueckmeldungen.AsNoTracking()
                .FirstOrDefaultAsync(r => r.DienstId == dienst.Id && r.MitgliedId == abwesenheit.MitgliedId, ct);
            if (bisher?.Antwort == RueckmeldungAntwort.Absage)
            {
                // Eine eigene Absage bleibt mit ihrer Begründung stehen.
                continue;
            }

            await MeldungSpeichernAsync(dienst, abwesenheit.MitgliedId, RueckmeldungAntwort.Absage, AbwesenheitBegruendung, jetzt, ct,
                unentschuldigtAufheben: true, abwesenheitId: abwesenheit.Id);
            anzahl++;
        }

        await db.SaveChangesAsync(ct);
        return anzahl;
    }

    /// <summary>Entfernt eine Absage samt der nur daraus übernommenen Anwesenheit „entschuldigt“.</summary>
    private async Task AbsageEntfernenAsync(Rueckmeldung absage, CancellationToken ct)
    {
        db.Rueckmeldungen.Remove(absage);
        var anwesenheit = await db.Anwesenheiten.FirstOrDefaultAsync(a => a.DienstId == absage.DienstId && a.MitgliedId == absage.MitgliedId
            && a.ErfasstUeber == ErfassungsWeg.Rueckmeldung && a.Status == AnwesenheitStatus.Entschuldigt, ct);
        if (anwesenheit is not null)
        {
            db.Anwesenheiten.Remove(anwesenheit);
        }
    }
}
