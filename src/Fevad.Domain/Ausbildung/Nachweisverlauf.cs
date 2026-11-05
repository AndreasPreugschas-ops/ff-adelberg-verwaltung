using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Ausbildung;

/// <summary>
/// Verlauf befristeter Einträge der Personalakte. Verlängern legt immer einen neuen Datensatz an; der alte bleibt als Verlauf
/// erhalten. Ein Eintrag gilt als verlängert, wenn es beim selben Mitglied einen jüngeren Eintrag derselben Art gibt
/// (gleiche Qualifikation, gleiche Nachweisart bzw. Vorsorge), der zum Stichtag schon gilt. Fristen und Erinnerungen werten nur
/// den jüngsten Eintrag.
/// </summary>
public static class Nachweisverlauf
{
    /// <summary>Jüngster Eintrag bis zum Stichtag; bei gleichem Datum der zuletzt angelegte.</summary>
    /// <typeparam name="T">Art des Eintrags.</typeparam>
    /// <param name="eintraege">Einträge einer Art eines Mitglieds.</param>
    /// <param name="datum">Datum des Eintrags, z. B. Erwerb oder Kontrolle.</param>
    /// <param name="stichtag">Tag der Bewertung; spätere Einträge zählen noch nicht.</param>
    /// <returns>Der jüngste Eintrag oder null.</returns>
    public static T? Neuester<T>(IEnumerable<T> eintraege, Func<T, DateOnly> datum, DateOnly stichtag) where T : Entitaet =>
        eintraege.Where(e => datum(e) <= stichtag)
            .OrderByDescending(datum).ThenByDescending(e => e.ErstelltAm).ThenByDescending(e => e.Id)
            .FirstOrDefault();

    /// <summary>Einträge, zu denen es einen jüngeren derselben Art gibt.</summary>
    /// <typeparam name="T">Art des Eintrags.</typeparam>
    /// <typeparam name="TArt">Schlüssel, der Einträge derselben Art zusammenfasst.</typeparam>
    /// <param name="eintraege">Einträge eines oder mehrerer Mitglieder.</param>
    /// <param name="art">Mitglied und Art, z. B. (MitgliedId, QualifikationId).</param>
    /// <param name="datum">Datum des Eintrags.</param>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <returns>Die Schlüssel der verlängerten Einträge.</returns>
    public static IReadOnlySet<Guid> Verlaengert<T, TArt>(IEnumerable<T> eintraege, Func<T, TArt> art, Func<T, DateOnly> datum, DateOnly stichtag)
        where T : Entitaet
    {
        var ergebnis = new HashSet<Guid>();
        foreach (var gruppe in eintraege.GroupBy(art))
        {
            if (Neuester(gruppe, datum, stichtag) is not { } neuester)
            {
                continue;
            }

            foreach (var e in gruppe.Where(e => e.Id != neuester.Id && datum(e) <= datum(neuester)))
            {
                ergebnis.Add(e.Id);
            }
        }

        return ergebnis;
    }

    /// <summary>Verlängerte Qualifikationsnachweise (jüngerer Nachweis derselben Qualifikation vorhanden).</summary>
    /// <param name="eintraege">Die Nachweise.</param>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <returns>Die Schlüssel der verlängerten Nachweise.</returns>
    public static IReadOnlySet<Guid> Verlaengert(IEnumerable<QualifikationNachweis> eintraege, DateOnly stichtag) =>
        Verlaengert(eintraege, q => (q.MitgliedId, q.QualifikationId), q => q.ErworbenAm, stichtag);

    /// <summary>Verlängerte wiederkehrende Nachweise (jüngerer Nachweis derselben Art vorhanden).</summary>
    /// <param name="eintraege">Die Nachweise.</param>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <returns>Die Schlüssel der verlängerten Nachweise.</returns>
    public static IReadOnlySet<Guid> Verlaengert(IEnumerable<Nachweis> eintraege, DateOnly stichtag) =>
        Verlaengert(eintraege, n => (n.MitgliedId, n.NachweisartId), n => n.Datum, stichtag);

    /// <summary>Durch eine jüngere Vorsorge erneuerte Vorsorgen (nur Status und Frist).</summary>
    /// <param name="eintraege">Die Vorsorgen.</param>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <returns>Die Schlüssel der erneuerten Vorsorgen.</returns>
    public static IReadOnlySet<Guid> Verlaengert(IEnumerable<AtemschutzVorsorge> eintraege, DateOnly stichtag) =>
        Verlaengert(eintraege, v => v.MitgliedId, v => v.UntersuchtAm, stichtag);
}
