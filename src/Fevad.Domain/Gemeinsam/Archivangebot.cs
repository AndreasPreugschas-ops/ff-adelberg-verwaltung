using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Gemeinsam;

/// <summary>Unterlagen, die der Löschlauf vor dem Löschen dem zuständigen Archiv anbietet (V65, Landesarchivgesetz BW).</summary>
public enum Archivgut
{
    /// <summary>Restdatensatz eines ausgetretenen Mitglieds vor der Anonymisierung (L7).</summary>
    [Display(Name = "Mitgliederakte")] Mitgliederakte,
    /// <summary>Kassenjahr mit Buchungen und Belegen vor dem Löschen (L42).</summary>
    [Display(Name = "Kassenjahr")] Kassenjahr,
    /// <summary>Bescheid an ein Mitglied vor dem Löschen (L49).</summary>
    [Display(Name = "Bescheid an Mitglied")] Mitgliederbescheid,
    /// <summary>Einsatzbericht nach der Aufbewahrungsfrist (L2); FEVAD löscht Einsatzberichte nicht selbst.</summary>
    [Display(Name = "Einsatzbericht")] Einsatzbericht,
}

/// <summary>Entscheidung des Archivs über ein Angebot.</summary>
public enum Archiventscheidung
{
    /// <summary>Noch nicht entschieden.</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>Das Archiv übernimmt die Unterlage; gelöscht wird erst nach der Übergabe.</summary>
    [Display(Name = "übernehmen")] Uebernehmen,
    /// <summary>Nicht archivwürdig; darf gelöscht werden.</summary>
    [Display(Name = "nicht archivwürdig")] Kassieren,
}

/// <summary>
/// Angebot einer Unterlage an das Archiv (V65): Der Löschlauf legt es an, sobald die Frist abgelaufen ist, und löscht erst, wenn das
/// Archiv die Unterlage als nicht archivwürdig bewertet hat, sie nach der Übernahme übergeben ist oder das Archiv innerhalb der
/// Entscheidungsfrist nach dem Anbieten nicht geantwortet hat (§ 4 Abs. 3 LArchG). Nach der Löschung bleibt der Eintrag ohne Personenbezug
/// als Nachweis (§ 6 Abs. 4 LArchG).
/// </summary>
public class Archivangebot : Entitaet
{
    /// <summary>Art der Unterlage.</summary>
    public Archivgut Art { get; set; }
    /// <summary>Schlüssel des Datensatzes (Mitglied, Jahresabschluss, Bescheid, Einsatz).</summary>
    public Guid DatensatzId { get; set; }
    /// <summary>Bezeichnung im Anbietungsverzeichnis; nach der Löschung ohne Personenbezug.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Jahr der Entstehung bzw. des Abschlusses der Unterlage.</summary>
    public int Jahr { get; set; }
    /// <summary>Tag, an dem die Aufbewahrungsfrist abgelaufen war und das Angebot entstand.</summary>
    public DateOnly FaelligAm { get; set; }
    /// <summary>Tag, an dem das Anbietungsverzeichnis an das Archiv ging.</summary>
    public DateOnly? AngebotenAm { get; set; }
    /// <summary>Entscheidung des Archivs.</summary>
    public Archiventscheidung Entscheidung { get; set; }
    /// <summary>Tag der Entscheidung.</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Tag, an dem das Archivpaket übergeben wurde.</summary>
    public DateOnly? UebergebenAm { get; set; }
    /// <summary>SHA-256 des übergebenen Archivpakets.</summary>
    public string? Pruefsumme { get; set; }
    /// <summary>Tag, an dem der Löschlauf die Unterlage gelöscht bzw. anonymisiert hat.</summary>
    public DateOnly? ErledigtAm { get; set; }

    /// <summary>Darf der Löschlauf die Unterlage jetzt löschen?</summary>
    /// <param name="heute">Stichtag.</param>
    /// <param name="fristMonate">Entscheidungsfrist des Archivs nach dem Anbieten.</param>
    /// <returns>true nach Kassation, nach der Übergabe oder nach Ablauf der Frist ohne Entscheidung.</returns>
    public bool DarfLoeschen(DateOnly heute, int fristMonate) => Entscheidung switch
    {
        Archiventscheidung.Kassieren => true,
        Archiventscheidung.Uebernehmen => UebergebenAm is not null,
        _ => AngebotenAm is { } am && am.AddMonths(fristMonate) <= heute,
    };
}
