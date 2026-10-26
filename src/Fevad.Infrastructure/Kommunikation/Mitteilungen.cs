using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kommunikation;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Kommunikation;

/// <summary>
/// Mitteilungen an Zielgruppen (V42): Empfänger ermitteln und festschreiben, veröffentlichen mit Benachrichtigung (V72) und auf Wunsch
/// sofortiger Mail, Lesen und Bestätigen vermerken, zurückziehen. Wer schreiben darf, regelt <see cref="Berechtigungen.MitteilungenSchreiben"/>
/// mit Abteilungsbezug; Elternpost braucht das Recht für die Jugend- oder Kinderfeuerwehr.
/// </summary>
public sealed class Mitteilungen(
    AppDbContext db,
    Benachrichtigungsdienst benachrichtigungen,
    IMailVersand mail,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<Mitteilungen> log)
{
    /// <summary>Ein Empfänger vor dem Festschreiben.</summary>
    /// <param name="KontoId">Empfangendes Konto.</param>
    /// <param name="MitgliedId">Mitglied, über das das Konto Empfänger ist (eigenes oder Kind).</param>
    public sealed record Empfaenger(Guid KontoId, Guid MitgliedId);

    /// <summary>Lesestand eines Empfängers für den Verfasser.</summary>
    /// <param name="Name">Name des Kontos bzw. „Eltern von …“.</param>
    /// <param name="GelesenAm">Zuerst geöffnet.</param>
    /// <param name="BestaetigtAm">Lesen bestätigt.</param>
    public sealed record Lesestand(string Name, DateTimeOffset? GelesenAm, DateTimeOffset? BestaetigtAm);

    /// <summary>Abteilungen samt ihren Unterabteilungen.</summary>
    /// <param name="abteilungen">Gewählte Abteilungen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Menge mit Unterabteilungen.</returns>
    public async Task<HashSet<Guid>> MitUnterabteilungenAsync(IEnumerable<Guid> abteilungen, CancellationToken ct = default)
    {
        var alle = await db.Abteilungen.AsNoTracking().Select(a => new { a.Id, a.UebergeordneteId }).ToListAsync(ct);
        var menge = abteilungen.ToHashSet();
        bool neu;
        do
        {
            neu = false;
            foreach (var a in alle.Where(a => a.UebergeordneteId is { } o && menge.Contains(o)))
            {
                neu |= menge.Add(a.Id);
            }
        }
        while (neu);
        return menge;
    }

    /// <summary>Abteilungen, deren Mitglieder oder Eltern die Mitteilung erreicht (bei einer Veranstaltung die Abteilungen ihres Dienstes).</summary>
    /// <param name="m">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Abteilungen.</returns>
    public async Task<IReadOnlyCollection<Guid>> ZielabteilungenAsync(Mitteilung m, CancellationToken ct = default)
    {
        if (m.Ziel != MitteilungZiel.Veranstaltung)
        {
            return await MitUnterabteilungenAsync(m.Abteilungen, ct);
        }

        return await db.Jugendveranstaltungen.Where(v => v.Id == m.JugendveranstaltungId)
            .SelectMany(v => v.Dienst!.Abteilungen.Select(a => a.Id)).ToListAsync(ct);
    }

    /// <summary>Ermittelt die Empfänger am Stichtag: je Konto ein Eintrag.</summary>
    /// <param name="m">Die Mitteilung.</param>
    /// <param name="stichtag">Tag der Veröffentlichung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Empfänger.</returns>
    public async Task<IReadOnlyList<Empfaenger>> EmpfaengerAsync(Mitteilung m, DateOnly stichtag, CancellationToken ct = default)
    {
        IQueryable<Mitglied> mitglieder;
        if (m.Ziel == MitteilungZiel.Veranstaltung)
        {
            mitglieder = db.Veranstaltungsanmeldungen.Where(a => a.JugendveranstaltungId == m.JugendveranstaltungId).Select(a => a.Mitglied!);
        }
        else
        {
            var abteilungen = await MitUnterabteilungenAsync(m.Abteilungen, ct);
            mitglieder = db.Mitglieder.Where(x => x.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag) && abteilungen.Contains(s.AbteilungId)));
        }

        mitglieder = mitglieder.Where(x => x.AnonymisiertAm == null);
        var aktiveKonten = db.Users.Where(u => u.DeaktiviertAm == null && u.AnonymisiertAm == null);
        List<Empfaenger> liste;
        if (m.Ziel == MitteilungZiel.Mitglieder)
        {
            liste = await mitglieder.Join(aktiveKonten, x => (Guid?)x.Id, u => u.MitgliedId, (x, u) => new Empfaenger(u.Id, x.Id)).ToListAsync(ct);
        }
        else
        {
            liste = await mitglieder.SelectMany(x => x.Erziehungsberechtigte.Where(e => e.KontoId != null).Select(e => new { e.KontoId, MitgliedId = x.Id }))
                .Join(aktiveKonten, e => e.KontoId, u => (Guid?)u.Id, (e, u) => new Empfaenger(u.Id, e.MitgliedId)).ToListAsync(ct);
        }

        return liste.GroupBy(e => e.KontoId).Select(g => g.First()).ToList();
    }

    /// <summary>
    /// Veröffentlicht einen Entwurf: schreibt die Empfänger fest, benachrichtigt sie in der Anwendung und schickt auf Wunsch sofort eine Mail
    /// mit Titel und Link (nie den Text, der bleibt in FEVAD).
    /// </summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="verfasser">Name des Verfassers für die Anzeige.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der Empfänger; null, wenn die Mitteilung kein Entwurf ist oder Fehler hat.</returns>
    public async Task<int?> VeroeffentlichenAsync(Guid id, string verfasser, CancellationToken ct = default)
    {
        var m = await db.Mitteilungen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is not { Status: MitteilungStatus.Entwurf } || m.Pruefen().Count > 0)
        {
            return null;
        }

        var jetzt = zeit.GetUtcNow();
        var empfaenger = await EmpfaengerAsync(m, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin).DateTime), ct);
        m.Status = MitteilungStatus.Veroeffentlicht;
        m.VeroeffentlichtAm = jetzt;
        m.Verfasser = verfasser;
        foreach (var e in empfaenger)
        {
            db.MitteilungEmpfaenger.Add(new MitteilungEmpfaenger { MitteilungId = m.Id, KontoId = e.KontoId, MitgliedId = e.MitgliedId });
        }

        await db.SaveChangesAsync(ct);
        var link = $"/Mitteilungen/Details/{m.Id}";
        await benachrichtigungen.SendenAsync(empfaenger.Select(e => e.KontoId),
            new(Benachrichtigungsart.Mitteilung, (m.Lesebestaetigung ? "Bitte lesen und bestätigen: " : "Neue Mitteilung: ") + m.Titel, link, m.Id), ct);

        if (m.SofortPerMail && mail.Aktiv)
        {
            var ids = empfaenger.Select(e => e.KontoId).ToList();
            var adressen = await db.Users.Where(u => ids.Contains(u.Id) && u.Email != null).Select(u => u.Email!).ToListAsync(ct);
            var adresse = konfiguration["Anmeldung:OeffentlicheAdresse"]?.TrimEnd('/');
            var text = $"{verfasser} hat in FEVAD eine Mitteilung veröffentlicht: „{m.Titel}“."
                + (m.Lesebestaetigung ? " Bitte lesen und das Lesen bestätigen." : "")
                + (string.IsNullOrEmpty(adresse) ? "" : $"\n\nLesen: {adresse}{link}")
                + "\n\nDiese Mail kommt nur, wenn die Mitteilung als dringend verschickt wurde. Keine Alarmierung.";
            foreach (var an in adressen)
            {
                try
                {
                    await mail.SendenAsync(an, $"FEVAD: {m.Titel}", text, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Mail zur Mitteilung {Mitteilung} fehlgeschlagen.", m.Id);
                }
            }

            // In der Sammelmail nicht noch einmal zählen.
            await db.Benachrichtigungen.Where(b => b.BezugId == m.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.GemailtAm, jetzt), ct);
        }

        return empfaenger.Count;
    }

    /// <summary>Zieht eine veröffentlichte Mitteilung zurück; Empfänger sehen sie nicht mehr, ungelesene Benachrichtigungen verschwinden.</summary>
    /// <param name="id">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn zurückgezogen.</returns>
    public async Task<bool> ZurueckziehenAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Mitteilungen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is not { Status: MitteilungStatus.Veroeffentlicht })
        {
            return false;
        }

        m.Status = MitteilungStatus.Zurueckgezogen;
        await db.SaveChangesAsync(ct);
        await db.Benachrichtigungen.Where(b => b.BezugId == id && b.GelesenAm == null).ExecuteDeleteAsync(ct);
        return true;
    }

    /// <summary>Der Empfängereintrag des Kontos zu einer veröffentlichten Mitteilung; vermerkt das erste Öffnen.</summary>
    /// <param name="mitteilungId">Die Mitteilung.</param>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Eintrag oder null, wenn das Konto kein Empfänger ist oder die Mitteilung zurückgezogen wurde.</returns>
    public async Task<MitteilungEmpfaenger?> LesenAsync(Guid mitteilungId, Guid kontoId, CancellationToken ct = default)
    {
        var e = await db.MitteilungEmpfaenger.FirstOrDefaultAsync(x => x.MitteilungId == mitteilungId && x.KontoId == kontoId
            && db.Mitteilungen.Any(m => m.Id == mitteilungId && m.Status == MitteilungStatus.Veroeffentlicht), ct);
        if (e is { GelesenAm: null })
        {
            e.GelesenAm = zeit.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        if (e is not null)
        {
            await benachrichtigungen.BezugGelesenAsync(kontoId, mitteilungId, ct);
        }

        return e;
    }

    /// <summary>Bestätigt das Lesen.</summary>
    /// <param name="mitteilungId">Die Mitteilung.</param>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn bestätigt (oder schon bestätigt).</returns>
    public async Task<bool> BestaetigenAsync(Guid mitteilungId, Guid kontoId, CancellationToken ct = default)
    {
        if (await LesenAsync(mitteilungId, kontoId, ct) is not { } e)
        {
            return false;
        }

        e.BestaetigtAm ??= zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Lesestand aller Empfänger, für den Verfasser.</summary>
    /// <param name="mitteilungId">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Lesestand nach Namen.</returns>
    public async Task<IReadOnlyList<Lesestand>> LesestandAsync(Guid mitteilungId, CancellationToken ct = default)
    {
        var ziel = await db.Mitteilungen.Where(m => m.Id == mitteilungId).Select(m => m.Ziel).FirstAsync(ct);
        var zeilen = await db.MitteilungEmpfaenger.Where(e => e.MitteilungId == mitteilungId)
            .Join(db.Mitglieder, e => e.MitgliedId, x => (Guid?)x.Id, (e, x) => new { x.Nachname, x.Vorname, e.GelesenAm, e.BestaetigtAm })
            .ToListAsync(ct);
        return zeilen
            .Select(z => new Lesestand(ziel == MitteilungZiel.Mitglieder ? $"{z.Nachname}, {z.Vorname}" : $"Eltern von {z.Vorname} {z.Nachname}", z.GelesenAm, z.BestaetigtAm))
            .OrderBy(z => z.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
