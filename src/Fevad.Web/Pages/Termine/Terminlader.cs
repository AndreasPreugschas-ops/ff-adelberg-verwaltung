using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Termine;

/// <summary>Termin aus Sicht des angemeldeten Mitglieds.</summary>
/// <param name="Betroffen">Das Mitglied gehört zur Zielgruppe.</param>
/// <param name="KannMelden">Zu- oder Absage ist (noch) möglich.</param>
public sealed record TerminKarte(Dienst Dienst, RueckmeldungAntwort? Antwort, string? Begruendung, bool Betroffen, bool KannMelden);

/// <summary>Lädt Termine samt eigener Rückmeldung für das Mitglied, das mit dem angemeldeten Konto verknüpft ist.</summary>
public sealed class Terminlader(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit)
{
    private bool _geladen;
    private Guid? _mitgliedId;
    private Teilnehmerprofil? _profil;

    public async Task<Guid?> MitgliedIdAsync(CancellationToken ct = default)
    {
        if (!_geladen)
        {
            _mitgliedId = await planung.MitgliedIdAsync(benutzer.Id, ct);
            _profil = _mitgliedId is { } id ? await planung.ProfilAsync(id, ct) : null;
            _geladen = true;
        }

        return _mitgliedId;
    }

    /// <summary>Termine im Zeitraum [von, bis). Ohne <paramref name="alle"/> nur die der eigenen Zielgruppe (sofern ein Mitglied verknüpft ist).</summary>
    public async Task<List<TerminKarte>> KartenAsync(DateTimeOffset von, DateTimeOffset bis, bool alle, CancellationToken ct = default)
    {
        await MitgliedIdAsync(ct);
        var dienste = await planung.TermineAsync(von, bis, alle ? null : _profil, ct);
        return await KartenAsync(dienste, ct);
    }

    public async Task<TerminKarte?> KarteAsync(Guid dienstId, CancellationToken ct = default)
    {
        var dienst = await db.Dienste.AsNoTracking().MitZielgruppe()
            .FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        return dienst is null ? null : (await KartenAsync([dienst], ct))[0];
    }

    private async Task<List<TerminKarte>> KartenAsync(List<Dienst> dienste, CancellationToken ct)
    {
        await MitgliedIdAsync(ct);
        var ids = dienste.Select(d => d.Id).ToList();
        var antworten = _mitgliedId is { } mitgliedId
            ? await db.Rueckmeldungen.AsNoTracking()
                .Where(r => r.MitgliedId == mitgliedId && ids.Contains(r.DienstId))
                .ToDictionaryAsync(r => r.DienstId, ct)
            : [];
        var schluss = await planung.RueckmeldungSchlussStundenAsync(ct);
        var jetzt = zeit.GetUtcNow();

        return dienste.Select(d =>
        {
            var betroffen = _profil?.Betrifft(d, Dienstplanung.LokalesDatum(d.Beginn)) ?? false;
            antworten.TryGetValue(d.Id, out var r);
            return new TerminKarte(d, r?.Antwort, r?.Begruendung, betroffen, betroffen && d.RueckmeldungMoeglich(jetzt, schluss));
        }).ToList();
    }
}
