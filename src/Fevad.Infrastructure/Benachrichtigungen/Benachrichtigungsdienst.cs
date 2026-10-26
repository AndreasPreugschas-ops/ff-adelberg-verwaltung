using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Benachrichtigungen;

/// <summary>
/// Gemeinsame Grundlage für Benachrichtigungen (V72): legt sie für Konten, Mitglieder oder Inhaber eines Rechts an, zählt die ungelesenen
/// für die Glocke und vermerkt das Lesen. Zustellung per Sammelmail übernimmt <see cref="Sammelmail"/>. Keine Alarmierung: Benachrichtigungen
/// erscheinen erst, wenn jemand FEVAD öffnet oder die Mail liest.
/// </summary>
public sealed class Benachrichtigungsdienst(AppDbContext db, Rechteverzeichnis rechte, UserManager<Benutzer> benutzer, TimeProvider zeit)
{
    /// <summary>Höchstlänge des Titels; längere werden gekürzt.</summary>
    public const int TitelLaenge = 200;

    /// <summary>Inhalt einer Benachrichtigung.</summary>
    /// <param name="Art">Art, nach der das Konto die Sammelmail einstellt.</param>
    /// <param name="Titel">Kurzer Titel ohne Gesundheitsdaten.</param>
    /// <param name="Link">Relativer Pfad in FEVAD oder leer.</param>
    /// <param name="BezugId">Bezug, z. B. die Mitteilung; verhindert doppelte ungelesene Benachrichtigungen zum selben Bezug.</param>
    /// <param name="Text">Optionaler kurzer Text.</param>
    public sealed record Inhalt(Benachrichtigungsart Art, string Titel, string? Link = null, Guid? BezugId = null, string? Text = null);

