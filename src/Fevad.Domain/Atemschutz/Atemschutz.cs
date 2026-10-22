using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Atemschutz;

public enum VorsorgeErgebnis
{
    [Display(Name = "keine Bedenken")] Geeignet,
    [Display(Name = "befristet keine Bedenken")] GeeignetBefristet,
    [Display(Name = "Bedenken")] NichtGeeignet,
}

/// <summary>
/// Arbeitsmedizinische Vorsorge. Gesundheitsdatum nach Art. 9 DSGVO:
/// gespeichert werden nur Datum, Ergebnis-Status und Frist, niemals der Befund.
/// </summary>
public class AtemschutzVorsorge : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateOnly UntersuchtAm { get; set; }
    public VorsorgeErgebnis Ergebnis { get; set; }
    public DateOnly NaechsteFaellig { get; set; }

    /// <summary>Kurze organisatorische Notiz, kein Befund.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>
    /// Vorschlag für die nächste Vorsorge: Standardintervall, ab einem Lebensalter das kürzere Intervall.
    /// Maßgeblich ist das Alter am Untersuchungstag. Die Bescheinigung des Arztes geht vor.
    /// </summary>
    public static DateOnly NaechsteVorschlagen(DateOnly untersuchtAm, DateOnly geburtsdatum, int intervallMonate, int intervallAbAlterMonate, int abAlter) =>
        untersuchtAm.AddMonths(Mitglied.Alter(geburtsdatum, untersuchtAm) >= abAlter ? intervallAbAlterMonate : intervallMonate);
}

public enum AtemschutzNachweisArt
{
    [Display(Name = "Einsatz")] Einsatz,
    [Display(Name = "Belastungsübung (Übungsanlage)")] Belastungsuebung,
    [Display(Name = "Einsatzübung")] Einsatzuebung,
    [Display(Name = "Unterweisung")] Unterweisung,
}

/// <summary>Eintrag im Atemschutznachweis nach FwDV 7.</summary>
public class AtemschutzNachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateOnly Datum { get; set; }
    public AtemschutzNachweisArt Art { get; set; }
    public string? Geraetetyp { get; set; }
    public int? Minuten { get; set; }
    public string? Taetigkeit { get; set; }
    public string? Ort { get; set; }

    public Guid? EinsatzId { get; set; }
    public Guid? DienstId { get; set; }
}
