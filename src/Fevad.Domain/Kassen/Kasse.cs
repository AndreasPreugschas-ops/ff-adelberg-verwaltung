using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Kassen;

/// <summary>Rechtsform einer Kasse.</summary>
public enum KassenRechtsform
{
    /// <summary>Sondervermögen der Gemeinde nach § 18 FwG, z. B. Kameradschaftskasse oder Kasse der Jugendfeuerwehr.</summary>
    [Display(Name = "Sondervermögen der Gemeinde (§ 18 FwG)")] SondervermoegenFwG,

    /// <summary>Eingetragener Verein; nur im Modell vorgesehen, in Adelberg gibt es keinen Förderverein (Andi, 25.10.2026).</summary>
    [Display(Name = "Verein")] Verein,
}

/// <summary>
/// Eine Kasse der Feuerwehr (V01). Die Kameradschaftskasse und die Kasse der Jugendfeuerwehr sind je ein Sondervermögen der Gemeinde
/// (§ 18 FwG) mit eigenen Konten, eigenem Wirtschaftsplan und eigener Belegnummernfolge. Der Kassenwart führt beide und verfügt allein;
/// Ausgaben über der Grenze aus dem Parameter <see cref="ParameterSchluessel.KasseAusschussbeschlussUeberEuro"/> brauchen einen
/// Ausschussbeschluss, FEVAD weist nur darauf hin.
/// </summary>
public class Kasse : Entitaet
{
    /// <summary>Name der Kasse, z. B. „Kameradschaftskasse“.</summary>
    public required string Name { get; set; }

    /// <summary>Rechtsform; in Adelberg immer Sondervermögen.</summary>
    public KassenRechtsform Rechtsform { get; set; }

    /// <summary>
    /// Abteilung, der die Kasse gehört, z. B. die Jugendfeuerwehr; leer für die Kasse der Gesamtwehr. Rechte mit Abteilungsbezug
    /// (Jugendwart nur für die Jugendfeuerwehr) gelten nur für Kassen ihrer Abteilungen; Kassen ohne Abteilung brauchen das Recht
    /// für alle Abteilungen.
    /// </summary>
    public Guid? AbteilungId { get; set; }

    /// <summary>Die Abteilung der Kasse.</summary>
    public Abteilung? Abteilung { get; set; }

    /// <summary>Abgeschaltete Kassen erscheinen nur noch zum Nachlesen.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Reihenfolge in Listen.</summary>
    public int Sortierung { get; set; }

    /// <summary>Freitext, z. B. Grundlage der Einrichtung (Ausschussbeschluss).</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Konten der Kasse (Girokonto, Barkasse, Rücklagen).</summary>
    public List<Zahlungsmittelkonto> Konten { get; set; } = [];

    /// <summary>Gliederung des Wirtschaftsplans (Kontenrahmen) der Kasse.</summary>
    public List<Planposition> Positionen { get; set; } = [];
}

/// <summary>Art eines Kontos der Kasse.</summary>
public enum KontoArt
{
    /// <summary>Girokonto bei einer Bank; Kontoinhaber ist die Gemeinde.</summary>
    [Display(Name = "Girokonto")] Girokonto,

    /// <summary>Bargeld, z. B. Handkasse oder Festkasse.</summary>
    [Display(Name = "Barkasse")] Barkasse,

    /// <summary>Rücklage als Tagesgeld, Festgeld oder Sparbuch.</summary>
    [Display(Name = "Rücklage")] Ruecklage,
}

/// <summary>Zahlungsmittelkonto einer Kasse mit Anfangsbestand ab dem Tag, an dem FEVAD es führt.</summary>
public class Zahlungsmittelkonto : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Bezeichnung, z. B. „Girokonto Kreissparkasse“ oder „Barkasse“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Art des Kontos.</summary>
    public KontoArt Art { get; set; }

    /// <summary>IBAN des Kontos der Kasse (kein Personenbezug, Kontoinhaber ist die Gemeinde).</summary>
    public string? Iban { get; set; }

    /// <summary>Name der Bank.</summary>
    public string? Bank { get; set; }

    /// <summary>Ab diesem Tag führt FEVAD das Konto; Buchungen davor sind nicht möglich.</summary>
    public DateOnly Eroeffnet { get; set; }

    /// <summary>Bestand in Euro zu Beginn des Tages <see cref="Eroeffnet"/>.</summary>
    public decimal Anfangsbestand { get; set; }

    /// <summary>Aufgelöste Konten bleiben für alte Buchungen erhalten, stehen aber für neue nicht mehr zur Wahl.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Reihenfolge in Listen.</summary>
    public int Sortierung { get; set; }
}

/// <summary>Einnahme oder Ausgabe; bestimmt, auf welcher Seite des Wirtschaftsplans eine Position steht.</summary>
public enum Planrichtung
{
    /// <summary>Einnahmen (LFV-Gliederung 0 bis 4).</summary>
    [Display(Name = "Einnahme")] Einnahme,

    /// <summary>Ausgaben (LFV-Gliederung 5 bis 9).</summary>
    [Display(Name = "Ausgabe")] Ausgabe,
}

