using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>Schritt in der Statuskette eines Kostenersatzbescheids (V22).</summary>
public enum BescheidSchritt
{
    /// <summary>Anhörung des Schuldners vor dem Bescheid (§ 28 LVwVfG), mit Frist.</summary>
    [Display(Name = "Anhörung")] Anhoerung,
    /// <summary>Festsetzung durch Bescheid; legt den Betrag fest.</summary>
    [Display(Name = "Festsetzung")] Festsetzung,
    /// <summary>Bekanntgabe des Bescheids (§ 41 LVwVfG); ab hier läuft die Fälligkeit.</summary>
    [Display(Name = "Bekanntgabe")] Bekanntgabe,
    /// <summary>Zahlungseingang, auch in Teilen.</summary>
    [Display(Name = "Zahlung")] Zahlung,
    /// <summary>Mahnung; in FEVAD nur als Status, das Verfahren führt die Gemeindekasse.</summary>
    [Display(Name = "Mahnung")] Mahnung,
    /// <summary>Vollstreckung nach LVwVG; nur als Status.</summary>
    [Display(Name = "Vollstreckung")] Vollstreckung,
    /// <summary>Widerspruch eingegangen (§ 70 VwGO).</summary>
    [Display(Name = "Widerspruch")] Widerspruch,
    /// <summary>Dem Widerspruch wird abgeholfen; ein neuer Betrag ersetzt den festgesetzten.</summary>
    [Display(Name = "Abhilfe")] Abhilfe,
    /// <summary>Widerspruch der Widerspruchsbehörde vorgelegt bzw. zurückgewiesen.</summary>
    [Display(Name = "Vorlage / Widerspruchsbescheid")] Widerspruchsbescheid,
    /// <summary>Klage erhoben.</summary>
    [Display(Name = "Klage")] Klage,
    /// <summary>Klageverfahren beendet; ein neuer Betrag ersetzt bei Bedarf den festgesetzten.</summary>
    [Display(Name = "Klage erledigt")] KlageErledigt,
    /// <summary>Niederschlagung der Forderung; Haushaltsverzicht, zu begründen.</summary>
    [Display(Name = "Niederschlagung")] Niederschlagung,
    /// <summary>Aufhebung oder Rücknahme des Bescheids.</summary>
    [Display(Name = "Aufhebung")] Aufhebung,
}

/// <summary>Stand eines Bescheids, aus seinen Schritten abgeleitet.</summary>
public enum BescheidStand
{
    /// <summary>Noch nichts geschehen.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Anhörung läuft.</summary>
    [Display(Name = "Anhörung")] Anhoerung,
    /// <summary>Festgesetzt, noch nicht bekanntgegeben.</summary>
    [Display(Name = "festgesetzt")] Festgesetzt,
    /// <summary>Bekanntgegeben, Zahlung offen.</summary>
    [Display(Name = "bekanntgegeben")] Bekanntgegeben,
    /// <summary>Gemahnt oder in der Vollstreckung.</summary>
    [Display(Name = "gemahnt")] Gemahnt,
    /// <summary>Widerspruch offen.</summary>
    [Display(Name = "Widerspruch")] Widerspruch,
    /// <summary>Klage offen.</summary>
    [Display(Name = "Klage")] Klage,
    /// <summary>Voll bezahlt.</summary>
    [Display(Name = "bezahlt")] Bezahlt,
    /// <summary>Niedergeschlagen.</summary>
    [Display(Name = "niedergeschlagen")] Niedergeschlagen,
    /// <summary>Aufgehoben.</summary>
    [Display(Name = "aufgehoben")] Aufgehoben,
}

/// <summary>
/// Kostenersatzbescheid an einen Schuldner (V22). Den Bescheid erlässt die Gemeinde; FEVAD führt die Statuskette mit Datum, Bearbeiter und Vermerk
/// je Schritt und erzeugt Anhörung und Bescheid als PDF. Mahnung und Vollstreckung sind nur Status, das Verfahren führt die Gemeindekasse.
/// </summary>
public class Kostenbescheid : Entitaet
{
    /// <summary>Kennung des Vorgangs.</summary>
    public Guid KostenersatzvorgangId { get; set; }
    /// <summary>Der Vorgang.</summary>
    public Kostenersatzvorgang? Kostenersatzvorgang { get; set; }

    /// <summary>Kennung des Schuldners; null, nachdem der Schuldner nach L3 gelöscht wurde.</summary>
    public Guid? KostenschuldnerId { get; set; }
    /// <summary>Der Schuldner.</summary>
    public Kostenschuldner? Kostenschuldner { get; set; }

    /// <summary>Kassenzeichen der Gemeindekasse.</summary>
    public string? Kassenzeichen { get; set; }

