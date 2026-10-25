using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>Prüft stündlich, ob der nächtliche Löschlauf fällig ist. Wann er läuft, entscheidet <see cref="Loeschlauf"/>.</summary>
public sealed class LoeschlaufDienst(IServiceScopeFactory scopes, TimeProvider zeit, ILogger<LoeschlaufDienst> log) : BackgroundService
{
    /// <inheritdoc />
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
