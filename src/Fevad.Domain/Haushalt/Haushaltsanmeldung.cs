using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Haushalt;

/// <summary>Teil des Gemeindehaushalts, in dem eine Haushaltsstelle steht.</summary>
public enum Haushaltsteil
{
    /// <summary>Ergebnishaushalt: laufender Aufwand.</summary>
    [Display(Name = "Ergebnishaushalt")] Ergebnishaushalt,
    /// <summary>Finanzhaushalt: Investitionen ab der Wertgrenze und Investitionszuwendungen.</summary>
    [Display(Name = "Finanzhaushalt")] Finanzhaushalt,
}

/// <summary>Haushaltsstelle der Gemeinde (Produkt, Kostenstelle bzw. Innenauftrag, Sachkonto); liefert die Kämmerei.</summary>
public class Haushaltsstelle : Katalogeintrag
{
    /// <summary>Produkt nach Produktplan BW, z. B. „12.60.01 Brandschutz“.</summary>
    public required string Produkt { get; set; }

    /// <summary>SAP-Kostenstelle.</summary>
    public required string Kostenstelle { get; set; }

    /// <summary>SAP-Innenauftrag, z. B. je Fahrzeug.</summary>
    public string? Innenauftrag { get; set; }

    /// <summary>Sachkonto nach Kontenrahmen BW.</summary>
    public required string Sachkonto { get; set; }

    /// <summary>Ergebnis- oder Finanzhaushalt.</summary>
    public Haushaltsteil Teil { get; set; }

    /// <summary>Einzahlung (z. B. Investitionszuwendung) statt Auszahlung bzw. Aufwand.</summary>
    public bool Einzahlung { get; set; }
}

/// <summary>Stand der Haushaltsanmeldung eines Jahres.</summary>
public enum AnmeldungStatus
{
    /// <summary>Wird gesammelt.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Im Feuerwehrausschuss beraten und beschlossen.</summary>
    [Display(Name = "beraten")] Beraten,
    /// <summary>Freigegeben und an die Kämmerei übergeben.</summary>
    [Display(Name = "eingereicht")] Eingereicht,
    /// <summary>Steht im Haushaltsentwurf der Gemeinde.</summary>
    [Display(Name = "im Haushaltsentwurf")] ImHaushaltsentwurf,
    /// <summary>Haushalt vom Gemeinderat beschlossen; Rückmeldungen je Position liegen vor.</summary>
    [Display(Name = "beschlossen")] Beschlossen,
}

/// <summary>Bereich, aus dem eine Position kommt; der Jugendwart meldet nur für die Jugendfeuerwehr.</summary>
public enum Haushaltsbereich
{
    /// <summary>Allgemein, Führung.</summary>
    [Display(Name = "Allgemein")] Allgemein,
    /// <summary>Fahrzeuge und Geräte.</summary>
    [Display(Name = "Technik")] Technik,
    /// <summary>Atemschutz.</summary>
    [Display(Name = "Atemschutz")] Atemschutz,
    /// <summary>Kleiderkammer und PSA.</summary>
    [Display(Name = "Kleiderkammer")] Kleiderkammer,
    /// <summary>Jugendfeuerwehr.</summary>
    [Display(Name = "Jugendfeuerwehr")] Jugend,
    /// <summary>Gerätehaus.</summary>
    [Display(Name = "Gerätehaus")] Geraetehaus,
}

/// <summary>Woher der Preis einer Position stammt.</summary>
public enum Preisquelle
{
    /// <summary>Schätzung.</summary>
    [Display(Name = "Schätzung")] Schaetzung,
    /// <summary>Angebot liegt vor.</summary>
    [Display(Name = "Angebot")] Angebot,
    /// <summary>Ist des Vorjahres.</summary>
    [Display(Name = "Vorjahr")] Vorjahr,
    /// <summary>Satz aus dem Katalog bzw. Parameter.</summary>
    [Display(Name = "Kostenansatz")] Kostenansatz,
    /// <summary>Letzter Kaufpreis bzw. Stückwert.</summary>
    [Display(Name = "letzter Kaufpreis")] Kaufpreis,
}

