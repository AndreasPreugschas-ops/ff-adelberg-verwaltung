using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

public enum AlarmQuelle
{
    Fe2Webhook,
    Fe2Feedback,
    Manuell,
    MpFeuerMigration,
}

/// <summary>
/// Rohdaten eines Alarms, getrennt vom Einsatzbericht. Alarmdaten dürfen höchstens
/// sechs Monate gespeichert werden (IM BW, § 35 Abs. 6 FwG); der Bericht bleibt länger.
/// </summary>
public class AlarmImport : Entitaet
{
    public AlarmQuelle Quelle { get; set; }

    /// <summary>Eindeutig je Quelle, damit doppelt gesendete Alarme nur einmal ankommen.</summary>
    public required string ExterneId { get; set; }
    public DateTimeOffset EmpfangenAm { get; set; }

    /// <summary>Vollständige Payload als JSON, weil das FE2-Ausgangsformat erst an der echten Instanz feststeht.</summary>
    [NichtProtokollieren]
    public required string Rohdaten { get; set; }
    public bool Verarbeitet { get; set; }

    public Guid? EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    public DateOnly LoeschenAb { get; set; }

    public static AlarmImport Empfangen(AlarmQuelle quelle, string externeId, string rohdaten, DateTimeOffset jetzt, int aufbewahrungMonate) =>
        new()
        {
            Quelle = quelle,
            ExterneId = externeId,
            Rohdaten = rohdaten,
            EmpfangenAm = jetzt,
            LoeschenAb = DateOnly.FromDateTime(jetzt.UtcDateTime).AddMonths(aufbewahrungMonate),
        };
}
