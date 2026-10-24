using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public enum AnschriftArt
{
    [Display(Name = "privat")] Privat,
    [Display(Name = "beruflich")] Beruflich,
}

/// <summary>
/// Abgelöste Anschrift eines Mitglieds, privat oder beim Arbeitgeber. Entsteht beim Speichern, wenn sich die Anschrift
/// ändert, und wird nach der Frist <c>Datenschutz.Anschriften.VerlaufLoeschenNachMonate</c> ab <see cref="BisDatum"/> gelöscht,
/// bei Ausgetretenen mit den Kontaktdaten (L6). Berufliche Einträge sieht nur, wer den Arbeitgeber sehen darf.
/// </summary>
public class FruehereAnschrift : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public AnschriftArt Art { get; set; }

    /// <summary>Nur bei beruflichen Anschriften.</summary>
    public string? Arbeitgeber { get; set; }
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }

    /// <summary>Letzter Tag, an dem die Anschrift galt, also der Tag der Änderung.</summary>
    public DateOnly BisDatum { get; set; }

    public string Zeile => string.Join(", ", new[] { Arbeitgeber, Strasse, $"{Plz} {Ort}".Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Die bisherige private Anschrift, wenn sie gefüllt war und sich gegenüber der neuen ändert; sonst null.</summary>
    public static FruehereAnschrift? Privat(Mitglied bisher, string? strasse, string? plz, string? ort, DateOnly heute) =>
        Abgeloest(AnschriftArt.Privat, bisher.Id, null, bisher.Strasse, bisher.Plz, bisher.Ort, null, strasse, plz, ort, heute);

    /// <summary>Die bisherige Anschrift beim Arbeitgeber (mit Name), wenn sie gefüllt war und sich ändert; sonst null.</summary>
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
