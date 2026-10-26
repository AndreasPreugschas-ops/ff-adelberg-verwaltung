using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Abgabe an und Rückgabe von einer fremden Werkstatt (V61): Nummer vergeben, doppelte Abgabe verhindern,
/// bei der Rückgabe das Werkstattprotokoll übernehmen und Prüfungen im Prüfbuch anlegen.
/// </summary>
public sealed class Werkstattauftraege(AppDbContext db, Pruefungserfassung erfassung)
{
    /// <summary>Rückmeldung der Werkstatt zu einem Gerät.</summary>
    /// <param name="PositionId">Kennung der Position im Auftrag.</param>
    /// <param name="Leistungen">Was die Werkstatt gemacht hat.</param>
    /// <param name="PruefplanPositionId">Prüfplanposition, nach der geprüft wurde; nur mit <see cref="Werkstattleistung.Geprueft"/>.</param>
    /// <param name="Ergebnis">Ergebnis der Prüfung.</param>
    /// <param name="Mangel">Befund der Werkstatt; wird Mangel am Gerät.</param>
    /// <param name="Bemerkung">Bemerkung zur Position.</param>
    public sealed record Rueckmeldung(Guid PositionId, Werkstattleistung Leistungen, Guid? PruefplanPositionId, PruefErgebnis Ergebnis, string? Mangel, string? Bemerkung);

    /// <summary>Legt einen Auftrag an; Geräte, die schon in einer Werkstatt sind, werden abgelehnt.</summary>
    /// <param name="auftrag">Der Auftrag ohne Nummer und Positionen.</param>
    /// <param name="geraeteIds">Abzugebende Geräte.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Fehlermeldung oder null; bei Erfolg ist der Auftrag dem Kontext hinzugefügt, gespeichert wird beim Aufrufer.</returns>
    public async Task<string?> AnlegenAsync(Werkstattauftrag auftrag, IReadOnlyCollection<Guid> geraeteIds, CancellationToken ct)
    {
        var ids = geraeteIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return "Bitte mindestens ein Gerät auswählen.";
        }

        var geraete = await db.Geraete.Where(g => ids.Contains(g.Id) && g.AusgesondertAm == null).Select(g => g.Id).ToListAsync(ct);
        if (geraete.Count != ids.Count)
        {
            return "Ein ausgewähltes Gerät gibt es nicht oder es ist ausgesondert.";
        }

        var unterwegs = await db.Werkstattpositionen
            .Where(p => ids.Contains(p.GeraetId) && p.Werkstattauftrag!.ZurueckAm == null)
            .Select(p => p.Geraet!.Inventarnummer + " (" + p.Werkstattauftrag!.Nummer + ")")
            .ToListAsync(ct);
        if (unterwegs.Count > 0)
        {
            return $"Schon in der Werkstatt: {string.Join(", ", unterwegs.Order(StringComparer.Ordinal))}.";
        }

        var nummern = await db.Werkstattauftraege.IgnoreQueryFilters()
            .Where(w => w.Nummer.StartsWith($"W-{auftrag.AbgegebenAm.Year}-"))
            .Select(w => w.Nummer)
            .ToListAsync(ct);
        auftrag.Nummer = Werkstattauftrag.NaechsteNummer(auftrag.AbgegebenAm.Year, nummern);
        auftrag.Positionen = ids.Select(id => new Werkstattposition { WerkstattauftragId = auftrag.Id, GeraetId = id }).ToList();
        db.Werkstattauftraege.Add(auftrag);
        return null;
    }

    /// <summary>
    /// Bucht die Rückgabe: Leistungen je Gerät, Prüfungen mit der Werkstatt als externem Prüfer (Sperren nach dem Einsatz
    /// fallen damit weg), Befunde als Mängel, Protokolle als Anhang.
    /// </summary>
    /// <param name="auftrag">Der offene Auftrag mit Positionen (verfolgt).</param>
    /// <param name="zurueckAm">Tag der Rückgabe.</param>
    /// <param name="beleg">Nummer des Werkstatt-Lieferscheins.</param>
    /// <param name="rueckmeldungen">Rückmeldung je Position.</param>
    /// <param name="protokolle">Angehängte Werkstattprotokolle.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Fehlermeldung oder null; gespeichert wird beim Aufrufer.</returns>
    public async Task<string?> ZurueckAsync(Werkstattauftrag auftrag, DateOnly zurueckAm, string? beleg, IReadOnlyList<Rueckmeldung> rueckmeldungen,
        IReadOnlyList<Werkstattprotokoll> protokolle, CancellationToken ct)
    {
        if (!auftrag.Offen)
        {
            return "Der Auftrag ist schon zurückgebucht.";
        }

        if (zurueckAm < auftrag.AbgegebenAm)
        {
            return "Die Rückgabe liegt vor der Abgabe.";
        }

        var geraeteIds = auftrag.Positionen.Select(p => p.GeraetId).ToList();
        var arten = await db.Geraete.Where(g => geraeteIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.GeraeteartId, ct);
        var planIds = rueckmeldungen.Where(r => r.PruefplanPositionId is not null).Select(r => r.PruefplanPositionId!.Value).Distinct().ToList();
        var plan = await db.PruefplanPositionen.Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        foreach (var position in auftrag.Positionen)
        {
            var meldung = rueckmeldungen.FirstOrDefault(r => r.PositionId == position.Id);
            if (meldung is null)
            {
                continue;
            }

            position.Leistungen = meldung.Leistungen;
            position.Bemerkung = string.IsNullOrWhiteSpace(meldung.Bemerkung) ? null : meldung.Bemerkung.Trim();
            if (!meldung.Leistungen.HasFlag(Werkstattleistung.Geprueft))
            {
                continue;
            }

            if (meldung.PruefplanPositionId is not { } planId || !plan.TryGetValue(planId, out var planPosition) || planPosition.GeraeteartId != arten[position.GeraetId])
            {
                return "Für geprüfte Geräte bitte die Prüfart aus dem Prüfplan wählen.";
            }

            var pruefung = new Pruefung
            {
                GeraetId = position.GeraetId,
                PruefplanPositionId = planPosition.Id,
                Pruefart = planPosition.Pruefart,
                Datum = zurueckAm,
                Ergebnis = meldung.Ergebnis,
                PrueferExtern = auftrag.Werkstattname,
                Bemerkung = $"Werkstattauftrag {auftrag.Nummer}" + (position.Bemerkung is null ? "" : $": {position.Bemerkung}"),
            };
            await erfassung.AnlegenAsync(pruefung, planPosition, meldung.Mangel, MangelSchwere.Einschraenkung, ct);
            position.PruefungId = pruefung.Id;
        }

        auftrag.ZurueckAm = zurueckAm;
        auftrag.BelegWerkstatt = string.IsNullOrWhiteSpace(beleg) ? null : beleg.Trim();
        foreach (var protokoll in protokolle)
        {
            protokoll.WerkstattauftragId = auftrag.Id;
            db.Werkstattprotokolle.Add(protokoll);
        }

        return null;
    }
}
