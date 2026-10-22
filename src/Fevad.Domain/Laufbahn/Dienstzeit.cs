using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Anrechnungsregeln der VwV Feuerwehr-Ehrenzeichen Nr. 4; die Alter kommen aus Parametern.</summary>
/// <param name="JugendAnrechnenAbAlter">Jugendfeuerwehr zählt ab diesem Lebensjahr (VwV: 14).</param>
/// <param name="AltersabteilungAnrechnenBisAlter">Altersabteilung zählt bis zu diesem Lebensjahr; 0 heißt gar nicht.</param>
public sealed record Dienstzeitregeln(int JugendAnrechnenAbAlter, int AltersabteilungAnrechnenBisAlter);

/// <summary>Zusammengefasste, überlappungsfreie Zeiträume. <c>Bis = null</c> heißt: läuft noch.</summary>
public sealed class Dienstzeit
{
    public IReadOnlyList<(DateOnly Von, DateOnly? Bis)> Zeitraeume { get; }

    public Dienstzeit(IEnumerable<(DateOnly Von, DateOnly? Bis)> zeitraeume) => Zeitraeume = Zusammenfassen(zeitraeume);

    /// <summary>Vollendete Diensttage vor dem Stichtag; der Stichtag selbst läuft noch.</summary>
    public int TageBis(DateOnly stichtag)
    {
        var gestern = stichtag.AddDays(-1);
        return Zeitraeume
            .Where(z => z.Von <= gestern)
            .Sum(z => (Min(z.Bis ?? gestern, gestern).DayNumber - z.Von.DayNumber) + 1);
    }

    /// <summary>
    /// Vollendete Dienstjahre am Stichtag. Gerechnet wird wie beim Lebensalter ab einem gedachten Beginn: dem Beginn des
    /// letzten Zeitraums, vorverlegt um die Tage der früheren Zeiträume. Ohne Unterbrechung ist das genau der Jahrestag.
    /// </summary>
    public int VolleJahreBis(DateOnly stichtag)
    {
        var summe = 0;
        var jahre = 0;
        foreach (var (von, bis) in Zeitraeume.Where(z => z.Von < stichtag))
        {
            var ende = bis is null || bis.Value >= stichtag ? stichtag : bis.Value.AddDays(1);
            jahre = Mitglieder.Mitglied.Alter(von.AddDays(-summe), ende);
            summe += ende.DayNumber - von.DayNumber;
        }

        return jahre;
    }

    /// <summary>
    /// Tag, an dem <paramref name="jahre"/> Dienstjahre vollendet sind (bei Eintritt am 01.01.2000 und 15 Jahren der
    /// 01.01.2015), wenn laufende Zeiträume weiterlaufen. Null, wenn das mit den bekannten Zeiträumen nicht mehr
    /// erreicht wird. Unterbrechungen verschieben den Tag um ihre Länge.
    /// </summary>
    public DateOnly? ErreichtAm(int jahre)
    {
        var summe = 0;
        foreach (var (von, bis) in Zeitraeume)
        {
            var am = von.AddDays(-summe).AddYears(jahre);
            if (bis is null || am <= bis.Value.AddDays(1))
            {
                return am;
            }

            summe += bis.Value.DayNumber - von.DayNumber + 1;
        }

        return null;
    }

    private static List<(DateOnly Von, DateOnly? Bis)> Zusammenfassen(IEnumerable<(DateOnly Von, DateOnly? Bis)> zeitraeume)
    {
        var ergebnis = new List<(DateOnly Von, DateOnly? Bis)>();
        foreach (var z in zeitraeume.Where(z => z.Bis is null || z.Bis >= z.Von).OrderBy(z => z.Von))
        {
            if (ergebnis.Count > 0)
            {
                var letzte = ergebnis[^1];

                // Doppelmitgliedschaften zählen nur einfach; lückenlos aneinander anschließende Abschnitte verschmelzen.
                if (letzte.Bis is null || z.Von.DayNumber <= letzte.Bis.Value.DayNumber + 1)
                {
                    ergebnis[^1] = (letzte.Von, letzte.Bis is null || z.Bis is null ? null : Max(letzte.Bis.Value, z.Bis.Value));
                    continue;
                }
            }

            ergebnis.Add(z);
        }

        return ergebnis;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

public static class Dienstzeitrechnung
{
    /// <summary>
    /// Anrechenbare Zeit im Einsatzdienst für das Feuerwehr-Ehrenzeichen: Einsatzabteilung, Jugendfeuerwehr ab dem
    /// Anrechnungsalter, Altersabteilung bis zur Grenze, Vordienstzeiten anderer Wehren. Befreite Zeiten zählen nicht.
    /// </summary>
    public static Dienstzeit Einsatzdienst(DateOnly geburtsdatum, IEnumerable<Abschnitt> abschnitte, Dienstzeitregeln regeln)
    {
        var zeitraeume = new List<(DateOnly, DateOnly?)>();
        foreach (var a in abschnitte.Where(a => a.Status is MitgliedStatus.Aktiv or MitgliedStatus.Probezeit))
        {
            switch (a.AbteilungCode)
            {
                case AbteilungCode.Einsatz:
                    zeitraeume.Add((a.Von, a.Bis));
                    break;
                case AbteilungCode.Jugend:
                    var ab = Laufbahnhinweise.Geburtstag(geburtsdatum, regeln.JugendAnrechnenAbAlter);
                    zeitraeume.Add((a.Von > ab ? a.Von : ab, a.Bis));
                    break;
                case AbteilungCode.Alters when regeln.AltersabteilungAnrechnenBisAlter > 0:
                    var bis = Laufbahnhinweise.Geburtstag(geburtsdatum, regeln.AltersabteilungAnrechnenBisAlter).AddDays(-1);
                    zeitraeume.Add((a.Von, a.Bis is null || a.Bis > bis ? bis : a.Bis));
                    break;
            }
        }

        return new Dienstzeit(zeitraeume);
    }

    /// <summary>Zugehörigkeit zur eigenen Feuerwehr in allen Abteilungen, für Vereinsjubiläen. Vordienstzeiten zählen nicht.</summary>
    public static Dienstzeit Zugehoerigkeit(IEnumerable<Abschnitt> abschnitte) =>
        new(abschnitte.Where(a => !a.Vordienstzeit).Select(a => (a.Von, a.Bis)));
}
