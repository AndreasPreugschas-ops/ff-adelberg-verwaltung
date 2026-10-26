using System.Security.Claims;
using Fevad.Domain.Kommunikation;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kommunikation;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitteilungen;

/// <summary>
/// Wer welche Mitteilungen schreiben und verwalten darf (V42): das Recht <see cref="Berechtigungen.MitteilungenSchreiben"/> muss alle
/// Zielabteilungen umfassen. Ohne Zugang zur Jugend fallen Jugend- und Kinderfeuerwehr heraus (Datenfreigabe).
/// </summary>
public sealed class Schreibrecht(Rechteverzeichnis rechte, AppDbContext db, Infrastructure.Kommunikation.Mitteilungen mitteilungen)
{
    /// <summary>Eine wählbare Abteilung.</summary>
    /// <param name="Id">Schlüssel.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Jugend">Jugend- oder Kinderfeuerwehr (Elternpost möglich).</param>
    public sealed record Abteilungswahl(Guid Id, string Bezeichnung, bool Jugend);

    /// <summary>Aktive Abteilungen, an die der Benutzer schreiben darf.</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Abteilungen in Reihenfolge der Stammdaten.</returns>
    public async Task<IReadOnlyList<Abteilungswahl>> AbteilungenAsync(ClaimsPrincipal benutzer, CancellationToken ct)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitteilungenSchreiben, ct);
        var alle = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung)
            .Select(a => new Abteilungswahl(a.Id, a.Bezeichnung, a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder)).ToListAsync(ct);
        return alle.Where(a => freigabe.Umfasst(a.Id)).ToList();
    }

    /// <summary>Darf der Benutzer diese Mitteilung bearbeiten, veröffentlichen und ihren Lesestand sehen?</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="m">Die Mitteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn das Recht alle Zielabteilungen umfasst.</returns>
    public async Task<bool> DarfAsync(ClaimsPrincipal benutzer, Mitteilung m, CancellationToken ct)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, Berechtigungen.MitteilungenSchreiben, ct);
        if (!freigabe.Irgendwo)
        {
            return false;
        }

        var ziel = await mitteilungen.ZielabteilungenAsync(m, ct);
        return ziel.Count > 0 ? ziel.All(freigabe.Umfasst) : freigabe.Alle;
    }
}
