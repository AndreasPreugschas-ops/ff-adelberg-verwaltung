using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Art einer Benachrichtigung (V72). Je Art entscheidet jedes Konto, ob sie zusätzlich in der täglichen Sammelmail steht.
/// Als Flags, damit das Konto die abgeschalteten Arten in einem Feld speichert.
/// </summary>
[Flags]
public enum Benachrichtigungsart
{
    /// <summary>Keine Art; Ausgangswert der Einstellungen im Konto.</summary>
    [Display(Name = "keine")] Keine = 0,
    /// <summary>Dienstliche Mitteilung des Kommandos, der Jugendleitung oder einer Funktion (V42).</summary>
    [Display(Name = "Mitteilungen")] Mitteilung = 1,
    /// <summary>Rundfrage oder Umfrage, auf die eine Antwort erwartet wird (V42).</summary>
    [Display(Name = "Rundfragen und Umfragen")] Rundfrage = 2,
    /// <summary>Neuer oder geänderter Termin.</summary>
    [Display(Name = "Termine")] Termin = 4,
    /// <summary>Erinnerung an eine eigene Frist oder Rückmeldung.</summary>
    [Display(Name = "Erinnerungen")] Erinnerung = 8,
    /// <summary>Aufgabe für eine Funktion, z. B. eine Freigabe oder Prüfung.</summary>
    [Display(Name = "Aufgaben")] Aufgabe = 16,
    /// <summary>Hinweis aus dem Betrieb, z. B. ein Fahrzeug außer Betrieb.</summary>
    [Display(Name = "Hinweise")] Hinweis = 32,
}

/// <summary>
/// Benachrichtigung an ein Konto (V72): erscheint in der Anwendung (Glocke) und, je nach Einstellung, in der täglichen Sammelmail.
/// Titel und Text sind datensparsam, nie Gesundheitsdaten; der Link führt in FEVAD, wo das Recht noch einmal geprüft wird.
/// Kein <see cref="Entitaet"/>: Lesen und Löschen gehören nicht ins Änderungsprotokoll; gelöscht wird nach Löschregel L64.
/// </summary>
public class Benachrichtigung
{
    /// <summary>Eindeutiger Schlüssel (UUID v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Empfangendes Konto.</summary>
    public Guid EmpfaengerId { get; set; }
    /// <summary>Art der Benachrichtigung.</summary>
    public Benachrichtigungsart Art { get; set; }
    /// <summary>Kurzer Titel, z. B. „Neue Mitteilung: Dienstplan 2027“.</summary>
    public required string Titel { get; set; }
    /// <summary>Optionaler kurzer Text; nie Gesundheitsdaten.</summary>
    public string? Text { get; set; }
    /// <summary>Relativer Pfad in FEVAD, z. B. „/Mitteilungen/Details/…“; leer ohne Ziel.</summary>
    public string? Link { get; set; }
    /// <summary>Bezug, z. B. die Mitteilung; damit wird die Benachrichtigung gelesen, sobald der Bezug geöffnet wird.</summary>
    public Guid? BezugId { get; set; }
    /// <summary>Zeitpunkt der Anlage in UTC.</summary>
    public DateTimeOffset ErstelltAm { get; set; }
    /// <summary>Zeitpunkt, zu dem das Konto sie gelesen hat; leer, solange ungelesen.</summary>
    public DateTimeOffset? GelesenAm { get; set; }
    /// <summary>Zeitpunkt, zu dem sie in einer Sammelmail stand; leer, solange nicht.</summary>
    public DateTimeOffset? GemailtAm { get; set; }

    /// <summary>Ist der Link ein Pfad innerhalb von FEVAD? Nur solche werden geöffnet (kein Sprung auf fremde Seiten).</summary>
    /// <param name="link">Zu prüfender Link.</param>
    /// <returns>true bei einem relativen Pfad, der mit genau einem Schrägstrich beginnt.</returns>
    public static bool IstInternerLink(string? link) =>
        link is { Length: > 0 } && link[0] == '/' && (link.Length == 1 || (link[1] != '/' && link[1] != '\\'));

    /// <summary>Die einzelnen Arten, ohne <see cref="Benachrichtigungsart.Keine"/>, in der Reihenfolge der Oberfläche.</summary>
    public static IReadOnlyList<Benachrichtigungsart> Arten { get; } =
        Enum.GetValues<Benachrichtigungsart>().Where(a => a != Benachrichtigungsart.Keine).ToList();
}
