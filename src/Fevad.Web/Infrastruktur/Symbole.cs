using Microsoft.AspNetCore.Html;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Linien-Symbole im Stil von Windows und Office (Fluent): 24er-Raster, 1,5 Strich, abgerundete Enden, Farbe aus der Schrift.
/// Rein schmückend (<c>aria-hidden</c>); die Bedeutung trägt immer der Text daneben.
/// </summary>
public static class Symbole
{
    /// <summary>Pfade je Symbolname.</summary>
    private static readonly Dictionary<string, string> Pfade = new(StringComparer.OrdinalIgnoreCase)
    {
        ["start"] = """<path d="M3.5 10.5 12 3.5l8.5 7"/><path d="M5.5 9v11h5v-6h3v6h5V9"/>""",
        ["mannschaft"] = """<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20c0-3.6 2.9-6 6.5-6s6.5 2.4 6.5 6"/><circle cx="17" cy="9" r="2.5"/><path d="M17 14c2.7 0 4.5 1.9 4.5 5"/>""",
        ["dienstbetrieb"] = """<rect x="3.5" y="5" width="17" height="15.5" rx="2"/><path d="M3.5 10h17M8 3v4M16 3v4M8 14h2M14 14h2M8 17h2"/>""",
        ["ausruestung"] = """<path d="M2.5 17V7.5a1 1 0 0 1 1-1H13v10.5"/><path d="M13 10h4.2l3.8 4.2V17h-1.8"/><circle cx="7" cy="17.5" r="2"/><circle cx="16.5" cy="17.5" r="2"/><path d="M9 17.5h5.5M2.5 17H5M5.5 9.5h4"/>""",
        ["verwaltung"] = """<path d="M3.5 7A1.5 1.5 0 0 1 5 5.5h4l2 2h8a1.5 1.5 0 0 1 1.5 1.5v9.5A1.5 1.5 0 0 1 19 20H5a1.5 1.5 0 0 1-1.5-1.5z"/>""",
        ["menue"] = """<path d="M4 6.5h16M4 12h16M4 17.5h16"/>""",
        ["raster"] = """<path d="M5 5h.01M12 5h.01M19 5h.01M5 12h.01M12 12h.01M19 12h.01M5 19h.01M12 19h.01M19 19h.01" stroke-width="3"/>""",
        ["suche"] = """<circle cx="10.5" cy="10.5" r="6"/><path d="m15 15 5 5"/>""",
        ["glocke"] = """<path d="M6 9.5a6 6 0 1 1 12 0c0 5 2 7 2 7H4s2-2 2-7"/><path d="M10 20a2.2 2.2 0 0 0 4 0"/>""",
        ["pfeil-unten"] = """<path d="m7 10 5 5 5-5"/>""",
        ["konto"] = """<circle cx="12" cy="8.5" r="4"/><path d="M4.5 20.5c0-4 3.4-6.5 7.5-6.5s7.5 2.5 7.5 6.5"/>""",
        ["abmelden"] = """<path d="M14 4.5H6.5a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1H14"/><path d="M11 12h9.5M17 8.5l3.5 3.5-3.5 3.5"/>""",
        ["haken"] = """<circle cx="12" cy="12" r="8.5"/><path d="m8.5 12.2 2.4 2.4 4.8-5"/>""",
        ["warnung"] = """<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5v5.5M12 16.2v.3"/>""",
        ["flamme"] = """<path d="M12 21c-3.9 0-6.5-2.6-6.5-6.1 0-3.4 2.3-5.4 3.6-7.6.4 1.6 1.2 2.6 2.2 3.1C11.6 7 12.9 4.6 15 3c-.2 3.2 3.5 5.6 3.5 11.1 0 4.1-2.6 6.9-6.5 6.9z"/><path d="M12 21c-1.7 0-2.9-1.2-2.9-2.9 0-1.9 1.6-2.9 2.4-4.6 1.6 1.3 3.4 2.6 3.4 4.6 0 1.7-1.2 2.9-2.9 2.9z"/>""",
    };

    /// <summary>Das Symbol als Inline-SVG.</summary>
    /// <param name="name">Name des Symbols, z. B. <c>mannschaft</c>.</param>
    /// <param name="klasse">CSS-Klassen für Größe und Farbe.</param>
    /// <returns>SVG oder nichts, wenn der Name unbekannt ist.</returns>
    public static IHtmlContent Svg(string name, string klasse = "h-5 w-5") =>
        Pfade.TryGetValue(name, out var pfade)
            ? new HtmlString($"""<svg aria-hidden="true" focusable="false" class="{klasse} shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">{pfade}</svg>""")
            : HtmlString.Empty;

    /// <summary>Symbol eines Hauptmenü-Bereichs nach seinem Titel.</summary>
    /// <param name="bereich">Titel des Bereichs.</param>
    /// <param name="klasse">CSS-Klassen für Größe und Farbe.</param>
    /// <returns>SVG.</returns>
    public static IHtmlContent Bereich(string bereich, string klasse = "h-5 w-5") =>
        Svg(bereich.Replace("ü", "ue", StringComparison.Ordinal), klasse);
}
