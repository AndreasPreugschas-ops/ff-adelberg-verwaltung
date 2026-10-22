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
public sealed class Entschaedigungssaetze(IEnumerable<Parameter> parameter)
{
    public const string Praefix = "Entschaedigung.";
    public const string VorgangEinsatz = "Einsatz";
    public const string PauschalePraefix = Praefix + "Pauschale.";

    private readonly ILookup<string, Parameter> _werte = parameter
        .Where(p => p.Schluessel.StartsWith(Praefix, StringComparison.Ordinal))
        .ToLookup(p => p.Schluessel, StringComparer.Ordinal);

    public static string Schluessel(string vorgang, string feld) => $"{Praefix}{vorgang}.{feld}";

    public static string Vorgang(DienstArt art) => art.ToString();

    /// <summary>Regel für den Vorgang am Stichtag; null, wenn kein Satz hinterlegt ist.</summary>
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
    public decimal? Pauschale(string funktionCode, DateOnly stichtag) => Dezimal(PauschalePraefix + funktionCode, stichtag);

    public IEnumerable<string> PauschaleFunktionen => _werte
        .Select(g => g.Key)
        .Where(k => k.StartsWith(PauschalePraefix, StringComparison.Ordinal))
        .Select(k => k[PauschalePraefix.Length..]);

    /// <summary>Der am Stichtag gültige Parameter, z. B. um seine Quelle zu prüfen.</summary>
    public Parameter? Gueltig(string schluessel, DateOnly stichtag) => _werte[schluessel]
        .Where(p => p.IstGueltigAm(stichtag))
        .MaxBy(p => p.GueltigAb);

    private decimal? Dezimal(string schluessel, DateOnly stichtag) => Lesen(Gueltig(schluessel, stichtag)?.Wert);

    /// <summary>Akzeptiert Komma und Punkt als Dezimaltrenner.</summary>
    public static decimal? Lesen(string? wert) =>
        decimal.TryParse(wert?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl) ? zahl : null;
}
