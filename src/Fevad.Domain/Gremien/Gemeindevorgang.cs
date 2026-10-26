using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Gremien;

/// <summary>Anlass, zu dem die Gemeinde (Gemeinderat oder Bürgermeister) entscheiden muss (V27).</summary>
public enum GemeindevorgangArt
{
    /// <summary>Zustimmung zur Wahl des Feuerwehrkommandanten oder seines Stellvertreters (Satzung § 10 Abs. 5, § 8 Abs. 2 FwG).</summary>
    [Display(Name = "Zustimmung zur Wahl des Kommandanten / Stellvertreters")] ZustimmungWahl,
    /// <summary>Bestellung durch den Bürgermeister nach der Zustimmung.</summary>
    [Display(Name = "Bestellung durch den Bürgermeister")] Bestellung,
    /// <summary>Beendigung des Feuerwehrdienstes aus wichtigem Grund nach Anhörung (Satzung § 4 Abs. 5).</summary>
    [Display(Name = "Beendigung des Dienstes aus wichtigem Grund (Ausschluss)")] Ausschluss,
    /// <summary>Verleihung der Ehrenmitgliedschaft oder des Ehrenkommandanten (Satzung § 8).</summary>
    [Display(Name = "Ehrenmitglied / Ehrenkommandant")] Ehrenmitglied,
    /// <summary>Beschaffung über der Wertgrenze des Bürgermeisters.</summary>
    [Display(Name = "Beschaffung über der Wertgrenze")] Beschaffung,
    /// <summary>Beschluss oder Fortschreibung des Feuerwehrbedarfsplans.</summary>
    [Display(Name = "Feuerwehrbedarfsplan")] Bedarfsplan,
    /// <summary>Annahme einer Spende, Schenkung oder ähnlichen Zuwendung (§ 78 Abs. 4 GemO).</summary>
    [Display(Name = "Annahme einer Spende")] Spendenannahme,
    /// <summary>Zustimmung des Bürgermeisters zum Wirtschaftsplan der Kameradschaftskasse (Satzung § 16 Abs. 3).</summary>
    [Display(Name = "Zustimmung zum Wirtschaftsplan der Kameradschaftskasse")] Wirtschaftsplan,
    /// <summary>Sonstiger Anlass.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Ergebnis der Entscheidung der Gemeinde.</summary>
public enum GemeindeErgebnis
{
    /// <summary>Zugestimmt bzw. beschlossen wie vorgeschlagen.</summary>
    [Display(Name = "zugestimmt")] Zugestimmt,
    /// <summary>Mit Änderungen zugestimmt.</summary>
    [Display(Name = "mit Änderungen zugestimmt")] Geaendert,
    /// <summary>Abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
}

/// <summary>Stand eines Gemeinde-Vorgangs.</summary>
public enum GemeindevorgangStand
{
    /// <summary>Die Feuerwehr oder das Hauptamt bereitet die Vorlage vor.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Der Gemeinde vorgelegt; die Entscheidung steht aus.</summary>
    [Display(Name = "vorgelegt, Entscheidung offen")] Vorgelegt,
    /// <summary>Die Gemeinde hat entschieden.</summary>
    [Display(Name = "entschieden")] Entschieden,
    /// <summary>Zurückgezogen.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>
/// Vorgang „Entscheidung der Gemeinde nötig“ (V27) mit Vorlagen-Entwurf aus Sachverhalt, Rechtsgrundlage und Beschlussvorschlag. Die Feuerwehr
/// bereitet vor, das Hauptamt legt vor und trägt die Entscheidung ein; FEVAD ersetzt nicht das Ratsinformationssystem der Gemeinde.
/// </summary>
public class Gemeindevorgang : Entitaet
{
    /// <summary>Anlass.</summary>
    public GemeindevorgangArt Art { get; set; }

    /// <summary>Betreff der Vorlage.</summary>
    public required string Betreff { get; set; }

    /// <summary>Betroffenes Mitglied, z. B. beim Ausschluss oder der Zustimmung zur Wahl; sonst null.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Das betroffene Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Zuständig: Gemeinderat oder Bürgermeister.</summary>
    public required string Zustaendig { get; set; }

    /// <summary>Sachverhalt.</summary>
    public string? Sachverhalt { get; set; }

    /// <summary>Rechtsgrundlage.</summary>
    public string? Rechtsgrundlage { get; set; }

    /// <summary>Beschlussvorschlag.</summary>
    public string? Beschlussvorschlag { get; set; }

    /// <summary>Tag, an dem die Vorlage der Gemeinde vorgelegt wurde.</summary>
    public DateOnly? VorgelegtAm { get; set; }

