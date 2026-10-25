using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Termine;

/// <summary>Zu-/Absage eines zugeordneten Kindes (Elternkonto) zu einem Termin.</summary>
/// <param name="MitgliedId">Schlüssel des Kindes.</param>
/// <param name="Vorname">Vorname des Kindes.</param>
/// <param name="Antwort">Zu- oder Absage; <c>null</c> ohne Rückmeldung.</param>
/// <param name="Begruendung">Begründung einer Absage.</param>
/// <param name="KannMelden">Zu- oder Absage ist (noch) möglich.</param>
/// <param name="KannEntschuldigen">Nach dem Rückmeldeschluss ist noch eine Entschuldigung möglich (Satzung § 5 Abs. 6).</param>
public sealed record Kindmeldung(Guid MitgliedId, string Vorname, RueckmeldungAntwort? Antwort, string? Begruendung, bool KannMelden, bool KannEntschuldigen = false);

/// <summary>Termin aus Sicht des angemeldeten Kontos: eigenes Mitglied und, bei Elternkonten, die betroffenen Kinder.</summary>
/// <param name="Betroffen">Das eigene Mitglied oder eines der Kinder gehört zur Zielgruppe.</param>
/// <param name="KannMelden">Eigene Zu- oder Absage ist (noch) möglich.</param>
/// <param name="Kinder">Kinder aus der Zielgruppe des Termins, je mit Antwort.</param>
/// <param name="Dienst">Der Dienst.</param>
/// <param name="Antwort">Eigene Zu- oder Absage; <c>null</c> ohne Rückmeldung.</param>
/// <param name="Begruendung">Begründung zur eigenen Absage.</param>
/// <param name="KannEntschuldigen">Nach dem Rückmeldeschluss kann sich das eigene Mitglied noch entschuldigen (Satzung § 5 Abs. 6).</param>
public sealed record TerminKarte(Dienst Dienst, RueckmeldungAntwort? Antwort, string? Begruendung, bool Betroffen, bool KannMelden, IReadOnlyList<Kindmeldung> Kinder,
    bool KannEntschuldigen = false)
{
    /// <summary>Für jemanden steht die Rückmeldung noch aus.</summary>
    public bool Offen => (KannMelden && Antwort is null) || Kinder.Any(k => k.KannMelden && k.Antwort is null);
}

/// <summary>Lädt Termine samt Rückmeldungen für die Personen des angemeldeten Kontos: das verknüpfte Mitglied und zugeordnete Kinder.</summary>
public sealed class Terminlader(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit)
{
    private List<(Dienstplanung.Person Person, Teilnehmerprofil Profil)>? _personen;

    private async Task<List<(Dienstplanung.Person Person, Teilnehmerprofil Profil)>> LadenAsync(CancellationToken ct)
    {
        if (_personen is null)
        {
            var personen = new List<(Dienstplanung.Person, Teilnehmerprofil)>();
            foreach (var person in await planung.PersonenAsync(benutzer.Id, ct))
            {
                personen.Add((person, await planung.ProfilAsync(person.MitgliedId, ct)));
            }

            _personen = personen;
        }

        return _personen;
    }

    /// <summary>Die Personen des Kontos: das verknüpfte Mitglied und zugeordnete Kinder.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Personen, einmal je Anfrage geladen.</returns>
    public async Task<IReadOnlyList<Dienstplanung.Person>> PersonenAsync(CancellationToken ct = default) =>
        (await LadenAsync(ct)).Select(p => p.Person).ToList();

    /// <summary>Das mit dem Konto verknüpfte Mitglied selbst.</summary>
    public async Task<Guid?> MitgliedIdAsync(CancellationToken ct = default) =>
        (await LadenAsync(ct)).FirstOrDefault(p => p.Person.Selbst).Person?.MitgliedId;

    /// <summary>Das Konto hat eine Zielgruppe: ein eigenes Mitglied oder zugeordnete Kinder.</summary>
    public async Task<bool> HatZielgruppeAsync(CancellationToken ct = default) => (await LadenAsync(ct)).Count > 0;

