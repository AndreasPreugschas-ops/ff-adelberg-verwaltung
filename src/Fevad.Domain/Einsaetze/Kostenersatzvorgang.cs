using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>Tatbestand, auf den sich der Kostenersatz stützt (§ 34 FwG, fiktive Kostenersatzsatzung Adelberg § 2).</summary>
public enum Kostentatbestand
{
    /// <summary>Gefahr oder Schaden vorsätzlich oder grob fahrlässig verursacht.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 1: vorsätzlich oder grob fahrlässig verursacht")] Abs1Nr1Vorsatz,
    /// <summary>Gefahr oder Schaden beim Betrieb eines Kraft-, Anhänger-, Schienen-, Luft- oder Wasserfahrzeugs.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 2: Betrieb eines Fahrzeugs")] Abs1Nr2Fahrzeug,
    /// <summary>Sonderlösch- und Einsatzmittel bei einem Brand in einem Gewerbe- oder Industriebetrieb.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 3: Sonderlöschmittel im Gewerbebetrieb")] Abs1Nr3Sonderloeschmittel,
    /// <summary>Gefahr oder Schaden beim gewerblichen oder militärischen Umgang mit Gefahrstoffen oder wassergefährdenden Stoffen.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 4: Gefahrstoffe, wassergefährdende Stoffe")] Abs1Nr4Gefahrstoffe,
    /// <summary>Vorsätzliche oder grob fahrlässige Alarmierung ohne Schadensereignis.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 5: Alarmierung ohne Schadensereignis")] Abs1Nr5Falschalarm,
    /// <summary>Fehlalarm einer Brandmeldeanlage oder anderen technischen Anlage mit automatischer Übertragung.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 6: Fehlalarm Brandmeldeanlage")] Abs1Nr6Brandmeldeanlage,
    /// <summary>Fehlalarm eines automatischen Notrufsystems eines Kraftfahrzeugs.</summary>
    [Display(Name = "§ 34 Abs. 1 Nr. 7: Fehlalarm Notrufsystem eines Fahrzeugs")] Abs1Nr7Notrufsystem,
    /// <summary>Leistung nach § 2 Abs. 2 FwG (Brandsicherheitswache, Absichern, Türöffnung ohne Gefahr usw.); stets kostenpflichtig.</summary>
    [Display(Name = "§ 34 Abs. 2: Leistung nach § 2 Abs. 2 FwG")] Abs2Leistung,
}

/// <summary>Empfehlung der Feuerwehr bzw. Entscheidung der Gemeinde über den Kostenersatz.</summary>
public enum KostenersatzErgebnis
{
    /// <summary>Kostenersatz wird erhoben.</summary>
    [Display(Name = "kostenpflichtig")] Kostenpflichtig,
    /// <summary>Kein Tatbestand: der Einsatz bleibt unentgeltlich (§ 2 Abs. 1 FwG).</summary>
    [Display(Name = "unentgeltlich")] Unentgeltlich,
    /// <summary>Ein Tatbestand liegt vor, aber es wird ganz oder teilweise verzichtet (§ 34 Abs. 3 FwG); Haushaltsverzicht, zu begründen.</summary>
    [Display(Name = "Verzicht")] Verzicht,
}

/// <summary>Grund eines Verzichts nach § 34 Abs. 3 FwG.</summary>
public enum Verzichtsgrund
{
    /// <summary>Die Erhebung wäre eine unbillige Härte.</summary>
    [Display(Name = "unbillige Härte")] UnbilligeHaerte,
    /// <summary>Der Verzicht liegt im öffentlichen Interesse.</summary>
    [Display(Name = "öffentliches Interesse")] OeffentlichesInteresse,
}

/// <summary>Stand eines Kostenersatz-Vorgangs.</summary>
public enum KostenersatzStand
{
    /// <summary>Angelegt, die Feuerwehr hat noch nicht empfohlen.</summary>
    [Display(Name = "in Bearbeitung")] InBearbeitung,
    /// <summary>Die Feuerwehr hat empfohlen; die Gemeinde entscheidet.</summary>
    [Display(Name = "empfohlen, Entscheidung der Gemeinde offen")] Empfohlen,
    /// <summary>Die Gemeinde hat entschieden.</summary>
    [Display(Name = "entschieden")] Entschieden,
}