    /// <summary>Frist der Anhörung.</summary>
    public DateOnly? AnhoerungBis { get; set; }

    /// <summary>Tag, an dem der Bescheid als bekanntgegeben gilt (Zugangsfiktion bei einfachem Brief).</summary>
    public DateOnly? BekanntgegebenAm { get; set; }

    /// <summary>Fälligkeit nach Bekanntgabe.</summary>
    public DateOnly? FaelligAm { get; set; }

    /// <summary>Schritte in der Reihenfolge der Erfassung.</summary>
    public List<BescheidEreignis> Ereignisse { get; set; } = [];

    /// <summary>Geltender festgesetzter Betrag: der jüngste aus Festsetzung, Abhilfe oder Klageende; null vor der Festsetzung.</summary>
    public decimal? Betrag => Ereignisse
        .Where(e => e.Betrag is not null && e.Schritt is BescheidSchritt.Festsetzung or BescheidSchritt.Abhilfe or BescheidSchritt.KlageErledigt)
        .OrderBy(e => e.Datum).ThenBy(e => e.ErfasstAm).LastOrDefault()?.Betrag;

    /// <summary>Summe der Zahlungen.</summary>
    public decimal Bezahlt => Ereignisse.Where(e => e.Schritt == BescheidSchritt.Zahlung).Sum(e => e.Betrag ?? 0);

    /// <summary>Offener Betrag; 0 nach Niederschlagung oder Aufhebung.</summary>
    public decimal Offen => Hat(BescheidSchritt.Aufhebung) || Hat(BescheidSchritt.Niederschlagung) ? 0 : Math.Max(0, (Betrag ?? 0) - Bezahlt);

    /// <summary>Die Forderung ist erledigt: bezahlt, niedergeschlagen oder aufgehoben.</summary>
    public bool Erledigt => Stand is BescheidStand.Bezahlt or BescheidStand.Niedergeschlagen or BescheidStand.Aufgehoben;

    /// <summary>Stand aus den Schritten.</summary>
    public BescheidStand Stand
    {
        get
        {
            if (Hat(BescheidSchritt.Aufhebung))
            {
                return BescheidStand.Aufgehoben;
            }

            // Volle Abhilfe oder Klageende ohne Betrag wirkt wie eine Aufhebung.
            if (Betrag == 0)
            {
                return BescheidStand.Aufgehoben;
            }

            if (Betrag is { } b && Bezahlt >= b)
            {
                return BescheidStand.Bezahlt;
            }

            if (Hat(BescheidSchritt.Niederschlagung))
            {
                return BescheidStand.Niedergeschlagen;
            }

            if (Laeuft(BescheidSchritt.Klage, BescheidSchritt.KlageErledigt))
            {
                return BescheidStand.Klage;
            }

            if (Laeuft(BescheidSchritt.Widerspruch, BescheidSchritt.Abhilfe, BescheidSchritt.Widerspruchsbescheid))
            {
                return BescheidStand.Widerspruch;
            }

            return Hat(BescheidSchritt.Mahnung) || Hat(BescheidSchritt.Vollstreckung) ? BescheidStand.Gemahnt
                : Hat(BescheidSchritt.Bekanntgabe) ? BescheidStand.Bekanntgegeben
                : Hat(BescheidSchritt.Festsetzung) ? BescheidStand.Festgesetzt
                : Hat(BescheidSchritt.Anhoerung) ? BescheidStand.Anhoerung
                : BescheidStand.Entwurf;
        }
    }

