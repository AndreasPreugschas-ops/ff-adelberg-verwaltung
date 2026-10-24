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

    /// <summary>Anschrift, meist das Gerätehaus; Pflicht beim Bearbeiten, erscheint auf den Formularen.</summary>
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }

    public string? Anschrift => Gemeinsam.Anschrift.Zeile(Strasse, Plz, Ort);

    /// <summary>Nur die Einsatzabteilung kennt eine Probezeit (Satzung § 3 Abs. 2).</summary>
    public bool MitProbezeit => Art == AbteilungArt.Einsatz;
}

/// <summary>Dienstgrad nach VwV Feuerwehrbekleidung BW.</summary>
public class Dienstgrad : Katalogeintrag
{
    /// <summary>Mindestzeit bis zur nächsten Beförderung.</summary>
    public int MindestwartezeitMonate { get; set; } = 12;
}

/// <summary>Funktion nach Feuerwehrgesetz, Satzung, Dienstvorschriften oder UVV (Kommandant, Gerätewart, Sicherheitsbeauftragter, ...).</summary>
public class Funktion : Katalogeintrag
{
    /// <summary>Rechtsgrundlage zur Anzeige, z. B. "FwG BW § 8" oder "DGUV Vorschrift 1 § 20".</summary>
    public string? Grundlage { get; set; }

    /// <summary>Nötige Qualifikationen wie bei Lehrgängen: "GF|ZF" heißt GF oder ZF, Gruppen mit ";" müssen alle erfüllt sein.</summary>
    public string? Voraussetzungen { get; set; }

    /// <summary>Die Funktion gehört zu einer Abteilung dieser Art (Abteilungskommandant, Jugendfeuerwehrwart); null für die ganze Feuerwehr.</summary>
    public AbteilungArt? Abteilungsart { get; set; }

    /// <summary>Wird gewählt (Hauptversammlung, Abteilung). Mit <see cref="MitBeschluss"/> zusätzlich bestätigt, z. B. Kommandant.</summary>
    public bool MitWahl { get; set; }

    /// <summary>Braucht einen Beschluss bzw. eine Bestellung durch <see cref="BeschlussDurch"/>.</summary>
    public bool MitBeschluss { get; set; } = true;

    /// <summary>Wer beschließt oder bestellt, z. B. "Gemeinderat" oder "Feuerwehrkommandant".</summary>
    public string? BeschlussDurch { get; set; }

    /// <summary>Amtszeit bei Wahl, z. B. 5 Jahre für Kommandant und Stellvertreter.</summary>
    public int? AmtszeitJahre { get; set; }

    /// <summary>Rolle, die der Administrator bei dieser Funktion typischerweise vergibt. Wird nicht automatisch zugewiesen.</summary>
    public string? VorgeschlageneRolle { get; set; }
}
