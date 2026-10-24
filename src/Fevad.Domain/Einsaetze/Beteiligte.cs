using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

public enum OrganisationArt
{
    [Display(Name = "Polizei")] Polizei,
    [Display(Name = "Rettungsdienst")] Rettungsdienst,
    [Display(Name = "THW")] Thw,
    [Display(Name = "Kreisbrandmeister")] Kreisbrandmeister,
    [Display(Name = "andere Feuerwehr")] Feuerwehr,
    [Display(Name = "Sonstige")] Sonstige,
}

/// <summary>Andere Organisation am Einsatzort mit Ansprechpartner (Polizei, Rettungsdienst, THW, Kreisbrandmeister ...).</summary>
public class EinsatzOrganisation : Entitaet
{
    public Guid EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    public OrganisationArt Art { get; set; }

    /// <summary>Dienststelle oder Einheit, z. B. "Polizeirevier Schorndorf", "DRK Rettungswache Göppingen".</summary>
    public string? Bezeichnung { get; set; }

    public string? Ansprechpartner { get; set; }
    public string? Telefon { get; set; }

    /// <summary>Eigenes Aktenzeichen der Organisation, z. B. Tagebuchnummer der Polizei.</summary>
    public string? Aktenzeichen { get; set; }

    public string? Bemerkung { get; set; }
}

public enum BeteiligtenRolle
{
    [Display(Name = "Geschädigter")] Geschaedigter,
    [Display(Name = "Verursacher / Fahrzeughalter")] Verursacher,
    [Display(Name = "Meldender")] Meldender,
    [Display(Name = "Sonstiger Beteiligter")] Sonstiger,
}

/// <summary>
/// Person oder Firma, die am Einsatz beteiligt war: Geschädigte, Verursacher, Meldende, Sonstige.
/// Daten nur so weit, wie sie für Kostenersatz, Rückfragen und Versicherung nötig sind (Datenminimierung);
/// sie werden mit dem Einsatzbericht nach dessen Aufbewahrungsfrist gelöscht.
/// </summary>
public class EinsatzBeteiligter : Entitaet
{
    public Guid EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    public BeteiligtenRolle Rolle { get; set; }

    public required string Name { get; set; }
    public string? Anschrift { get; set; }
    public string? Telefon { get; set; }

    /// <summary>Beteiligtes Fahrzeug: Kennzeichen und Fahrzeugbeschreibung.</summary>
    public string? Kennzeichen { get; set; }
    public string? Fahrzeugbeschreibung { get; set; }

    /// <summary>Haftpflicht- bzw. Kfz-Versicherung des Beteiligten und Versicherungsscheinnummer.</summary>
    public string? Versicherung { get; set; }
    public string? Versicherungsnummer { get; set; }

    /// <summary>Keine Angaben zu Verletzungen oder Gesundheit (Art. 9 DSGVO).</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Ausrüstung oder Verbrauchsmaterial, das zusätzlich zu Fahrzeugen und Personal in den Kostenersatz eingeht.</summary>
public class EinsatzAusruestung : Entitaet
{
    public Guid EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    public Guid KostensatzId { get; set; }
    public Kostensatz? Kostensatz { get; set; }

    /// <summary>Menge in der Einheit des Kostensatzes (Stunden, Stück, Tage; bei Pauschalen 1).</summary>
    public decimal Menge { get; set; } = 1;

    public string? Bemerkung { get; set; }
}
