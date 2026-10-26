using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kommunikation;

/// <summary>An wen eine Mitteilung geht (V42).</summary>
public enum MitteilungZiel
{
    /// <summary>Mitglieder mit Konto, die am Tag der Veröffentlichung einer der gewählten Abteilungen angehören.</summary>
    [Display(Name = "Mitglieder")] Mitglieder,
    /// <summary>Elternkonten der Kinder und Jugendlichen in den gewählten Abteilungen (Elternpost).</summary>
    [Display(Name = "Eltern")] Eltern,
    /// <summary>Elternkonten der Angemeldeten einer Jugendveranstaltung, z. B. einer Fahrt.</summary>
    [Display(Name = "Eltern der Angemeldeten einer Veranstaltung")] Veranstaltung,
}

/// <summary>Stand einer Mitteilung.</summary>
public enum MitteilungStatus
{
    /// <summary>Noch nicht veröffentlicht; nur für den Verfasser sichtbar.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Veröffentlicht; die Empfänger sind festgeschrieben.</summary>
    [Display(Name = "veröffentlicht")] Veroeffentlicht,
    /// <summary>Zurückgezogen; die Empfänger sehen sie nicht mehr.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>
/// Dienstliche Mitteilung an eine Zielgruppe (V42, E5), wahlweise mit Lesebestätigung. Keine Alarmierung: Mitteilungen erscheinen
/// in FEVAD und als Benachrichtigung (V72), nie als Alarm. Die Empfänger werden bei der Veröffentlichung festgeschrieben, damit der
/// Lesestand nachvollziehbar bleibt. Gelöscht nach Löschregel L65.
/// </summary>
public class Mitteilung : Entitaet
{
    /// <summary>Höchstlänge des Textes.</summary>
    public const int TextLaenge = 4000;

    /// <summary>Titel.</summary>
    public required string Titel { get; set; }
    /// <summary>Text, als reiner Text mit Absätzen; keine Gesundheitsdaten.</summary>
    public required string Text { get; set; }
    /// <summary>Zielgruppe.</summary>
    public MitteilungZiel Ziel { get; set; }
    /// <summary>Gewählte Abteilungen bei <see cref="MitteilungZiel.Mitglieder"/> und <see cref="MitteilungZiel.Eltern"/>; Unterabteilungen zählen mit.</summary>
    public List<Guid> Abteilungen { get; set; } = [];
    /// <summary>Jugendveranstaltung bei <see cref="MitteilungZiel.Veranstaltung"/>.</summary>
    public Guid? JugendveranstaltungId { get; set; }
    /// <summary>Empfänger sollen das Lesen ausdrücklich bestätigen.</summary>
    public bool Lesebestaetigung { get; set; }
    /// <summary>Bei der Veröffentlichung zusätzlich sofort eine Mail mit Titel und Link schicken (nicht nur die Sammelmail).</summary>
    public bool SofortPerMail { get; set; }
    /// <summary>Stand.</summary>
    public MitteilungStatus Status { get; set; }
    /// <summary>Zeitpunkt der Veröffentlichung in UTC.</summary>
    public DateTimeOffset? VeroeffentlichtAm { get; set; }
    /// <summary>Name des Verfassers zum Zeitpunkt der Veröffentlichung, z. B. „Kommandant Max Muster“.</summary>
    public string? Verfasser { get; set; }
    /// <summary>Letzter Tag, an dem sie in der Liste der Empfänger oben steht; danach unter „ältere“.</summary>
    public DateOnly? AktuellBis { get; set; }
    /// <summary>Festgeschriebene Empfänger mit Lesestand.</summary>
    public List<MitteilungEmpfaenger> Empfaenger { get; set; } = [];

    /// <summary>Prüft Titel, Text und Ziel vor dem Speichern.</summary>
    /// <returns>Die Fehler; leer, wenn alles passt.</returns>
    public IReadOnlyList<string> Pruefen()
    {
        var fehler = new List<string>();
        if (string.IsNullOrWhiteSpace(Titel))
        {
            fehler.Add("Bitte einen Titel angeben.");
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            fehler.Add("Bitte einen Text angeben.");
        }
        else if (Text.Length > TextLaenge)
        {
            fehler.Add($"Der Text ist zu lang (höchstens {TextLaenge} Zeichen).");
        }

        if (Ziel == MitteilungZiel.Veranstaltung ? JugendveranstaltungId is null : Abteilungen.Count == 0)
        {
            fehler.Add(Ziel == MitteilungZiel.Veranstaltung ? "Bitte die Veranstaltung wählen." : "Bitte mindestens eine Abteilung wählen.");
        }

        return fehler;
    }
}

/// <summary>
/// Empfänger einer veröffentlichten Mitteilung mit Lesestand. Bei Elternpost ist das Konto das Elternkonto und <see cref="MitgliedId"/>
/// das Kind, für das die Mitteilung kommt. Kein <see cref="Entitaet"/>: Lesen gehört nicht ins Änderungsprotokoll.
/// </summary>
public class MitteilungEmpfaenger
{
    /// <summary>Eindeutiger Schlüssel (UUID v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Die Mitteilung.</summary>
    public Guid MitteilungId { get; set; }
    /// <summary>Empfangendes Konto.</summary>
    public Guid KontoId { get; set; }
    /// <summary>Mitglied, über das das Konto Empfänger ist: das eigene oder das Kind.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Zuerst geöffnet in UTC.</summary>
    public DateTimeOffset? GelesenAm { get; set; }
    /// <summary>Lesen bestätigt in UTC (nur bei Mitteilungen mit Lesebestätigung).</summary>
    public DateTimeOffset? BestaetigtAm { get; set; }
}
