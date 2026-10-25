using System.Security.Cryptography;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dokumente;

/// <summary>Hochladen, Ausliefern, Lesebestätigungen und endgültiges Löschen von Dokumenten.</summary>
public sealed class Dokumentablage(AppDbContext db, IDateiablage dateien, ParameterLeser parameter, Rechteverzeichnis rechte, TimeProvider zeit)
{
    /// <summary>Größte Dateigröße in Megabyte, solange kein Parameter gepflegt ist.</summary>
    public const int StandardMaxMegabyte = 20;

    /// <summary>Ein Leser einer Dokumentversion mit seiner Lesebestätigung.</summary>
    /// <param name="BenutzerId">Schlüssel des Kontos.</param>
    /// <param name="Name">Anzeigename.</param>
    /// <param name="BestaetigtAm">Zeitpunkt der Bestätigung; leer, solange nicht bestätigt.</param>
    public sealed record Leser(Guid BenutzerId, string Name, DateTimeOffset? BestaetigtAm);

    /// <summary>Größte zulässige Dateigröße in Byte nach dem Parameter <see cref="ParameterSchluessel.DokumentMaxMegabyte"/>.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Größe in Byte.</returns>
    public async Task<long> MaxBytesAsync(CancellationToken ct = default)
    {
        var heute = DateOnly.FromDateTime(zeit.GetUtcNow().UtcDateTime);
        return await parameter.ZahlAsync(ParameterSchluessel.DokumentMaxMegabyte, heute, StandardMaxMegabyte, ct) * 1024L * 1024L;
    }

    /// <summary>Prüft Endung und Größe. Gibt eine Fehlermeldung zurück oder null.</summary>
    public async Task<string?> PruefenAsync(string dateiname, long groesse, CancellationToken ct = default)
    {
        if (groesse == 0)
        {
            return "Die Datei ist leer.";
        }

        if (Dateitypen.Inhaltstyp(dateiname) is null)
        {
            return $"Dateityp nicht erlaubt. Erlaubt sind {string.Join(", ", Dateitypen.Endungen)}.";
        }

        var max = await MaxBytesAsync(ct);
        return groesse > max ? $"Die Datei ist größer als {max / 1024 / 1024} MB." : null;
    }

    /// <summary>Hängt eine neue Version an. Gespeichert wird erst mit SaveChanges.</summary>
    public async Task<DokumentVersion> VersionHinzufuegenAsync(Dokument dokument, string dateiname, Stream inhalt, string? aenderungshinweis, CancellationToken ct = default)
    {
        var name = Dateitypen.Bereinigen(dateiname);
        var inhaltstyp = Dateitypen.Inhaltstyp(name) ?? throw new InvalidOperationException("Dateityp nicht erlaubt.");

        using var puffer = new MemoryStream();
        await inhalt.CopyToAsync(puffer, ct);
        var bytes = puffer.ToArray();
        if (bytes.Length > await MaxBytesAsync(ct))
        {
            throw new InvalidOperationException("Datei zu groß.");
        }

        var version = new DokumentVersion
        {
            DokumentId = dokument.Id,
            Nummer = dokument.NaechsteVersionsnummer,
            Dateiname = name,
            Inhaltstyp = inhaltstyp,
            Groesse = bytes.Length,
            Pruefsumme = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            DateiId = dateien.Speichern(bytes),
            Aenderungshinweis = string.IsNullOrWhiteSpace(aenderungshinweis) ? null : aenderungshinweis.Trim(),
        };
        dokument.Versionen.Add(version);
        // Ausdrücklich als neu markieren: mit gesetztem Schlüssel hielte EF die Version sonst für vorhanden.
        db.DokumentVersionen.Add(version);
        return version;
    }

    /// <summary>Version samt Inhalt, wenn der Benutzer sie sehen darf; sonst null.</summary>
    public async Task<(DokumentVersion Version, byte[] Inhalt)?> HerunterladenAsync(Guid versionId, bool darfVertrauliche, CancellationToken ct = default)
    {
        var version = await db.DokumentVersionen.AsNoTracking().Include(v => v.Dokument)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version?.Dokument is null || (version.Dokument.Sichtbarkeit == DokumentSichtbarkeit.Fuehrung && !darfVertrauliche))
        {
            return null;
        }

