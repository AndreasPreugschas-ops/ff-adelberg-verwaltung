using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Kommunikation;

/// <summary>
/// Rundfragen und Umfragen (V42): verschicken mit Benachrichtigung (V72), antworten, beenden und auswerten, bei „Wer kann?“ auch nach
/// Qualifikationen (z. B. wie viele Atemschutzgeräteträger und Maschinisten zugesagt haben). Keine Alarmierung.
/// </summary>
public sealed class Rundfragen(AppDbContext db, Mitteilungen mitteilungen, Benachrichtigungsdienst benachrichtigungen, TimeProvider zeit)
{
    /// <summary>Antwort einer Person für den Verfasser (nicht bei anonymen Umfragen).</summary>
    /// <param name="Name">Name bzw. „Eltern von …“.</param>
    /// <param name="Auswahl">Gewählte Antworten als Text; leer ohne Antwort.</param>
    /// <param name="Bemerkung">Bemerkung.</param>
    /// <param name="Nummern">Gewählte Nummern.</param>
    public sealed record Personenantwort(string Name, string? Auswahl, string? Bemerkung, IReadOnlyList<int> Nummern);

    /// <summary>Zusagen je Qualifikation bei „Wer kann?“.</summary>
    /// <param name="Bezeichnung">Qualifikation.</param>
    /// <param name="Ja">Zusagen mit dieser Qualifikation.</param>
    /// <param name="Vielleicht">Vielleicht mit dieser Qualifikation.</param>
    public sealed record Qualifikationszeile(string Bezeichnung, int Ja, int Vielleicht);

    /// <summary>Auswertung einer Rundfrage.</summary>
    /// <param name="Empfaenger">Zahl der Empfänger.</param>
    /// <param name="Geantwortet">Zahl der Antwortenden.</param>
    /// <param name="JeMoeglichkeit">Stimmen je Antwortmöglichkeit.</param>
    /// <param name="Personen">Antworten je Person; leer bei anonymen Umfragen.</param>
    /// <param name="JeQualifikation">Zusagen je Qualifikation; leer außer bei „Wer kann?“ an Mitglieder.</param>
    public sealed record Ergebnis(int Empfaenger, int Geantwortet, IReadOnlyList<int> JeMoeglichkeit, IReadOnlyList<Personenantwort> Personen, IReadOnlyList<Qualifikationszeile> JeQualifikation);

    /// <summary>Wie viele Konten die Rundfrage heute erreichen würde.</summary>
    /// <param name="r">Die Rundfrage.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der Konten.</returns>
    public async Task<int> VorschauAsync(Rundfrage r, DateOnly heute, CancellationToken ct = default) =>
        (await mitteilungen.EmpfaengerAsync(r.Ziel, r.Abteilungen, r.JugendveranstaltungId, heute, ct)).Count;

    /// <summary>Verschickt einen Entwurf: schreibt die Empfänger fest und benachrichtigt sie.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="verfasser">Name des Verfassers.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der Empfänger; null, wenn die Rundfrage kein Entwurf ist oder Fehler hat.</returns>
    public async Task<int?> VerschickenAsync(Guid id, string verfasser, CancellationToken ct = default)
    {
        var r = await db.Rundfragen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is not { Status: RundfrageStatus.Entwurf } || r.Pruefen().Count > 0)
        {
            return null;
        }