    /// <summary>Benachrichtigt die angegebenen Konten; deaktivierte und anonymisierte Konten bleiben außen vor.</summary>
    /// <param name="kontoIds">Empfangende Konten.</param>
    /// <param name="inhalt">Inhalt der Benachrichtigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der angelegten Benachrichtigungen.</returns>
    public async Task<int> SendenAsync(IEnumerable<Guid> kontoIds, Inhalt inhalt, CancellationToken ct = default)
    {
        if (!Benachrichtigung.IstInternerLink(inhalt.Link) && inhalt.Link is not null)
        {
            throw new ArgumentException("Benachrichtigungen verlinken nur innerhalb von FEVAD.", nameof(inhalt));
        }

        var ids = kontoIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var aktiv = await db.Users.Where(u => ids.Contains(u.Id) && u.DeaktiviertAm == null && u.AnonymisiertAm == null).Select(u => u.Id).ToListAsync(ct);
        if (inhalt.BezugId is { } bezug)
        {
            var schon = await db.Benachrichtigungen
                .Where(b => b.BezugId == bezug && b.Art == inhalt.Art && b.GelesenAm == null && aktiv.Contains(b.EmpfaengerId))
                .Select(b => b.EmpfaengerId).ToListAsync(ct);
            aktiv = aktiv.Except(schon).ToList();
        }

        var jetzt = zeit.GetUtcNow();
        var titel = inhalt.Titel.Trim();
        titel = titel.Length > TitelLaenge ? titel[..(TitelLaenge - 1)] + "…" : titel;
        var text = inhalt.Text?.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, 500)] : null;
        foreach (var konto in aktiv)
        {
            db.Benachrichtigungen.Add(new Benachrichtigung
            {
                EmpfaengerId = konto, Art = inhalt.Art, Titel = titel, Text = text, Link = inhalt.Link, BezugId = inhalt.BezugId, ErstelltAm = jetzt,
            });
        }

        await db.SaveChangesAsync(ct);
        return aktiv.Count;
    }

    /// <summary>Benachrichtigt die Konten der angegebenen Mitglieder; Mitglieder ohne Konto erreicht sie nicht.</summary>
    /// <param name="mitgliedIds">Empfangende Mitglieder.</param>
    /// <param name="inhalt">Inhalt der Benachrichtigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der angelegten Benachrichtigungen.</returns>
    public async Task<int> AnMitgliederAsync(IEnumerable<Guid> mitgliedIds, Inhalt inhalt, CancellationToken ct = default)
    {
        var ids = mitgliedIds.Distinct().ToList();
        var konten = await db.Users.Where(u => u.MitgliedId != null && ids.Contains(u.MitgliedId.Value)).Select(u => u.Id).ToListAsync(ct);
        return await SendenAsync(konten, inhalt, ct);
    }

    /// <summary>Benachrichtigt alle Konten, deren Rollen das Recht haben, z. B. die Freigebenden einer Rechteänderung.</summary>
    /// <param name="recht">Name des Rechts aus <see cref="Berechtigungen"/>.</param>
    /// <param name="inhalt">Inhalt der Benachrichtigung.</param>
    /// <param name="ohne">Konto, das nicht benachrichtigt wird, z. B. wer die Aufgabe selbst ausgelöst hat.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der angelegten Benachrichtigungen.</returns>
    public async Task<int> AnRechtAsync(string recht, Inhalt inhalt, Guid? ohne = null, CancellationToken ct = default) =>
        await SendenAsync((await KontenMitRechtAsync(recht, ct)).Where(k => k != ohne), inhalt, ct);

    /// <summary>Konten, deren Rollen das Recht haben.</summary>
    /// <param name="recht">Name des Rechts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Schlüssel der Konten.</returns>
    public async Task<IReadOnlyCollection<Guid>> KontenMitRechtAsync(string recht, CancellationToken ct = default)
    {
        var stand = await rechte.StandAsync(ct);
        var konten = new HashSet<Guid>();
        foreach (var rolle in stand.RollenMit(recht))
        {
            foreach (var konto in await benutzer.GetUsersInRoleAsync(rolle))
            {
                if (!konto.IstDeaktiviert)
                {
                    konten.Add(konto.Id);
                }
            }
        }

        return konten;
    }

    /// <summary>Zahl der ungelesenen Benachrichtigungen eines Kontos, für die Glocke.</summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Anzahl.</returns>
    public Task<int> UngeleseneAsync(Guid kontoId, CancellationToken ct = default) =>
        db.Benachrichtigungen.CountAsync(b => b.EmpfaengerId == kontoId && b.GelesenAm == null, ct);

    /// <summary>Die neuesten Benachrichtigungen eines Kontos.</summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="nurUngelesene">Nur ungelesene.</param>
    /// <param name="anzahl">Höchstzahl.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Benachrichtigungen, neueste zuerst.</returns>
    public async Task<IReadOnlyList<Benachrichtigung>> ListeAsync(Guid kontoId, bool nurUngelesene, int anzahl, CancellationToken ct = default) =>
        await db.Benachrichtigungen.AsNoTracking()
            .Where(b => b.EmpfaengerId == kontoId && (!nurUngelesene || b.GelesenAm == null))
            .OrderByDescending(b => b.ErstelltAm).ThenByDescending(b => b.Id)
            .Take(anzahl)
            .ToListAsync(ct);

    /// <summary>Vermerkt eine Benachrichtigung des Kontos als gelesen.</summary>
    /// <param name="kontoId">Das Konto; fremde Benachrichtigungen bleiben unberührt.</param>
    /// <param name="id">Die Benachrichtigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Benachrichtigung oder null, wenn sie dem Konto nicht gehört.</returns>
    public async Task<Benachrichtigung?> GelesenAsync(Guid kontoId, Guid id, CancellationToken ct = default)
    {
        var b = await db.Benachrichtigungen.FirstOrDefaultAsync(x => x.Id == id && x.EmpfaengerId == kontoId, ct);
        if (b is { GelesenAm: null })
        {
            b.GelesenAm = zeit.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        return b;
    }

    /// <summary>Vermerkt alle Benachrichtigungen des Kontos als gelesen.</summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der geänderten Benachrichtigungen.</returns>
    public Task<int> AlleGelesenAsync(Guid kontoId, CancellationToken ct = default)
    {
        var jetzt = zeit.GetUtcNow();
        return db.Benachrichtigungen.Where(b => b.EmpfaengerId == kontoId && b.GelesenAm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.GelesenAm, jetzt), ct);
    }

    /// <summary>Vermerkt die Benachrichtigungen zu einem Bezug als gelesen, z. B. wenn das Konto die Mitteilung öffnet.</summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="bezugId">Der Bezug.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der geänderten Benachrichtigungen.</returns>
    public Task<int> BezugGelesenAsync(Guid kontoId, Guid bezugId, CancellationToken ct = default)
    {
        var jetzt = zeit.GetUtcNow();
        return db.Benachrichtigungen.Where(b => b.EmpfaengerId == kontoId && b.BezugId == bezugId && b.GelesenAm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.GelesenAm, jetzt), ct);
    }
}
