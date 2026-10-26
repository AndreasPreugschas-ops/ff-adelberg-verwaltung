using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kommunikation;

/// <summary>Art einer Rundfrage (V42).</summary>
public enum RundfrageArt
{
    /// <summary>„Wer kann?“: Antworten ja, vielleicht, nein; Auswertung auch nach Qualifikationen.</summary>
    [Display(Name = "Wer kann? (Verfügbarkeit)")] Verfuegbarkeit,
    /// <summary>Umfrage mit eigenen Antwortmöglichkeiten, wahlweise anonym.</summary>
    [Display(Name = "Umfrage")] Umfrage,
}

/// <summary>Stand einer Rundfrage.</summary>
public enum RundfrageStatus
{
    /// <summary>Noch nicht verschickt.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Verschickt, Antworten möglich (bis <see cref="Rundfrage.AntwortBis"/>).</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>Beendet; keine Antworten mehr.</summary>
    [Display(Name = "beendet")] Beendet,
}

/// <summary>
/// Rundfrage oder Umfrage an eine Zielgruppe (V42), z. B. „Wer kann Samstag beim Altpapier helfen?“ oder „Welcher Termin passt für den
/// Kameradschaftsabend?“. Keine Alarmierung und keine Einsatzverfügbarkeit: die Tagesverfügbarkeit ist eine eigene, freiwillige Angabe.
/// Empfänger werden beim Verschicken festgeschrieben. Gelöscht nach Löschregel L66.
/// </summary>
public class Rundfrage : Entitaet
{
    /// <summary>Höchstzahl der Antwortmöglichkeiten einer Umfrage.</summary>
    public const int HoechstensOptionen = 10;

    /// <summary>Antwortmöglichkeiten einer Verfügbarkeitsfrage, in dieser Reihenfolge gespeichert (0 ja, 1 vielleicht, 2 nein).</summary>
    public static readonly IReadOnlyList<string> Verfuegbarkeitsoptionen = ["Ja", "Vielleicht", "Nein"];

    /// <summary>Frage bzw. Titel.</summary>
    public required string Titel { get; set; }
    /// <summary>Erläuterung, z. B. Treffpunkt und Uhrzeit.</summary>
    public string? Text { get; set; }
    /// <summary>Art der Rundfrage.</summary>
    public RundfrageArt Art { get; set; }
    /// <summary>Zielgruppe wie bei Mitteilungen.</summary>
    public MitteilungZiel Ziel { get; set; }
    /// <summary>Gewählte Abteilungen; Unterabteilungen zählen mit.</summary>
    public List<Guid> Abteilungen { get; set; } = [];
    /// <summary>Jugendveranstaltung bei <see cref="MitteilungZiel.Veranstaltung"/>.</summary>
    public Guid? JugendveranstaltungId { get; set; }
    /// <summary>Tag, um den es geht (bei „Wer kann?“).</summary>
    public DateOnly? Tag { get; set; }
    /// <summary>Antwortmöglichkeiten einer Umfrage.</summary>
    public List<string> Optionen { get; set; } = [];
    /// <summary>Bei Umfragen mehrere Antworten zulassen.</summary>
    public bool Mehrfachauswahl { get; set; }
    /// <summary>
    /// Anonyme Umfrage: Antworten werden ohne Konto und ohne Zeitpunkt gespeichert, der Verfasser sieht nur Summen. Ob jemand geantwortet
    /// hat, steht beim Empfänger, damit niemand zweimal abstimmt.
    /// </summary>
    public bool Anonym { get; set; }
    /// <summary>Letzter Tag für Antworten; leer bis zum Beenden.</summary>
    public DateOnly? AntwortBis { get; set; }
    /// <summary>Stand.</summary>
    public RundfrageStatus Status { get; set; }
    /// <summary>Zeitpunkt des Verschickens in UTC.</summary>
    public DateTimeOffset? VerschicktAm { get; set; }
    /// <summary>Zeitpunkt des Beendens in UTC.</summary>
    public DateTimeOffset? BeendetAm { get; set; }
    /// <summary>Name des Verfassers beim Verschicken.</summary>
    public string? Verfasser { get; set; }

