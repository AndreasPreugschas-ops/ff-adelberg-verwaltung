using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Stand einer eingereichten Bescheinigung.</summary>
public enum EinreichungStatus
{
    /// <summary>Wartet auf die Prüfung durch die Ausbildung.</summary>
    [Display(Name = "wartet auf Prüfung")] Eingereicht,
    /// <summary>Geprüft und in die Akte übernommen.</summary>
    [Display(Name = "übernommen")] Uebernommen,
    /// <summary>Nicht übernommen, mit Antwort.</summary>
    [Display(Name = "nicht übernommen")] Abgelehnt,
}

/// <summary>
/// Vom Mitglied selbst eingereichter Nachweis (V44), z. B. Erste-Hilfe-Kurs, Führerschein oder ein externer Lehrgang, mit Scan oder Foto zur
/// Prüfung durch die Ausbildung. Bezieht sich auf eine Nachweisart oder eine Qualifikation, sonst auf eine freie Bezeichnung. Gesundheitsbezogene
/// Nachweisarten sind ausgeschlossen. Die Datei wird mit der Entscheidung gelöscht; der Eintrag selbst nach Löschregel L68.
/// </summary>
public class Nachweiseinreichung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Wiederkehrender Nachweis, z. B. Erste-Hilfe-Auffrischung oder Führerscheinkontrolle.</summary>
    public Guid? NachweisartId { get; set; }
    /// <summary>Die Nachweisart.</summary>
    public Nachweisart? Nachweisart { get; set; }

    /// <summary>Lehrgang oder Fahrerlaubnis, z. B. ein Lehrgang an einer anderen Schule.</summary>
    public Guid? QualifikationId { get; set; }
    /// <summary>Die Qualifikation.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Freie Bezeichnung, wenn nichts aus den Katalogen passt.</summary>
    public string? Bezeichnung { get; set; }

    /// <summary>Tag des Kurses, der Prüfung oder der Ausstellung.</summary>
    public DateOnly Datum { get; set; }
    /// <summary>Gültig bis laut Bescheinigung, falls angegeben.</summary>
    public DateOnly? GueltigBis { get; set; }
    /// <summary>Ausstellende Stelle, z. B. „DRK Göppingen“.</summary>
    public string? Aussteller { get; set; }
    /// <summary>Hinweis des Mitglieds.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Dateiname des Scans oder Fotos; leer ohne Datei oder nach der Entscheidung.</summary>
    public string? Dateiname { get; set; }
    /// <summary>Inhaltstyp der Datei.</summary>
    public string? Inhaltstyp { get; set; }
    /// <summary>Inhalt der Datei, Fotos ohne Metadaten; wird mit der Entscheidung gelöscht.</summary>
    public byte[]? Inhalt { get; set; }

    /// <summary>Stand der Prüfung.</summary>
    public EinreichungStatus Status { get; set; }
    /// <summary>Wann geprüft wurde.</summary>
    public DateTimeOffset? GeprueftAm { get; set; }
    /// <summary>Antwort der Ausbildung an das Mitglied.</summary>
    public string? Antwort { get; set; }

    /// <summary>Bezeichnung für Listen: Nachweisart, Qualifikation oder freie Bezeichnung.</summary>
    public string Anzeigename => Nachweisart?.Bezeichnung ?? Qualifikation?.Bezeichnung ?? Bezeichnung ?? "Nachweis";
}
