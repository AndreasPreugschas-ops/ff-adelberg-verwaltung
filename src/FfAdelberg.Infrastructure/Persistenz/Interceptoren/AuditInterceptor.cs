using System.Text.Json;
using FfAdelberg.Domain.Gemeinsam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FfAdelberg.Infrastructure.Persistenz.Interceptoren;

/// <summary>
/// Setzt die Audit-Felder, wandelt Löschungen in Soft-Deletes um und schreibt
/// jede Änderung an einer Entität in die Tabelle audit_eintraege.
/// </summary>
public sealed class AuditInterceptor(IAktuellerBenutzer aktuellerBenutzer, TimeProvider zeit) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Protokollieren(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Protokollieren(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Protokollieren(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var jetzt = zeit.GetUtcNow();
        var benutzer = aktuellerBenutzer.Id;
        var eintraege = new List<AuditEintrag>();

        foreach (var entry in context.ChangeTracker.Entries<Entitaet>().ToList())
        {
            AuditAktion aktion;
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.ErstelltAm = jetzt;
                    entry.Entity.ErstelltVon = benutzer;
                    aktion = AuditAktion.Angelegt;
                    break;
                case EntityState.Modified:
                    entry.Entity.GeaendertAm = jetzt;
                    entry.Entity.GeaendertVon = benutzer;
                    aktion = AuditAktion.Geaendert;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.GeloeschtAm = jetzt;
                    entry.Entity.GeaendertAm = jetzt;
                    entry.Entity.GeaendertVon = benutzer;
                    aktion = AuditAktion.Geloescht;
                    break;
                default:
                    continue;
            }

            eintraege.Add(new AuditEintrag
            {
                Zeitpunkt = jetzt,
                BenutzerId = benutzer,
                Tabelle = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                DatensatzId = entry.Entity.Id,
                Aktion = aktion,
                Aenderungen = Aenderungen(entry, aktion),
            });
        }

        context.Set<AuditEintrag>().AddRange(eintraege);
    }

    private static readonly HashSet<string> TechnischeFelder =
    [
        nameof(Entitaet.ErstelltAm), nameof(Entitaet.ErstelltVon),
        nameof(Entitaet.GeaendertAm), nameof(Entitaet.GeaendertVon), "Version",
    ];

    private static string? Aenderungen(EntityEntry<Entitaet> entry, AuditAktion aktion)
    {
        var felder = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (TechnischeFelder.Contains(name))
            {
                continue;
            }

            if (aktion == AuditAktion.Angelegt)
            {
                felder[name] = new { neu = property.CurrentValue };
            }
            else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
            {
                felder[name] = new { alt = property.OriginalValue, neu = property.CurrentValue };
            }
        }

        return felder.Count == 0 ? null : JsonSerializer.Serialize(felder);
    }
}
