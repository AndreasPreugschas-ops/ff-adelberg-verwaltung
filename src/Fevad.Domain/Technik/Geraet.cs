using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

public enum Pruefart
{
    [Display(Name = "Sichtprüfung")] Sichtpruefung,
    [Display(Name = "Funktionsprüfung")] Funktionspruefung,
    [Display(Name = "Belastungsprüfung")] Belastungspruefung,
    [Display(Name = "Prüfung nach Gebrauch")] NachGebrauch,
    [Display(Name = "Herstellerprüfung")] Hersteller,
    [Display(Name = "Hauptuntersuchung")] Hauptuntersuchung,
    [Display(Name = "UVV-Prüfung")] Uvv,
    [Display(Name = "Prüfung elektrischer Betriebsmittel")] Elektro,
}

/// <summary>Geräteart mit Prüfplan nach DGUV Grundsatz 305-002. Auch Fahrzeuge bekommen darüber ihren Prüfplan.</summary>
public class Geraeteart : Katalogeintrag
{
    public int? AussonderungNachJahren { get; set; }
    public string? Pruefgrundlage { get; set; }

    /// <summary>Prüfvorlage für Fahrzeuge (HU, UVV) statt für Geräte.</summary>
    public bool FuerFahrzeuge { get; set; }

    public List<PruefplanPosition> Pruefplan { get; set; } = [];
}

public class PruefplanPosition : Entitaet
{
    public Guid GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    public Pruefart Pruefart { get; set; }
    public int IntervallMonate { get; set; }

    /// <summary>Wer prüfen darf, z. B. „Hersteller oder autorisierte Person“.</summary>
    public string? Pruefer { get; set; }

    /// <summary>Prüfpunkte als JSON (<see cref="Pruefpunkt"/>). Jedes Protokoll speichert den Stand, mit dem geprüft wurde.</summary>
    public string Checkliste { get; set; } = "[]";
}

public enum GeraetZustand
{
    [Display(Name = "einsatzbereit")] Einsatzbereit,
    [Display(Name = "eingeschränkt")] Eingeschraenkt,
    [Display(Name = "außer Betrieb")] AusserBetrieb,
    [Display(Name = "ausgesondert")] Ausgesondert,
}

public class Geraet : Entitaet
{
    public required string Inventarnummer { get; set; }

    public Guid GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    public required string Bezeichnung { get; set; }
    public string? Hersteller { get; set; }
    public string? Typ { get; set; }
    public string? Seriennummer { get; set; }
    public int? Baujahr { get; set; }

    /// <summary>Für Lebensdauergrenzen ab Herstellung (Haltegurte, Helme). Genauer als <see cref="Baujahr"/>.</summary>
    public DateOnly? Herstelldatum { get; set; }

    /// <summary>Größe bei PSA-Einzelstücken (Helm, Jacke, Haltegurt).</summary>
    public string? Groesse { get; set; }

    public DateOnly? BeschafftAm { get; set; }
    public decimal? Anschaffungspreis { get; set; }
    public DateOnly? IndienstAm { get; set; }
    public DateOnly? AusgesondertAm { get; set; }
    public string? Aussonderungsgrund { get; set; }

    /// <summary>Fahrzeug, auf dem das Gerät verlastet ist.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Geräteraum bzw. Fach auf dem Fahrzeug, z. B. „G3“.</summary>
    public string? Lagerort { get; set; }

    /// <summary>Beschaffung, aus der das Gerät stammt.</summary>
    public Guid? BeschaffungId { get; set; }

    public string? Bemerkung { get; set; }

    public List<Pruefung> Pruefungen { get; set; } = [];

    /// <summary>Spätestes Aussonderungsdatum aus der Lebensdauer der Geräteart, gerechnet ab Herstellung.</summary>
    public DateOnly? AussonderungFaellig(int? lebensdauerJahre)
    {
        if (lebensdauerJahre is null)
        {
            return null;
        }

        var ab = Herstelldatum ?? (Baujahr is null ? IndienstAm : new DateOnly(Baujahr.Value, 1, 1));
        return ab?.AddYears(lebensdauerJahre.Value);
    }

    /// <summary>Zustand aus Aussonderung und schwerstem offenem Mangel. Wird nie gespeichert.</summary>
    public static GeraetZustand ZustandBerechnen(DateOnly? ausgesondertAm, IEnumerable<MangelSchwere> offeneMaengel)
    {
        if (ausgesondertAm is not null)
        {
            return GeraetZustand.Ausgesondert;
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
