using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Art einer Pflegemaßnahme an einem Einzelstück (V63).</summary>
public enum Pflegeart
{
    /// <summary>Wäsche nach Herstellerangabe.</summary>
    [Display(Name = "Wäsche")] Waesche,
    /// <summary>Imprägnierung nach der Wäsche.</summary>
    [Display(Name = "Imprägnierung")] Impraegnierung,
    /// <summary>Reparatur, z. B. Naht, Reflexstreifen, Einband.</summary>
    [Display(Name = "Reparatur")] Reparatur,
    /// <summary>Kontaminiert (Brandrauch, Gefahrstoff); Wäsche oder Aussonderung folgt.</summary>
    [Display(Name = "Kontamination")] Kontamination,
}

/// <summary>
/// Eintrag im Pflege-Lebenslauf eines Einzelstücks (V63), z. B. Einsatzjacke, Helm oder Druckschlauch.
/// Enthält keine Personen: wer das Stück trug, steht in der Kleiderkammer, nicht hier.
/// </summary>
public class Pflegeeintrag : Entitaet
{
    /// <summary>Kennung des Geräts.</summary>
    public Guid GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Tag der Maßnahme.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Art der Maßnahme.</summary>
    public Pflegeart Art { get; set; }

    /// <summary>Einsatz, nach dem gepflegt wurde.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Wer gepflegt hat, als Firma oder Stelle (z. B. Wäscherei, Schlauchwerkstatt Göppingen); keine Namen.</summary>
    public string? Durchfuehrung { get; set; }

    /// <summary>Bemerkung (Sachstand).</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Pflegestand eines Einzelstücks: Zahl der Wäschen gegen die Herstellergrenze der Geräteart.</summary>
/// <param name="Waeschen">Bisherige Wäschen.</param>
/// <param name="HoechstWaeschen">Grenze der Geräteart; leer, wenn der Hersteller keine nennt.</param>
public sealed record Pflegestand(int Waeschen, int? HoechstWaeschen)
{
    /// <summary>Die Grenze ist erreicht oder überschritten: Stück prüfen und aussondern.</summary>
    public bool GrenzeErreicht => HoechstWaeschen is { } grenze && Waeschen >= grenze;

    /// <summary>Noch höchstens zwei Wäschen bis zur Grenze.</summary>
    public bool GrenzeNah => HoechstWaeschen is { } grenze && !GrenzeErreicht && grenze - Waeschen <= 2;

    /// <summary>Stand aus den Einträgen eines Stücks.</summary>
    /// <param name="eintraege">Pflegeeinträge des Stücks.</param>
    /// <param name="hoechstWaeschen">Grenze der Geräteart.</param>
    /// <returns>Der Stand.</returns>
    public static Pflegestand Aus(IEnumerable<Pflegeeintrag> eintraege, int? hoechstWaeschen) =>
        new(eintraege.Count(e => e.Art == Pflegeart.Waesche), hoechstWaeschen);
}

/// <summary>Größe und Länge eines Druckschlauchs aus der Typangabe nach DIN 14811, z. B. „B 75-20“ (V63, Schlauchbuch).</summary>
/// <param name="Groesse">Kurzzeichen A, B, C, D oder C 52.</param>
/// <param name="DurchmesserMm">Innendurchmesser in mm.</param>
/// <param name="LaengeM">Länge in m.</param>
public sealed partial record Schlauchtyp(string Groesse, int DurchmesserMm, int LaengeM)
{
    /// <summary>Liest die Typangabe; null, wenn sie nicht dem Muster „B 75-20“ folgt.</summary>
    /// <param name="typ">Typangabe aus dem Materialstamm.</param>
    /// <returns>Der Schlauchtyp oder null.</returns>
    public static Schlauchtyp? Lesen(string? typ)
    {
        if (string.IsNullOrWhiteSpace(typ))
        {
            return null;
        }

        var treffer = Muster().Match(typ.Trim());
        if (!treffer.Success)
        {
            return null;
        }

        var durchmesser = int.Parse(treffer.Groups["d"].Value, CultureInfo.InvariantCulture);
        var groesse = treffer.Groups["g"].Value.ToUpperInvariant();
        if (groesse == "C" && durchmesser == 52)
        {
            groesse = "C 52";
        }

        return new Schlauchtyp(groesse, durchmesser, int.Parse(treffer.Groups["l"].Value, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^(?<g>[A-Da-d])\s*(?<d>\d{2,3})\s*[-/]\s*(?<l>\d{1,3})\s*(m)?$")]
    private static partial Regex Muster();
}
