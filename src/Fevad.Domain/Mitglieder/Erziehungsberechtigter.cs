using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Verhältnis eines Erziehungsberechtigten zum Kind.</summary>
public enum Sorgeverhaeltnis
{
    /// <summary>Mutter des Kindes.</summary>
    [Display(Name = "Mutter")] Mutter,
    /// <summary>Vater des Kindes.</summary>
    [Display(Name = "Vater")] Vater,
    /// <summary>Vom Familiengericht bestellter Vormund.</summary>
    [Display(Name = "Vormund")] Vormund,
    /// <summary>Andere sorgeberechtigte Person.</summary>
    [Display(Name = "sonstige")] Sonstige,
}

/// <summary>
/// Erziehungsberechtigte eines minderjährigen Mitglieds (Jugend- und Kinderfeuerwehr), höchstens zwei. Sie unterschreiben
/// Aufnahme und Übertritt und sind Ansprechpartner im Dienstbetrieb.
/// </summary>
public class Erziehungsberechtigter : Entitaet
{
    /// <summary>Schlüssel des minderjährigen Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das minderjährige Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>1 oder 2.</summary>
    public int Nummer { get; set; }

    /// <summary>Verhältnis zum Kind; darf offen bleiben.</summary>
    public Sorgeverhaeltnis? Verhaeltnis { get; set; }
    /// <summary>Vorname des Erziehungsberechtigten.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname des Erziehungsberechtigten.</summary>
    public required string Nachname { get; set; }
    /// <summary>Straße und Hausnummer, wenn abweichend vom Kind.</summary>
    public string? Strasse { get; set; }
    /// <summary>Postleitzahl.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort.</summary>
    public string? Ort { get; set; }
    /// <summary>Festnetznummer.</summary>
    public string? Telefon { get; set; }
    /// <summary>Mobilnummer, für Rückfragen im Dienstbetrieb.</summary>
    public string? Mobil { get; set; }
    /// <summary>E-Mail-Adresse, auch für die Einladung zum Elternkonto.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Elternkonto: Benutzerkonto dieses Erziehungsberechtigten, mit dem er die Termine der Jugend-/Kinderfeuerwehr seines Kindes
    /// sieht und für das Kind zu- oder absagt. Endet mit dem Erziehungsberechtigten (Volljährigkeit, Austritt) oder vorher durch den
    /// Löschlauf (L35), wenn das Kind in keiner Jugend- oder Kinderabteilung mehr ist. Nur Jugendwart und Kommando ordnen zu.
    /// </summary>
    public Guid? KontoId { get; set; }
    /// <summary>Zeitpunkt, zu dem das Elternkonto zugeordnet wurde.</summary>
    public DateTimeOffset? KontoZugeordnetAm { get; set; }

    /// <summary>Datenschutzinformation für Eltern ausgehändigt (Voraussetzung für das Elternkonto).</summary>
    public DateOnly? DatenschutzinformationAm { get; set; }

    /// <summary>Sperre, z. B. bei Sorgerechtsentzug oder Kontaktverbot: kein Elternkonto für diese Person.</summary>
    public bool KeineKontozuordnung { get; set; }

    /// <summary>Vor- und Nachname für Listen und Formulare.</summary>
    public string Anzeigename => $"{Vorname} {Nachname}";
}