/// <summary>
/// Position der Gliederung einer Kasse nach dem Muster der LFV-Handreichung „Kameradschaftskassen“ (2026), z. B. „1.1 Zuwendung der
/// Gemeinde“. Die Positionen gelten für alle Jahre; die Beträge je Jahr stehen im <see cref="Planansatz"/>.
/// </summary>
public class Planposition : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Nummer in der Gliederung, z. B. „1.1“ oder „6.2“; die erste Ziffer ist die Hauptgruppe.</summary>
    public required string Nummer { get; set; }

    /// <summary>Bezeichnung, z. B. „Kameradschaftspflege“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Einnahme- oder Ausgabeseite.</summary>
    public Planrichtung Richtung { get; set; }

    /// <summary>Gegenseitig deckungsfähig mit den übrigen deckungsfähigen Positionen derselben Hauptgruppe.</summary>
    public bool Deckungsfaehig { get; set; }

    /// <summary>Hinweis beim Buchen, z. B. dass Fahrzeuge, Geräte und PSA in den Gemeindehaushalt gehören.</summary>
    public string? Hinweis { get; set; }

    /// <summary>Nicht mehr verwendete Positionen bleiben für alte Jahre erhalten, stehen beim Buchen aber nicht mehr zur Wahl.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Hauptgruppe aus der ersten Ziffer der Nummer, z. B. 6 für „6.2“; -1, wenn die Nummer nicht mit einer Ziffer beginnt.</summary>
    public int Hauptgruppe => Nummer.Length > 0 && char.IsAsciiDigit(Nummer[0]) ? Nummer[0] - '0' : -1;
}

/// <summary>Stand eines Wirtschaftsplans.</summary>
public enum WirtschaftsplanStatus
{
    /// <summary>Vom Kassenwart aufgestellt, Ansätze änderbar.</summary>
    [Display(Name = "Entwurf")] Entwurf,

    /// <summary>Vom Feuerwehrausschuss beschlossen; wartet auf die Zustimmung des Bürgermeisters.</summary>
    [Display(Name = "vom Ausschuss beschlossen")] Beschlossen,

    /// <summary>Der Bürgermeister hat zugestimmt; der Plan gilt.</summary>
    [Display(Name = "Bürgermeister hat zugestimmt")] Zugestimmt,

    /// <summary>Der Bürgermeister hat die Zustimmung verweigert; der Plan muss überarbeitet werden.</summary>
    [Display(Name = "Zustimmung verweigert")] Verweigert,
}

/// <summary>
/// Wirtschaftsplan einer Kasse für ein Kalenderjahr (Satzung § 16): Kassenwart stellt auf, der Ausschuss beschließt, der Bürgermeister
/// stimmt zu. Ein Nachtrag setzt den Plan auf Entwurf zurück; das Änderungsprotokoll hält die alten Ansätze fest.
/// </summary>
public class Wirtschaftsplan : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Haushaltsjahr (Kalenderjahr).</summary>
    public int Jahr { get; set; }

    /// <summary>Stand des Plans.</summary>
    public WirtschaftsplanStatus Status { get; set; }

    /// <summary>Beschluss des Feuerwehrausschusses aus dem Gremienmodul.</summary>
    public Guid? BeschlussId { get; set; }

    /// <summary>Der Beschluss des Ausschusses.</summary>
    public Beschluss? Beschluss { get; set; }

    /// <summary>Tag des Ausschussbeschlusses.</summary>
    public DateOnly? BeschlossenAm { get; set; }

    /// <summary>Tag der Zustimmung oder ihrer Verweigerung durch den Bürgermeister.</summary>
    public DateOnly? ZustimmungAm { get; set; }

    /// <summary>Freitext, z. B. Aktenzeichen des Zustimmungsschreibens oder Gründe einer Verweigerung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Wie oft der Plan nach einem Beschluss wieder geöffnet wurde (Nachtrag); 0 für den ursprünglichen Plan.</summary>
    public int Nachtrag { get; set; }

    /// <summary>Ansätze je Position.</summary>
    public List<Planansatz> Ansaetze { get; set; } = [];

    /// <summary>Ansätze lassen sich nur im Entwurf oder nach verweigerter Zustimmung ändern.</summary>
    public bool Aenderbar => Status is WirtschaftsplanStatus.Entwurf or WirtschaftsplanStatus.Verweigert;
}

/// <summary>Geplanter Betrag einer Position in einem Wirtschaftsplan.</summary>
public class Planansatz : Entitaet
{
    /// <summary>Schlüssel des Wirtschaftsplans.</summary>
    public Guid WirtschaftsplanId { get; set; }

    /// <summary>Der Wirtschaftsplan.</summary>
    public Wirtschaftsplan? Wirtschaftsplan { get; set; }

    /// <summary>Schlüssel der Position.</summary>
    public Guid PlanpositionId { get; set; }

    /// <summary>Die Position.</summary>
    public Planposition? Planposition { get; set; }

    /// <summary>Geplanter Betrag in Euro.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Erläuterung, z. B. „Jahresausflug 40 Personen × 25 €“.</summary>
    public string? Erlaeuterung { get; set; }
}