    /// <summary>
    /// Prüft, ob ein Schritt jetzt erlaubt ist: Festsetzung nicht doppelt und nur ohne vorläufige Sätze, Bekanntgabe nach der Festsetzung,
    /// Zahlung, Mahnung und Widerspruch erst nach der Bekanntgabe, Abschlüsse nur zu einem offenen Rechtsbehelf, nichts nach Erledigung.
    /// </summary>
    /// <param name="schritt">Der Schritt.</param>
    /// <param name="betrag">Betrag des Schritts.</param>
    /// <param name="vorlaeufig">Die Aufstellung enthält vorläufige Sätze (Platzhalter-Sperre, F3).</param>
    /// <returns>Fehlermeldung oder null.</returns>
    public string? Pruefen(BescheidSchritt schritt, decimal? betrag, bool vorlaeufig)
    {
        if (Erledigt)
        {
            return "Der Bescheid ist erledigt; es lässt sich kein Schritt mehr erfassen.";
        }

        var festgesetzt = Hat(BescheidSchritt.Festsetzung);
        var bekannt = Hat(BescheidSchritt.Bekanntgabe);
        return schritt switch
        {
            BescheidSchritt.Anhoerung when festgesetzt => "Die Anhörung gehört vor die Festsetzung.",
            BescheidSchritt.Festsetzung when festgesetzt => "Der Bescheid ist schon festgesetzt; Änderungen laufen über Abhilfe oder Aufhebung.",
            BescheidSchritt.Festsetzung when vorlaeufig => "Die Aufstellung enthält vorläufige Sätze (Platzhalter). Festsetzen geht erst mit beschlossener Satzung.",
            BescheidSchritt.Festsetzung when betrag is null or <= 0 => "Für die Festsetzung fehlt der Betrag.",
            BescheidSchritt.Bekanntgabe when !festgesetzt => "Bekanntgegeben werden kann nur ein festgesetzter Bescheid.",
            BescheidSchritt.Bekanntgabe when bekannt => "Der Bescheid ist schon bekanntgegeben.",
            BescheidSchritt.Zahlung when !festgesetzt => "Zahlungen gibt es erst nach der Festsetzung.",
            BescheidSchritt.Zahlung when betrag is null or <= 0 => "Für die Zahlung fehlt der Betrag.",
            BescheidSchritt.Mahnung or BescheidSchritt.Vollstreckung or BescheidSchritt.Widerspruch when !bekannt => "Dieser Schritt setzt die Bekanntgabe voraus.",
            BescheidSchritt.Abhilfe or BescheidSchritt.Widerspruchsbescheid when !Laeuft(BescheidSchritt.Widerspruch, BescheidSchritt.Abhilfe, BescheidSchritt.Widerspruchsbescheid)
                => "Es ist kein Widerspruch offen.",
            BescheidSchritt.Abhilfe when betrag is null or < 0 => "Für die Abhilfe fehlt der neue Betrag (0 bei voller Abhilfe).",
            BescheidSchritt.Klage when !Hat(BescheidSchritt.Widerspruchsbescheid) => "Klage ist erst nach dem Widerspruchsbescheid möglich.",
            BescheidSchritt.KlageErledigt when !Laeuft(BescheidSchritt.Klage, BescheidSchritt.KlageErledigt) => "Es ist keine Klage offen.",
            BescheidSchritt.Niederschlagung when !festgesetzt => "Niedergeschlagen werden kann nur eine festgesetzte Forderung.",
            _ => null,
        };
    }

    /// <summary>
    /// Tag der Bekanntgabe: bei einfachem Brief und Einschreiben so viele Tage nach Aufgabe zur Post (§ 41 Abs. 2 LVwVfG, § 4 Abs. 2 LVwZG, Parameter),
    /// sonst der Tag der Übergabe bzw. Zustellung. Fällt der Tag auf ein Wochenende, wird er nicht verschoben.
    /// </summary>
    /// <param name="aufgabe">Tag der Aufgabe zur Post bzw. der Übergabe.</param>
    /// <param name="weg">Zustellweg.</param>
    /// <param name="fiktionTage">Tage der Zugangsfiktion.</param>
    /// <returns>Tag der Bekanntgabe.</returns>
    public static DateOnly Bekanntgabe(DateOnly aufgabe, Zustellweg weg, int fiktionTage) =>
        weg is Zustellweg.Brief or Zustellweg.Einschreiben ? aufgabe.AddDays(fiktionTage) : aufgabe;

    /// <summary>Ende der Zahlungsverjährung: Ablauf des Jahres der Fälligkeit plus so viele Jahre; null ohne Fälligkeit.</summary>
    /// <param name="jahre">Verjährungsfrist in Jahren.</param>
    /// <returns>Letzter Tag vor der Verjährung.</returns>
    public DateOnly? VerjaehrtAm(int jahre) => FaelligAm is { } f ? new DateOnly(f.Year + jahre, 12, 31) : null;

    private bool Hat(BescheidSchritt schritt) => Ereignisse.Any(e => e.Schritt == schritt);

    // Ein Verfahren ist offen, wenn seit dem letzten Beginn kein Abschluss erfasst wurde.
    private bool Laeuft(BescheidSchritt beginn, params BescheidSchritt[] abschluesse)
    {
        var start = Ereignisse.Where(e => e.Schritt == beginn).MaxBy(e => (e.Datum, e.ErfasstAm));
        return start is not null && !Ereignisse.Any(e => abschluesse.Contains(e.Schritt) && (e.Datum, e.ErfasstAm).CompareTo((start.Datum, start.ErfasstAm)) > 0);
    }
}

/// <summary>Ein Schritt in der Statuskette mit Datum, Bearbeiter und Vermerk.</summary>
public class BescheidEreignis : Entitaet
{
    /// <summary>Kennung des Bescheids.</summary>
    public Guid KostenbescheidId { get; set; }
    /// <summary>Der Bescheid.</summary>
    public Kostenbescheid? Kostenbescheid { get; set; }

