using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dokumente;

/// <summary>Speicher für Dateiinhalte. Gespeichert wird mit dem nächsten SaveChanges, also zusammen mit den Metadaten.</summary>
public interface IDateiablage
{
    Guid Speichern(byte[] inhalt);
    Task<byte[]?> LesenAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Legt Dateien als bytea in PostgreSQL ab. Damit sichert das nächtliche pg_dump sie mit,
/// und der Gerätehaus-PC hat sie über die Replikation auch offline. Kein eigenes Volume nötig.
/// </summary>
public sealed class DatenbankDateiablage(AppDbContext db) : IDateiablage
{
    public Guid Speichern(byte[] inhalt)
    {
        var datei = new DokumentDatei { Inhalt = inhalt };
        db.DokumentDateien.Add(datei);
        return datei.Id;
    }

    public async Task<byte[]?> LesenAsync(Guid id, CancellationToken ct = default) =>
        await db.DokumentDateien.AsNoTracking().Where(d => d.Id == id).Select(d => d.Inhalt).FirstOrDefaultAsync(ct);
}
