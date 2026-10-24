using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Persistenz;

public static class OrganisationAbfrage
{
    /// <summary>Der eine Datensatz mit Feuerwehr, Gemeinde und Logo; vor dem ersten Start die Vorbelegung.</summary>
    public static async Task<Organisation> OrganisationAsync(this AppDbContext db, CancellationToken ct = default) =>
        await db.Organisationen.AsNoTracking().OrderBy(o => o.ErstelltAm).FirstOrDefaultAsync(ct) ?? Stammdaten.Organisation;
}
