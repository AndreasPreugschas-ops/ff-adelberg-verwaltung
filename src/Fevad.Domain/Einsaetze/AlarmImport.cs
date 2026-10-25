using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>Woher ein Alarm kommt.</summary>
public enum AlarmQuelle
{
    /// <summary>HTTP-Ausgang von alamos FE2.</summary>
    Fe2Webhook,
    /// <summary>Feedback-Schnittstelle von alamos FE2.</summary>
    Fe2Feedback,
    /// <summary>Von Hand erfasst.</summary>
    Manuell,
    /// <summary>Übernahme aus MP-Feuer.</summary>
    MpFeuerMigration,
}

/// <summary>
/// Rohdaten eines Alarms, getrennt vom Einsatzbericht. Alarmdaten dürfen höchstens
/// sechs Monate gespeichert werden (IM BW, § 35 Abs. 6 FwG); der Bericht bleibt länger.
/// </summary>
public class AlarmImport : Entitaet
{
    /// <summary>Quelle des Alarms.</summary>
    public AlarmQuelle Quelle { get; set; }

    /// <summary>Eindeutig je Quelle, damit doppelt gesendete Alarme nur einmal ankommen.</summary>
    public required string ExterneId { get; set; }
    /// <summary>Zeitpunkt des Empfangs (UTC).</summary>
    public DateTimeOffset EmpfangenAm { get; set; }

    /// <summary>Vollständige Payload als JSON, weil das FE2-Ausgangsformat erst an der echten Instanz feststeht.</summary>
    [NichtProtokollieren]
    public required string Rohdaten { get; set; }
    /// <summary>Daraus wurde schon ein Einsatzbericht angelegt oder ergänzt.</summary>
    public bool Verarbeitet { get; set; }

    /// <summary>Kennung des zugehörigen Einsatzberichts.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Der zugehörige Einsatzbericht.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Ab diesem Tag löscht der Löschlauf die Rohdaten.</summary>
    public DateOnly LoeschenAb { get; set; }

    /// <summary>Legt einen empfangenen Alarm mit Löschtermin an.</summary>
    /// <param name="quelle">Quelle des Alarms.</param>
    /// <param name="externeId">Kennung in der Quelle.</param>
    /// <param name="rohdaten">Payload als JSON.</param>
    /// <param name="jetzt">Empfangszeitpunkt.</param>
    /// <param name="aufbewahrungMonate">Aufbewahrung in Monaten (Parameter).</param>
    /// <returns>Der neue Alarm.</returns>
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
