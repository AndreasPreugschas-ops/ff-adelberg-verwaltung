using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public enum Sorgeverhaeltnis
{
    [Display(Name = "Mutter")] Mutter,
    [Display(Name = "Vater")] Vater,
    [Display(Name = "Vormund")] Vormund,
    [Display(Name = "sonstige")] Sonstige,
}

/// <summary>
/// Erziehungsberechtigte eines minderjährigen Mitglieds (Jugend- und Kinderfeuerwehr), höchstens zwei. Sie unterschreiben
/// Aufnahme und Übertritt und sind Ansprechpartner im Dienstbetrieb.
/// </summary>
public class Erziehungsberechtigter : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    /// <summary>1 oder 2.</summary>
    public int Nummer { get; set; }

    public Sorgeverhaeltnis? Verhaeltnis { get; set; }
    public required string Vorname { get; set; }
    public required string Nachname { get; set; }
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Telefon { get; set; }
    public string? Mobil { get; set; }
    public string? Email { get; set; }

    public string Anzeigename => $"{Vorname} {Nachname}";
}
