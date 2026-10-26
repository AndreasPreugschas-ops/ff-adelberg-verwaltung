using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Gremien;

/// <summary>Aufbau eines Schreibens.</summary>
public enum VorlageForm
{
    /// <summary>Brief mit Briefkopf, Anschrift, Datum und Betreff.</summary>
    [Display(Name = "Brief")] Brief,
    /// <summary>Urkunde: zentriert, ohne Anschrift, mit Unterschriftslinie.</summary>
    [Display(Name = "Urkunde")] Urkunde,
}

/// <summary>An wen ein Serienbrief geht.</summary>
public enum VorlageEmpfaenger
{
    /// <summary>An die Mitglieder selbst.</summary>
    [Display(Name = "Mitglieder")] Mitglieder,
    /// <summary>An die Erziehungsberechtigten der Mitglieder (Elternbrief).</summary>
    [Display(Name = "Erziehungsberechtigte")] Erziehungsberechtigte,
}

/// <summary>
/// Vorlage für Briefe, Urkunden und Mails mit Platzhaltern wie <c>{Briefanrede}</c> (V56). Die Schriftführung pflegt die Texte; Serienbriefe
/// und Serienmails füllen sie je Empfänger.
/// </summary>
public class Briefvorlage : Katalogeintrag
{
    /// <summary>Brief oder Urkunde.</summary>
    public VorlageForm Form { get; set; }

    /// <summary>Mitglieder oder deren Erziehungsberechtigte.</summary>
    public VorlageEmpfaenger Empfaenger { get; set; }

    /// <summary>Betreff des Briefs oder Überschrift der Urkunde, mit Platzhaltern.</summary>
    public required string Betreff { get; set; }

    /// <summary>Text mit Platzhaltern; Leerzeilen trennen Absätze.</summary>
    public required string Text { get; set; }
}

/// <summary>Platzhalter der Briefvorlagen und ihr Ersetzen.</summary>
public static partial class Platzhalter
{
    /// <summary>Alle bekannten Platzhalter mit Erklärung für die Oberfläche.</summary>
    public static IReadOnlyList<(string Name, string Beschreibung)> Alle { get; } =
    [
        ("Briefanrede", "„Sehr geehrte Frau …,“ oder „Sehr geehrter Herr …,“, ohne Angabe zum Geschlecht „Guten Tag Vorname Nachname,“"),
        ("Vorname", "Vorname des Empfängers"),
        ("Nachname", "Nachname des Empfängers"),
        ("Dienstgrad", "heutiger Dienstgrad des Mitglieds"),
        ("Strasse", "Straße und Hausnummer"),
        ("PlzOrt", "Postleitzahl und Ort"),
        ("Kind", "Vor- und Nachname des Kindes (nur Elternbrief)"),
        ("Datum", "Datum des Schreibens"),
        ("Jahr", "Jahr des Schreibens"),
        ("Feuerwehr", "Name der Feuerwehr aus den Stammdaten"),
    ];

    /// <summary>Ersetzt die Platzhalter; unbekannte bleiben stehen, damit sie beim Lesen auffallen.</summary>
    /// <param name="text">Text mit Platzhaltern.</param>
    /// <param name="werte">Werte je Platzhaltername.</param>
    /// <returns>Der gefüllte Text.</returns>
    public static string Ersetzen(string text, IReadOnlyDictionary<string, string?> werte) =>
        Muster().Replace(text, m => werte.TryGetValue(m.Groups[1].Value, out var wert) ? wert ?? "" : m.Value);

    /// <summary>Platzhalter im Text, die es nicht gibt, z. B. Tippfehler.</summary>
    /// <param name="text">Text mit Platzhaltern.</param>
    /// <returns>Die unbekannten Namen.</returns>
    public static IReadOnlyList<string> Unbekannte(string? text) =>
        Muster().Matches(text ?? "").Select(m => m.Groups[1].Value).Where(n => Alle.All(p => p.Name != n)).Distinct().ToList();

    /// <summary>Anrede eines Briefs.</summary>
    /// <param name="geschlecht">Geschlecht, wenn bekannt.</param>
    /// <param name="titel">Akademischer Titel, z. B. „Dr.“.</param>
    /// <param name="vorname">Vorname.</param>
    /// <param name="nachname">Nachname.</param>
    /// <returns>Die Anrede mit Komma.</returns>
    public static string Briefanrede(Geschlecht? geschlecht, string? titel, string vorname, string nachname)
    {
        var name = string.IsNullOrWhiteSpace(titel) ? nachname : $"{titel.Trim()} {nachname}";
        return geschlecht switch
        {
            Geschlecht.Weiblich => $"Sehr geehrte Frau {name},",
            Geschlecht.Maennlich => $"Sehr geehrter Herr {name},",
            _ => $"Guten Tag {vorname} {nachname},",
        };
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Muster();
}
