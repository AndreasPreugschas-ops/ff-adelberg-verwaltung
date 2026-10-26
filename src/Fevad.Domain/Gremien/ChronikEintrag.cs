using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Gremien;

/// <summary>
/// Ereignis in der Chronik der Feuerwehr (V56, F25), z. B. Indienststellung eines Fahrzeugs oder ein Jubiläum. Die Chronik ist intern; einzelne
/// Einträge gibt die Schriftführung zur Veröffentlichung frei, nur diese erscheinen im Jahresbericht und in der Ausgabe für Presse und Homepage.
/// </summary>
public class ChronikEintrag : Entitaet
{
    /// <summary>Tag des Ereignisses.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Kurzer Titel, z. B. „Indienststellung HLF 10“.</summary>
    public required string Titel { get; set; }

    /// <summary>Beschreibung des Ereignisses.</summary>
    public string? Text { get; set; }

    /// <summary>Zur Veröffentlichung freigegeben (Jahresbericht, Presse, Homepage).</summary>
    public bool Veroeffentlichen { get; set; }

    /// <summary>Optional ein Dokument oder Foto aus der Dokumentablage.</summary>
    public Guid? DokumentId { get; set; }
    /// <summary>Navigationsverweis auf das Dokument.</summary>
    public Dokument? Dokument { get; set; }

    /// <summary>Wer den Eintrag angelegt hat.</summary>
    public string? ErfasstVon { get; set; }
}

/// <summary>Jubiläen eines Jahres (V56): Mitgliedsjahre und runde Geburtstage aus den Parametern.</summary>
public static class Jubilaeum
{
    /// <summary>Zahlen aus einem Parameterwert wie „25|40|50“.</summary>
    /// <param name="wert">Parameterwert.</param>
    /// <returns>Die Zahlen, aufsteigend und ohne Doppelte.</returns>
    public static IReadOnlyList<int> Stufen(string? wert) =>
        (wert ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var z) ? z : 0).Where(z => z > 0).Distinct().Order().ToList();

    /// <summary>Welches Jubiläum ein Ausgangsdatum im Jahr ergibt, z. B. 25 Jahre Mitgliedschaft.</summary>
    /// <param name="seit">Eintritt oder Geburtstag.</param>
    /// <param name="jahr">Betrachtetes Jahr.</param>
    /// <param name="stufen">Jubiläumsstufen.</param>
    /// <returns>Die Jahre und der Tag im Jahr, oder <c>null</c>, wenn kein Jubiläum ansteht.</returns>
    public static (int Jahre, DateOnly Tag)? Im(DateOnly seit, int jahr, IReadOnlyList<int> stufen)
    {
        var jahre = jahr - seit.Year;
        if (!stufen.Contains(jahre))
        {
            return null;
        }

        // Am 29.02. Geborene feiern in Nichtschaltjahren am 28.02.
        var tag = Math.Min(seit.Day, DateTime.DaysInMonth(jahr, seit.Month));
        return (jahre, new DateOnly(jahr, seit.Month, tag));
    }
}
