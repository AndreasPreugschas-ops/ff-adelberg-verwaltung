using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Endgültig gelöschte Daten dürfen nicht im Änderungsprotokoll weiterleben. Die Einträge bleiben stehen
/// (wer hat wann was angelegt, geändert, gelöscht), nur die alten und neuen Feldwerte werden ersetzt.
/// </summary>
public static class Protokollschwaerzung
{
    public const string Geschwaerzt = """{"geschwaerzt": true}""";

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
