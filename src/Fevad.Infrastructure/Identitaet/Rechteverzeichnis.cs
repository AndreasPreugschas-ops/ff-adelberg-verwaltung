using System.Security.Claims;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>
/// Policy-Anforderung: ein Recht für alle Abteilungen, oder bei abgeleiteten Policies für irgendeine (bzw. die Jugend). Mit
/// <paramref name="Oder"/> genügt auch dieses zweite Recht für alle Abteilungen.
/// </summary>
/// <param name="Recht">Name des Rechts.</param>
/// <param name="Abgeleitet">Das Recht für irgendeine Abteilung genügt.</param>
/// <param name="NurJugend">Nur die Jugend- und Kinderabteilungen zählen.</param>
/// <param name="Oder">Zweites Recht, das die Anforderung ebenfalls erfüllt.</param>
public sealed record RechtAnforderung(string Recht, bool Abgeleitet, bool NurJugend, string? Oder = null) : IAuthorizationRequirement;

/// <summary>
/// Für welche Abteilungen jemand ein Recht hat: alle, einzelne (mit Unterabteilungen) oder keine. <paramref name="OhneJugend"/>
/// gesetzt heißt: alle außer diesen Jugend- und Kinderabteilungen (Recht für alle, aber ohne <see cref="Berechtigungen.JugendZugang"/>).
/// </summary>
public sealed record Freigabe(bool Alle, IReadOnlySet<Guid> Abteilungen, IReadOnlySet<Guid>? OhneJugend = null)
{
    /// <summary>Kein Recht in keiner Abteilung.</summary>
    public static readonly Freigabe Keine = new(false, new HashSet<Guid>());

    /// <summary>Das Recht gilt in mindestens einer Abteilung.</summary>
    public bool Irgendwo => Alle || Abteilungen.Count > 0;

    /// <summary>Prüft, ob die Freigabe eine Abteilung umfasst.</summary>
    /// <param name="abteilungId">Schlüssel der Abteilung.</param>
    /// <returns>true bei Freigabe für alle oder für diese Abteilung.</returns>
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

    /// <summary>Geladener Stand der Rechte aller Rollen, unveränderlich bis zum nächsten Laden.</summary>
    /// <param name="rechte">Freigaben je Rolle und Recht.</param>
    /// <param name="abteilungen">Schlüssel aller Abteilungen.</param>
    /// <param name="jugendabteilungen">Schlüssel der Jugend- und Kinderabteilungen.</param>
    /// <param name="bezeichnungen">Anzeigenamen je Rolle aus den Rollenprofilen.</param>
    /// <param name="geladen">Zeitpunkt des Ladens.</param>
    public sealed class Stand(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Freigabe>> rechte,
        IReadOnlySet<Guid> abteilungen,
        IReadOnlySet<Guid> jugendabteilungen,
        IReadOnlyDictionary<string, string> bezeichnungen,
        DateTimeOffset geladen)
    {
        /// <summary>Zeitpunkt, zu dem der Stand geladen wurde; danach gilt er fünf Minuten.</summary>
        public DateTimeOffset Geladen { get; } = geladen;

        /// <summary>Abteilungen der Art Jugend oder Kinder.</summary>
        public IReadOnlySet<Guid> Jugendabteilungen { get; } = jugendabteilungen;

        /// <summary>Anzeigename einer Rolle, mit dem gepflegten Namen vor der Vorbelegung.</summary>
        /// <param name="rolle">Technischer Rollenname.</param>
        /// <returns>Der Anzeigename.</returns>
        public string Bezeichnung(string rolle) => bezeichnungen.GetValueOrDefault(rolle, Rollen.Bezeichnung(rolle));

        /// <summary>Rollen, die das Recht für alle Abteilungen haben (Empfänger von Erinnerungen, Leser von Dokumenten).</summary>
        public IReadOnlyList<string> RollenMit(string recht) =>
            rechte.Where(r => r.Value.TryGetValue(recht, out var f) && f.Alle).Select(r => r.Key).ToList();

        /// <summary>Vereinigt die Freigaben mehrerer Rollen für ein Recht.</summary>
        /// <param name="rollen">Rollen des Benutzers.</param>
        /// <param name="recht">Name des Rechts.</param>
        /// <returns>Die gemeinsame Freigabe.</returns>
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

        /// <summary>Darf die Jugend- oder Kinderfeuerwehr betreten: Jugend.Zugang oder Mitglieder.Lesen ausdrücklich für sie.</summary>
        public bool Jugendzugang(IEnumerable<string> rollen)
        {
            var liste = rollen.ToList();
            return Freigabe(liste, Berechtigungen.JugendZugang).Alle
                || Freigabe(liste, Berechtigungen.MitgliederLesen) is { Alle: false } f && f.Abteilungen.Overlaps(Jugendabteilungen);
        }

        /// <summary>
        /// Freigabe für Daten: Rechte für alle Abteilungen mit Bezug zu Mitgliedern lassen ohne Zugang zur Jugendfeuerwehr deren
        /// Abteilungen aus.
        /// </summary>
        public Freigabe Datenfreigabe(IEnumerable<string> rollen, string recht)
        {
            var liste = rollen.ToList();
            var freigabe = Freigabe(liste, recht);
            if (!freigabe.Alle || !Berechtigungen.MitJugendbezug.Contains(recht) || Jugendabteilungen.Count == 0 || Jugendzugang(liste))
            {
                return freigabe;
            }

            return new Freigabe(false, abteilungen.Except(Jugendabteilungen).ToHashSet(), Jugendabteilungen);
        }
    }