/// <summary>Wie ein Kostenschuldner zum Einsatz steht (fiktive Kostenersatzsatzung § 3).</summary>
public enum SchuldnerArt
{
    /// <summary>Verursacher.</summary>
    [Display(Name = "Verursacher")] Verursacher,
    /// <summary>Halter des Fahrzeugs.</summary>
    [Display(Name = "Fahrzeughalter")] Halter,
    /// <summary>Betreiber der Anlage, z. B. der Brandmeldeanlage.</summary>
    [Display(Name = "Betreiber der Anlage")] Betreiber,
    /// <summary>Eigentümer oder Besitzer der Sache.</summary>
    [Display(Name = "Eigentümer / Besitzer")] Eigentuemer,
    /// <summary>In dessen Interesse die Leistung nach § 2 Abs. 2 FwG erbracht wurde.</summary>
    [Display(Name = "Begünstigter")] Beguenstigter,
    /// <summary>Sonstiger Pflichtiger.</summary>
    [Display(Name = "Sonstiger")] Sonstiger,
}

/// <summary>Wie der Bescheid den Schuldner erreichen soll.</summary>
public enum Zustellweg
{
    /// <summary>Einfacher Brief (Bekanntgabe am vierten Tag nach Aufgabe zur Post, § 41 Abs. 2 LVwVfG).</summary>
    [Display(Name = "einfacher Brief")] Brief,
    /// <summary>Einschreiben.</summary>
    [Display(Name = "Einschreiben")] Einschreiben,
    /// <summary>Postzustellungsurkunde.</summary>
    [Display(Name = "Postzustellungsurkunde")] Zustellungsurkunde,
    /// <summary>Gegen Empfangsbekenntnis, z. B. an einen Anwalt oder eine Versicherung.</summary>
    [Display(Name = "Empfangsbekenntnis")] Empfangsbekenntnis,
    /// <summary>Persönliche Übergabe.</summary>
    [Display(Name = "persönlich")] Persoenlich,
}

/// <summary>Kosten nach tatsächlichem Aufwand, die nicht aus Sätzen berechnet werden (fiktive Kostenersatzsatzung § 4 Abs. 6).</summary>
public enum ZusatzkostenArt
{
    /// <summary>Kosten anderer Feuerwehren und Dritter (Überlandhilfe).</summary>
    [Display(Name = "Überlandhilfe, andere Feuerwehren, Dritte")] Ueberlandhilfe,
    /// <summary>Sonderlösch- und Sondereinsatzmittel.</summary>
    [Display(Name = "Sonderlöschmittel")] Sonderloeschmittel,
    /// <summary>Verbrauchsmaterial zu Wiederbeschaffungskosten; darauf kommt der Vorhaltezuschlag.</summary>
    [Display(Name = "Verbrauchsmaterial (Wiederbeschaffung)")] Verbrauch,
    /// <summary>Entsorgungskosten.</summary>
    [Display(Name = "Entsorgung")] Entsorgung,
    /// <summary>Sonstige Kosten.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>
/// Kostenersatz-Vorgang je Einsatz (V21, V15). Die Feuerwehr erfasst Tatbestand, Schuldner und Zusatzkosten und gibt eine Empfehlung ab;
/// entscheiden kann nur die Gemeinde (Hauptamt), die Feuerwehr nie (Andi, 25.10.2026). Verzichte bleiben mit Begründung dokumentiert, weil sie
/// Haushaltsverzicht sind.
/// </summary>
public class Kostenersatzvorgang : Entitaet
{
    /// <summary>Kennung des Einsatzes; je Einsatz höchstens ein Vorgang.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Tatbestand nach § 34 FwG; null, solange keiner gewählt ist oder der Einsatz unentgeltlich bleibt.</summary>
    public Kostentatbestand? Tatbestand { get; set; }

    /// <summary>Kurzer Sachverhalt für die Gemeinde und den späteren Bescheid, ohne Gesundheitsangaben.</summary>
    public string? Sachverhalt { get; set; }

