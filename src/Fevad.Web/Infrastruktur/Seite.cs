namespace Fevad.Web.Infrastruktur;

/// <summary>Ein Ausschnitt einer langen Liste. Die Seitennummer zählt ab 1 und wird auf den gültigen Bereich begrenzt.</summary>
public sealed record Seite<T>(IReadOnlyList<T> Eintraege, int Nummer, int Groesse, int Gesamt) : ISeitenleiste
{
    public static readonly Seite<T> Leer = new([], 1, Seitenwahl.Standardgroesse, 0);

    public int Seiten => Math.Max(1, (Gesamt + Groesse - 1) / Groesse);
    public int Erster => Gesamt == 0 ? 0 : (Nummer - 1) * Groesse + 1;
    public int Letzter => Erster + Eintraege.Count - 1;
    public int Count => Eintraege.Count;
}

/// <summary>Was die Seitenleiste <c>_Blaettern</c> braucht, unabhängig vom Zeilentyp.</summary>
public interface ISeitenleiste
{
    int Nummer { get; }
    int Seiten { get; }
    int Gesamt { get; }
    int Erster { get; }
    int Letzter { get; }
}

public static class Seitenwahl
{
    /// <summary>Zeilen je Seite. Reine Darstellung, keine Frist, daher kein Parameter.</summary>
    public const int Standardgroesse = 50;

    public static Seite<T> Seite<T>(this IReadOnlyList<T> alle, int nummer, int groesse = Standardgroesse)
    {
        var seiten = Math.Max(1, (alle.Count + groesse - 1) / groesse);
        var n = Math.Clamp(nummer, 1, seiten);
        return new(alle.Skip((n - 1) * groesse).Take(groesse).ToList(), n, groesse, alle.Count);
    }

    /// <summary>Alles auf einer Seite, etwa die Beladung eines Fahrzeugs.</summary>
    public static Seite<T> Ganz<T>(this IReadOnlyList<T> alle) => new(alle, 1, Math.Max(1, alle.Count), alle.Count);
}
