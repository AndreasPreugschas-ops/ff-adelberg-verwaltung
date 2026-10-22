using System.Reflection;
using System.Text.Json;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fevad.Infrastructure.Persistenz.Interceptoren;

/// <summary>
/// Setzt die Audit-Felder, wandelt Löschungen in Soft-Deletes um und schreibt
/// jede Änderung an einer Entität in die Tabelle audit_eintraege.
/// Konten und Rollenvergaben werden ebenfalls protokolliert, Passwort-Hash und Sicherheitsstempel nur als „geändert“.
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

        foreach (var entry in context.ChangeTracker.Entries<Benutzer>().ToList())
        {
            if (Aktion(entry.State) is not { } aktion || Aenderungen(entry, aktion, KontoTechnischeFelder, KontoGeheimeFelder) is not { } aenderungen)
            {
                continue;
            }

            eintraege.Add(new AuditEintrag
            {
                Zeitpunkt = jetzt,
                BenutzerId = benutzer,
                Tabelle = entry.Metadata.GetTableName() ?? nameof(Benutzer),
                DatensatzId = entry.Entity.Id,
                Aktion = aktion,
                Aenderungen = aenderungen,
            });
        }

        foreach (var entry in context.ChangeTracker.Entries<IdentityUserRole<Guid>>().ToList())
        {
            if (Aktion(entry.State) is not { } aktion)
            {
                continue;
            }

            var rolle = context.Set<IdentityRole<Guid>>().Local.FirstOrDefault(r => r.Id == entry.Entity.RoleId)?.Name
                ?? entry.Entity.RoleId.ToString();
            eintraege.Add(new AuditEintrag
            {
                Zeitpunkt = jetzt,
                BenutzerId = benutzer,
                Tabelle = entry.Metadata.GetTableName() ?? "Benutzerrollen",
                DatensatzId = entry.Entity.UserId,
                Aktion = aktion,
                Aenderungen = JsonSerializer.Serialize(new { Rolle = rolle }),
            });
        }

        context.Set<AuditEintrag>().AddRange(eintraege);
    }

    private static AuditAktion? Aktion(EntityState zustand) => zustand switch
    {
        EntityState.Added => AuditAktion.Angelegt,
        EntityState.Modified => AuditAktion.Geaendert,
        EntityState.Deleted => AuditAktion.Geloescht,
        _ => null,
    };

    /// <summary>Ändern sich bei jeder Anmeldung; allein kein Protokolleintrag.</summary>
    private static readonly HashSet<string> KontoTechnischeFelder =
    [
        nameof(Benutzer.ConcurrencyStamp), nameof(Benutzer.AccessFailedCount), nameof(Benutzer.LetzteAnmeldungAm),
        nameof(Benutzer.NormalizedEmail), nameof(Benutzer.NormalizedUserName),
    ];

    private static readonly HashSet<string> KontoGeheimeFelder = [nameof(Benutzer.PasswordHash), nameof(Benutzer.SecurityStamp)];

    private static readonly HashSet<string> TechnischeFelder =
    [
        nameof(Entitaet.ErstelltAm), nameof(Entitaet.ErstelltVon),
        nameof(Entitaet.GeaendertAm), nameof(Entitaet.GeaendertVon), "Version",
    ];

    private static string? Aenderungen(EntityEntry<Entitaet> entry, AuditAktion aktion) =>
        Aenderungen(entry, aktion, TechnischeFelder, []);

    private static string? Aenderungen(EntityEntry entry, AuditAktion aktion, HashSet<string> technisch, HashSet<string> geheim)
    {
        var felder = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (technisch.Contains(name))
            {
                continue;
            }

            var geaendert = aktion == AuditAktion.Angelegt
                || (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue));
            if (!geaendert)
            {
                continue;
            }

            if (geheim.Contains(name) || property.Metadata.PropertyInfo?.GetCustomAttribute<NichtProtokollierenAttribute>() is not null)
            {
                felder[name] = new { geaendert = true };
            }
            else if (aktion == AuditAktion.Angelegt)
            {
                felder[name] = new { neu = property.CurrentValue };
            }
            else
            {
                felder[name] = new { alt = property.OriginalValue, neu = property.CurrentValue };
            }
        }

        return felder.Count == 0 ? null : JsonSerializer.Serialize(felder);
    }
}