    /// <summary>Verwirft den gespeicherten Stand; der nächste Zugriff lädt die Rechte neu.</summary>
    public void Verwerfen() => stand = null;

    /// <summary>Für welche Abteilungen jemand Daten zu diesem Recht sehen bzw. bearbeiten darf (<see cref="Stand.Datenfreigabe"/>).</summary>
    public async Task<Freigabe> FreigabeAsync(ClaimsPrincipal benutzer, string recht, CancellationToken ct = default) =>
        (await StandAsync(ct)).Datenfreigabe(RollenVon(benutzer), recht);

    /// <summary>Darf der Benutzer Daten der Jugend- und Kinderfeuerwehr sehen (<see cref="Berechtigungen.JugendZugang"/>)?</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true mit Jugendzugang.</returns>
    public async Task<bool> JugendzugangAsync(ClaimsPrincipal benutzer, CancellationToken ct = default) =>
        (await StandAsync(ct)).Jugendzugang(RollenVon(benutzer));

    private static IEnumerable<string> RollenVon(ClaimsPrincipal benutzer) => benutzer.FindAll(ClaimTypes.Role).Select(c => c.Value);

    /// <summary>Der aktuelle Stand; älter als fünf Minuten oder verworfen wird er neu aus der Datenbank geladen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Stand der Rechte.</returns>
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
        return new Stand(rechte, abteilungen.Select(a => a.Id).ToHashSet(), jugend, bezeichnungen, zeit.GetUtcNow());
    }
}

/// <summary>Prüft <see cref="RechtAnforderung"/> gegen die Rollen-Claims des Benutzers.</summary>
public sealed class RechtHandler(Rechteverzeichnis verzeichnis) : AuthorizationHandler<RechtAnforderung>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RechtAnforderung anforderung)
    {
        var stand = await verzeichnis.StandAsync();
        var rollen = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var freigabe = anforderung.NurJugend ? stand.Datenfreigabe(rollen, anforderung.Recht) : stand.Freigabe(rollen, anforderung.Recht);
        var erfuellt = !anforderung.Abgeleitet ? freigabe.Alle
            : anforderung.NurJugend ? freigabe.Alle || freigabe.Abteilungen.Overlaps(stand.Jugendabteilungen)
            : freigabe.Irgendwo;
        erfuellt = erfuellt || (anforderung.Oder is { } oder && stand.Freigabe(rollen, oder).Alle);
        if (erfuellt)
        {
            context.Succeed(anforderung);
        }
    }
}
