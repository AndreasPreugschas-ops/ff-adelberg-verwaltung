using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Für wen eine Nachweisart verpflichtend ist.</summary>
public enum NachweisPflicht
{
    [Display(Name = "freiwillig")] Keine,
    [Display(Name = "alle Einsatzkräfte")] AlleEinsatzkraefte,
    [Display(Name = "Inhaber einer Fahrerlaubnis")] MitFahrerlaubnis,
    [Display(Name = "Inhaber einer bestimmten Qualifikation")] MitQualifikation,
}

/// <summary>
/// Wiederkehrender Nachweis, z. B. Führerscheinkontrolle, jährliche Unterweisung, Erste-Hilfe-Auffrischung.
/// Das Intervall ist ein Katalogwert und wird in der Oberfläche gepflegt. Keine Gesundheitsdaten:
/// Atemschutz-Vorsorge und -Übungen haben eigene Klassen mit eingeschränktem Zugriff.
/// </summary>
public class Nachweisart : Katalogeintrag
{
    public int IntervallMonate { get; set; } = 12;
    public NachweisPflicht Pflicht { get; set; }

    /// <summary>Nur bei <see cref="NachweisPflicht.MitQualifikation"/>.</summary>
    public Guid? QualifikationId { get; set; }
    public Qualifikation? Qualifikation { get; set; }

    public string? Beschreibung { get; set; }
}

public class Nachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid NachweisartId { get; set; }
    public Nachweisart? Nachweisart { get; set; }

    public DateOnly Datum { get; set; }
    public DateOnly GueltigBis { get; set; }

    /// <summary>Wer kontrolliert bzw. unterwiesen hat, als Freitext.</summary>
    public string? DurchgefuehrtVon { get; set; }
    public string? Bemerkung { get; set; }

    public static DateOnly GueltigkeitAus(DateOnly datum, Nachweisart art) => datum.AddMonths(art.IntervallMonate);
}
