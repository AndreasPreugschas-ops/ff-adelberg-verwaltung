using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Für wen eine Nachweisart verpflichtend ist.</summary>
public enum NachweisPflicht
{
    /// <summary>Freiwillig; erscheint nur als Frist, wenn ein Nachweis eingetragen ist.</summary>
    [Display(Name = "freiwillig")] Keine,
    /// <summary>Pflicht für alle Einsatzkräfte.</summary>
    [Display(Name = "alle Einsatzkräfte")] AlleEinsatzkraefte,
    /// <summary>Pflicht für Mitglieder mit Fahrerlaubnis, z. B. Führerscheinkontrolle.</summary>
    [Display(Name = "Inhaber einer Fahrerlaubnis")] MitFahrerlaubnis,
    /// <summary>Pflicht für Inhaber der Qualifikation aus <see cref="Nachweisart.QualifikationId"/>.</summary>
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
    /// <summary>Für wen der Nachweis Pflicht ist.</summary>
    public NachweisPflicht Pflicht { get; set; }

    /// <summary>Nur bei <see cref="NachweisPflicht.MitQualifikation"/>.</summary>
    public Guid? QualifikationId { get; set; }
    /// <summary>Die Qualifikation, deren Inhaber den Nachweis brauchen.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Erläuterung der Nachweisart für die Oberfläche.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>
    /// Untersuchung oder sonstiger Nachweis mit Gesundheitsbezug (Art. 9 DSGVO): nur für Berechtigte mit
    /// Atemschutz-Recht sichtbar. Gespeichert werden auch dann nur Datum und Frist, nie ein Befund.
    /// </summary>
    public bool Gesundheitsbezogen { get; set; }
}

/// <summary>Eingetragener wiederkehrender Nachweis eines Mitglieds, z. B. eine Führerscheinkontrolle oder Unterweisung.</summary>
public class Nachweis : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel der Nachweisart.</summary>
    public Guid NachweisartId { get; set; }
    /// <summary>Die Nachweisart.</summary>
    public Nachweisart? Nachweisart { get; set; }

    /// <summary>Tag der Kontrolle oder Unterweisung.</summary>
    public DateOnly Datum { get; set; }
    /// <summary>Leer bei einmaligen Nachweisen.</summary>
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Wer kontrolliert bzw. unterwiesen hat, als Freitext.</summary>
    public string? DurchgefuehrtVon { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Gültigkeit eines Nachweises aus dem Intervall der Nachweisart.</summary>
    /// <param name="datum">Tag des Nachweises.</param>
    /// <param name="art">Die Nachweisart.</param>
    /// <returns>Das Ablaufdatum oder null bei einmaligen Nachweisen.</returns>
    public static DateOnly? GueltigkeitAus(DateOnly datum, Nachweisart art) => art.Einmalig ? null : datum.AddMonths(art.IntervallMonate);
}
