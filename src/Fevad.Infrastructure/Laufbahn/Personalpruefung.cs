using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>
/// Prüft Mitglieder auf Anlässe (Beförderung möglich, Ehrung fällig, Übertritt oder Altersgrenze, Funktion ohne
/// Voraussetzung oder mit auslaufender Amtszeit) und hält die <see cref="Pruefhinweis"/>e aktuell: neue Anlässe kommen
/// hinzu, weggefallene werden „entfallen“, zurückgestellte bleiben zurückgestellt.
/// </summary>
public sealed class Personalpruefung(AppDbContext db, Laufbahnabfrage laufbahn, ParameterLeser parameter, ILogger<Personalpruefung> log)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public sealed record Ergebnis(int Geprueft, int Neu, int Entfallen);

    /// <summary>Prüft die angegebenen Mitglieder oder, mit null, alle mit offener Mitgliedschaft.</summary>
    public async Task<Ergebnis> PruefenAsync(IReadOnlyCollection<Guid>? mitgliedIds, DateTimeOffset jetzt, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);
        var regeln = await laufbahn.RegelnAsync(heute, ct);
        // Ausgetretene haben keine Anlässe mehr; ihre offenen Hinweise entfallen.
        var staende = (await laufbahn.LadenAsync(heute, mitgliedIds, ct)).Where(s => s.OffeneAbschnitte(heute).Any()).ToList();
        var ids = staende.Select(s => s.MitgliedId).ToList();

        var funktionen = (await db.FunktionZuordnungen.AsNoTracking()
                .Where(f => ids.Contains(f.MitgliedId) && (f.GueltigBis == null || f.GueltigBis >= heute))
                .Select(f => new
                {
                    f.MitgliedId,
                    F = new LaufendeFunktion(f.Id, f.Funktion!.Bezeichnung, f.Funktion.Voraussetzungen, f.Art, f.GueltigAb, f.GueltigBis, f.Funktion.AmtszeitJahre),
                })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.F);
        var qualifikationen = (await db.QualifikationNachweise.AsNoTracking()
                .Where(q => ids.Contains(q.MitgliedId) && q.ErworbenAm <= heute && (q.GueltigBis == null || q.GueltigBis >= heute))
                .Select(q => new { q.MitgliedId, q.Qualifikation!.Code })
                .ToListAsync(ct))
            .ToLookup(x => x.MitgliedId, x => x.Code);
        var namen = await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Code, q => q.Bezeichnung, ct);

        var volljaehrigAb = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var erinnernMonate = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitErinnernMonate, heute, 12, ct);
        var erklaert = (await db.Antraege.AsNoTracking()
                .Where(a => a.Art == AntragArt.Volljaehrigkeit && a.Status == AntragStatus.Angenommen && a.MitgliedId != null && ids.Contains(a.MitgliedId.Value))
                .Select(a => a.MitgliedId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        var soll = new Dictionary<(Guid, string), Pruefanlass>();
        foreach (var stand in staende)
        {
            foreach (var a in Anlaesse(stand, regeln.VorlaufMonate, heute, funktionen[stand.MitgliedId],
                         qualifikationen[stand.MitgliedId].ToHashSet(StringComparer.OrdinalIgnoreCase), namen))
            {
                soll.TryAdd((stand.MitgliedId, a.Schluessel), a);
            }

            if (!erklaert.Contains(stand.MitgliedId)
                && Volljaehrigkeitsanlass(stand, heute, volljaehrigAb, regeln.VorlaufMonate, erinnernMonate) is { } v)
            {
                soll.TryAdd((stand.MitgliedId, v.Schluessel), v);
            }
        }

        // Bei einem Gesamtlauf auch die Hinweise ausgetretener Mitglieder abschließen.
        var vorhanden = await db.Pruefhinweise
            .Where(p => mitgliedIds == null ? p.Status != PruefhinweisStatus.Entfallen : mitgliedIds.Contains(p.MitgliedId))
            .ToListAsync(ct);
        var bekannt = vorhanden.ToDictionary(p => (p.MitgliedId, p.Schluessel));

        int neu = 0, entfallen = 0;
        foreach (var ((mitglied, schluessel), a) in soll)
        {
            if (bekannt.TryGetValue((mitglied, schluessel), out var hinweis))
            {
                if (hinweis.Status == PruefhinweisStatus.Entfallen)
                {
                    hinweis.Status = PruefhinweisStatus.Offen;
                    hinweis.ErkanntAm = jetzt;
                    hinweis.AbgeschlossenAm = null;
                    neu++;
                }

                hinweis.Text = a.Text;
                hinweis.FaelligAm = a.FaelligAm;
                hinweis.Pflicht = a.Pflicht;
                continue;
            }

            db.Pruefhinweise.Add(new Pruefhinweis
            {
                MitgliedId = mitglied, Art = a.Art, Schluessel = a.Schluessel, Text = a.Text, FaelligAm = a.FaelligAm, Pflicht = a.Pflicht,
                Status = PruefhinweisStatus.Offen, ErkanntAm = jetzt,
            });
            neu++;
        }

        foreach (var hinweis in vorhanden.Where(p => p.Status == PruefhinweisStatus.Offen && !soll.ContainsKey((p.MitgliedId, p.Schluessel))))
        {
            hinweis.Status = PruefhinweisStatus.Entfallen;
            hinweis.AbgeschlossenAm = jetzt;
            entfallen++;
        }

        await db.SaveChangesAsync(ct);
        if (neu + entfallen > 0)
        {
            log.LogInformation("Personalprüfung: {Geprueft} Mitglieder, {Neu} neue und {Entfallen} entfallene Hinweise.", staende.Count, neu, entfallen);
        }

        return new Ergebnis(staende.Count, neu, entfallen);
    }

    /// <summary>
    /// Wer minderjährig in die Einsatzabteilung kam, unterschreibt mit der Volljährigkeit selbst (Erklärung bei
    /// Volljährigkeit). Der Hinweis entfällt, sobald eine solche Erklärung angenommen ist.
    /// </summary>
    public static Pruefanlass? Volljaehrigkeitsanlass(Laufbahnstand stand, DateOnly heute, int volljaehrigAb, int vorlaufMonate, int erinnernMonate)
    {
        var einsatz = stand.Abschnitte.Where(a => !a.Vordienstzeit && a.Art == AbteilungArt.Einsatz).ToList();
        if (!einsatz.Any(a => a.IstOffenAm(heute)))
        {
            return null;
        }

        return Antragsregeln.VolljaehrigkeitFaellig(stand.Geburtsdatum, einsatz.Min(a => a.Von), heute, volljaehrigAb, vorlaufMonate, erinnernMonate) is { } tag
            ? new(PruefhinweisArt.Mitgliedschaft, "Volljaehrigkeit",
                $"Volljährig am {tag:dd.MM.yyyy}: Erklärung bei Volljährigkeit unterschreiben lassen (Anträge).", tag, true)
            : null;
    }

    /// <summary>Anlässe eines Mitglieds; dieselbe Regel wie in der Mitgliederakte.</summary>
    public static IEnumerable<Pruefanlass> Anlaesse(
        Laufbahnstand stand, int vorlaufMonate, DateOnly heute, IEnumerable<LaufendeFunktion> funktionen, IReadOnlySet<string> qualifikationen,
        IReadOnlyDictionary<string, string>? namen = null)
    {
        if (stand.Befoerderung is { } b)
        {
            yield return new(PruefhinweisArt.Befoerderung, $"Befoerderung:{b.Dienstgrad.Code}",
                $"Beförderung zum {b.Dienstgrad.Bezeichnung} möglich ab {b.MoeglichAb:dd.MM.yyyy}.", b.MoeglichAb, false);
        }

        foreach (var e in stand.Ehrungsvorschlaege)
        {
            yield return new(PruefhinweisArt.Ehrung, $"Ehrung:{e.EhrungsartId}",
                $"{e.Ehrungsart}: Voraussetzung erfüllt am {e.ErreichtAm:dd.MM.yyyy}.", e.ErreichtAm, false);
        }

        foreach (var h in stand.Hinweise)
        {
            yield return new(PruefhinweisArt.Mitgliedschaft, $"Mitgliedschaft:{h.MitgliedschaftId}:{h.Art}", h.Text, h.FaelligAm, h.Pflicht);
        }

        foreach (var f in Funktionsanlaesse.Pruefen(funktionen, qualifikationen, heute, vorlaufMonate, namen))
        {
            yield return f;
        }
    }
}
