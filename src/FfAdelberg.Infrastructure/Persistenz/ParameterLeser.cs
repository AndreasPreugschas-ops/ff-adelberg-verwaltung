using FfAdelberg.Domain.Gemeinsam;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Infrastructure.Persistenz;

/// <summary>Liest den am Stichtag gültigen Wert eines versionierten Parameters.</summary>
public sealed class ParameterLeser(AppDbContext db)
{
    public async Task<string?> WertAsync(string schluessel, DateOnly stichtag, CancellationToken ct = default) =>
        await db.Parameter
            .Where(p => p.Schluessel == schluessel
                && p.GueltigAb <= stichtag
                && (p.GueltigBis == null || p.GueltigBis >= stichtag))
            .OrderByDescending(p => p.GueltigAb)
            .Select(p => p.Wert)
            .FirstOrDefaultAsync(ct);

    public async Task<int> ZahlAsync(string schluessel, DateOnly stichtag, int ersatzwert, CancellationToken ct = default) =>
        int.TryParse(await WertAsync(schluessel, stichtag, ct), out var wert) ? wert : ersatzwert;
}
