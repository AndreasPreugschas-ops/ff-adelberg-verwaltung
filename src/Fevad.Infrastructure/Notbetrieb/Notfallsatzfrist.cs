using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Notbetrieb;

/// <summary>
/// Erinnert an den monatlichen Druck des Papier-Notfallsatzes (R18). Jeder Druck wird als <see cref="Betriebsmeldung"/> vermerkt; liegt der
/// letzte länger als der Parameter <see cref="ParameterSchluessel.NotfallsatzErneuernTage"/> zurück oder gab es noch keinen, erscheint der
/// Hinweis bei allen mit dem Recht <see cref="Berechtigungen.Notfallsatz"/>.
/// </summary>
public sealed class Notfallsatzfrist(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    /// <summary>Tag des letzten Drucks und der nächste fällige Tag.</summary>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Letzter Druck in UTC (leer, wenn nie gedruckt) und Fälligkeit des nächsten.</returns>
    public async Task<(DateTimeOffset? Letzter, DateOnly Faellig)> StandAsync(DateOnly stichtag, CancellationToken ct)
    {
        var tage = await parameter.ZahlAsync(ParameterSchluessel.NotfallsatzErneuernTage, stichtag, 31, ct);
        var letzter = await db.Betriebsmeldungen.AsNoTracking()
            .Where(m => m.Art == BetriebsmeldungArt.Notfallsatz && m.Erfolgreich)
            .MaxAsync(m => (DateTimeOffset?)m.Zeitpunkt, ct);
        return (letzter, letzter is { } l ? DateOnly.FromDateTime(l.UtcDateTime).AddDays(tage) : stichtag);
    }

    /// <summary>Vermerkt einen Druck.</summary>
    /// <param name="zeitpunkt">Zeitpunkt in UTC.</param>
    /// <param name="durch">Name der druckenden Person.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task GedrucktAsync(DateTimeOffset zeitpunkt, string? durch, CancellationToken ct)
    {
        db.Betriebsmeldungen.Add(new Betriebsmeldung { Art = BetriebsmeldungArt.Notfallsatz, Zeitpunkt = zeitpunkt, Erfolgreich = true, Text = durch });
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var (letzter, faellig) = await StandAsync(stichtag, ct);
        var ampel = Frist.Bewerten(faellig, stichtag, 3);
        return ampel == Fristampel.Gruen ? [] :
        [
            new Fristhinweis("Notbetrieb", "Papier-Notfallsatz", letzter is null ? "noch nie gedruckt" : "neu drucken und im Gerätehaus austauschen",
                faellig, letzter is null ? Fristampel.Rot : ampel, Berechtigungen.Notfallsatz),
        ];
    }
}
