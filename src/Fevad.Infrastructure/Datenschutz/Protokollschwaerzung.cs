using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Endgültig gelöschte Daten dürfen nicht im Änderungsprotokoll weiterleben. Die Einträge bleiben stehen
/// (wer hat wann was angelegt, geändert, gelöscht), nur die alten und neuen Feldwerte werden ersetzt.
/// </summary>
public static class Protokollschwaerzung
{
    /// <summary>Ersatzwert für die Feldwerte geschwärzter Protokolleinträge.</summary>
    public const string Geschwaerzt = """{"geschwaerzt": true}""";

    /// <summary>Ersetzt die Feldwerte aller Protokolleinträge zu den angegebenen Datensätzen durch <see cref="Geschwaerzt"/>.</summary>
    /// <typeparam name="T">Entitätstyp; bestimmt die Tabelle.</typeparam>
    /// <param name="db">Der Datenbankkontext.</param>
    /// <param name="ids">Schlüssel der endgültig gelöschten Datensätze.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl der geschwärzten Einträge.</returns>
    public static async Task<int> ProtokollSchwaerzenAsync<T>(this AppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var tabelle = db.Model.FindEntityType(typeof(T))?.GetTableName() ?? typeof(T).Name;
        return await db.AuditEintraege
            .Where(a => a.Tabelle == tabelle && ids.Contains(a.DatensatzId) && a.Aenderungen != null && a.Aenderungen != Geschwaerzt)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Aenderungen, Geschwaerzt), ct);
    }
}
