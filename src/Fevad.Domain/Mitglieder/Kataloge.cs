using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art einer Abteilung; an ihr hängen Altersgrenzen, Probezeit und Anrechnung für das Ehrenzeichen.</summary>
public enum AbteilungArt
{
    [Display(Name = "Einsatzabteilung")] Einsatz,
    [Display(Name = "Jugendfeuerwehr")] Jugend,
    [Display(Name = "Kinderfeuerwehr")] Kinder,
    [Display(Name = "Altersabteilung")] Alters,
    [Display(Name = "Musikabteilung")] Musik,
}

/// <summary>
/// Abteilung der Gemeindefeuerwehr. Es kann mehrere Einsatzabteilungen geben (etwa je Ortsteil); die Kinderfeuerwehr
/// hängt als Unterabteilung an der Jugendfeuerwehr. Was nicht gebraucht wird, bleibt inaktiv.
/// </summary>
public class Abteilung : Katalogeintrag
{
    public AbteilungArt Art { get; set; }

    /// <summary>Übergeordnete Abteilung, z. B. die Jugendfeuerwehr für die Kinderfeuerwehr.</summary>
    public Guid? UebergeordneteId { get; set; }
    public Abteilung? Uebergeordnete { get; set; }

    /// <summary>Nur die Einsatzabteilung kennt eine Probezeit (Satzung § 3 Abs. 2).</summary>
    public bool MitProbezeit => Art == AbteilungArt.Einsatz;
}

/// <summary>Dienstgrad nach VwV Feuerwehrbekleidung BW.</summary>
public class Dienstgrad : Katalogeintrag
{
    /// <summary>Mindestzeit bis zur nächsten Beförderung.</summary>
    public int MindestwartezeitMonate { get; set; } = 12;
}

/// <summary>Satzungsfunktion (Kommandant, Gerätewart, Kassier, ...).</summary>
public class Funktion : Katalogeintrag
{
    /// <summary>Amtszeit bei Wahl, z. B. 5 Jahre für Kommandant und Stellvertreter.</summary>
    public int? AmtszeitJahre { get; set; }

    /// <summary>Rolle, die der Administrator bei dieser Funktion typischerweise vergibt. Wird nicht automatisch zugewiesen.</summary>
    public string? VorgeschlageneRolle { get; set; }
}