    /// <summary>Termine im Zeitraum [von, bis). Ohne <paramref name="alle"/> nur die der eigenen Zielgruppen (sofern es welche gibt).</summary>
    public async Task<List<TerminKarte>> KartenAsync(DateTimeOffset von, DateTimeOffset bis, bool alle, CancellationToken ct = default)
    {
        var personen = await LadenAsync(ct);
        var dienste = await planung.TermineAsync(von, bis, null, ct);
        if (!alle && personen.Count > 0)
        {
            dienste = dienste.Where(d => personen.Any(p => p.Profil.Betrifft(d, Dienstplanung.LokalesDatum(d.Beginn)))).ToList();
        }

        return await KartenAsync(dienste, ct);
    }

    /// <summary>Lädt einen einzelnen Termin als Karte.</summary>
    /// <param name="dienstId">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Karte oder <c>null</c>, wenn der Dienst fehlt.</returns>
    public async Task<TerminKarte?> KarteAsync(Guid dienstId, CancellationToken ct = default)
    {
        var dienst = await db.Dienste.AsNoTracking().MitZielgruppe()
            .FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        return dienst is null ? null : (await KartenAsync([dienst], ct))[0];
    }

    private async Task<List<TerminKarte>> KartenAsync(List<Dienst> dienste, CancellationToken ct)
    {
        var personen = await LadenAsync(ct);
        var ids = dienste.Select(d => d.Id).ToList();
        var mitglieder = personen.Select(p => p.Person.MitgliedId).ToList();
        var antworten = mitglieder.Count > 0
            ? await db.Rueckmeldungen.AsNoTracking()
                .Where(r => mitglieder.Contains(r.MitgliedId) && ids.Contains(r.DienstId))
                .ToDictionaryAsync(r => (r.DienstId, r.MitgliedId), ct)
            : [];
        var schluss = await planung.RueckmeldungSchlussStundenAsync(ct);
        var nachfrist = await planung.NachfristTageAsync(ct);
        var jetzt = zeit.GetUtcNow();
        var heute = Dienstplanung.LokalesDatum(jetzt);
        var selbst = personen.FirstOrDefault(p => p.Person.Selbst);

        return dienste.Select(d =>
        {
            var tag = Dienstplanung.LokalesDatum(d.Beginn);
            var moeglich = d.RueckmeldungMoeglich(jetzt, schluss);
            var entschuldigen = d.EntschuldigungMoeglich(jetzt, schluss, heute, tag, nachfrist);
            var betroffen = selbst.Profil?.Betrifft(d, tag) ?? false;
            antworten.TryGetValue((d.Id, selbst.Person?.MitgliedId ?? Guid.Empty), out var eigene);
            var kinder = personen.Where(p => !p.Person.Selbst && p.Profil.Betrifft(d, tag)).Select(p =>
            {
                antworten.TryGetValue((d.Id, p.Person.MitgliedId), out var r);
                return new Kindmeldung(p.Person.MitgliedId, p.Person.Vorname, r?.Antwort, r?.Begruendung, moeglich,
                    entschuldigen && r?.Antwort != RueckmeldungAntwort.Absage);
            }).ToList();
            return new TerminKarte(d, eigene?.Antwort, eigene?.Begruendung, betroffen || kinder.Count > 0, betroffen && moeglich, kinder,
                betroffen && entschuldigen && eigene?.Antwort != RueckmeldungAntwort.Absage);
        }).ToList();
    }
}

/// <summary>Daten des Formulars „nachträglich entschuldigen“ auf einer Terminkarte.</summary>
/// <param name="Url">Adresse des Rückmelde-Handlers, ggf. mit Kind.</param>
/// <param name="Schluessel">Eindeutiger Teil der Element-Kennungen.</param>
/// <param name="Beispiel">Platzhaltertext für die Begründung.</param>
public sealed record Entschuldigung(string Url, string Schluessel, string Beispiel);
