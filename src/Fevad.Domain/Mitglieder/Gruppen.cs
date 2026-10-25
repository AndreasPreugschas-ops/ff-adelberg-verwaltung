using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art einer Untergliederung.</summary>
public enum GruppeArt
{
    /// <summary>Gruppe, ggf. Teil eines Zugs.</summary>
    [Display(Name = "Gruppe")] Gruppe,
    /// <summary>Zug aus mehreren Gruppen.</summary>
    [Display(Name = "Zug")] Zug,
}

/// <summary>
/// Untergliederung einer Abteilung: Zug oder Gruppe (z. B. Zug 1, Gruppe 1, Jugendgruppe). Eine Gruppe kann zu einem Zug
/// gehören. Ohne Abteilung gilt sie abteilungsübergreifend (z. B. Führungsgruppe). Dient auch als Zielgruppe für Übungen.
/// </summary>
public class Gruppe : Katalogeintrag
{
    /// <summary>Zug oder Gruppe.</summary>
    public GruppeArt Art { get; set; }

    /// <summary>Schlüssel der Abteilung; leer bei abteilungsübergreifenden Gruppen.</summary>
    public Guid? AbteilungId { get; set; }
    /// <summary>Die Abteilung, zu der die Gruppe gehört.</summary>
    public Abteilung? Abteilung { get; set; }

    /// <summary>Zug, zu dem die Gruppe gehört.</summary>
    public Guid? UebergeordneteId { get; set; }
    /// <summary>Der Zug, zu dem die Gruppe gehört.</summary>
    public Gruppe? Uebergeordnete { get; set; }

    /// <summary>Zuordnungen der Mitglieder zu dieser Gruppe.</summary>
    public List<GruppenMitgliedschaft> Mitglieder { get; set; } = [];
}

/// <summary>Zuordnung eines Mitglieds zu einer Gruppe oder einem Zug.</summary>
public class GruppenMitgliedschaft : Entitaet
{
    /// <summary>Schlüssel der Gruppe.</summary>
    public Guid GruppeId { get; set; }
    /// <summary>Die Gruppe.</summary>
    public Gruppe? Gruppe { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }
}
