using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Kassen;

/// <summary>Art einer Buchung im Kassenbuch.</summary>
public enum BuchungArt
{
    /// <summary>Geld geht auf einem Konto der Kasse ein.</summary>
    [Display(Name = "Einnahme")] Einnahme,

    /// <summary>Geld geht von einem Konto der Kasse ab.</summary>
    [Display(Name = "Ausgabe")] Ausgabe,

    /// <summary>Geld wechselt zwischen zwei Konten derselben Kasse, z. B. Bareinzahlung oder Zuführung an die Rücklage.</summary>
    [Display(Name = "Umbuchung")] Umbuchung,
}

/// <summary>Art des Belegs einer Buchung.</summary>
public enum Belegart
{
    /// <summary>Rechnung eines Lieferanten.</summary>
    [Display(Name = "Rechnung")] Rechnung,

    /// <summary>Quittung, Kassenbon.</summary>
    [Display(Name = "Quittung oder Kassenbon")] Quittung,

    /// <summary>Kontoauszug, z. B. für Zinsen, Gebühren oder die Zuwendung der Gemeinde.</summary>
    [Display(Name = "Kontoauszug")] Kontoauszug,

    /// <summary>Bescheid, Vertrag oder Schreiben, z. B. Zuwendungsbescheid der Gemeinde.</summary>
    [Display(Name = "Bescheid, Vertrag oder Schreiben")] Schreiben,

    /// <summary>Kassenbericht oder Zählprotokoll einer Veranstaltung.</summary>
    [Display(Name = "Kassenbericht oder Zählprotokoll")] Kassenbericht,

    /// <summary>Selbst ausgestellter Beleg, weil kein fremder vorliegt; nur mit Begründung.</summary>
    [Display(Name = "Eigenbeleg (mit Begründung)")] Eigenbeleg,
}

/// <summary>
/// Buchung im Kassenbuch einer Kasse (V02). Die Belegnummer läuft je Kasse und Jahr fortlaufend. Solange eine Buchung nicht
/// festgeschrieben ist, lässt sie sich ändern; danach nur noch durch eine Stornobuchung korrigieren (GoBD). Festschreiben geht nur
/// mit Beleg: hochgeladene Datei oder Eigenbeleg mit Begründung. Aufbewahrung zehn Jahre nach Ende des Jahres (§ 147 AO).
/// </summary>
public class Buchung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Jahr der Belegnummer, immer das Jahr von <see cref="Datum"/>.</summary>
    public int Jahr { get; set; }

    /// <summary>Fortlaufende Belegnummer je Kasse und Jahr, beginnend bei 1.</summary>
    public int Belegnummer { get; set; }

    /// <summary>Tag der Zahlung (Wertstellung oder Barzahlung).</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Einnahme, Ausgabe oder Umbuchung.</summary>
    public BuchungArt Art { get; set; }

    /// <summary>Betrag in Euro, immer positiv; die Richtung folgt aus <see cref="Art"/>, ein Storno kehrt sie um.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Konto der Kasse; bei einer Umbuchung das abgebende Konto.</summary>
    public Guid KontoId { get; set; }

    /// <summary>Das Konto.</summary>
    public Zahlungsmittelkonto? Konto { get; set; }

    /// <summary>Bei einer Umbuchung das empfangende Konto.</summary>
    public Guid? GegenkontoId { get; set; }

    /// <summary>Das Gegenkonto.</summary>
    public Zahlungsmittelkonto? Gegenkonto { get; set; }

    /// <summary>Position des Wirtschaftsplans; Pflicht bei Einnahmen und Ausgaben, bei Umbuchungen nur für Rücklagen (Positionen 0 und 9).</summary>
    public Guid? PlanpositionId { get; set; }

    /// <summary>Die Position.</summary>
    public Planposition? Planposition { get; set; }

    /// <summary>Steuerlicher Bereich (V03); Vorschlag aus der Position.</summary>
    public Steuerbereich Steuerbereich { get; set; }

    /// <summary>Umsatzsteuersatz; nur im wirtschaftlichen Bereich von Bedeutung.</summary>
    public Umsatzsteuersatz Steuersatz { get; set; }

    /// <summary>Veranstaltung als Kostenträger, z. B. Hocketse.</summary>
    public Guid? VeranstaltungId { get; set; }

    /// <summary>Die Veranstaltung.</summary>
    public Veranstaltung? Veranstaltung { get; set; }

    /// <summary>Zahlungspartner, z. B. Lieferant, Gemeinde oder Mitglied.</summary>
    public string? Partner { get; set; }

    /// <summary>Verwendungszweck, z. B. „Getränke Hauptübung“.</summary>
    public required string Text { get; set; }

    /// <summary>Art des Belegs.</summary>
    public Belegart Belegart { get; set; }

    /// <summary>Begründung bei Eigenbeleg, z. B. „Parkautomat ohne Quittung“.</summary>
    public string? EigenbelegBegruendung { get; set; }

    /// <summary>Hochgeladene Belege (PDF oder Bild).</summary>
    public List<Kassenbeleg> Belege { get; set; } = [];

    /// <summary>Ausschussbeschluss zu dieser Ausgabe aus dem Gremienmodul (über der Beschlussgrenze; nie Pflicht).</summary>
    public Guid? AusschussbeschlussId { get; set; }

    /// <summary>Der Ausschussbeschluss.</summary>
    public Beschluss? Ausschussbeschluss { get; set; }

    /// <summary>Verweis auf einen Beschluss ohne Eintrag im Gremienmodul, z. B. „Ausschuss 12.03.2026, TOP 4“.</summary>
    public string? Beschlussverweis { get; set; }

    /// <summary>Zeitpunkt der Festschreibung in UTC; leer, solange die Buchung änderbar ist.</summary>
    public DateTimeOffset? FestgeschriebenAm { get; set; }

    /// <summary>Konto, das festgeschrieben hat.</summary>
    public Guid? FestgeschriebenVon { get; set; }

    /// <summary>Bei einer Stornobuchung die stornierte Buchung.</summary>
    public Guid? StornoVonId { get; set; }

    /// <summary>Die stornierte Buchung.</summary>
    public Buchung? StornoVon { get; set; }

    /// <summary>Vorbereitete Ausgabe, aus der die Buchung entstand.</summary>
    public Guid? ZahlungsanordnungId { get; set; }

    /// <summary>
    /// Begründung einer Ausnahmebuchung (V05): Buchungen in einem abgeschlossenen Jahr sind nur mit Begründung möglich und erscheinen
    /// im Jahresabschluss und bei der Kassenprüfung gesondert.
    /// </summary>
    public string? AusnahmeBegruendung { get; set; }

    /// <summary>Die Ausgabe ist keine Anschaffung für das Bestandsverzeichnis, obwohl sie über der Wertgrenze liegt (V04).</summary>
    public bool KeinGegenstand { get; set; }

    /// <summary>Die Buchung ist festgeschrieben und nicht mehr änderbar.</summary>
    public bool Festgeschrieben => FestgeschriebenAm is not null;

    /// <summary>Kennung „Jahr-Belegnummer“, z. B. „2026-017“.</summary>
    public string Kennung => $"{Jahr}-{Belegnummer:000}";
}

