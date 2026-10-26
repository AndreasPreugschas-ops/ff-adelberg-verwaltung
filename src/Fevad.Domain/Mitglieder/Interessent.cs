using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Wie ein Eintrag auf der Warteliste ausgegangen ist.</summary>
public enum Wartelistenergebnis
{
    /// <summary>Aufnahmeantrag gestellt oder aufgenommen.</summary>
    [Display(Name = "aufgenommen")] Aufgenommen,
    /// <summary>Die Familie hat kein Interesse mehr.</summary>
    [Display(Name = "kein Interesse mehr")] KeinInteresse,
    /// <summary>Nicht erreichbar oder aus anderen Gründen beendet.</summary>
    [Display(Name = "beendet")] Beendet,
}

/// <summary>
/// Kind auf der Warteliste der Jugend- oder Kinderfeuerwehr (V50): Interessenten, die noch zu jung sind oder auf einen Platz warten. Die
/// Wiedervorlage ist der Tag, an dem das Kind das Mindestalter der Abteilung erreicht. Ein Schnupperdienst braucht die Zustimmung der
/// Eltern. Gespeichert wird nur, was für die Kontaktaufnahme nötig ist; Löschung nach der Frist ab Wiedervorlage oder Abschluss (L56).
/// </summary>
public class Interessent : Entitaet
{
    /// <summary>Vorname des Kindes.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname des Kindes.</summary>
    public required string Nachname { get; set; }
    /// <summary>Geburtsdatum des Kindes.</summary>
    public DateOnly Geburtsdatum { get; set; }
    /// <summary>Gewünschte Jugend- oder Kinderfeuerwehr.</summary>
    public Guid AbteilungId { get; set; }
    /// <summary>Die Abteilung.</summary>
    public Abteilung? Abteilung { get; set; }
    /// <summary>Name eines Elternteils als Ansprechpartner.</summary>
    public required string Ansprechpartner { get; set; }
    /// <summary>Telefon des Ansprechpartners.</summary>
    public string? Telefon { get; set; }
    /// <summary>E-Mail des Ansprechpartners.</summary>
    public string? Email { get; set; }
    /// <summary>Tag der Aufnahme in die Warteliste.</summary>
    public DateOnly EingetragenAm { get; set; }
    /// <summary>Tag, an dem sich der Jugendwart meldet: Erreichen des Mindestalters, frühestens der Eintrag.</summary>
    public DateOnly Wiedervorlage { get; set; }
    /// <summary>Freie Bemerkung, z. B. „Bruder ist schon dabei“.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Dienst, an dem das Kind schnuppert.</summary>
    public Guid? SchnupperdienstId { get; set; }
    /// <summary>Der Schnupperdienst.</summary>
    public Dienst? Schnupperdienst { get; set; }
    /// <summary>Tag, an dem die Eltern dem Schnupperdienst zugestimmt haben.</summary>
    public DateOnly? SchnupperZustimmungAm { get; set; }

    /// <summary>Tag, an dem der Eintrag erledigt wurde.</summary>
    public DateOnly? ErledigtAm { get; set; }
    /// <summary>Wie der Eintrag ausgegangen ist.</summary>
    public Wartelistenergebnis? Ergebnis { get; set; }

    /// <summary>Anzeigename „Nachname, Vorname“.</summary>
    public string Anzeigename => $"{Nachname}, {Vorname}";

    /// <summary>Tag der Wiedervorlage: Erreichen des Mindestalters, frühestens der Stichtag.</summary>
    /// <param name="geburtsdatum">Geburtsdatum des Kindes.</param>
    /// <param name="mindestalter">Mindestalter der Abteilung in Jahren.</param>
    /// <param name="heute">Stichtag.</param>
    /// <returns>Der Tag der Wiedervorlage.</returns>
    public static DateOnly WiedervorlageAm(DateOnly geburtsdatum, int mindestalter, DateOnly heute)
    {
        var alt = geburtsdatum.AddYears(mindestalter);
        return alt > heute ? alt : heute;
    }

    /// <summary>Ab diesem Tag darf der Eintrag gelöscht werden (L56): Frist ab Abschluss, sonst ab Wiedervorlage.</summary>
    /// <param name="monate">Frist in Monaten.</param>
    /// <returns>Der erste Tag, an dem gelöscht wird.</returns>
    public DateOnly LoeschenAb(int monate) => (ErledigtAm ?? Wiedervorlage).AddMonths(monate);
}
