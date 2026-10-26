using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Ein Stand der Alarm- und Ausrückeordnung (V20). Die AAO wird als Ganzes beschlossen und dem Bürgermeister mitgeteilt (§ 9 FwG);
/// ein beschlossener Stand bleibt unverändert, Änderungen kommen als neuer Stand mit eigenem Gültigkeitsbeginn.
/// FEVAD alarmiert nicht; die AAO dient nur dem Soll-Ist-Vergleich im Einsatzbericht und als Nachweis.
/// </summary>
public class AaoStand : Entitaet
{
    /// <summary>Bezeichnung, z. B. „AAO 2027“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Erster Tag, an dem der Stand gilt.</summary>
    public DateOnly GueltigAb { get; set; }

    /// <summary>Tag des Beschlusses (Feuerwehrausschuss bzw. Kommandant); ab dann ist der Stand gesperrt.</summary>
    public DateOnly? BeschlossenAm { get; set; }

    /// <summary>Tag, an dem die AAO dem Bürgermeister mitgeteilt wurde (§ 9 FwG).</summary>
    public DateOnly? BuergermeisterMitgeteiltAm { get; set; }

    /// <summary>Bemerkung, z. B. Anlass der Änderung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Regeln je Stichwort.</summary>
    public List<AaoRegel> Regeln { get; set; } = [];

    /// <summary>Ein beschlossener Stand wird nicht mehr geändert.</summary>
    public bool Gesperrt => BeschlossenAm is not null;
}

/// <summary>Eine Zeile der AAO: Stichwort → Alarmstufe → Fahrzeuge → Mindeststärke.</summary>
public class AaoRegel : Entitaet
{
    /// <summary>Kennung des Stands.</summary>
    public Guid AaoStandId { get; set; }

    /// <summary>Der Stand.</summary>
    public AaoStand? AaoStand { get; set; }

    /// <summary>Einsatzstichwort wie von der Leitstelle, z. B. „B 2“ oder „TH 1“.</summary>
    public required string Stichwort { get; set; }

    /// <summary>Alarmstufe oder Alarmierungsumfang, z. B. „Gesamtwehr“ oder „Abteilung Hundsholz + DLK Göppingen“.</summary>
    public string? Alarmstufe { get; set; }

    /// <summary>Eigene Fahrzeuge, die laut AAO ausrücken sollen.</summary>
    public List<Guid> Fahrzeuge { get; set; } = [];

    /// <summary>Mindeststärke an Einsatzkräften.</summary>
    public int? Mindeststaerke { get; set; }

    /// <summary>Mindestzahl an Atemschutzgeräteträgern.</summary>
    public int? MindestAtemschutz { get; set; }

    /// <summary>Bemerkung, z. B. nachzufordernde Kräfte anderer Wehren.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Vereinheitlicht ein Stichwort für den Vergleich: ohne Leerzeichen, Großbuchstaben.</summary>
    /// <param name="stichwort">Das Stichwort.</param>
    /// <returns>Der Schlüssel.</returns>
    public static string Schluessel(string? stichwort) =>
        new string((stichwort ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>
    /// Vergleicht den Einsatz mit der Regel und nennt die Abweichungen. Mehr Kräfte oder zusätzliche Fahrzeuge sind keine Abweichung.
    /// </summary>
    /// <param name="fahrzeugeIst">Eigene Fahrzeuge im Einsatz.</param>
    /// <param name="staerkeIst">Zahl der Einsatzkräfte.</param>
    /// <param name="atemschutzIst">Zahl der Atemschutzgeräteträger.</param>
    /// <param name="namen">Liefert den Anzeigenamen eines Fahrzeugs.</param>
    /// <returns>Die Abweichungen in Worten; leer, wenn die AAO erfüllt ist.</returns>
    public IReadOnlyList<string> Abweichungen(IReadOnlyCollection<Guid> fahrzeugeIst, int staerkeIst, int atemschutzIst, Func<Guid, string> namen)
    {
        var liste = new List<string>();
        var fehlend = Fahrzeuge.Where(f => !fahrzeugeIst.Contains(f)).Select(namen).ToList();
        if (fehlend.Count > 0)
        {
            liste.Add("Nicht ausgerückt: " + string.Join(", ", fehlend));
        }

        if (Mindeststaerke is { } staerke && staerkeIst < staerke)
        {
            liste.Add($"Stärke {staerkeIst} statt mindestens {staerke}");
        }

        if (MindestAtemschutz is { } agt && atemschutzIst < agt)
        {
            liste.Add($"{atemschutzIst} Atemschutzgeräteträger statt mindestens {agt}");
        }

        return liste;
    }
}
