using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Mitglieder;

/// <summary>Einsatz-, Jugend- oder Altersabteilung.</summary>
public class Abteilung : Katalogeintrag
{
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
