using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Gemeinsam;

/// <summary>Art einer Meldung aus den Betriebsskripten.</summary>
public enum BetriebsmeldungArt
{
    /// <summary>Nächtliche Sicherung (deploy/sicherung.sh).</summary>
    [Display(Name = "Sicherung")] Sicherung,
    /// <summary>Wöchentlicher Restore-Test (deploy/sicherung-testen.sh).</summary>
    [Display(Name = "Restore-Test")] Restoretest,
}

/// <summary>
/// Ergebnis eines Betriebslaufs auf dem Server, z. B. der Sicherung oder des Restore-Tests (V71). Die Skripte schreiben die Zeile direkt per
/// psql; die Betriebsseite zeigt daraus das Alter der letzten Sicherung. Keine personenbezogenen Daten, kein Änderungsprotokoll.
/// </summary>
public class Betriebsmeldung
{
    /// <summary>Eindeutiger Schlüssel.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Art des Laufs.</summary>
    public BetriebsmeldungArt Art { get; set; }
    /// <summary>Ende des Laufs in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Der Lauf war erfolgreich.</summary>
    public bool Erfolgreich { get; set; }
    /// <summary>Kurzer Text, z. B. Größe des Dumps oder Fehlermeldung.</summary>
    public string? Text { get; set; }
}