        var jetzt = zeit.GetUtcNow();
        var empfaenger = await mitteilungen.EmpfaengerAsync(r.Ziel, r.Abteilungen, r.JugendveranstaltungId,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin).DateTime), ct);
        r.Status = RundfrageStatus.Offen;
        r.VerschicktAm = jetzt;
        r.Verfasser = verfasser;
        foreach (var e in empfaenger)
        {
            db.RundfrageEmpfaenger.Add(new RundfrageEmpfaenger { RundfrageId = r.Id, KontoId = e.KontoId, MitgliedId = e.MitgliedId });
        }

        await db.SaveChangesAsync(ct);
        await benachrichtigungen.SendenAsync(empfaenger.Select(e => e.KontoId),
            new(Benachrichtigungsart.Rundfrage, (r.Art == RundfrageArt.Verfuegbarkeit ? "Wer kann? " : "Umfrage: ") + r.Titel, $"/Rundfragen/Details/{r.Id}", r.Id), ct);
        return empfaenger.Count;
    }

    /// <summary>Beendet eine offene Rundfrage; offene Benachrichtigungen dazu verschwinden.</summary>
    /// <param name="id">Die Rundfrage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn beendet.</returns>
    public async Task<bool> BeendenAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.Rundfragen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is not { Status: RundfrageStatus.Offen })
        {
            return false;
        }

        r.Status = RundfrageStatus.Beendet;
        r.BeendetAm = zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await db.Benachrichtigungen.Where(b => b.BezugId == id && b.GelesenAm == null).ExecuteDeleteAsync(ct);
        return true;
    }

    /// <summary>Empfängereintrag und eigene Antwort des Kontos; die Benachrichtigung gilt als gelesen.</summary>
    /// <param name="rundfrageId">Die Rundfrage.</param>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Eintrag und Antwort (bei anonymen Umfragen ohne Antwort) oder null, wenn das Konto kein Empfänger ist.</returns>
    public async Task<(RundfrageEmpfaenger Empfaenger, RundfrageAntwort? Antwort)?> EigeneAsync(Guid rundfrageId, Guid kontoId, CancellationToken ct = default)
    {
        var e = await db.RundfrageEmpfaenger.AsNoTracking().FirstOrDefaultAsync(x => x.RundfrageId == rundfrageId && x.KontoId == kontoId, ct);
        if (e is null)
        {
            return null;
        }

        await benachrichtigungen.BezugGelesenAsync(kontoId, rundfrageId, ct);
        var antwort = await db.RundfrageAntworten.AsNoTracking().FirstOrDefaultAsync(a => a.EmpfaengerId == e.Id, ct);
        return (e, antwort);
    }

    /// <summary>
    /// Speichert die Antwort des Kontos. Namentliche Antworten lassen sich bis zum Ende ändern, anonyme nur einmal abgeben.
    /// </summary>
    /// <param name="rundfrageId">Die Rundfrage.</param>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="auswahl">Gewählte Nummern.</param>
    /// <param name="bemerkung">Bemerkung; bei anonymen Umfragen ignoriert.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Fehlermeldung oder null bei Erfolg.</returns>
    public async Task<string?> AntwortenAsync(Guid rundfrageId, Guid kontoId, IReadOnlyCollection<int> auswahl, string? bemerkung, CancellationToken ct = default)
    {
        var r = await db.Rundfragen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rundfrageId, ct);
        var e = await db.RundfrageEmpfaenger.FirstOrDefaultAsync(x => x.RundfrageId == rundfrageId && x.KontoId == kontoId, ct);
        var jetzt = zeit.GetUtcNow();
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin).DateTime);
        if (r is null || e is null)
        {
            return "Diese Rundfrage ist nicht an dich gerichtet.";
        }

        if (!r.AntwortenMoeglich(heute))
        {
            return "Die Rundfrage ist beendet.";
        }

        if (!r.AuswahlGueltig(auswahl))
        {
            return r.Art == RundfrageArt.Umfrage && r.Mehrfachauswahl ? "Bitte mindestens eine Antwort wählen." : "Bitte genau eine Antwort wählen.";
        }

        bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
        if (bemerkung?.Length > 200)
        {
            return "Die Bemerkung darf höchstens 200 Zeichen haben.";
        }

        if (r.Anonym)
        {
            if (e.BeantwortetAm is not null)
            {
                return "Du hast schon abgestimmt; anonyme Antworten lassen sich nicht ändern.";
            }

            // Zufallsschlüssel statt UUID v7, damit die Reihenfolge der Antworten nichts über die Person verrät.
            db.RundfrageAntworten.Add(new RundfrageAntwort { Id = Guid.NewGuid(), RundfrageId = r.Id, Auswahl = auswahl.Order().ToList() });
        }
        else
        {
            var antwort = await db.RundfrageAntworten.FirstOrDefaultAsync(a => a.EmpfaengerId == e.Id, ct);
            if (antwort is null)
            {
                antwort = new RundfrageAntwort { RundfrageId = r.Id, EmpfaengerId = e.Id };
                db.RundfrageAntworten.Add(antwort);
            }

            antwort.Auswahl = auswahl.Order().ToList();
            antwort.Bemerkung = bemerkung;
        }

        e.BeantwortetAm = heute;
        await db.SaveChangesAsync(ct);
        await benachrichtigungen.BezugGelesenAsync(kontoId, rundfrageId, ct);
        return null;
    }

    /// <summary>Wertet die Rundfrage aus.</summary>
    /// <param name="r">Die Rundfrage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<Ergebnis> ErgebnisAsync(Rundfrage r, CancellationToken ct = default)
    {
        var empfaenger = await db.RundfrageEmpfaenger.AsNoTracking().Where(e => e.RundfrageId == r.Id).ToListAsync(ct);
        var antworten = await db.RundfrageAntworten.AsNoTracking().Where(a => a.RundfrageId == r.Id).ToListAsync(ct);
        var je = r.Moeglichkeiten.Select((_, i) => antworten.Count(a => a.Auswahl.Contains(i))).ToList();
        var geantwortet = empfaenger.Count(e => e.BeantwortetAm is not null);
        if (r.Anonym)
        {
            return new Ergebnis(empfaenger.Count, geantwortet, je, [], []);
        }

        var mitgliedIds = empfaenger.Where(e => e.MitgliedId is not null).Select(e => e.MitgliedId!.Value).Distinct().ToList();
        var namen = await db.Mitglieder.AsNoTracking().IgnoreQueryFilters().Where(m => mitgliedIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Vorname, m.Nachname }).ToDictionaryAsync(m => m.Id, ct);
        var nachEmpfaenger = antworten.Where(a => a.EmpfaengerId is not null).ToDictionary(a => a.EmpfaengerId!.Value);
        var personen = empfaenger.Select(e =>
            {
                var n = e.MitgliedId is { } m && namen.TryGetValue(m, out var x) ? x : null;
                var name = n is null ? "unbekannt" : r.Ziel == MitteilungZiel.Mitglieder ? $"{n.Nachname}, {n.Vorname}" : $"Eltern von {n.Vorname} {n.Nachname}";
                var a = nachEmpfaenger.GetValueOrDefault(e.Id);
                return new Personenantwort(name, a is null ? null : string.Join(", ", a.Auswahl.Where(i => i < r.Moeglichkeiten.Count).Select(i => r.Moeglichkeiten[i])),
                    a?.Bemerkung, a?.Auswahl ?? []);
            })
            .OrderBy(p => p.Nummern.Count == 0 ? int.MaxValue : p.Nummern.Min()).ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        IReadOnlyList<Qualifikationszeile> jeQualifikation = [];
        if (r.Art == RundfrageArt.Verfuegbarkeit && r.Ziel == MitteilungZiel.Mitglieder)
        {
            var stichtag = r.Tag ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Ortszeit.Berlin).DateTime);
            var zusagen = empfaenger.Where(e => e.MitgliedId is not null && nachEmpfaenger.TryGetValue(e.Id, out var a) && a.Auswahl.Count > 0 && a.Auswahl[0] < 2)
                .ToDictionary(e => e.MitgliedId!.Value, e => nachEmpfaenger[e.Id].Auswahl[0] == 0);
            jeQualifikation = await QualifikationenAsync(zusagen, stichtag, ct);
        }

        return new Ergebnis(empfaenger.Count, geantwortet, je, personen, jeQualifikation);
    }

    /// <summary>Zählt Zusagen je gültiger Qualifikation; wer eine höhere Stufe hat, zählt auch für die Vorstufen.</summary>
    private async Task<IReadOnlyList<Qualifikationszeile>> QualifikationenAsync(Dictionary<Guid, bool> zusagen, DateOnly stichtag, CancellationToken ct)
    {
        if (zusagen.Count == 0)
        {
            return [];
        }

        var ids = zusagen.Keys.ToList();
        var katalog = await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv && q.Art != QualifikationArt.Abzeichen)
            .Select(q => new { q.Id, q.Bezeichnung, q.VorstufeId, q.Sortierung }).ToListAsync(ct);
        var vorstufe = katalog.ToDictionary(q => q.Id, q => q.VorstufeId);
        var nachweise = await db.QualifikationNachweise.AsNoTracking()
            .Where(n => ids.Contains(n.MitgliedId) && n.ErworbenAm <= stichtag && (n.GueltigBis == null || n.GueltigBis >= stichtag))
            .Select(n => new { n.MitgliedId, n.QualifikationId }).ToListAsync(ct);
        var jeMitglied = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var n in nachweise)
        {
            var menge = jeMitglied.TryGetValue(n.MitgliedId, out var m) ? m : jeMitglied[n.MitgliedId] = [];
            Guid? q = n.QualifikationId;
            while (q is { } aktuell && vorstufe.ContainsKey(aktuell) && menge.Add(aktuell))
            {
                q = vorstufe[aktuell];
            }
        }

        return katalog
            .Select(q => new
            {
                q.Sortierung, q.Bezeichnung,
                Ja = jeMitglied.Count(m => m.Value.Contains(q.Id) && zusagen[m.Key]),
                Vielleicht = jeMitglied.Count(m => m.Value.Contains(q.Id) && !zusagen[m.Key]),
            })
            .Where(z => z.Ja + z.Vielleicht > 0)
            .OrderBy(z => z.Sortierung).ThenBy(z => z.Bezeichnung)
            .Select(z => new Qualifikationszeile(z.Bezeichnung, z.Ja, z.Vielleicht))
            .ToList();
    }
}
