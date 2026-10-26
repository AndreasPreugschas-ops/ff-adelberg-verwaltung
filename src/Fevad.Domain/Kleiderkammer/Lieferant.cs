using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kleiderkammer;

/// <summary>
/// Lieferant der Kleiderkammer (Fachhandel, Hersteller). Nur Firmenangaben, keine Ansprechpersonen: Wer beim Lieferanten zuständig ist,
/// steht in dessen Angebot und gehört nicht in FEVAD.
/// </summary>
public class Lieferant : Entitaet
{
    /// <summary>Firmenname.</summary>
    public required string Name { get; set; }
    /// <summary>Kundennummer der Feuerwehr bzw. Gemeinde beim Lieferanten.</summary>
    public string? Kundennummer { get; set; }
    /// <summary>Telefon der Firma.</summary>
    public string? Telefon { get; set; }
    /// <summary>E-Mail-Adresse der Firma, z. B. für Bestellungen.</summary>
    public string? EMail { get; set; }
    /// <summary>Webseite oder Bestellportal.</summary>
    public string? Webseite { get; set; }
    /// <summary>Freitext, z. B. Lieferzeiten oder Rahmenvertrag; nur Sachstand, keine Ansprechpersonen.</summary>
    public string? Bemerkung { get; set; }
    /// <summary>Nur aktive Lieferanten werden für neue Preise angeboten und zählen in den Haushaltsvorschlägen.</summary>
    public bool Aktiv { get; set; } = true;
    /// <summary>
    /// Tag der Deaktivierung. Löschregel L69: Telefon, E-Mail und Bemerkung werden nach der Frist geleert, weil bei Einzelunternehmern auch
    /// Firmenangaben personenbezogen sind; Name und Kundennummer bleiben für Beschaffungen und Preise.
    /// </summary>
    public DateOnly? DeaktiviertAm { get; set; }

    /// <summary>Preise der Artikel bei diesem Lieferanten.</summary>
    public List<ArtikelPreis> Preise { get; set; } = [];
}

/// <summary>Preis eines Artikels bei einem Lieferanten (Artikel-Lieferanten-Beziehung). Je Paar gibt es höchstens einen Preis.</summary>
public class ArtikelPreis : Entitaet
{
    /// <summary>Schlüssel des Artikels.</summary>
    public Guid ArtikelId { get; set; }
    /// <summary>Der Artikel.</summary>
    public Artikel? Artikel { get; set; }
    /// <summary>Schlüssel des Lieferanten.</summary>
    public Guid LieferantId { get; set; }
    /// <summary>Der Lieferant.</summary>
    public Lieferant? Lieferant { get; set; }

    /// <summary>Stückpreis brutto in Euro, so wie er im Haushalt angemeldet wird.</summary>
    public decimal Preis { get; set; }
    /// <summary>Artikel- bzw. Bestellnummer beim Lieferanten.</summary>
    public string? Bestellnummer { get; set; }
    /// <summary>Tag des Angebots bzw. der Preisliste.</summary>
    public DateOnly? StandAm { get; set; }
    /// <summary>Bei diesem Lieferanten wird bestellt; sonst gilt der günstigste Preis.</summary>
    public bool Bevorzugt { get; set; }
}

/// <summary>Wahl des Preises, mit dem ein Artikel geplant wird.</summary>
public static class Preiswahl
{
    /// <summary>Preis des bevorzugten Lieferanten, sonst der günstigste; nur aktive Lieferanten und Preise über 0.</summary>
    /// <param name="preise">Preise eines Artikels mit geladenem Lieferanten.</param>
    /// <returns>Der maßgebliche Preis oder null, wenn keiner erfasst ist.</returns>
    public static ArtikelPreis? Massgeblich(IEnumerable<ArtikelPreis> preise)
    {
        var gueltig = preise.Where(p => p.Preis > 0 && p.Lieferant is not { Aktiv: false }).ToList();
        return gueltig.Where(p => p.Bevorzugt).MinBy(p => p.Preis) ?? gueltig.MinBy(p => p.Preis);
    }
}
