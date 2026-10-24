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
/// Das Intervall ist ein Katalogwert und wird in der Oberfläche gepflegt. Atemschutz-Vorsorge und
/// -Übungen haben eigene Klassen; andere Untersuchungen werden als <see cref="Gesundheitsbezogen"/> markiert.
/// </summary>
public class Nachweisart : Katalogeintrag
{
    /// <summary>Gültigkeit ab dem Datum des Nachweises; wird beim Eintragen vorgeschlagen.</summary>
    public int IntervallMonate { get; set; } = 12;

    /// <summary>Einmalig nötig, läuft nicht ab (z. B. eine Erstunterweisung): ohne Ablaufdatum und ohne Frist, sobald eingetragen.</summary>
    public bool Einmalig { get; set; }
    public NachweisPflicht Pflicht { get; set; }

    /// <summary>Nur bei <see cref="NachweisPflicht.MitQualifikation"/>.</summary>
    public Guid? QualifikationId { get; set; }
    public Qualifikation? Qualifikation { get; set; }

    public string? Beschreibung { get; set; }

    /// <summary>
    /// Untersuchung oder sonstiger Nachweis mit Gesundheitsbezug (Art. 9 DSGVO): nur für Berechtigte mit
    /// Atemschutz-Recht sichtbar. Gespeichert werden auch dann nur Datum und Frist, nie ein Befund.
    /// </summary>
    public bool Gesundheitsbezogen { get; set; }
}

public class Nachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid NachweisartId { get; set; }
    public Nachweisart? Nachweisart { get; set; }

    public DateOnly Datum { get; set; }
    /// <summary>Leer bei einmaligen Nachweisen.</summary>
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Wer kontrolliert bzw. unterwiesen hat, als Freitext.</summary>
    public string? DurchgefuehrtVon { get; set; }
    public string? Bemerkung { get; set; }

    public static DateOnly? GueltigkeitAus(DateOnly datum, Nachweisart art) => art.Einmalig ? null : datum.AddMonths(art.IntervallMonate);
}