    /// <summary>Ergebnis; null, solange nicht entschieden.</summary>
    public GemeindeErgebnis? Ergebnis { get; set; }
    /// <summary>Tag der Entscheidung (Sitzung bzw. Verfügung).</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Vermerk zur Entscheidung, z. B. Sitzung und Tagesordnungspunkt oder Änderungen.</summary>
    public string? Entscheidungsvermerk { get; set; }
    /// <summary>Name dessen, der die Entscheidung eingetragen hat.</summary>
    public string? EingetragenVon { get; set; }

    /// <summary>Der Vorgang wurde zurückgezogen.</summary>
    public bool Zurueckgezogen { get; set; }

    /// <summary>Stand des Vorgangs.</summary>
    public GemeindevorgangStand Stand => Zurueckgezogen ? GemeindevorgangStand.Zurueckgezogen
        : Ergebnis is not null ? GemeindevorgangStand.Entschieden
        : VorgelegtAm is not null ? GemeindevorgangStand.Vorgelegt
        : GemeindevorgangStand.Entwurf;
}

/// <summary>Mustertexte für die Vorlagen je Anlass; das Hauptamt passt sie im einzelnen Vorgang an.</summary>
public static class Gemeindevorlagen
{
    /// <summary>Gremium „Gemeinderat“.</summary>
    public const string Gemeinderat = "Gemeinderat";
    /// <summary>Gremium „Bürgermeister“.</summary>
    public const string Buergermeister = "Bürgermeister";

    /// <summary>Muster für einen Anlass.</summary>
    /// <param name="art">Anlass.</param>
    /// <returns>Zuständiges Gremium, Rechtsgrundlage und Beschlussvorschlag.</returns>
    public static (string Zustaendig, string Rechtsgrundlage, string Beschlussvorschlag) Muster(GemeindevorgangArt art) => art switch
    {
        GemeindevorgangArt.ZustimmungWahl => (Gemeinderat, "§ 8 Abs. 2 FwG; Feuerwehrsatzung § 10 Abs. 5",
            "Der Gemeinderat stimmt der Wahl von [Name] zum [Feuerwehrkommandanten / stellvertretenden Feuerwehrkommandanten] durch die Hauptversammlung vom [Datum] zu."),
        GemeindevorgangArt.Bestellung => (Buergermeister, "§ 8 Abs. 2 FwG; Feuerwehrsatzung § 10 Abs. 5",
            "[Name] wird mit Wirkung vom [Datum] für die Dauer von fünf Jahren zum [Feuerwehrkommandanten / stellvertretenden Feuerwehrkommandanten] bestellt."),
        GemeindevorgangArt.Ausschluss => (Gemeinderat, "Feuerwehrsatzung § 4 Abs. 5 (nach Anhörung des Feuerwehrausschusses und des Betroffenen)",
            "Der Gemeinderat beendet nach Anhörung des Feuerwehrausschusses vom [Datum] und des Betroffenen vom [Datum] den ehrenamtlichen Feuerwehrdienst von [Name] aus wichtigem Grund. Der Bürgermeister stellt die Beendigung durch schriftlichen Bescheid fest."),
        GemeindevorgangArt.Ehrenmitglied => (Gemeinderat, "Feuerwehrsatzung § 8 (auf Vorschlag des Feuerwehrausschusses)",
            "Der Gemeinderat verleiht [Name] auf Vorschlag des Feuerwehrausschusses vom [Datum] die Eigenschaft als [Ehrenmitglied / Ehrenkommandant] der Freiwilligen Feuerwehr."),
        GemeindevorgangArt.Beschaffung => (Gemeinderat, "Hauptsatzung (Wertgrenzen); GemO §§ 39, 44; Vergaberecht",
            "Der Gemeinderat beschließt die Beschaffung von [Gegenstand] zum Preis von [Betrag] und ermächtigt die Verwaltung zur Vergabe an [Bieter]. Die Mittel stehen unter [Haushaltsstelle] zur Verfügung."),
        GemeindevorgangArt.Bedarfsplan => (Gemeinderat, "§ 3 Abs. 1 FwG; VwV Feuerwehrbedarfsplanung",
            "Der Gemeinderat beschließt den Feuerwehrbedarfsplan [Zeitraum] in der vorgelegten Fassung."),
        GemeindevorgangArt.Spendenannahme => (Gemeinderat, "§ 78 Abs. 4 GemO",
            "Der Gemeinderat stimmt der Annahme der Spende von [Geber] über [Betrag / Sache] zugunsten der Freiwilligen Feuerwehr zu."),
        GemeindevorgangArt.Wirtschaftsplan => (Buergermeister, "Feuerwehrsatzung § 16 Abs. 3",
            "Der Bürgermeister stimmt dem vom Feuerwehrausschuss am [Datum] aufgestellten Wirtschaftsplan der Kameradschaftskasse für das Jahr [Jahr] zu."),
        _ => (Gemeinderat, "", ""),
    };
}