/// <summary>Stand einer Position innerhalb der Feuerwehr.</summary>
public enum PositionStatus
{
    /// <summary>Automatisch vorgeschlagen, noch nicht bestätigt.</summary>
    [Display(Name = "Vorschlag")] Vorschlag,
    /// <summary>Angemeldet bzw. vom Gerätewart bestätigt.</summary>
    [Display(Name = "angemeldet")] Angemeldet,
    /// <summary>Intern gestrichen; geht nicht an die Gemeinde.</summary>
    [Display(Name = "intern gestrichen")] Gestrichen,
}

/// <summary>Rückmeldung der Gemeinde zu einer Position.</summary>
public enum Haushaltsergebnis
{
    /// <summary>Wie angemeldet beschlossen.</summary>
    [Display(Name = "beschlossen")] Beschlossen,
    /// <summary>Mit geringerem Betrag beschlossen.</summary>
    [Display(Name = "gekürzt")] Gekuerzt,
    /// <summary>Gestrichen.</summary>
    [Display(Name = "gestrichen")] Gestrichen,
    /// <summary>In ein späteres Jahr verschoben.</summary>
    [Display(Name = "verschoben")] Verschoben,
}

/// <summary>Haushaltsmittelanmeldung der Feuerwehr an die Gemeinde für ein Haushaltsjahr (V29); nur Gemeindehaushalt, kein Sondervermögen.</summary>
public class Haushaltsanmeldung : Entitaet
{
    /// <summary>Haushaltsjahr.</summary>
    public int Jahr { get; set; }

    /// <summary>Stand.</summary>
    public AnmeldungStatus Status { get; set; }

    /// <summary>Tag der Beratung im Feuerwehrausschuss.</summary>
    public DateOnly? BeratenAm { get; set; }

    /// <summary>Verweis auf den Beschluss (Sitzung, TOP).</summary>
    public string? Ausschussbeschluss { get; set; }

    /// <summary>Freigabe durch Kommandant oder Stellvertreter.</summary>
    public DateOnly? FreigegebenAm { get; set; }

    /// <summary>Wer freigegeben hat (Benutzer).</summary>
    public Guid? FreigegebenVon { get; set; }

    /// <summary>Übergabe an die Kämmerei.</summary>
    public DateOnly? EingereichtAm { get; set; }

    /// <summary>Beschluss des Gemeinderats über den Haushalt.</summary>
    public DateOnly? HaushaltBeschlossenAm { get; set; }

    /// <summary>Bemerkung, nur Sachstand.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Positionen.</summary>
    public List<Haushaltsposition> Positionen { get; set; } = [];

    /// <summary>Positionen, die an die Gemeinde gehen (ohne Vorschläge und intern gestrichene).</summary>
    public IEnumerable<Haushaltsposition> Angemeldet => Positionen.Where(p => p.Status == PositionStatus.Angemeldet);

    /// <summary>Positionen lassen sich bis zur Übergabe an die Kämmerei ändern.</summary>
    public bool Offen => Status is AnmeldungStatus.Entwurf or AnmeldungStatus.Beraten;

    /// <summary>Zulässige nächste Schritte.</summary>
    /// <param name="status">Aktueller Stand.</param>
    /// <returns>Nächste Stände.</returns>
    public static IReadOnlyList<AnmeldungStatus> Folgestatus(AnmeldungStatus status) => status switch
    {
        AnmeldungStatus.Entwurf => [AnmeldungStatus.Beraten],
        AnmeldungStatus.Beraten => [AnmeldungStatus.Eingereicht, AnmeldungStatus.Entwurf],
        AnmeldungStatus.Eingereicht => [AnmeldungStatus.ImHaushaltsentwurf],
        AnmeldungStatus.ImHaushaltsentwurf => [AnmeldungStatus.Beschlossen],
        _ => [],
    };

    /// <summary>Summe der angemeldeten Auszahlungen bzw. Aufwendungen im Planjahr je Haushaltsteil.</summary>
    /// <param name="teil">Haushaltsteil.</param>
    /// <returns>Summe brutto.</returns>
    public decimal Summe(Haushaltsteil teil) => Angemeldet.Where(p => p.Haushaltsstelle?.Teil == teil && p.Haushaltsstelle?.Einzahlung != true).Sum(p => p.BetragPlanjahr);

