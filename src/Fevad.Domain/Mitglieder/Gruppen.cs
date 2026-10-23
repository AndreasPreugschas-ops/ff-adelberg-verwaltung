using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public enum GruppeArt
{
    [Display(Name = "Gruppe")] Gruppe,
    [Display(Name = "Zug")] Zug,
}

/// <summary>
/// Untergliederung einer Abteilung: Zug oder Gruppe (z. B. Zug 1, Gruppe 1, Jugendgruppe). Eine Gruppe kann zu einem Zug
/// gehören. Ohne Abteilung gilt sie abteilungsübergreifend (z. B. Führungsgruppe). Dient auch als Zielgruppe für Übungen.
/// </summary>
public class Gruppe : Katalogeintrag
{
    public GruppeArt Art { get; set; }

    public Guid? AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    /// <summary>Zug, zu dem die Gruppe gehört.</summary>
    public Guid? UebergeordneteId { get; set; }
    public Gruppe? Uebergeordnete { get; set; }

    public List<GruppenMitgliedschaft> Mitglieder { get; set; } = [];
}

public class GruppenMitgliedschaft : Entitaet
{
    public Guid GruppeId { get; set; }
    public Gruppe? Gruppe { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }
}