    /// <summary>Mehrere Schuldner haften als Gesamtschuldner (Satzung § 3 Abs. 2); sonst trägt jeder nur seinen Anteil.</summary>
    public bool Gesamtschuld { get; set; } = true;

    /// <summary>Die Verwaltungsgebühr für den Bescheid wird erhoben.</summary>
    public bool Verwaltungsgebuehr { get; set; } = true;

    /// <summary>Empfehlung der Feuerwehr; null, solange nicht empfohlen.</summary>
    public KostenersatzErgebnis? Empfehlung { get; set; }
    /// <summary>Grund bei empfohlenem Verzicht.</summary>
    public Verzichtsgrund? EmpfehlungGrund { get; set; }
    /// <summary>Begründung der Empfehlung.</summary>
    public string? EmpfehlungBegruendung { get; set; }
    /// <summary>Zeitpunkt der Empfehlung (UTC).</summary>
    public DateTimeOffset? EmpfohlenAm { get; set; }
    /// <summary>Konto, das empfohlen hat.</summary>
    public Guid? EmpfohlenVon { get; set; }
    /// <summary>Name des Verantwortlichen der Feuerwehr zum Zeitpunkt der Empfehlung.</summary>
    public string? EmpfohlenVonName { get; set; }

    /// <summary>Entscheidung der Gemeinde; null, solange offen.</summary>
    public KostenersatzErgebnis? Entscheidung { get; set; }
    /// <summary>Grund bei Verzicht.</summary>
    public Verzichtsgrund? EntscheidungGrund { get; set; }
    /// <summary>Begründung der Entscheidung, Pflicht bei Verzicht und bei Abweichung von der Empfehlung.</summary>
    public string? EntscheidungBegruendung { get; set; }
    /// <summary>Tag der Entscheidung.</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Konto, das entschieden hat.</summary>
    public Guid? EntschiedenVon { get; set; }
    /// <summary>Name des Verantwortlichen der Gemeinde.</summary>
    public string? EntschiedenVonName { get; set; }

    /// <summary>Kostenschuldner.</summary>
    public List<Kostenschuldner> Schuldner { get; set; } = [];

    /// <summary>Kosten nach tatsächlichem Aufwand.</summary>
    public List<Zusatzkosten> Zusatzkosten { get; set; } = [];

    /// <summary>Bescheide an die Schuldner nach der Entscheidung (V22).</summary>
    public List<Kostenbescheid> Bescheide { get; set; } = [];

    /// <summary>Stand des Vorgangs.</summary>
    public KostenersatzStand Stand => Entscheidung is not null ? KostenersatzStand.Entschieden
        : Empfehlung is not null ? KostenersatzStand.Empfohlen
        : KostenersatzStand.InBearbeitung;

    /// <summary>Ab der Empfehlung ändert die Feuerwehr nichts mehr, bis sie die Empfehlung zurücknimmt.</summary>
    public bool FeuerwehrDarfAendern => Stand == KostenersatzStand.InBearbeitung;