    /// <summary>Summe der erwarteten Zuwendungen (Einzahlungen) der angemeldeten Positionen.</summary>
    public decimal Zuwendungen => Angemeldet.Sum(p => p.Zuwendung ?? 0);
}

/// <summary>Eine Zeile der Haushaltsanmeldung: Aufwand oder Investitionsmaßnahme mit Raten.</summary>
public class Haushaltsposition : Entitaet
{
    /// <summary>Anmeldung.</summary>
    public Guid HaushaltsanmeldungId { get; set; }
    /// <summary>Navigation zur Anmeldung.</summary>
    public Haushaltsanmeldung? Haushaltsanmeldung { get; set; }

    /// <summary>Kostenträger: Haushaltsstelle (Pflicht).</summary>
    public Guid HaushaltsstelleId { get; set; }
    /// <summary>Navigation zur Haushaltsstelle.</summary>
    public Haushaltsstelle? Haushaltsstelle { get; set; }

    /// <summary>Bereich der Feuerwehr.</summary>
    public Haushaltsbereich Bereich { get; set; }

    /// <summary>Bezeichnung.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Menge.</summary>
    public decimal Menge { get; set; } = 1;

    /// <summary>Einzelpreis brutto.</summary>
    public decimal Einzelpreis { get; set; }

    /// <summary>Herkunft des Preises.</summary>
    public Preisquelle Preisquelle { get; set; }

    /// <summary>Dringlichkeit.</summary>
    public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;

    /// <summary>Pflichtaufgabe (Gesetz, Prüfpflicht) statt Wunsch.</summary>
    public bool Pflicht { get; set; }

    /// <summary>Begründung; ab der Wertgrenze Pflicht.</summary>
    public string? Begruendung { get; set; }

    /// <summary>Rechtsgrundlage, z. B. § 3 FwG, DGUV G 305-002, Maßnahme des Bedarfsplans.</summary>
    public string? Rechtsgrundlage { get; set; }

    /// <summary>Folgen bei Ablehnung.</summary>
    public string? FolgenBeiAblehnung { get; set; }

    /// <summary>Stand innerhalb der Feuerwehr.</summary>
    public PositionStatus Status { get; set; }

    /// <summary>Schlüssel des automatischen Vorschlags, damit er nicht doppelt kommt (z. B. „beschaffung:{id}“).</summary>
    public string? Herkunft { get; set; }

    /// <summary>Verknüpfte Beschaffung.</summary>
    public Guid? BeschaffungId { get; set; }

    /// <summary>Verknüpftes Fahrzeug.</summary>
    public Guid? FahrzeugId { get; set; }

    /// <summary>Verknüpfter Mangel.</summary>
    public Guid? MangelId { get; set; }

    /// <summary>Raten der Investition in den drei Folgejahren (§ 85 GemO); das Planjahr ist der Gesamtbetrag abzüglich dieser Raten.</summary>
    public decimal? RateFolgejahr1 { get; set; }

    /// <summary>Rate im zweiten Folgejahr.</summary>
    public decimal? RateFolgejahr2 { get; set; }

    /// <summary>Rate im dritten Folgejahr.</summary>
    public decimal? RateFolgejahr3 { get; set; }

    /// <summary>Verpflichtungsermächtigung (§ 86 GemO) in Euro.</summary>
    public decimal? Verpflichtungsermaechtigung { get; set; }

    /// <summary>Erwartete Zuwendung (z. B. Z-Feu-Festbetrag).</summary>
    public decimal? Zuwendung { get; set; }

    /// <summary>Jährliche Folgekosten der Investition.</summary>
    public decimal? Folgekosten { get; set; }

    /// <summary>Bezug zum Feuerwehrbedarfsplan, z. B. Maßnahme-Nr.</summary>
    public string? Bedarfsplan { get; set; }

    /// <summary>Rückmeldung der Gemeinde.</summary>
    public Haushaltsergebnis? Ergebnis { get; set; }

    /// <summary>Beschlossener Betrag im Planjahr.</summary>
    public decimal? BeschlossenerBetrag { get; set; }

