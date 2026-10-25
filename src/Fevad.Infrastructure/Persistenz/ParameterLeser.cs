using Fevad.Domain.Gemeinsam;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Persistenz;

/// <summary>Liest den am Stichtag gültigen Wert eines versionierten Parameters.</summary>
public sealed class ParameterLeser(AppDbContext db)
{
    /// <summary>Der am Stichtag gültige Wert eines Parameters; bei mehreren der mit dem jüngsten Beginn.</summary>
    /// <param name="schluessel">Parameterschlüssel.</param>
    /// <param name="stichtag">Tag, an dem der Wert gelten muss.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Wert oder null, wenn keiner gilt.</returns>
    public async Task<string?> WertAsync(string schluessel, DateOnly stichtag, CancellationToken ct = default) =>
        await db.Parameter
            .Where(p => p.Schluessel == schluessel
                && p.GueltigAb <= stichtag
                && (p.GueltigBis == null || p.GueltigBis >= stichtag))
            .OrderByDescending(p => p.GueltigAb)
            .Select(p => p.Wert)
            .FirstOrDefaultAsync(ct);

    /// <summary>Der am Stichtag gültige Wert als ganze Zahl.</summary>
    /// <param name="schluessel">Parameterschlüssel.</param>
    /// <param name="stichtag">Tag, an dem der Wert gelten muss.</param>
    /// <param name="ersatzwert">Wert, wenn kein Parameter gilt oder der Wert keine Zahl ist.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl.</returns>
    public async Task<int> ZahlAsync(string schluessel, DateOnly stichtag, int ersatzwert, CancellationToken ct = default) =>
        int.TryParse(await WertAsync(schluessel, stichtag, ct), out var wert) ? wert : ersatzwert;

    /// <summary>Der am Stichtag gültige Wert als Dezimalzahl (Punkt oder Komma).</summary>
    /// <param name="schluessel">Parameterschlüssel.</param>
    /// <param name="stichtag">Tag, an dem der Wert gelten muss.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl oder null, wenn kein Parameter gilt oder der Wert keine Zahl ist.</returns>
    public async Task<decimal?> DezimalAsync(string schluessel, DateOnly stichtag, CancellationToken ct = default) =>
        Domain.Entschaedigung.Entschaedigungssaetze.Lesen(await WertAsync(schluessel, stichtag, ct));
}