    /// <summary>Der Schritt.</summary>
    public BescheidSchritt Schritt { get; set; }

    /// <summary>Tag des Schritts.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Betrag bei Festsetzung, Abhilfe, Klageende und Zahlung.</summary>
    public decimal? Betrag { get; set; }

    /// <summary>Vermerk, z. B. Aktenzeichen, Zahlungsweg oder Begründung der Niederschlagung; keine Gesundheitsangaben.</summary>
    public string? Vermerk { get; set; }

    /// <summary>Anmeldename des Bearbeiters.</summary>
    public string? Bearbeiter { get; set; }

    /// <summary>Zeitpunkt der Erfassung (UTC); ordnet Schritte am selben Tag.</summary>
    public DateTimeOffset ErfasstAm { get; set; }
}

/// <summary>
/// Mustertexte für Anhörung und Bescheid und das Einsetzen der Platzhalter. Die geltenden Texte stehen als Parameter und werden vom Hauptamt
/// gepflegt; diese Muster sind nur die Vorbelegung. Platzhalter: {Gemeinde}, {Einsatznummer}, {Einsatzdatum}, {Tatbestand}, {Betrag},
/// {AnhoerungBis}, {FaelligAm}, {Kassenzeichen}.
/// </summary>
public static class Bescheidtexte
{
    /// <summary>Die erlaubten Platzhalter.</summary>
    public static readonly string[] Platzhalter = ["{Gemeinde}", "{Einsatznummer}", "{Einsatzdatum}", "{Tatbestand}", "{Betrag}", "{AnhoerungBis}", "{FaelligAm}", "{Kassenzeichen}"];

    /// <summary>Muster: Anschreiben zur Anhörung.</summary>
    public const string Anhoerung =
        "Die Feuerwehr der {Gemeinde} war am {Einsatzdatum} im Einsatz (Einsatznummer {Einsatznummer}). Für diesen Einsatz beabsichtigt die {Gemeinde}, "
        + "nach § 34 Feuerwehrgesetz und der Feuerwehr-Kostenersatzsatzung Kostenersatz von voraussichtlich {Betrag} zu erheben ({Tatbestand}). "
        + "Die Berechnung liegt bei. Vor der Entscheidung erhalten Sie Gelegenheit, sich bis {AnhoerungBis} zu äußern (§ 28 LVwVfG). "
        + "Bitte teilen Sie uns insbesondere mit, ob eine Versicherung für den Schaden eintritt.";

    /// <summary>Muster: Begründung.</summary>
    public const string Begruendung =
        "Die Feuerwehr der {Gemeinde} war am {Einsatzdatum} im Einsatz (Einsatznummer {Einsatznummer}). Der Einsatz ist nach § 34 Feuerwehrgesetz "
        + "Baden-Württemberg kostenersatzpflichtig ({Tatbestand}). Sie sind als Kostenschuldner heranzuziehen. Die Höhe ergibt sich aus der Feuerwehr-Kostenersatzsatzung "
        + "der {Gemeinde} und der beigefügten Berechnung. Gründe, von der Erhebung ganz oder teilweise abzusehen, liegen nicht vor.";

    /// <summary>Muster: Zahlungshinweis.</summary>
    public const string Zahlung =
        "Bitte überweisen Sie {Betrag} bis {FaelligAm} unter Angabe des Kassenzeichens {Kassenzeichen} an die Gemeindekasse der {Gemeinde} "
        + "(IBAN und Bank bitte im Textbaustein ergänzen).";

    /// <summary>Muster: Rechtsbehelfsbelehrung.</summary>
    public const string Rechtsbehelf =
        "Gegen diesen Bescheid kann innerhalb eines Monats nach Bekanntgabe Widerspruch erhoben werden. Der Widerspruch ist schriftlich oder "
        + "zur Niederschrift bei der {Gemeinde} einzulegen. Der Widerspruch hat keine aufschiebende Wirkung (§ 80 Abs. 2 Satz 1 Nr. 1 VwGO); "
        + "der festgesetzte Betrag ist zum Fälligkeitstag zu zahlen.";

    /// <summary>Setzt die Platzhalter ein; fehlende Werte erscheinen als „[fehlt]“.</summary>
    /// <param name="text">Textbaustein.</param>
    /// <param name="werte">Werte je Platzhalter, z. B. „{Betrag}“.</param>
    /// <returns>Der fertige Text.</returns>
    public static string Einsetzen(string text, IReadOnlyDictionary<string, string?> werte)
    {
        foreach (var p in Platzhalter)
        {
            text = text.Replace(p, werte.TryGetValue(p, out var w) && !string.IsNullOrWhiteSpace(w) ? w : "[fehlt]", StringComparison.Ordinal);
        }

        return text;
    }
}