    /// <summary>Tag der Rückmeldung.</summary>
    public DateOnly? RueckmeldungAm { get; set; }

    /// <summary>Gesamtbetrag brutto.</summary>
    public decimal Betrag => Math.Round(Menge * Einzelpreis, 2);

    /// <summary>Betrag im Planjahr: Gesamtbetrag abzüglich der Raten der Folgejahre.</summary>
    public decimal BetragPlanjahr => Betrag - (RateFolgejahr1 ?? 0) - (RateFolgejahr2 ?? 0) - (RateFolgejahr3 ?? 0);

    /// <summary>Ist die Position nach der Wertgrenze eine Investition (Einzelpreis netto ab der Grenze)?</summary>
    /// <param name="grenzeNetto">Wertgrenze netto.</param>
    /// <param name="steuersatzProzent">Umsatzsteuer-Regelsatz.</param>
    /// <returns>True bei Investition.</returns>
    public bool Investiv(decimal grenzeNetto, decimal steuersatzProzent) => Einzelpreis / (1 + steuersatzProzent / 100m) >= grenzeNetto;

    /// <summary>Was an der Position für die Anmeldung fehlt.</summary>
    /// <param name="grenzeNetto">Wertgrenze netto, ab der eine Begründung Pflicht ist.</param>
    /// <param name="steuersatzProzent">Umsatzsteuer-Regelsatz.</param>
    /// <returns>Fehlende Angaben.</returns>
    public IReadOnlyList<string> Fehlt(decimal grenzeNetto, decimal steuersatzProzent)
    {
        var fehlt = new List<string>();
        if (Betrag <= 0)
        {
            fehlt.Add("Betrag");
        }

        if (Investiv(grenzeNetto, steuersatzProzent) || Betrag / (1 + steuersatzProzent / 100m) >= grenzeNetto)
        {
            if (string.IsNullOrWhiteSpace(Begruendung))
            {
                fehlt.Add("Begründung");
            }

            if (string.IsNullOrWhiteSpace(Rechtsgrundlage))
            {
                fehlt.Add("Rechtsgrundlage");
            }
        }

        if (BetragPlanjahr < 0)
        {
            fehlt.Add("Raten größer als der Gesamtbetrag");
        }

        return fehlt;
    }
}

/// <summary>
/// Kostenansatz für Vorschlagszeilen (Katalog): Satz je Leistung der Werkstatt Göppingen, je Fremdprüfung einer Geräteart oder je Fahrzeug.
/// Ein Satz 0 heißt „Satz fehlt“ und erscheint so in der Vorschlagszeile.
/// </summary>
public class Kostenansatz : Katalogeintrag
{
    /// <summary>Bezugsgröße, aus der FEVAD die Menge zählt.</summary>
    public Kostenbezug Bezug { get; set; }

    /// <summary>Code der Geräteart bei Fremdprüfungen.</summary>
    public string? GeraeteartCode { get; set; }

    /// <summary>Satz brutto je Einheit.</summary>
    public decimal Satz { get; set; }

    /// <summary>Haushaltsstelle (Code), auf die die Vorschlagszeile geht.</summary>
    public required string HaushaltsstelleCode { get; set; }

    /// <summary>Quelle des Satzes.</summary>
    public string? Quelle { get; set; }
}

/// <summary>Woraus FEVAD die Menge einer Vorschlagszeile zählt.</summary>
public enum Kostenbezug
{
    /// <summary>Werkstattpositionen der Schlauchwerkstatt im Vorjahr.</summary>
    [Display(Name = "Schläuche in der Schlauchwerkstatt (Vorjahr)")] Schlauchwerkstatt,
    /// <summary>Werkstattpositionen der Atemschutzwerkstatt im Vorjahr.</summary>
    [Display(Name = "Geräte in der Atemschutzwerkstatt (Vorjahr)")] Atemschutzwerkstatt,
    /// <summary>Rückläufe aus der Atemschutzwerkstatt mit Füllung im Vorjahr.</summary>
    [Display(Name = "Flaschenfüllungen (Vorjahr)")] Flaschenfuellung,
    /// <summary>Prüfungen der Geräteart durch Fremde im Vorjahr.</summary>
    [Display(Name = "Fremdprüfungen der Geräteart (Vorjahr)")] Fremdpruefung,
    /// <summary>Je Fahrzeug im Dienst (Versicherung, Steuer).</summary>
    [Display(Name = "je Fahrzeug im Dienst")] JeFahrzeug,
    /// <summary>Wäschen der Einsatzkleidung im Vorjahr.</summary>
    [Display(Name = "Wäschen der Einsatzkleidung (Vorjahr)")] Waesche,
}

