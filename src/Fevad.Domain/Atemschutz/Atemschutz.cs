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

    /// <summary>Inventar- oder Gerätenummer des Pressluftatmers, damit sich Gerät und Träger zuordnen lassen.</summary>
    public string? PaGeraet { get; set; }

    /// <summary>Nummer der Atemschutzmaske.</summary>
    public string? Maske { get; set; }

    /// <summary>
    /// Gefahrstoffe, denen der Träger ausgesetzt war (z. B. Brandrauch, Asbest), für den Expositionsnachweis nach § 14 Abs. 3 GefStoffV.
    /// Kein Befund; aufbewahrt wie der Nachweis (L8, 40 Jahre).
    /// </summary>
    public string? Exposition { get; set; }

    /// <summary>Gehört in den Expositionsnachweis: Einsätze und Einsatzübungen sowie alles mit eingetragenem Gefahrstoff.</summary>
    public bool Expositionsrelevant => Art is AtemschutzNachweisArt.Einsatz or AtemschutzNachweisArt.Einsatzuebung || Exposition is not null;

    public Guid? EinsatzId { get; set; }
    public Guid? DienstId { get; set; }
}
