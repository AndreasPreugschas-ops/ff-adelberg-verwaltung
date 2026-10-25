using System.Security.Claims;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Welche Kassen jemand mit einem Kassenrecht sehen oder bearbeiten darf. Eine Kasse mit Abteilung (Jugendfeuerwehr) ist mit einem
/// Recht für diese Abteilung erreichbar, eine Kasse ohne Abteilung nur mit dem Recht für alle Abteilungen. Zahlungspartner der
/// Jugendkasse sehen nur Benutzer mit Zugang zur Jugendfeuerwehr, ausdrücklichem Recht für sie oder Buchungsrecht; das Hauptamt
/// sieht dort nur Summen (keine Jugenddaten, Andi 25.10.2026).
/// </summary>
public sealed class Kassenzugriff(AppDbContext db, Rechteverzeichnis rechte)
{
    /// <summary>Darf der Benutzer das Recht für diese Kasse ausüben?</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="recht">Ein Kassenrecht aus <see cref="Berechtigungen"/>.</param>
    /// <param name="kasse">Die Kasse.</param>
    /// <returns>true, wenn erlaubt.</returns>
    public async Task<bool> DarfAsync(ClaimsPrincipal benutzer, string recht, Kasse kasse)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, recht);
        return kasse.AbteilungId is { } abteilung ? freigabe.Umfasst(abteilung) : freigabe.Alle;
    }

    /// <summary>Kassen, für die der Benutzer das Recht hat, sortiert; abgeschaltete nur auf Wunsch.</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="recht">Ein Kassenrecht.</param>
    /// <param name="auchInaktive">Auch abgeschaltete Kassen liefern.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Kassen ohne Nachverfolgung.</returns>
    public async Task<IReadOnlyList<Kasse>> KassenAsync(ClaimsPrincipal benutzer, string recht, bool auchInaktive = false, CancellationToken ct = default)
    {
        var freigabe = await rechte.FreigabeAsync(benutzer, recht);
        if (!freigabe.Irgendwo)
        {
            return [];
        }

        var abteilungen = freigabe.Abteilungen.ToList();
        return await db.Kassen.AsNoTracking()
            .Where(k => auchInaktive || k.Aktiv)
            .Where(k => freigabe.Alle ? true : k.AbteilungId != null && abteilungen.Contains(k.AbteilungId.Value))
            .OrderBy(k => k.Sortierung).ThenBy(k => k.Name)
            .ToListAsync(ct);
    }

    /// <summary>Lädt eine Kasse, wenn der Benutzer das Recht für sie hat.</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="recht">Ein Kassenrecht.</param>
    /// <param name="kasseId">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Kasse oder null, wenn es sie nicht gibt oder das Recht fehlt.</returns>
    public async Task<Kasse?> LadenAsync(ClaimsPrincipal benutzer, string recht, Guid kasseId, CancellationToken ct = default)
    {
        var kasse = await db.Kassen.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kasseId, ct);
        return kasse is not null && await DarfAsync(benutzer, recht, kasse) ? kasse : null;
    }

    /// <summary>Darf der Benutzer die Zahlungspartner dieser Kasse sehen?</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="kasse">Die Kasse.</param>
    /// <returns>false nur bei einer Kasse der Jugend- oder Kinderfeuerwehr ohne Zugang zu ihr.</returns>
    public async Task<bool> PartnerSichtbarAsync(ClaimsPrincipal benutzer, Kasse kasse)
    {
        var stand = await rechte.StandAsync();
        if (kasse.AbteilungId is not { } abteilung || !stand.Jugendabteilungen.Contains(abteilung))
        {
            return true;
        }

        if (await rechte.JugendzugangAsync(benutzer) || (await rechte.FreigabeAsync(benutzer, Berechtigungen.KasseBuchen)).Umfasst(abteilung))
        {
            return true;
        }

        var lesen = await rechte.FreigabeAsync(benutzer, Berechtigungen.KasseLesen);
        return !lesen.Alle && lesen.Abteilungen.Contains(abteilung);
    }
}
