using System.Security.Claims;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Policy-Anforderung: ein Recht für alle Abteilungen, oder bei abgeleiteten Policies für irgendeine (bzw. die Jugend).</summary>
public sealed record RechtAnforderung(string Recht, bool Abgeleitet, bool NurJugend) : IAuthorizationRequirement;

/// <summary>Für welche Abteilungen jemand ein Recht hat: alle, einzelne (mit Unterabteilungen) oder keine.</summary>
public sealed record Freigabe(bool Alle, IReadOnlySet<Guid> Abteilungen)
{
    public static readonly Freigabe Keine = new(false, new HashSet<Guid>());

    public bool Irgendwo => Alle || Abteilungen.Count > 0;

    public bool Umfasst(Guid abteilungId) => Alle || Abteilungen.Contains(abteilungId);
}

/// <summary>
/// Rechte aller Rollen aus der Datenbank, im Speicher gehalten. Nach einer Änderung an Rollen oder Abteilungen
/// <see cref="Verwerfen"/> aufrufen; spätestens nach fünf Minuten wird ohnehin neu geladen.
/// </summary>
public sealed class Rechteverzeichnis(IServiceScopeFactory scopes, TimeProvider zeit)
{
    private static readonly TimeSpan Gueltigkeit = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim sperre = new(1, 1);
    private Stand? stand;

    public sealed class Stand(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Freigabe>> rechte,
        IReadOnlySet<Guid> jugendabteilungen,
        IReadOnlyDictionary<string, string> bezeichnungen,
        DateTimeOffset geladen)
    {
        public DateTimeOffset Geladen { get; } = geladen;

        /// <summary>Abteilungen der Art Jugend oder Kinder.</summary>
        public IReadOnlySet<Guid> Jugendabteilungen { get; } = jugendabteilungen;

        public string Bezeichnung(string rolle) => bezeichnungen.GetValueOrDefault(rolle, Rollen.Bezeichnung(rolle));

        /// <summary>Rollen, die das Recht für alle Abteilungen haben (Empfänger von Erinnerungen, Leser von Dokumenten).</summary>
        public IReadOnlyList<string> RollenMit(string recht) =>
            rechte.Where(r => r.Value.TryGetValue(recht, out var f) && f.Alle).Select(r => r.Key).ToList();

        public Freigabe Freigabe(IEnumerable<string> rollen, string recht)
        {
            var abteilungen = new HashSet<Guid>();
            foreach (var rolle in rollen)
            {
                if (rechte.TryGetValue(rolle, out var je) && je.TryGetValue(recht, out var f))
                {
                    if (f.Alle)
                    {
                        return f;
                    }

                    abteilungen.UnionWith(f.Abteilungen);
                }
            }

            return abteilungen.Count == 0 ? Identitaet.Freigabe.Keine : new Freigabe(false, abteilungen);
        }
    }

    public void Verwerfen() => stand = null;

    public async Task<Freigabe> FreigabeAsync(ClaimsPrincipal benutzer, string recht, CancellationToken ct = default) =>
        (await StandAsync(ct)).Freigabe(benutzer.FindAll(ClaimTypes.Role).Select(c => c.Value), recht);

    public async Task<Stand> StandAsync(CancellationToken ct = default)
    {
        if (stand is { } aktuell && zeit.GetUtcNow() - aktuell.Geladen < Gueltigkeit)
        {
            return aktuell;
        }

        await sperre.WaitAsync(ct);
        try
        {
            if (stand is { } inzwischen && zeit.GetUtcNow() - inzwischen.Geladen < Gueltigkeit)
            {
                return inzwischen;
            }

            stand = await LadenAsync(ct);
            return stand;
        }
        finally
        {
            sperre.Release();
        }
    }

    private async Task<Stand> LadenAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var abteilungen = await db.Abteilungen.AsNoTracking().Select(a => new { a.Id, a.UebergeordneteId, a.Art }).ToListAsync(ct);
        var eintraege = await db.RollenRechte.AsNoTracking().ToListAsync(ct);
        var bezeichnungen = await db.Rollenprofile.AsNoTracking().ToDictionaryAsync(r => r.Name, r => r.Bezeichnung, ct);

        // Eine freigegebene Abteilung schließt ihre Unterabteilungen ein (Jugendfeuerwehr → Kinderfeuerwehr).
        HashSet<Guid> MitUnterabteilungen(IEnumerable<Guid> ids)
        {
            var menge = ids.ToHashSet();
            bool neu;
            do
            {
                neu = false;
                foreach (var a in abteilungen.Where(a => a.UebergeordneteId is { } o && menge.Contains(o)))
                {
                    neu |= menge.Add(a.Id);
                }
            }
            while (neu);

            return menge;
        }

        var rechte = eintraege.GroupBy(e => e.Rolle).ToDictionary(
            g => g.Key,
            g => (IReadOnlyDictionary<string, Freigabe>)g.ToDictionary(
                e => e.Recht,
                e => e.Abteilungen.Count == 0 ? new Freigabe(true, new HashSet<Guid>()) : new Freigabe(false, MitUnterabteilungen(e.Abteilungen))));
        var jugend = abteilungen.Where(a => a.Art is AbteilungArt.Jugend or AbteilungArt.Kinder).Select(a => a.Id).ToHashSet();
        return new Stand(rechte, jugend, bezeichnungen, zeit.GetUtcNow());
    }
}

/// <summary>Prüft <see cref="RechtAnforderung"/> gegen die Rollen-Claims des Benutzers.</summary>
public sealed class RechtHandler(Rechteverzeichnis verzeichnis) : AuthorizationHandler<RechtAnforderung>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RechtAnforderung anforderung)
    {
        var stand = await verzeichnis.StandAsync();
        var freigabe = stand.Freigabe(context.User.FindAll(ClaimTypes.Role).Select(c => c.Value), anforderung.Recht);
        var erfuellt = !anforderung.Abgeleitet ? freigabe.Alle
            : anforderung.NurJugend ? freigabe.Alle || freigabe.Abteilungen.Overlaps(stand.Jugendabteilungen)
            : freigabe.Irgendwo;
        if (erfuellt)
        {
            context.Succeed(anforderung);
        }
    }
}
