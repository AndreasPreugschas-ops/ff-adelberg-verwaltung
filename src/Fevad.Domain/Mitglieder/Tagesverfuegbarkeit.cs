using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Mitglieder;

/// <summary>
/// Werktage, an denen ein Mitglied tagsüber (06:00–18:00 Uhr) im Ort ist und ausrücken kann (V43). Freiwillige Angabe als Wochenmuster,
/// ohne Standort und ohne Uhrzeiten; ausgewertet wird nur als Summe je Funktion (Entscheidung E4).
/// </summary>
[Flags]
public enum Werktage
{
    /// <summary>An keinem Werktag tagsüber im Ort.</summary>
    [Display(Name = "an keinem Werktag")] Keine = 0,
    /// <summary>Montag.</summary>
    [Display(Name = "Montag")] Montag = 1,
    /// <summary>Dienstag.</summary>
    [Display(Name = "Dienstag")] Dienstag = 2,
    /// <summary>Mittwoch.</summary>
    [Display(Name = "Mittwoch")] Mittwoch = 4,
    /// <summary>Donnerstag.</summary>
    [Display(Name = "Donnerstag")] Donnerstag = 8,
    /// <summary>Freitag.</summary>
    [Display(Name = "Freitag")] Freitag = 16,
}

/// <summary>Hilfen zum Wochenmuster der Tagesverfügbarkeit.</summary>
public static class Tagesverfuegbarkeit
{
    /// <summary>Die fünf Werktage in Wochenfolge.</summary>
    public static IReadOnlyList<Werktage> Tage { get; } = [Werktage.Montag, Werktage.Dienstag, Werktage.Mittwoch, Werktage.Donnerstag, Werktage.Freitag];

    /// <summary>Kurzname des Tages für Tabellenköpfe.</summary>
    /// <param name="tag">Ein einzelner Werktag.</param>
    /// <returns>„Mo“ bis „Fr“.</returns>
    public static string Kurz(Werktage tag) => tag switch
    {
        Werktage.Montag => "Mo",
        Werktage.Dienstag => "Di",
        Werktage.Mittwoch => "Mi",
        Werktage.Donnerstag => "Do",
        Werktage.Freitag => "Fr",
        _ => "",
    };

    /// <summary>Zählt je Werktag, wie viele der Angaben den Tag enthalten.</summary>
    /// <param name="angaben">Wochenmuster der Personen mit Angabe.</param>
    /// <returns>Fünf Zahlen von Montag bis Freitag.</returns>
    public static int[] Summen(IEnumerable<Werktage> angaben)
    {
        var liste = angaben.ToList();
        return Tage.Select(t => liste.Count(a => a.HasFlag(t))).ToArray();
    }
}
