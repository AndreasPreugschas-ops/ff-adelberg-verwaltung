using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art einer Anschrift im Anschriftenverlauf.</summary>
public enum AnschriftArt
{
    /// <summary>Private Wohnanschrift des Mitglieds.</summary>
    [Display(Name = "privat")] Privat,
    /// <summary>Anschrift des Arbeitgebers.</summary>
    [Display(Name = "beruflich")] Beruflich,
}

/// <summary>
/// Abgelöste Anschrift eines Mitglieds, privat oder beim Arbeitgeber. Entsteht beim Speichern, wenn sich die Anschrift
/// ändert, und wird nach der Frist <c>Datenschutz.Anschriften.VerlaufLoeschenNachMonate</c> ab <see cref="BisDatum"/> gelöscht,
/// bei Ausgetretenen mit den Kontaktdaten (L6). Berufliche Einträge sieht nur, wer den Arbeitgeber sehen darf.
/// </summary>
public class FruehereAnschrift : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied, dessen Anschrift sich geändert hat.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Privat oder beruflich.</summary>
    public AnschriftArt Art { get; set; }

    /// <summary>Nur bei beruflichen Anschriften.</summary>
    public string? Arbeitgeber { get; set; }
    /// <summary>Straße und Hausnummer der abgelösten Anschrift.</summary>
    public string? Strasse { get; set; }
    /// <summary>Postleitzahl der abgelösten Anschrift.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort der abgelösten Anschrift.</summary>
    public string? Ort { get; set; }

    /// <summary>Letzter Tag, an dem die Anschrift galt, also der Tag der Änderung.</summary>
    public DateOnly BisDatum { get; set; }

    /// <summary>Arbeitgeber, Straße und „PLZ Ort“ als eine Zeile; leere Teile entfallen.</summary>
    public string Zeile => string.Join(", ", new[] { Arbeitgeber, Strasse, $"{Plz} {Ort}".Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Die bisherige private Anschrift, wenn sie gefüllt war und sich gegenüber der neuen ändert; sonst null.</summary>
    /// <param name="bisher">Das Mitglied mit der bisher gespeicherten Anschrift.</param>
    /// <param name="strasse">Neue Straße.</param>
    /// <param name="plz">Neue Postleitzahl.</param>
    /// <param name="ort">Neuer Ort.</param>
    /// <param name="heute">Tag der Änderung; wird <see cref="BisDatum"/>.</param>
    public static FruehereAnschrift? Privat(Mitglied bisher, string? strasse, string? plz, string? ort, DateOnly heute) =>
        Abgeloest(AnschriftArt.Privat, bisher.Id, null, bisher.Strasse, bisher.Plz, bisher.Ort, null, strasse, plz, ort, heute);

    /// <summary>Die bisherige Anschrift beim Arbeitgeber (mit Name), wenn sie gefüllt war und sich ändert; sonst null.</summary>
    /// <param name="bisher">Das Mitglied mit der bisher gespeicherten Arbeitgeberanschrift.</param>
    /// <param name="arbeitgeber">Neuer Name des Arbeitgebers.</param>
    /// <param name="strasse">Neue Straße.</param>
    /// <param name="plz">Neue Postleitzahl.</param>
    /// <param name="ort">Neuer Arbeitsort.</param>
    /// <param name="heute">Tag der Änderung; wird <see cref="BisDatum"/>.</param>
    public static FruehereAnschrift? Beruflich(Mitglied bisher, string? arbeitgeber, string? strasse, string? plz, string? ort, DateOnly heute) =>
        Abgeloest(AnschriftArt.Beruflich, bisher.Id, bisher.Arbeitgeber, bisher.ArbeitgeberStrasse, bisher.ArbeitgeberPlz, bisher.Arbeitsort,
            arbeitgeber, strasse, plz, ort, heute);

    private static FruehereAnschrift? Abgeloest(AnschriftArt art, Guid mitgliedId, string? altName, string? altStrasse, string? altPlz, string? altOrt,
        string? name, string? strasse, string? plz, string? ort, DateOnly heute)
    {
        static string N(string? s) => string.Join(' ', (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

        var alt = new[] { altName, altStrasse, altPlz, altOrt };
        if (alt.All(string.IsNullOrWhiteSpace) || alt.Select(N).SequenceEqual(new[] { name, strasse, plz, ort }.Select(N)))
        {
            return null;
        }

        return new FruehereAnschrift
        {
            MitgliedId = mitgliedId, Art = art, Arbeitgeber = altName, Strasse = altStrasse, Plz = altPlz, Ort = altOrt, BisDatum = heute,
        };
    }
}
