using System.ComponentModel.DataAnnotations;
using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Mitglieder;

public enum MitgliedStatus
{
    [Display(Name = "Probezeit")] Probezeit,
    [Display(Name = "aktiv")] Aktiv,
    [Display(Name = "befreit")] Befreit,
    [Display(Name = "ausgeschieden")] Ausgeschieden,
}

public enum Austrittsgrund
{
    Altersgrenze,
    ProbezeitNichtBestanden,
    Gesundheit,
    Wohnortwechsel,
    Austritt,
    Ausschluss,
    Tod,
    Uebertritt,
}

/// <summary>
/// Zugehörigkeit zu einer Abteilung über einen Zeitraum. Wird nie überschrieben,
/// sondern beendet und neu angelegt (Dienstjahre, Personalstand zum Stichtag).
/// </summary>
public class Mitgliedschaft : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    public MitgliedStatus Status { get; set; }
    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public Austrittsgrund? Austrittsgrund { get; set; }

    /// <summary>Zeiten in anderen Wehren, für das Ehrenzeichen anrechenbar.</summary>
    public bool Vordienstzeit { get; set; }
    public string? Bemerkung { get; set; }

    public bool IstAktivAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);

    public DateOnly ProbezeitEnde(int probezeitMonate) => GueltigAb.AddMonths(probezeitMonate);
}
