using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>Prüft stündlich, ob der nächtliche Löschlauf fällig ist. Wann er läuft, entscheidet <see cref="Loeschlauf"/>.</summary>
public sealed class LoeschlaufDienst(IServiceScopeFactory scopes, TimeProvider zeit, ILogger<LoeschlaufDienst> log) : BackgroundService
{
    /// <summary>Fragt eine Minute nach dem Start und dann stündlich den Löschlauf, ob er fällig ist; Fehler landen im Protokoll, ohne die Anwendung anzuhalten.</summary>
    /// <param name="stoppingToken">Wird beim Herunterfahren der Anwendung ausgelöst und beendet die Schleife.</param>
    /// <returns>Aufgabe, die bis zum Herunterfahren läuft.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start abwarten, damit Migrationen und Seed durch sind.
        await Task.Delay(TimeSpan.FromMinutes(1), zeit, stoppingToken);

        using var takt = new PeriodicTimer(TimeSpan.FromHours(1), zeit);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<Loeschlauf>().FaelligAusfuehrenAsync(zeit.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Löschlauf fehlgeschlagen.");
            }
        }
        while (await takt.WaitForNextTickAsync(stoppingToken));
    }
}
