using System.Globalization;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Entschaedigung;

/// <summary>
/// Liest Sätze, Rundung und Funktionspauschalen aus den Parametern mit Präfix <c>Entschaedigung.</c>.
/// Schlüssel: <c>Entschaedigung.{Vorgang}.SatzStunde</c>, <c>.RundungMinuten</c>, <c>.SatzTag</c>,
/// <c>.SatzHalberTag</c>, <c>.HalberTagUnterStunden</c> mit Vorgang <c>Einsatz</c> oder einer <see cref="DienstArt"/>;
/// <c>Entschaedigung.Pauschale.{Funktionscode}</c> in Euro je Jahr.
/// </summary>
/// <param name="parameter">Alle Parameter; nur die mit Präfix <c>Entschaedigung.</c> werden gelesen.</param>
public sealed class Entschaedigungssaetze(IEnumerable<Parameter> parameter)
{
    /// <summary>Präfix aller Entschädigungsparameter.</summary>
    public const string Praefix = "Entschaedigung.";
    /// <summary>Vorgangsname für Einsätze.</summary>
    public const string VorgangEinsatz = "Einsatz";
    /// <summary>Präfix der Funktionspauschalen.</summary>
    public const string PauschalePraefix = Praefix + "Pauschale.";

    private readonly ILookup<string, Parameter> _werte = parameter
        .Where(p => p.Schluessel.StartsWith(Praefix, StringComparison.Ordinal))
        .ToLookup(p => p.Schluessel, StringComparer.Ordinal);

    /// <summary>Baut den Parameterschlüssel für einen Vorgang und ein Feld.</summary>
    /// <param name="vorgang">Vorgang, z. B. „Einsatz“ oder „Uebung“.</param>
    /// <param name="feld">Feld, z. B. „SatzStunde“.</param>
    /// <returns>Der Schlüssel, z. B. „Entschaedigung.Einsatz.SatzStunde“.</returns>
    public static string Schluessel(string vorgang, string feld) => $"{Praefix}{vorgang}.{feld}";

    /// <summary>Vorgangsname einer Dienstart in den Schlüsseln.</summary>
    /// <param name="art">Dienstart.</param>
    /// <returns>Der Name der Dienstart.</returns>
    public static string Vorgang(DienstArt art) => art.ToString();

    /// <summary>Regel für den Vorgang am Stichtag; null, wenn kein Satz hinterlegt ist.</summary>
    /// <param name="vorgang">Vorgangsname.</param>
    /// <param name="stichtag">Tag des Vorgangs.</param>
    public Entschaedigungsregel? Regel(string vorgang, DateOnly stichtag)
    {
        var satzStunde = Dezimal(Schluessel(vorgang, "SatzStunde"), stichtag);
        var satzTag = Dezimal(Schluessel(vorgang, "SatzTag"), stichtag);
        if (satzStunde is null && satzTag is null)
        {
            return null;
        }

        return new Entschaedigungsregel(
            satzStunde ?? 0,
            (int)(Dezimal(Schluessel(vorgang, "RundungMinuten"), stichtag) ?? 1),
            satzTag,
            Dezimal(Schluessel(vorgang, "SatzHalberTag"), stichtag),
            Dezimal(Schluessel(vorgang, "HalberTagUnterStunden"), stichtag));
    }

    /// <summary>Jahrespauschale für eine Funktion am Stichtag; null, wenn keine hinterlegt ist.</summary>
    /// <param name="funktionCode">Code der Funktion, z. B. „KDT“.</param>
    /// <param name="stichtag">Tag, für den der Satz gilt.</param>
    public decimal? Pauschale(string funktionCode, DateOnly stichtag) => Dezimal(PauschalePraefix + funktionCode, stichtag);

    /// <summary>Codes aller Funktionen, für die eine Pauschale hinterlegt ist.</summary>
    public IEnumerable<string> PauschaleFunktionen => _werte
        .Select(g => g.Key)
        .Where(k => k.StartsWith(PauschalePraefix, StringComparison.Ordinal))
        .Select(k => k[PauschalePraefix.Length..]);

    /// <summary>Der am Stichtag gültige Parameter, z. B. um seine Quelle zu prüfen.</summary>
    /// <param name="schluessel">Parameterschlüssel.</param>
    /// <param name="stichtag">Tag, an dem der Wert gelten muss.</param>
    public Parameter? Gueltig(string schluessel, DateOnly stichtag) => _werte[schluessel]
        .Where(p => p.IstGueltigAm(stichtag))
        .MaxBy(p => p.GueltigAb);

    private decimal? Dezimal(string schluessel, DateOnly stichtag) => Lesen(Gueltig(schluessel, stichtag)?.Wert);

    /// <summary>Akzeptiert Komma und Punkt als Dezimaltrenner.</summary>
    /// <param name="wert">Wert als Text.</param>
    /// <returns>Die Zahl oder null, wenn der Wert fehlt oder keine Zahl ist.</returns>
    public static decimal? Lesen(string? wert) =>
        decimal.TryParse(wert?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl) ? zahl : null;
}
