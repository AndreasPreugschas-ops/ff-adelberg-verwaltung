using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Stand einer Prüfplanposition für einen Prüfgegenstand. Wird nie gespeichert.</summary>
public sealed record Faelligkeit(
    Guid PositionId,
    Pruefart Pruefart,
    int IntervallMonate,
    DateOnly? LetztePruefung,
    DateOnly? FaelligAm,
    Fristampel Ampel)
{
    /// <summary>Für diese Prüfart gibt es noch keine bestandene Prüfung.</summary>
    public bool NieGeprueft => LetztePruefung is null;
}

/// <summary>Kurzfassung einer Prüfung für die Fristberechnung. <see cref="ErfasstAm"/> ordnet Korrekturen am selben Prüftag.</summary>
public sealed record PruefungKurz(Pruefart Pruefart, DateOnly Datum, PruefErgebnis Ergebnis, DateOnly? NaechsteFaellig, DateTimeOffset ErfasstAm = default);

/// <summary>Berechnet Fälligkeiten von Prüfungen nach dem Prüfplan.</summary>
public static class Pruefplanung
{
    /// <summary>
    /// Fälligkeiten je Prüfplanposition. Maßgeblich ist die jüngste bestandene Prüfung derselben Prüfart;
    /// bei gleichem Prüfdatum die zuletzt erfasste. Eine nicht bestandene Prüfung setzt die Frist nicht zurück.
    /// Ohne Prüfung ist die Position sofort fällig (rot).
    /// Positionen mit Intervall 0 (z. B. „nach Gebrauch“) haben keine Frist.
    /// </summary>
    public static IReadOnlyList<Faelligkeit> Berechnen(
        IEnumerable<PruefplanPosition> plan,
        IEnumerable<PruefungKurz> pruefungen,
        DateOnly stichtag,
        int warnungTage)
    {
        var bestanden = pruefungen
            .Where(p => p.Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln)
            .GroupBy(p => p.Pruefart)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Datum).ThenBy(p => p.ErfasstAm).Last());

        return plan
            .Where(p => p.IntervallMonate > 0)
            .OrderBy(p => p.Pruefart)
            .Select(position =>
            {
                if (!bestanden.TryGetValue(position.Pruefart, out var letzte))
                {
                    return new Faelligkeit(position.Id, position.Pruefart, position.IntervallMonate, null, null, Fristampel.Rot);
                }

                var faellig = letzte.NaechsteFaellig ?? letzte.Datum.AddMonths(position.IntervallMonate);
                return new Faelligkeit(position.Id, position.Pruefart, position.IntervallMonate, letzte.Datum, faellig,
                    Frist.Bewerten(faellig, stichtag, warnungTage));
            })
            .ToList();
    }

    /// <summary>Gesamtampel eines Prüfgegenstands: die schlechteste Position.</summary>
    public static Fristampel Ampel(IEnumerable<Faelligkeit> faelligkeiten) =>
        faelligkeiten.Select(f => f.Ampel).DefaultIfEmpty(Fristampel.Gruen).Max();

    /// <summary>Nächster Termin über alle Positionen; null, wenn nichts geplant oder noch nie geprüft.</summary>
    public static DateOnly? NaechsterTermin(IEnumerable<Faelligkeit> faelligkeiten) =>
        faelligkeiten.Where(f => f.FaelligAm is not null).Select(f => f.FaelligAm).Min();
}
