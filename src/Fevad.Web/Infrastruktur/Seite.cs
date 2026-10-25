namespace Fevad.Web.Infrastruktur;

/// <summary>Ein Ausschnitt einer langen Liste. Die Seitennummer zählt ab 1 und wird auf den gültigen Bereich begrenzt.</summary>
public sealed record Seite<T>(IReadOnlyList<T> Eintraege, int Nummer, int Groesse, int Gesamt) : ISeitenleiste
{
    /// <summary>Leere erste Seite.</summary>
    public static readonly Seite<T> Leer = new([], 1, Seitenwahl.Standardgroesse, 0);

    /// <inheritdoc/>
    public int Seiten => Math.Max(1, (Gesamt + Groesse - 1) / Groesse);
    /// <inheritdoc/>
    public int Erster => Gesamt == 0 ? 0 : (Nummer - 1) * Groesse + 1;
    /// <inheritdoc/>
    public int Letzter => Erster + Eintraege.Count - 1;
    /// <summary>Anzahl der Einträge auf dieser Seite.</summary>
    public int Count => Eintraege.Count;
}

/// <summary>Was die Seitenleiste <c>_Blaettern</c> braucht, unabhängig vom Zeilentyp.</summary>
public interface ISeitenleiste
{
    /// <summary>Nummer der angezeigten Seite, ab 1.</summary>
    int Nummer { get; }
    /// <summary>Anzahl der Seiten, mindestens 1.</summary>
    int Seiten { get; }
    /// <summary>Anzahl aller Einträge.</summary>
    int Gesamt { get; }
    /// <summary>Laufende Nummer des ersten Eintrags der Seite; 0 ohne Einträge.</summary>
    int Erster { get; }
    /// <summary>Laufende Nummer des letzten Eintrags der Seite.</summary>
    int Letzter { get; }
}

/// <summary>Teilt Listen in Seiten.</summary>
public static class Seitenwahl
{
    /// <summary>Zeilen je Seite. Reine Darstellung, keine Frist, daher kein Parameter.</summary>
    public const int Standardgroesse = 50;

    /// <summary>Schneidet eine Seite aus der Liste; die Nummer wird auf den gültigen Bereich begrenzt.</summary>
    /// <typeparam name="T">Typ der Einträge.</typeparam>
    /// <param name="alle">Alle Einträge.</param>
    /// <param name="nummer">Gewünschte Seite, ab 1.</param>
    /// <param name="groesse">Einträge je Seite.</param>
    /// <returns>Die Seite.</returns>
    public static Seite<T> Seite<T>(this IReadOnlyList<T> alle, int nummer, int groesse = Standardgroesse)
    {
        var seiten = Math.Max(1, (alle.Count + groesse - 1) / groesse);
        var n = Math.Clamp(nummer, 1, seiten);
        return new(alle.Skip((n - 1) * groesse).Take(groesse).ToList(), n, groesse, alle.Count);
    }

    /// <summary>Alles auf einer Seite, etwa die Beladung eines Fahrzeugs.</summary>
    public static Seite<T> Ganz<T>(this IReadOnlyList<T> alle) => new(alle, 1, Math.Max(1, alle.Count), alle.Count);
}
