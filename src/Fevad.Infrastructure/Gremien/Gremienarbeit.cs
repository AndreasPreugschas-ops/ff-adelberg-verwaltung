using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fevad.Infrastructure.Gremien;

/// <summary>Beschlussnummern vergeben und Wahlergebnisse in die Funktionen der Mitglieder übernehmen.</summary>
public sealed class Gremienarbeit(AppDbContext db)
{
    private const int Versuche = 5;
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static DateOnly Sitzungstag(Sitzung sitzung) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(sitzung.Beginn, Berlin).DateTime);

    /// <summary>Speichert einen neuen Beschluss mit der nächsten laufenden Nummer im Jahr der Sitzung.</summary>
    public async Task BeschlussAnlegenAsync(Sitzung sitzung, Beschluss beschluss, CancellationToken ct = default)
    {
        beschluss.SitzungId = sitzung.Id;
        beschluss.Jahr = Sitzungstag(sitzung).Year;
        db.Beschluesse.Add(beschluss);

        for (var versuch = 1; ; versuch++)
        {
            // Gelöschte Beschlüsse zählen mit, ihre Nummer wird nicht neu vergeben.
            beschluss.Nummer = await db.Beschluesse.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.Jahr == beschluss.Jahr)
                .MaxAsync(b => (int?)b.Nummer, ct) is { } hoechste ? hoechste + 1 : 1;
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (versuch < Versuche
                && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var eintrag in db.ChangeTracker.Entries<AuditEintrag>().Where(e => e.State == EntityState.Added).ToList())
                {
                    eintrag.State = EntityState.Detached;
                }
            }
        }
    }

    /// <summary>
    /// Legt für die Gewählten die Funktion mit Amtszeit an. Auf Wunsch endet die Amtszeit der bisherigen
    /// gewählten Inhaber dieser Funktion am Tag vor dem neuen Amtsbeginn.
    /// </summary>
    public async Task<int> WahlUebernehmenAsync(Wahl wahl, bool bisherigeBeenden, CancellationToken ct = default)
    {
        if (wahl.Status == WahlStatus.Uebernommen)
        {
            throw new InvalidOperationException("Die Wahl wurde bereits übernommen.");
        }

        var sitzung = wahl.Sitzung ?? await db.Sitzungen.FirstAsync(s => s.Id == wahl.SitzungId, ct);
        var neue = wahl.FunktionenErzeugen(Sitzungstag(sitzung));
        if (neue.Count == 0)
        {
            throw new InvalidOperationException("Niemand ist gewählt und hat die Wahl angenommen.");
        }

        if (bisherigeBeenden)
        {
            var ende = wahl.Amtsbeginn!.Value.AddDays(-1);
            var bisherige = await db.FunktionZuordnungen
                .Where(f => f.FunktionId == wahl.FunktionId && f.GueltigAb <= ende && (f.GueltigBis == null || f.GueltigBis > ende))
                .ToListAsync(ct);
            bisherige.ForEach(f => f.GueltigBis = ende);
        }

        db.FunktionZuordnungen.AddRange(neue);
        wahl.Status = WahlStatus.Uebernommen;
        await db.SaveChangesAsync(ct);
        return neue.Count;
    }
}
