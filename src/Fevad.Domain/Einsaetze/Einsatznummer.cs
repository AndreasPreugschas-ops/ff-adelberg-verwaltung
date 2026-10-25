using System.Globalization;
using System.Text.RegularExpressions;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Bildet und liest Einsatznummern nach einem Format aus dem Parameter
/// <c>Einsatz.Nummer.Format</c>. Platzhalter: <c>{JJJJ}</c> bzw. <c>{JJ}</c> für das Jahr,
/// <c>{N}</c>, <c>{NN}</c>, <c>{NNN}</c> ... für die laufende Nummer mit so vielen Stellen.
/// </summary>
public static partial class Einsatznummer
{
    /// <summary>Vorgabe, wenn der Parameter fehlt, z. B. „2026-007“.</summary>
    public const string StandardFormat = "{JJJJ}-{NNN}";

    [GeneratedRegex(@"\{(N+)\}")]
    private static partial Regex LaufendeNummerPlatzhalter();

    /// <summary>Bildet eine Einsatznummer.</summary>
    /// <param name="format">Format mit Platzhaltern.</param>
    /// <param name="jahr">Jahr des Einsatzes.</param>
    /// <param name="laufendeNummer">Laufende Nummer im Jahr.</param>
    /// <returns>Die Einsatznummer.</returns>
    public static string Bilden(string format, int jahr, int laufendeNummer)
    {
        var (praefix, stellen, suffix) = Zerlegen(format, jahr);
        return praefix + laufendeNummer.ToString(new string('0', stellen), CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>Teil vor und nach der laufenden Nummer für ein Jahr; damit lassen sich die Nummern des Jahres in der Datenbank finden.</summary>
    public static (string Praefix, string Suffix) Rahmen(string format, int jahr)
    {
        var (praefix, _, suffix) = Zerlegen(format, jahr);
        return (praefix, suffix);
    }

    /// <summary>Laufende Nummer aus einer Einsatznummer, wenn sie zum Format und Jahr passt.</summary>
    public static int? LaufendeNummer(string format, int jahr, string nummer)
    {
        var (praefix, _, suffix) = Zerlegen(format, jahr);
        if (nummer.Length <= praefix.Length + suffix.Length
            || !nummer.StartsWith(praefix, StringComparison.Ordinal)
            || !nummer.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var mitte = nummer[praefix.Length..^suffix.Length];
        return mitte.All(char.IsAsciiDigit) && int.TryParse(mitte, NumberStyles.None, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;
    }

    private static (string Praefix, int Stellen, string Suffix) Zerlegen(string format, int jahr)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            format = StandardFormat;
        }

        var treffer = LaufendeNummerPlatzhalter().Matches(format);
        if (treffer.Count != 1)
        {
            throw new FormatException($"Das Einsatznummernformat \"{format}\" braucht genau einen Platzhalter {{N...}} für die laufende Nummer.");
        }

        var platzhalter = treffer[0];
        string Jahr(string teil) => teil
            .Replace("{JJJJ}", jahr.ToString("0000", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{JJ}", (jahr % 100).ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal);

        return (Jahr(format[..platzhalter.Index]), platzhalter.Groups[1].Length, Jahr(format[(platzhalter.Index + platzhalter.Length)..]));
    }
}
