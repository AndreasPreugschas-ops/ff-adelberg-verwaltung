using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>Art einer anderen Organisation am Einsatzort.</summary>
public enum OrganisationArt
{
    /// <summary>Polizei.</summary>
    [Display(Name = "Polizei")] Polizei,
    /// <summary>Rettungsdienst.</summary>
    [Display(Name = "Rettungsdienst")] Rettungsdienst,
    /// <summary>Technisches Hilfswerk.</summary>
    [Display(Name = "THW")] Thw,
    /// <summary>Kreisbrandmeister.</summary>
    [Display(Name = "Kreisbrandmeister")] Kreisbrandmeister,
    /// <summary>Eine andere Feuerwehr.</summary>
    [Display(Name = "andere Feuerwehr")] Feuerwehr,
    /// <summary>Sonstige Organisation.</summary>
    [Display(Name = "Sonstige")] Sonstige,
}

/// <summary>Andere Organisation am Einsatzort mit Ansprechpartner (Polizei, Rettungsdienst, THW, Kreisbrandmeister ...).</summary>
public class EinsatzOrganisation : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Art der Organisation.</summary>
    public OrganisationArt Art { get; set; }

    /// <summary>Dienststelle oder Einheit, z. B. "Polizeirevier Schorndorf", "DRK Rettungswache Göppingen".</summary>
    public string? Bezeichnung { get; set; }

    /// <summary>Ansprechpartner vor Ort.</summary>
    public string? Ansprechpartner { get; set; }
    /// <summary>Telefon des Ansprechpartners.</summary>
    public string? Telefon { get; set; }

    /// <summary>Eigenes Aktenzeichen der Organisation, z. B. Tagebuchnummer der Polizei.</summary>
    public string? Aktenzeichen { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Rolle einer beteiligten Person oder Firma.</summary>
public enum BeteiligtenRolle
{
    /// <summary>Geschädigter.</summary>
    [Display(Name = "Geschädigter")] Geschaedigter,
    /// <summary>Verursacher oder Fahrzeughalter, meist kostenersatzpflichtig.</summary>
    [Display(Name = "Verursacher / Fahrzeughalter")] Verursacher,
    /// <summary>Wer den Einsatz gemeldet hat.</summary>
    [Display(Name = "Meldender")] Meldender,
    /// <summary>Sonstiger Beteiligter.</summary>
    [Display(Name = "Sonstiger Beteiligter")] Sonstiger,
}

/// <summary>
/// Person oder Firma, die am Einsatz beteiligt war: Geschädigte, Verursacher, Meldende, Sonstige.
/// Daten nur so weit, wie sie für Kostenersatz, Rückfragen und Versicherung nötig sind (Datenminimierung);
/// sie werden mit dem Einsatzbericht nach dessen Aufbewahrungsfrist gelöscht.
/// </summary>
public class EinsatzBeteiligter : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Rolle im Einsatz.</summary>
    public BeteiligtenRolle Rolle { get; set; }

    /// <summary>Name der Person oder Firma.</summary>
    public required string Name { get; set; }
    /// <summary>Anschrift.</summary>
    public string? Anschrift { get; set; }
    /// <summary>Telefon.</summary>
    public string? Telefon { get; set; }

    /// <summary>Beteiligtes Fahrzeug: Kennzeichen und Fahrzeugbeschreibung.</summary>
    public string? Kennzeichen { get; set; }
    /// <summary>Beschreibung des beteiligten Fahrzeugs, z. B. Marke und Farbe.</summary>
    public string? Fahrzeugbeschreibung { get; set; }

    /// <summary>Haftpflicht- bzw. Kfz-Versicherung des Beteiligten und Versicherungsscheinnummer.</summary>
    public string? Versicherung { get; set; }
    /// <summary>Nummer des Versicherungsscheins.</summary>
    public string? Versicherungsnummer { get; set; }

    /// <summary>Keine Angaben zu Verletzungen oder Gesundheit (Art. 9 DSGVO).</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Ausrüstung oder Verbrauchsmaterial, das zusätzlich zu Fahrzeugen und Personal in den Kostenersatz eingeht.</summary>
public class EinsatzAusruestung : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Kennung des Kostensatzes der Ausrüstung.</summary>
    public Guid KostensatzId { get; set; }
    /// <summary>Der Kostensatz.</summary>
    public Kostensatz? Kostensatz { get; set; }

    /// <summary>Menge in der Einheit des Kostensatzes (Stunden, Stück, Tage; bei Pauschalen 1).</summary>
    public decimal Menge { get; set; } = 1;

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }
}