/// <summary>
/// Stand der Gemeindebuchhaltung (SAP) zu einem Sachkonto und einer Kostenstelle bzw. einem Innenauftrag (V29). Die Kämmerei liefert ihn als CSV;
/// ein neuer Import ersetzt den Stand des Jahres.
/// </summary>
public class HaushaltIst : Entitaet
{
    /// <summary>Haushaltsjahr.</summary>
    public int Jahr { get; set; }

    /// <summary>Sachkonto.</summary>
    public required string Sachkonto { get; set; }

    /// <summary>Kostenstelle.</summary>
    public string? Kostenstelle { get; set; }

    /// <summary>Innenauftrag.</summary>
    public string? Innenauftrag { get; set; }

    /// <summary>Gebuchtes Ist (Aufwand bzw. Auszahlung, bei Einzahlungsstellen die Einzahlung).</summary>
    public decimal Ist { get; set; }

    /// <summary>Obligo: in SAP erfasste Bestellungen, noch nicht bezahlt.</summary>
    public decimal Obligo { get; set; }

    /// <summary>Buchungsstand laut Kämmerei.</summary>
    public DateOnly Stand { get; set; }
}

/// <summary>Unterjähriger Stand einer Haushaltsstelle: Ansatz, Bindung, Ist und freie Mittel (V29).</summary>
/// <param name="Stelle">Haushaltsstelle.</param>
/// <param name="Ansatz">Beschlossener Ansatz; vor dem Beschluss der angemeldete Betrag.</param>
/// <param name="Vorlaeufig">Der Ansatz ist noch nicht beschlossen.</param>
/// <param name="GebundenFevad">In FEVAD bestellte, noch nicht gelieferte Beschaffungen.</param>
/// <param name="Obligo">Obligo laut SAP.</param>
/// <param name="Ist">Ist laut SAP.</param>
public sealed record Haushaltsstand(Haushaltsstelle Stelle, decimal Ansatz, bool Vorlaeufig, decimal GebundenFevad, decimal Obligo, decimal Ist)
{
    /// <summary>Gebunden: das Obligo der Kämmerei, solange es die Bestellungen in FEVAD abdeckt, sonst die Bestellungen in FEVAD.</summary>
    public decimal Gebunden => Math.Max(GebundenFevad, Obligo);

    /// <summary>Frei verfügbare Mittel.</summary>
    public decimal Frei => Ansatz - Ist - Gebunden;

    /// <summary>Ausgeschöpfter Anteil in Prozent; null ohne Ansatz.</summary>
    public decimal? Auslastung => Ansatz == 0 ? null : Math.Round((Ist + Gebunden) / Ansatz * 100, 0);

    /// <summary>Mehr gebunden oder ausgegeben als angesetzt (bei Einzahlungsstellen nie).</summary>
    public bool Ueberschritten => !Stelle.Einzahlung && Frei < 0;
}

/// <summary>Berechnung der unterjährigen Haushaltsüberwachung (V29).</summary>
public static class Haushaltsueberwachung
{
    /// <summary>Betrag einer Position, der im Haushaltsjahr zur Verfügung steht: nach dem Beschluss laut Rückmeldung, vorher der angemeldete Betrag.</summary>
    /// <param name="position">Angemeldete Position.</param>
    /// <returns>Ansatz der Position im Planjahr.</returns>
    public static decimal Ansatz(Haushaltsposition position) => position.Ergebnis switch
    {
        Haushaltsergebnis.Gekuerzt => position.BeschlossenerBetrag ?? position.BetragPlanjahr,
        Haushaltsergebnis.Gestrichen or Haushaltsergebnis.Verschoben => 0,
        _ => position.BetragPlanjahr,
    };

