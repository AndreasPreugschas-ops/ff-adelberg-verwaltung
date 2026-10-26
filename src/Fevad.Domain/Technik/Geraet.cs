using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Art einer Geräteprüfung.</summary>
public enum Pruefart
{
    /// <summary>Sichtprüfung.</summary>
    [Display(Name = "Sichtprüfung")] Sichtpruefung,
    /// <summary>Funktionsprüfung.</summary>
    [Display(Name = "Funktionsprüfung")] Funktionspruefung,
    /// <summary>Belastungsprüfung.</summary>
    [Display(Name = "Belastungsprüfung")] Belastungspruefung,
    /// <summary>Prüfung nach Gebrauch.</summary>
    [Display(Name = "Prüfung nach Gebrauch")] NachGebrauch,
    /// <summary>Prüfung durch den Hersteller.</summary>
    [Display(Name = "Herstellerprüfung")] Hersteller,
    /// <summary>Hauptuntersuchung nach StVZO.</summary>
    [Display(Name = "Hauptuntersuchung")] Hauptuntersuchung,
    /// <summary>UVV-Prüfung.</summary>
    [Display(Name = "UVV-Prüfung")] Uvv,
    /// <summary>Prüfung elektrischer Betriebsmittel.</summary>
    [Display(Name = "Prüfung elektrischer Betriebsmittel")] Elektro,

    /// <summary>Eingangsprüfung vor der ersten Nutzung (DGUV Grundsatz 305-002, V60).</summary>
    [Display(Name = "Eingangsprüfung")] Eingang,
}

/// <summary>Wohin ein Gerät nach dem Einsatz zur Pflege und Prüfung geht (V13).</summary>
public enum Ruecklauf
{
    /// <summary>Prüfung im eigenen Haus durch den Gerätewart.</summary>
    [Display(Name = "Gerätewart")] Geraetewart,
    /// <summary>Pressluftatmer, Masken, Flaschen: reinigen, desinfizieren, prüfen, füllen.</summary>
    [Display(Name = "Atemschutzwerkstatt")] Atemschutzwerkstatt,
    /// <summary>Schläuche: waschen, Druckprüfung, trocknen.</summary>
    [Display(Name = "Schlauchwerkstatt")] Schlauchwerkstatt,
}

/// <summary>Geräteart mit Prüfplan nach DGUV Grundsatz 305-002. Auch Fahrzeuge bekommen darüber ihren Prüfplan.</summary>
public class Geraeteart : Katalogeintrag
{
    /// <summary>Lebensdauer ab Herstellung in Jahren; danach aussondern.</summary>
    public int? AussonderungNachJahren { get; set; }
    /// <summary>Rechtsgrundlage oder Norm der Prüfung.</summary>
    public string? Pruefgrundlage { get; set; }

    /// <summary>Werkstatt-Arbeitsliste, auf der benutzte Geräte dieser Art nach dem Einsatz landen.</summary>
    public Ruecklauf Ruecklauf { get; set; }

    /// <summary>Prüfvorlage für Fahrzeuge (HU, UVV) statt für Geräte.</summary>
    public bool FuerFahrzeuge { get; set; }

    /// <summary>Prüfarten mit Intervallen.</summary>
    public List<PruefplanPosition> Pruefplan { get; set; } = [];
}

/// <summary>Eine Prüfart mit Intervall im Prüfplan einer Geräteart.</summary>
public class PruefplanPosition : Entitaet
{
    /// <summary>Kennung der Geräteart.</summary>
    public Guid GeraeteartId { get; set; }
    /// <summary>Die Geräteart.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Art der Prüfung.</summary>
    public Pruefart Pruefart { get; set; }
    /// <summary>Intervall in Monaten.</summary>
    public int IntervallMonate { get; set; }

    /// <summary>Wer prüfen darf, z. B. „Hersteller oder autorisierte Person“.</summary>
    public string? Pruefer { get; set; }

    /// <summary>Prüfpunkte als JSON (<see cref="Pruefpunkt"/>). Jedes Protokoll speichert den Stand, mit dem geprüft wurde.</summary>
    public string Checkliste { get; set; } = "[]";
}

/// <summary>Zustand eines Geräts.</summary>
public enum GeraetZustand
{
    /// <summary>Einsatzbereit.</summary>
    [Display(Name = "einsatzbereit")] Einsatzbereit,
    /// <summary>Eingeschränkt nutzbar.</summary>
    [Display(Name = "eingeschränkt")] Eingeschraenkt,
    /// <summary>Außer Betrieb.</summary>
    [Display(Name = "außer Betrieb")] AusserBetrieb,
    /// <summary>Ausgesondert.</summary>
    [Display(Name = "ausgesondert")] Ausgesondert,
    /// <summary>An eine fremde Werkstatt abgegeben (V61), nicht verfügbar.</summary>
    [Display(Name = "in der Werkstatt")] InWerkstatt,
}

/// <summary>Inventarisiertes Einzelstück mit Prüfbuch, Standort und Anlagendaten.</summary>
public class Geraet : Entitaet, IAnlagegut
{
    /// <summary>Eindeutige Inventarnummer.</summary>
    public required string Inventarnummer { get; set; }

    /// <summary>Materialstamm mit Hersteller, Typ und Teilenummer. Pflicht für jedes Gerät.</summary>
    public Guid MaterialId { get; set; }
    /// <summary>Der Materialstamm.</summary>
    public Material? Material { get; set; }

