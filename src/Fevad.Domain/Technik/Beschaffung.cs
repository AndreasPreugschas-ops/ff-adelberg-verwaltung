using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;

namespace Fevad.Domain.Technik;

public enum BeschaffungStatus
{
    [Display(Name = "Bedarf")] Bedarf,
    [Display(Name = "beim Haushalt angemeldet")] Angemeldet,
    [Display(Name = "genehmigt")] Genehmigt,
    [Display(Name = "bestellt")] Bestellt,
    [Display(Name = "geliefert")] Geliefert,
    [Display(Name = "abgelehnt")] Abgelehnt,
}

public enum Prioritaet
{
    [Display(Name = "niedrig")] Niedrig,
    [Display(Name = "normal")] Normal,
    [Display(Name = "hoch")] Hoch,
    [Display(Name = "einsatzrelevant")] Einsatzrelevant,
}

/// <summary>Beschaffungsvorgang vom Bedarf über Haushaltsanmeldung und Bestellung bis zum Eingang.</summary>
public class Beschaffung : Entitaet
{
    public required string Bezeichnung { get; set; }
    public int Menge { get; set; } = 1;
    public string? Begruendung { get; set; }
    public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;
    public BeschaffungStatus Status { get; set; }

    public Guid? GeraeteartId { get; set; }
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Für welches Fahrzeug, z. B. Ersatz für Beladung.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Mangel, der den Bedarf ausgelöst hat (Ersatzbeschaffung).</summary>
    public Guid? MangelId { get; set; }
    public Mangel? Mangel { get; set; }

    /// <summary>Nachbestellung für die Kleiderkammer: Artikel und Größe.</summary>
    public Guid? ArtikelId { get; set; }
    public Artikel? Artikel { get; set; }
    public string? Groesse { get; set; }

    public int? Haushaltsjahr { get; set; }
    public decimal? Kostenschaetzung { get; set; }
    public bool Foerderfaehig { get; set; }

    public Guid? GenehmigtVon { get; set; }
    public DateOnly? GenehmigtAm { get; set; }

    public string? Lieferant { get; set; }
    public string? Bestellnummer { get; set; }
    public DateOnly? BestelltAm { get; set; }
    public DateOnly? GeliefertAm { get; set; }
    public decimal? Istkosten { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Zulässige nächste Schritte. Abgelehnt ist aus jedem offenen Status möglich, Geliefert ist endgültig.</summary>
    public static IReadOnlyList<BeschaffungStatus> Folgestatus(BeschaffungStatus status) => status switch
    {
        BeschaffungStatus.Bedarf => [BeschaffungStatus.Angemeldet, BeschaffungStatus.Genehmigt, BeschaffungStatus.Abgelehnt],
        BeschaffungStatus.Angemeldet => [BeschaffungStatus.Genehmigt, BeschaffungStatus.Abgelehnt],
        BeschaffungStatus.Genehmigt => [BeschaffungStatus.Bestellt, BeschaffungStatus.Abgelehnt],
        BeschaffungStatus.Bestellt => [BeschaffungStatus.Geliefert],
        BeschaffungStatus.Abgelehnt => [BeschaffungStatus.Bedarf],
        _ => [],
    };

    public bool IstOffen => Status is not (BeschaffungStatus.Geliefert or BeschaffungStatus.Abgelehnt);
}
