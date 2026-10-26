using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Löschlauf mit Archivanbietung (V65): erst anbieten, dann löschen.</summary>
internal static class Archivhilfe
{
    /// <summary>
    /// Erster Löschlauf nimmt die Unterlagen ins Anbietungsverzeichnis und löscht sie nicht; das Archiv bewertet sie als nicht archivwürdig;
    /// der zweite Lauf löscht. Liefert die Anzahlen beider Läufe zusammen.
    /// </summary>
    public static async Task<IReadOnlyList<Loeschlauf.Ergebnis>> AnbietenUndLoeschenAsync(
        IServiceProvider dienste, DateTimeOffset jetzt, Archivgut art, params Guid[] ids)
    {
        IReadOnlyList<Loeschlauf.Ergebnis> erster;
        await using (var scope = dienste.CreateAsyncScope())
        {
            erster = await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var angebote = await db.Archivangebote.Where(a => a.Art == art && ids.Contains(a.DatensatzId) && a.ErledigtAm == null).ToListAsync();
            Assert.Equal(ids.Length, angebote.Count);
            foreach (var a in angebote)
            {
                a.Entscheidung = Archiventscheidung.Kassieren;
            }

            await db.SaveChangesAsync();
        }

        await using var zweiter = dienste.CreateAsyncScope();
        var ergebnisse = await zweiter.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(jetzt, null, default);
        return ergebnisse.Select(e => e with { Anzahl = e.Anzahl + erster.Where(x => x.Regel == e.Regel).Sum(x => x.Anzahl) }).ToList();
    }
}