        var inhalt = await dateien.LesenAsync(version.DateiId, ct);
        return inhalt is null ? null : (version, inhalt);
    }

    /// <summary>Bestätigt das Lesen einer Version. Mehrfaches Bestätigen ändert nichts.</summary>
    public async Task BestaetigenAsync(Guid versionId, Guid benutzerId, CancellationToken ct = default)
    {
        if (await db.Lesebestaetigungen.AnyAsync(l => l.DokumentVersionId == versionId && l.BenutzerId == benutzerId, ct))
        {
            return;
        }

        db.Lesebestaetigungen.Add(new Lesebestaetigung { DokumentVersionId = versionId, BenutzerId = benutzerId, BestaetigtAm = zeit.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Alle aktiven Konten, die das Dokument lesen dürfen, mit Zeitpunkt ihrer Bestätigung der Version.
    /// Wer eine ältere Version bestätigt hat, steht wieder auf offen.
    /// </summary>
    public async Task<IReadOnlyList<Leser>> BestaetigungsstandAsync(Guid versionId, DokumentSichtbarkeit sichtbarkeit, CancellationToken ct = default)
    {
        var rollen = (await rechte.StandAsync(ct)).RollenMit(
            sichtbarkeit == DokumentSichtbarkeit.Fuehrung ? Berechtigungen.DokumenteVertraulichLesen : Berechtigungen.DokumenteLesen);

        var konten = await (
            from b in db.Users
            where b.DeaktiviertAm == null
                && db.UserRoles.Any(ur => ur.UserId == b.Id && db.Roles.Any(r => r.Id == ur.RoleId && rollen.Contains(r.Name!)))
            join m in db.Mitglieder on b.MitgliedId equals m.Id into mitglieder
            from m in mitglieder.DefaultIfEmpty()
            select new { b.Id, Name = m != null ? m.Nachname + ", " + m.Vorname : b.Email ?? b.UserName ?? "" })
            .AsNoTracking()
            .ToListAsync(ct);

        var bestaetigt = await db.Lesebestaetigungen.AsNoTracking()
            .Where(l => l.DokumentVersionId == versionId)
            .ToDictionaryAsync(l => l.BenutzerId, l => l.BestaetigtAm, ct);

        return konten
            .Select(k => new Leser(k.Id, k.Name, bestaetigt.TryGetValue(k.Id, out var am) ? am : null))
            .OrderBy(l => l.BestaetigtAm is not null)
            .ThenBy(l => l.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Löscht ein Dokument mit allen Versionen, Dateien und Lesebestätigungen endgültig (L29) und schwärzt das Protokoll.
    /// Verweise aus Sitzungen werden geleert. Gibt die Zahl der gelöschten Versionen zurück, null wenn es das Dokument nicht gibt.
    /// </summary>
    public async Task<int?> EndgueltigLoeschenAsync(Guid dokumentId, Guid? benutzerId, CancellationToken ct = default)
    {
        if (!await db.Dokumente.IgnoreQueryFilters().AnyAsync(d => d.Id == dokumentId, ct))
        {
            return null;
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        await db.Sitzungen.IgnoreQueryFilters().Where(s => s.ProtokollDokumentId == dokumentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ProtokollDokumentId, (Guid?)null), ct);
        await db.QualifikationNachweise.IgnoreQueryFilters().Where(q => q.DokumentId == dokumentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DokumentId, (Guid?)null), ct);

        var versionen = await VersionenLoeschenAsync(db.DokumentVersionen.IgnoreQueryFilters().Where(v => v.DokumentId == dokumentId), ct);
        await db.Dokumente.IgnoreQueryFilters().Where(d => d.Id == dokumentId).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Dokument>([dokumentId], ct);

        db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = zeit.GetUtcNow(), Regel = Loeschlauf.RegelDokument, Anzahl = versionen, AusgeloestVon = benutzerId });
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
        return versionen;
    }

    /// <summary>Löscht alle Versionen vor der jüngsten (eine gleichzeitig hochgeladene neuere bleibt) samt Dateien und Lesebestätigungen. Gibt die Zahl der gelöschten Versionen zurück.</summary>
    public async Task<int> AlteVersionenLoeschenAsync(Guid dokumentId, Guid? benutzerId, CancellationToken ct = default)
    {
        var juengste = await db.DokumentVersionen.Where(v => v.DokumentId == dokumentId)
            .MaxAsync(v => (int?)v.Nummer, ct);
        if (juengste is null)
        {
            return 0;
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var versionen = await VersionenLoeschenAsync(
            db.DokumentVersionen.IgnoreQueryFilters().Where(v => v.DokumentId == dokumentId && v.Nummer < juengste), ct);
        if (versionen > 0)
        {
            db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = zeit.GetUtcNow(), Regel = Loeschlauf.RegelDokumentVersionen, Anzahl = versionen, AusgeloestVon = benutzerId });
            await db.SaveChangesAsync(ct);
        }

        await transaktion.CommitAsync(ct);
        return versionen;
    }

    private async Task<int> VersionenLoeschenAsync(IQueryable<DokumentVersion> auswahl, CancellationToken ct)
    {
        var versionen = await auswahl.Select(v => new { v.Id, v.DateiId }).ToListAsync(ct);
        if (versionen.Count == 0)
        {
            return 0;
        }

        var versionIds = versionen.Select(v => v.Id).ToList();
        var dateiIds = versionen.Select(v => v.DateiId).Distinct().ToList();
        var bestaetigungen = await db.Lesebestaetigungen.IgnoreQueryFilters()
            .Where(l => versionIds.Contains(l.DokumentVersionId)).Select(l => l.Id).ToListAsync(ct);

        await db.Lesebestaetigungen.IgnoreQueryFilters().Where(l => bestaetigungen.Contains(l.Id)).ExecuteDeleteAsync(ct);
        await db.DokumentVersionen.IgnoreQueryFilters().Where(v => versionIds.Contains(v.Id)).ExecuteDeleteAsync(ct);
        // Eine Datei kann theoretisch von mehreren Versionen genutzt werden; nur verwaiste löschen.
        await db.DokumentDateien.IgnoreQueryFilters()
            .Where(d => dateiIds.Contains(d.Id) && !db.DokumentVersionen.IgnoreQueryFilters().Any(v => v.DateiId == d.Id))
            .ExecuteDeleteAsync(ct);

        await db.ProtokollSchwaerzenAsync<Lesebestaetigung>(bestaetigungen, ct);
        await db.ProtokollSchwaerzenAsync<DokumentVersion>(versionIds, ct);
        await db.ProtokollSchwaerzenAsync<DokumentDatei>(dateiIds, ct);
        return versionen.Count;
    }
}
