using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Kleiderkammer;

/// <summary>Warum ein Mitglied ein Stück seiner Ausstattung tauschen möchte.</summary>
public enum Tauschgrund
{
    /// <summary>Die Größe passt nicht.</summary>
    [Display(Name = "Größe passt nicht")] Groesse,
    /// <summary>Verschlissen.</summary>
    [Display(Name = "verschlissen")] Verschlissen,
    /// <summary>Beschädigt oder verschmutzt; schwere Mängel zusätzlich als Mangel melden.</summary>
    [Display(Name = "beschädigt oder stark verschmutzt")] Beschaedigt,
    /// <summary>Verloren.</summary>
    [Display(Name = "verloren")] Verloren,
    /// <summary>Anderer Grund, steht in der Bemerkung.</summary>
    [Display(Name = "anderer Grund")] Sonstiges,
}

/// <summary>Bearbeitungsstand einer Tauschanforderung.</summary>
public enum TauschStatus
{
    /// <summary>Wartet auf die Kleiderkammer.</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>Getauscht oder anders erledigt.</summary>
    [Display(Name = "erledigt")] Erledigt,
    /// <summary>Abgelehnt, mit Antwort.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
}

/// <summary>
/// Bitte eines Mitglieds an die Kleiderkammer, ein ausgegebenes Stück zu tauschen (V44). Der eigentliche Tausch läuft weiter als Rücknahme und
/// Ausgabe mit Quittung; die Anforderung hält nur Wunsch, Grund und Antwort fest. Gelöscht nach Löschregel L67.
/// </summary>
public class Tauschanforderung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel des Stücks, das getauscht werden soll.</summary>
    public Guid AusstattungId { get; set; }
    /// <summary>Das Stück.</summary>
    public Ausstattung? Ausstattung { get; set; }

    /// <summary>Grund des Tauschs.</summary>
    public Tauschgrund Grund { get; set; }
    /// <summary>Gewünschte Größe, wenn sie sich ändern soll.</summary>
    public string? GewuenschteGroesse { get; set; }
    /// <summary>Freitext des Mitglieds.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public TauschStatus Status { get; set; }
    /// <summary>Wann die Kleiderkammer die Anforderung erledigt oder abgelehnt hat.</summary>
    public DateTimeOffset? BearbeitetAm { get; set; }
    /// <summary>Antwort der Kleiderkammer, z. B. Termin zur Ausgabe.</summary>
    public string? Antwort { get; set; }
}
