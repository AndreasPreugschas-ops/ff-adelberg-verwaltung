using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Fristen;

/// <summary>Prüft stündlich, ob eine Fristerinnerung fällig ist. Ob und wann sie verschickt wird, entscheidet <see cref="FristErinnerung"/>.</summary>
public sealed class FristErinnerungsDienst(IServiceScopeFactory scopes, TimeProvider zeit, ILogger<FristErinnerungsDienst> log) : BackgroundService
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
                await scope.ServiceProvider.GetRequiredService<FristErinnerung>().AusfuehrenAsync(zeit.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Fristerinnerung fehlgeschlagen.");
            }
        }
        while (await takt.WaitForNextTickAsync(stoppingToken));
    }
}
