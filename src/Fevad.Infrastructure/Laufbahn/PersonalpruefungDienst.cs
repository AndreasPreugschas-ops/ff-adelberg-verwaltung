using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>
/// Arbeitet die <see cref="Pruefwarteschlange"/> ab: gemeldete Mitglieder werden kurz nach der Änderung geprüft.
/// Beim Start (Parameter <see cref="ParameterSchluessel.PruefungGesamtlaufBeimStart"/>) und danach täglich laufen alle,
/// weil Fristen und Altersgrenzen auch ohne Änderung erreicht werden.
/// </summary>
public sealed class PersonalpruefungDienst(
    Pruefwarteschlange warteschlange, IServiceScopeFactory scopes, TimeProvider zeit, ILogger<PersonalpruefungDienst> log) : BackgroundService
{
    private static readonly TimeSpan Sammelzeit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Gesamtabstand = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var naechsterGesamtlauf = zeit.GetUtcNow() + Gesamtabstand;
        if (await GesamtlaufBeimStartAsync(stoppingToken))
        {
            Leeren(out _);
            await PruefenAsync(null, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var warten = naechsterGesamtlauf - zeit.GetUtcNow();
            if (warten > TimeSpan.Zero)
            {
                using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                zeitlimit.CancelAfter(warten);
                try
                {
                    await warteschlange.Leser.WaitToReadAsync(zeitlimit.Token);
                    await Task.Delay(Sammelzeit, zeit, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Zeit für den täglichen Gesamtlauf.
                }
            }

            var ids = Leeren(out var alle);
            if (alle || zeit.GetUtcNow() >= naechsterGesamtlauf)
            {
                await PruefenAsync(null, stoppingToken);
                naechsterGesamtlauf = zeit.GetUtcNow() + Gesamtabstand;
            }
            else if (ids.Count > 0)
            {
                await PruefenAsync(ids, stoppingToken);
            }
        }
    }

    private HashSet<Guid> Leeren(out bool alle)
    {
        alle = false;
        var ids = new HashSet<Guid>();
        while (warteschlange.Leser.TryRead(out var id))
        {
            if (id is { } g)
            {
                ids.Add(g);
            }
            else
            {
                alle = true;
            }
        }

        return ids;
    }

    private async Task<bool> GesamtlaufBeimStartAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var heute = DateOnly.FromDateTime(zeit.GetLocalNow().DateTime);
        return await scope.ServiceProvider.GetRequiredService<ParameterLeser>().ZahlAsync(ParameterSchluessel.PruefungGesamtlaufBeimStart, heute, 1, ct) == 1;
    }

    private async Task PruefenAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<Personalpruefung>().PruefenAsync(ids, zeit.GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Personalprüfung fehlgeschlagen.");
        }
    }
}