/// <summary>Datei eines Belegs (PDF, PNG oder JPEG); Bilder ohne Metadaten. Inhalt nicht im Änderungsprotokoll.</summary>
public class Kassenbeleg : Entitaet
{
    /// <summary>Buchung, zu der der Beleg gehört.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Vorbereitete Ausgabe, zu der der Beleg gehört, solange sie nicht gebucht ist.</summary>
    public Guid? ZahlungsanordnungId { get; set; }

    /// <summary>Bereinigter Dateiname.</summary>
    public required string Dateiname { get; set; }

    /// <summary>MIME-Typ nach Dateiendung.</summary>
    public required string Inhaltstyp { get; set; }

    /// <summary>Inhalt.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }

    /// <summary>SHA-256 des Inhalts als Hex-Text; zeigt dem Prüfer, dass der Beleg seit dem Hochladen unverändert ist.</summary>
    public required string Pruefsumme { get; set; }
}

/// <summary>Stand einer vorbereiteten Ausgabe.</summary>
public enum AnordnungStatus
{
    /// <summary>Vorbereitet, z. B. vom Jugendwart; wartet auf den Kassenwart.</summary>
    [Display(Name = "vorbereitet")] Vorbereitet,

    /// <summary>Gezahlt und im Kassenbuch gebucht.</summary>
    [Display(Name = "gezahlt")] Gezahlt,

    /// <summary>Vom Kassenwart abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
}

/// <summary>Art einer vorbereiteten Ausgabe.</summary>
public enum AnordnungArt
{
    /// <summary>Ausgabe an einen Lieferanten oder Dritten.</summary>
    [Display(Name = "Ausgabe")] Ausgabe,

    /// <summary>Ein Mitglied hat Geld vorgestreckt und reicht den Beleg zur Erstattung ein (V07).</summary>
    [Display(Name = "Auslagenerstattung")] Auslagenerstattung,
}

/// <summary>
/// Vorbereitete Ausgabe (V02, V06): Jugendwart, Kommandant oder Kassenwart halten fest, was zu zahlen ist, mit Beleg. Der Kassenwart
/// zahlt und bucht allein (Andi, 25.10.2026). Kommandant oder Stellvertreter können eine Anordnung zur Dokumentation vermerken; Pflicht
/// ist sie nicht und sie sperrt nichts.
/// </summary>
public class Zahlungsanordnung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Betrag in Euro.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Ausgabe oder Auslagenerstattung.</summary>
    public AnordnungArt Art { get; set; }

    /// <summary>Bei einer Auslagenerstattung das Mitglied, das ausgelegt hat; es sieht seine Erstattungen immer.</summary>
    public Guid? MitgliedId { get; set; }

    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Empfänger, z. B. Lieferant oder Mitglied.</summary>
    public required string Empfaenger { get; set; }

    /// <summary>Zweck der Zahlung.</summary>
    public required string Zweck { get; set; }

    /// <summary>Vorgeschlagene Position des Wirtschaftsplans.</summary>
    public Guid? PlanpositionId { get; set; }

    /// <summary>Die Position.</summary>
    public Planposition? Planposition { get; set; }

    /// <summary>Zahlen bis.</summary>
    public DateOnly? FaelligAm { get; set; }

    /// <summary>Stand.</summary>
    public AnordnungStatus Status { get; set; }

    /// <summary>Ausschussbeschluss zur Ausgabe, falls vorhanden.</summary>
    public Guid? AusschussbeschlussId { get; set; }

    /// <summary>Der Ausschussbeschluss.</summary>
    public Beschluss? Ausschussbeschluss { get; set; }

    /// <summary>Zeitpunkt, zu dem Kommandant oder Stellvertreter die Anordnung vermerkt haben (nur Dokumentation).</summary>
    public DateTimeOffset? AngeordnetAm { get; set; }

    /// <summary>Konto, das die Anordnung vermerkt hat.</summary>
    public Guid? AngeordnetVon { get; set; }

    /// <summary>Buchung, mit der die Ausgabe gezahlt wurde.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Freitext, z. B. Grund einer Ablehnung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Belege zur Ausgabe; beim Buchen gehen sie auf die Buchung über.</summary>
    public List<Kassenbeleg> Belege { get; set; } = [];
}