    /// <summary>Die Antwortmöglichkeiten je nach Art.</summary>
    public IReadOnlyList<string> Moeglichkeiten => Art == RundfrageArt.Verfuegbarkeit ? Verfuegbarkeitsoptionen : Optionen;

    /// <summary>Sind am Tag noch Antworten möglich?</summary>
    /// <param name="heute">Heutiger Tag.</param>
    /// <returns>true, wenn offen und die Antwortfrist nicht vorbei ist.</returns>
    public bool AntwortenMoeglich(DateOnly heute) => Status == RundfrageStatus.Offen && (AntwortBis is null || heute <= AntwortBis);

    /// <summary>Prüft eine Auswahl gegen die Antwortmöglichkeiten.</summary>
    /// <param name="auswahl">Gewählte Nummern.</param>
    /// <returns>true, wenn gültig.</returns>
    public bool AuswahlGueltig(IReadOnlyCollection<int> auswahl) =>
        auswahl.Count > 0 && auswahl.Distinct().Count() == auswahl.Count && auswahl.All(a => a >= 0 && a < Moeglichkeiten.Count)
        && (auswahl.Count == 1 || (Art == RundfrageArt.Umfrage && Mehrfachauswahl));

    /// <summary>Prüft die Angaben vor dem Speichern.</summary>
    /// <returns>Die Fehler; leer, wenn alles passt.</returns>
    public IReadOnlyList<string> Pruefen()
    {
        var fehler = new List<string>();
        if (string.IsNullOrWhiteSpace(Titel))
        {
            fehler.Add("Bitte die Frage angeben.");
        }

        if (Ziel == MitteilungZiel.Veranstaltung ? JugendveranstaltungId is null : Abteilungen.Count == 0)
        {
            fehler.Add(Ziel == MitteilungZiel.Veranstaltung ? "Bitte die Veranstaltung wählen." : "Bitte mindestens eine Abteilung wählen.");
        }

        if (Art == RundfrageArt.Umfrage)
        {
            if (Optionen.Count is < 2 or > HoechstensOptionen)
            {
                fehler.Add($"Eine Umfrage braucht 2 bis {HoechstensOptionen} Antwortmöglichkeiten.");
            }

            if (Optionen.Any(o => o.Length > 100))
            {
                fehler.Add("Eine Antwortmöglichkeit darf höchstens 100 Zeichen haben.");
            }
        }
        else if (Anonym)
        {
            fehler.Add("„Wer kann?“ lässt sich nicht anonym fragen.");
        }

        return fehler;
    }
}

/// <summary>Empfänger einer verschickten Rundfrage. Kein <see cref="Entitaet"/>: Antworten gehören nicht ins Änderungsprotokoll.</summary>
public class RundfrageEmpfaenger
{
    /// <summary>Eindeutiger Schlüssel (UUID v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Die Rundfrage.</summary>
    public Guid RundfrageId { get; set; }
    /// <summary>Empfangendes Konto.</summary>
    public Guid KontoId { get; set; }
    /// <summary>Mitglied, über das das Konto Empfänger ist (eigenes oder Kind).</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Hat geantwortet; bei anonymen Umfragen nur der Tag, damit sich Antworten nicht zuordnen lassen.</summary>
    public DateOnly? BeantwortetAm { get; set; }
}

/// <summary>
/// Eine Antwort. Bei anonymen Umfragen ohne <see cref="EmpfaengerId"/>, mit Zufallsschlüssel und ohne Bemerkung, damit sie sich nicht
/// über Reihenfolge oder Text einer Person zuordnen lässt.
/// </summary>
public class RundfrageAntwort
{
    /// <summary>Schlüssel; bei anonymen Antworten zufällig, nicht zeitlich geordnet.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Die Rundfrage.</summary>
    public Guid RundfrageId { get; set; }
    /// <summary>Wer geantwortet hat; leer bei anonymen Umfragen.</summary>
    public Guid? EmpfaengerId { get; set; }
    /// <summary>Gewählte Antwortmöglichkeiten (Nummern ab 0).</summary>
    public List<int> Auswahl { get; set; } = [];
    /// <summary>Kurze Bemerkung, z. B. „erst ab 10:00 Uhr“; keine Gesundheitsangaben.</summary>
    public string? Bemerkung { get; set; }
}