    /// <summary>
    /// Prüft eine Empfehlung oder Entscheidung: kostenpflichtig braucht Tatbestand und Schuldner, Fahrzeugtatbestand ein Kennzeichen,
    /// Verzicht einen Tatbestand, einen Grund und eine Begründung.
    /// </summary>
    /// <param name="ergebnis">Empfohlenes oder entschiedenes Ergebnis.</param>
    /// <param name="grund">Grund bei Verzicht.</param>
    /// <param name="begruendung">Begründung.</param>
    /// <returns>Fehlermeldungen; leer, wenn alles passt.</returns>
    public IReadOnlyList<string> Pruefen(KostenersatzErgebnis ergebnis, Verzichtsgrund? grund, string? begruendung)
    {
        var fehler = new List<string>();
        if (ergebnis != KostenersatzErgebnis.Unentgeltlich && Tatbestand is null)
        {
            fehler.Add("Für Kostenersatz oder Verzicht ist ein Tatbestand nach § 34 FwG nötig.");
        }

        if (ergebnis == KostenersatzErgebnis.Unentgeltlich && Tatbestand == Kostentatbestand.Abs2Leistung)
        {
            fehler.Add("Leistungen nach § 2 Abs. 2 FwG sind stets kostenpflichtig; ganz oder teilweise absehen geht nur als Verzicht mit Begründung.");
        }

        if (ergebnis == KostenersatzErgebnis.Kostenpflichtig)
        {
            if (Schuldner.Count == 0)
            {
                fehler.Add("Mindestens ein Kostenschuldner fehlt.");
            }

            if (Tatbestand == Kostentatbestand.Abs1Nr2Fahrzeug && !Schuldner.Any(s => !string.IsNullOrWhiteSpace(s.Kennzeichen)))
            {
                fehler.Add("Beim Fahrzeugtatbestand braucht ein Schuldner das Kennzeichen (Halterauskunft).");
            }

            if (Tatbestand is Kostentatbestand.Abs1Nr6Brandmeldeanlage && !Schuldner.Any(s => s.Art == SchuldnerArt.Betreiber))
            {
                fehler.Add("Beim Fehlalarm einer Brandmeldeanlage ist der Betreiber der Anlage als Schuldner nötig.");
            }
        }

        if (ergebnis == KostenersatzErgebnis.Verzicht && (grund is null || string.IsNullOrWhiteSpace(begruendung)))
        {
            fehler.Add("Ein Verzicht braucht Grund und Begründung (§ 34 Abs. 3 FwG).");
        }

        return fehler;
    }
}

/// <summary>Kostenschuldner eines Vorgangs; nur die Angaben, die für den Bescheid nötig sind (Satzung § 3 Abs. 3).</summary>
public class Kostenschuldner : Entitaet
{
    /// <summary>Kennung des Vorgangs.</summary>
    public Guid KostenersatzvorgangId { get; set; }
    /// <summary>Der Vorgang.</summary>
    public Kostenersatzvorgang? Kostenersatzvorgang { get; set; }

    /// <summary>Wie der Schuldner zum Einsatz steht.</summary>
    public SchuldnerArt Art { get; set; }

    /// <summary>Name der Person oder Firma.</summary>
    public required string Name { get; set; }
    /// <summary>Zustellanschrift.</summary>
    public string? Anschrift { get; set; }

    /// <summary>Kennzeichen des beteiligten Fahrzeugs.</summary>
    public string? Kennzeichen { get; set; }
    /// <summary>Halterauskunft, z. B. „Zulassungsstelle GP, 03.11.2026“.</summary>
    public string? Halterauskunft { get; set; }

    /// <summary>Haftpflicht- bzw. Kfz-Versicherung.</summary>
    public string? Versicherung { get; set; }
    /// <summary>Versicherungsscheinnummer bzw. Schadennummer.</summary>
    public string? Versicherungsnummer { get; set; }

    /// <summary>Weg der Bekanntgabe des Bescheids.</summary>
    public Zustellweg Zustellweg { get; set; }

    /// <summary>Anteil in Prozent, wenn keine Gesamtschuld besteht; sonst null.</summary>
    public decimal? AnteilProzent { get; set; }

    /// <summary>Aus diesem Beteiligten des Einsatzberichts übernommen, falls so angelegt.</summary>
    public Guid? EinsatzBeteiligterId { get; set; }
}

/// <summary>Kosten nach tatsächlichem Aufwand in einem Kostenersatz-Vorgang.</summary>
public class Zusatzkosten : Entitaet
{
    /// <summary>Kennung des Vorgangs.</summary>
    public Guid KostenersatzvorgangId { get; set; }
    /// <summary>Der Vorgang.</summary>
    public Kostenersatzvorgang? Kostenersatzvorgang { get; set; }

    /// <summary>Art der Kosten.</summary>
    public ZusatzkostenArt Art { get; set; }

    /// <summary>Bezeichnung, z. B. „Rechnung FF Göppingen Nr. 123“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Betrag in Euro, bei Verbrauch ohne Vorhaltezuschlag.</summary>
    public decimal Betrag { get; set; }
}
