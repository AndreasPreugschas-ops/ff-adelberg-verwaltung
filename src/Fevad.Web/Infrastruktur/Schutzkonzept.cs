using System.Globalization;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Prüftermin des Schutzkonzepts der Jugend- und Kinderfeuerwehr (F26). Der Termin steht als Parameter; jede Änderung legt einen neuen
/// Wert ab heute an, so bleibt nachvollziehbar, wann geprüft wurde.
/// </summary>
public sealed class Schutzkonzept(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Stand des Prüftermins.</summary>
    /// <param name="PruefungAm">Nächster Prüftermin; null, solange kein Schutzkonzept eingetragen ist.</param>
    /// <param name="IntervallMonate">Abstand der Prüfungen.</param>
    /// <param name="VorlaufTage">Erinnerung so viele Tage vorher.</param>
    /// <param name="Ampel">Rot überfällig, gelb bald, grün sonst; null ohne Termin.</param>
    public sealed record Stand(DateOnly? PruefungAm, int IntervallMonate, int VorlaufTage, Fristampel? Ampel);

    /// <summary>Liest den Prüftermin und bewertet ihn.</summary>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Stand.</returns>
    public async Task<Stand> StandAsync(DateOnly heute, CancellationToken ct)
    {
        var wert = await parameter.WertAsync(ParameterSchluessel.SchutzkonzeptPruefungAm, heute, ct);
        DateOnly? termin = DateOnly.TryParseExact(wert, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var intervall = await parameter.ZahlAsync(ParameterSchluessel.SchutzkonzeptPruefIntervallMonate, heute, 24, ct);
        var vorlauf = await parameter.ZahlAsync(ParameterSchluessel.SchutzkonzeptVorlaufTage, heute, 28, ct);
        Fristampel? ampel = termin switch
        {
            null => null,
            { } t when t < heute => Fristampel.Rot,
            { } t when t <= heute.AddDays(vorlauf) => Fristampel.Gelb,
            _ => Fristampel.Gruen,
        };
        return new(termin, intervall, vorlauf, ampel);
    }

    /// <summary>Setzt den nächsten Prüftermin ab heute.</summary>
    /// <param name="naechste">Neuer Prüftermin.</param>
    /// <param name="quelle">Vermerk, z. B. wer geprüft hat.</param>
    /// <param name="heute">Stichtag, ab dem der Wert gilt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task SetzenAsync(DateOnly naechste, string quelle, DateOnly heute, CancellationToken ct)
    {
        var wert = naechste.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == ParameterSchluessel.SchutzkonzeptPruefungAm && p.GueltigAb == heute, ct);
        if (vonHeute is null)
        {
            db.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.SchutzkonzeptPruefungAm, Wert = wert, GueltigAb = heute, Quelle = quelle });
        }
        else
        {
            vonHeute.Wert = wert;
            vonHeute.Quelle = quelle;
        }

        await db.SaveChangesAsync(ct);
    }
}