    /// <summary>Nummer eines Innenauftrags ohne Zusätze, z. B. „126010“ aus „IA 126010 Atemschutz“.</summary>
    /// <param name="innenauftrag">Innenauftrag wie gepflegt oder geliefert.</param>
    /// <returns>Erste Ziffernfolge oder der getrimmte Text; null bei leerem Wert.</returns>
    public static string? Auftragsnummer(string? innenauftrag)
    {
        if (string.IsNullOrWhiteSpace(innenauftrag))
        {
            return null;
        }

        var ziffern = new string(innenauftrag.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return ziffern.Length > 0 ? ziffern : innenauftrag.Trim();
    }

    /// <summary>
    /// Gehört eine Ist-Zeile zur Haushaltsstelle? Gleiches Sachkonto; hat die Stelle einen Innenauftrag, entscheidet dieser, sonst die Kostenstelle
    /// bei Zeilen ohne Innenauftrag.
    /// </summary>
    /// <param name="stelle">Haushaltsstelle.</param>
    /// <param name="ist">Ist-Zeile.</param>
    /// <returns>Wahr, wenn die Zeile zur Stelle gehört.</returns>
    public static bool Passt(Haushaltsstelle stelle, HaushaltIst ist)
    {
        if (!string.Equals(stelle.Sachkonto.Trim(), ist.Sachkonto.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Auftragsnummer(stelle.Innenauftrag) is { } auftrag)
        {
            return Auftragsnummer(ist.Innenauftrag) == auftrag;
        }

        return Auftragsnummer(ist.Innenauftrag) is null
            && (string.IsNullOrWhiteSpace(ist.Kostenstelle) || string.Equals(stelle.Kostenstelle.Trim(), ist.Kostenstelle.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Stand je Haushaltsstelle.</summary>
    /// <param name="stellen">Alle Haushaltsstellen.</param>
    /// <param name="anmeldung">Anmeldung des Jahres oder null.</param>
    /// <param name="gebunden">Bestellte, nicht gelieferte Beschaffungen je Haushaltsstelle.</param>
    /// <param name="ist">Ist-Zeilen des Jahres.</param>
    /// <returns>Stände der Stellen mit Ansatz, Bindung oder Ist, in der Sortierung der Stellen.</returns>
    public static IReadOnlyList<Haushaltsstand> Berechnen(IEnumerable<Haushaltsstelle> stellen, Haushaltsanmeldung? anmeldung,
        IReadOnlyDictionary<Guid, decimal> gebunden, IReadOnlyList<HaushaltIst> ist)
    {
        var vorlaeufig = anmeldung?.Status != AnmeldungStatus.Beschlossen;
        var positionen = anmeldung?.Angemeldet.ToList() ?? [];
        var ergebnis = new List<Haushaltsstand>();
        foreach (var stelle in stellen.OrderBy(s => s.Sortierung).ThenBy(s => s.Code))
        {
            var ansatz = positionen.Where(p => p.HaushaltsstelleId == stelle.Id).Sum(Ansatz);
            var zeilen = ist.Where(i => Passt(stelle, i)).ToList();
            var stand = new Haushaltsstand(stelle, ansatz, vorlaeufig, gebunden.GetValueOrDefault(stelle.Id), zeilen.Sum(i => i.Obligo), zeilen.Sum(i => i.Ist));
            if (stand.Ansatz != 0 || stand.Gebunden != 0 || stand.Ist != 0)
            {
                ergebnis.Add(stand);
            }
        }

        return ergebnis;
    }

    /// <summary>Ist-Zeilen, die zu keiner Haushaltsstelle passen.</summary>
    /// <param name="stellen">Alle Haushaltsstellen.</param>
    /// <param name="ist">Ist-Zeilen.</param>
    /// <returns>Nicht zugeordnete Zeilen.</returns>
    public static IReadOnlyList<HaushaltIst> NichtZugeordnet(IReadOnlyList<Haushaltsstelle> stellen, IEnumerable<HaushaltIst> ist) =>
        ist.Where(i => !stellen.Any(s => Passt(s, i))).ToList();
}
