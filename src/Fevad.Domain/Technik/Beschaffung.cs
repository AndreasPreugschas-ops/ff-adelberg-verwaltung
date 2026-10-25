using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;

namespace Fevad.Domain.Technik;

/// <summary>Stand eines Beschaffungsvorgangs.</summary>
public enum BeschaffungStatus
{
    /// <summary>Bedarf erfasst.</summary>
    [Display(Name = "Bedarf")] Bedarf,
    /// <summary>Zum Haushalt angemeldet.</summary>
    [Display(Name = "beim Haushalt angemeldet")] Angemeldet,
    /// <summary>Genehmigt.</summary>
    [Display(Name = "genehmigt")] Genehmigt,
    /// <summary>Bestellt.</summary>
    [Display(Name = "bestellt")] Bestellt,
    /// <summary>Geliefert; endgültig.</summary>
    [Display(Name = "geliefert")] Geliefert,
    /// <summary>Abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
}

/// <summary>Dringlichkeit einer Beschaffung.</summary>
public enum Prioritaet
{
    /// <summary>Niedrig.</summary>
    [Display(Name = "niedrig")] Niedrig,
    /// <summary>Normal.</summary>
    [Display(Name = "normal")] Normal,
    /// <summary>Hoch.</summary>
    [Display(Name = "hoch")] Hoch,
    /// <summary>Für den Einsatz erforderlich.</summary>
    [Display(Name = "einsatzrelevant")] Einsatzrelevant,
}

/// <summary>Beschaffungsvorgang vom Bedarf über Haushaltsanmeldung und Bestellung bis zum Eingang.</summary>
public class Beschaffung : Entitaet
{
    /// <summary>Was beschafft werden soll.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Stückzahl.</summary>
    public int Menge { get; set; } = 1;
    /// <summary>Begründung des Bedarfs.</summary>
    public string? Begruendung { get; set; }
    /// <summary>Dringlichkeit.</summary>
    public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;
    /// <summary>Aktueller Stand.</summary>
    public BeschaffungStatus Status { get; set; }

    /// <summary>Geräteart, falls ein Gerät beschafft wird.</summary>
    public Guid? GeraeteartId { get; set; }
    /// <summary>Die Geräteart.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Für welches Fahrzeug, z. B. Ersatz für Beladung.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Mangel, der den Bedarf ausgelöst hat (Ersatzbeschaffung).</summary>
    public Guid? MangelId { get; set; }
    /// <summary>Der auslösende Mangel.</summary>
    public Mangel? Mangel { get; set; }

    /// <summary>Nachbestellung für die Kleiderkammer: Artikel und Größe.</summary>
    public Guid? ArtikelId { get; set; }
    /// <summary>Der Kleiderkammer-Artikel.</summary>
    public Artikel? Artikel { get; set; }
    /// <summary>Größe des Artikels.</summary>
    public string? Groesse { get; set; }

    /// <summary>Haushaltsjahr, in dem die Mittel angemeldet sind.</summary>
    public int? Haushaltsjahr { get; set; }
    /// <summary>Geschätzte Kosten in Euro.</summary>
    public decimal? Kostenschaetzung { get; set; }
    /// <summary>Förderfähig, z. B. nach der Zuwendungsrichtlinie Feuerwehrwesen.</summary>
    public bool Foerderfaehig { get; set; }

    /// <summary>Benutzer, der genehmigt hat.</summary>
    public Guid? GenehmigtVon { get; set; }
    /// <summary>Tag der Genehmigung.</summary>
    public DateOnly? GenehmigtAm { get; set; }

    /// <summary>Lieferant.</summary>
    public string? Lieferant { get; set; }
    /// <summary>Bestellnummer beim Lieferanten.</summary>
    public string? Bestellnummer { get; set; }
    /// <summary>Tag der Bestellung.</summary>
    public DateOnly? BestelltAm { get; set; }
    /// <summary>Tag der Lieferung.</summary>
    public DateOnly? GeliefertAm { get; set; }
    /// <summary>Tatsächliche Kosten in Euro.</summary>
    public decimal? Istkosten { get; set; }
    /// <summary>Bemerkung.</summary>
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

    /// <summary>Weder geliefert noch abgelehnt.</summary>
    public bool IstOffen => Status is not (BeschaffungStatus.Geliefert or BeschaffungStatus.Abgelehnt);
}