    /// <summary>Immer die Geräteart des Materialstamms; hier mitgeführt, damit Prüffristen ohne Umweg rechnen.</summary>
    public Guid GeraeteartId { get; set; }
    /// <summary>Die Geräteart.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Bezeichnung des Geräts.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Seriennummer des Herstellers.</summary>
    public string? Seriennummer { get; set; }
    /// <summary>Baujahr.</summary>
    public int? Baujahr { get; set; }

    /// <summary>Für Lebensdauergrenzen ab Herstellung (Haltegurte, Helme). Genauer als <see cref="Baujahr"/>.</summary>
    public DateOnly? Herstelldatum { get; set; }

    /// <summary>
    /// Ende der Nutzungsdauer laut Prägung, z. B. bei Composite-Atemluftflaschen (V61). Danach ist das Gerät gesperrt,
    /// unabhängig von Prüfungen.
    /// </summary>
    public DateOnly? NutzbarBis { get; set; }

    /// <summary>Größe bei PSA-Einzelstücken (Helm, Jacke, Haltegurt).</summary>
    public string? Groesse { get; set; }

    /// <summary>Tag der Beschaffung.</summary>
    public DateOnly? BeschafftAm { get; set; }

    /// <summary>Anschaffungspreis brutto je Stück in Euro.</summary>
    public decimal? Anschaffungspreis { get; set; }

    /// <inheritdoc />
    decimal? IAnlagegut.Anschaffungswert => Anschaffungspreis;

    /// <inheritdoc />
    public string? Anlagennummer { get; set; }

    /// <inheritdoc />
    public int? NutzungsdauerJahre { get; set; }

    /// <inheritdoc />
    public Eigentuemer Eigentuemer { get; set; }

    /// <inheritdoc />
    public decimal? Foerdermittel { get; set; }

    /// <inheritdoc />
    public string? Foerdergeber { get; set; }

    /// <inheritdoc />
    public DateOnly? ZweckbindungBis { get; set; }

    /// <inheritdoc />
    public Verwertung? Verwertung { get; set; }

    /// <inheritdoc />
    public decimal? Erloes { get; set; }

    /// <inheritdoc />
    public string? Aussonderungsbeschluss { get; set; }

    /// <inheritdoc />
    public DateOnly? FoerdergeberMitgeteiltAm { get; set; }

    /// <summary>Tag der Indienststellung.</summary>
    public DateOnly? IndienstAm { get; set; }
    /// <summary>Tag der Aussonderung.</summary>
    public DateOnly? AusgesondertAm { get; set; }
    /// <summary>Grund der Aussonderung.</summary>
    public string? Aussonderungsgrund { get; set; }

    /// <summary>Fahrzeug, auf dem das Gerät verlastet ist.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Fahrzeug, auf dem das Gerät verlastet ist.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Geräteraum bzw. Fach auf dem Fahrzeug, z. B. „G3“.</summary>
    public string? Lagerort { get; set; }

    /// <summary>Wo das Gerät bei der Inventur gezählt wird. Pflicht, wenn der Materialstamm inventurpflichtig ist.</summary>
    public Guid? InventurgruppeId { get; set; }
    /// <summary>Die Inventurgruppe.</summary>
    public Inventurgruppe? Inventurgruppe { get; set; }

    /// <summary>Beschaffung, aus der das Gerät stammt.</summary>
    public Guid? BeschaffungId { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Prüfbuch des Geräts.</summary>
    public List<Pruefung> Pruefungen { get; set; } = [];

    /// <summary>
    /// Spätestes Aussonderungsdatum: Lebensdauer der Geräteart ab Herstellung oder das Ende laut Prägung
    /// (<see cref="NutzbarBis"/>), je nachdem, was früher kommt.
    /// </summary>
    public DateOnly? AussonderungFaellig(int? lebensdauerJahre)
    {
        DateOnly? ausLebensdauer = null;
        if (lebensdauerJahre is not null)
        {
            var ab = Herstelldatum ?? (Baujahr is null ? IndienstAm : new DateOnly(Baujahr.Value, 1, 1));
            ausLebensdauer = ab?.AddYears(lebensdauerJahre.Value);
        }

        return (ausLebensdauer, NutzbarBis) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, var b) => b,
        };
    }

    /// <summary>
    /// Zustand aus Aussonderung, Werkstatt, abgelaufener Nutzungsdauer laut Prägung und schwerstem offenem Mangel. Wird nie gespeichert.
    /// </summary>
    /// <param name="ausgesondertAm">Tag der Aussonderung.</param>
    /// <param name="offeneMaengel">Schweregrade der offenen Mängel.</param>
    /// <param name="inWerkstatt">Das Gerät ist an eine fremde Werkstatt abgegeben.</param>
    /// <param name="nutzungAbgelaufen">Die Nutzungsdauer laut Prägung ist überschritten.</param>
    /// <returns>Der Zustand.</returns>
    public static GeraetZustand ZustandBerechnen(DateOnly? ausgesondertAm, IEnumerable<MangelSchwere> offeneMaengel, bool inWerkstatt = false, bool nutzungAbgelaufen = false)
    {
        if (ausgesondertAm is not null)
        {
            return GeraetZustand.Ausgesondert;
        }

        if (inWerkstatt)
        {
            return GeraetZustand.InWerkstatt;
        }

        if (nutzungAbgelaufen)
        {
            return GeraetZustand.AusserBetrieb;
        }

        var schwerste = offeneMaengel.DefaultIfEmpty(MangelSchwere.Info).Max();
        return schwerste switch
        {
            MangelSchwere.AusserBetrieb => GeraetZustand.AusserBetrieb,
            MangelSchwere.Einschraenkung => GeraetZustand.Eingeschraenkt,
            _ => GeraetZustand.Einsatzbereit,
        };
    }
}
