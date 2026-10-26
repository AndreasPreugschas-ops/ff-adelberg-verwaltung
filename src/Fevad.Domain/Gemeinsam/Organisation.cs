using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Anschriften der Feuerwehr und ihrer Trägerin, der Gemeinde, dazu das Logo für die Formulare. Es gibt genau einen
/// Datensatz; der Start legt ihn an. Die Gemeinde ist Verantwortliche im Sinne der DSGVO.
/// </summary>
public class Organisation : Entitaet
{
    /// <summary>Name der Feuerwehr für Formulare und Ausdrucke, z. B. „Freiwillige Feuerwehr Adelberg“.</summary>
    public string FeuerwehrName { get; set; } = "Freiwillige Feuerwehr";
    /// <summary>Straße und Hausnummer der Feuerwehr (Feuerwehrhaus).</summary>
    public string? FeuerwehrStrasse { get; set; }
    /// <summary>Postleitzahl der Feuerwehr.</summary>
    public string? FeuerwehrPlz { get; set; }
    /// <summary>Ort der Feuerwehr.</summary>
    public string? FeuerwehrOrt { get; set; }
    /// <summary>Telefonnummer der Feuerwehr für Formulare.</summary>
    public string? FeuerwehrTelefon { get; set; }
    /// <summary>E-Mail-Adresse der Feuerwehr für Formulare.</summary>
    public string? FeuerwehrEmail { get; set; }

    /// <summary>Name der Trägergemeinde, z. B. „Gemeinde Adelberg“; Verantwortliche im Sinne der DSGVO.</summary>
    public string GemeindeName { get; set; } = "Gemeinde";

    /// <summary>Name des Bürgermeisters für Einladungen und Anschreiben (F20).</summary>
    public string? BuergermeisterName { get; set; }
    /// <summary>E-Mail-Adresse, an die Einladungen und Niederschriften für den Bürgermeister gehen (F20).</summary>
    public string? BuergermeisterEmail { get; set; }

    /// <summary>Vertretung der Gemeinde, z. B. „vertreten durch den Bürgermeister“.</summary>
    public string? GemeindeVertretung { get; set; }
    /// <summary>Straße und Hausnummer der Gemeindeverwaltung.</summary>
    public string? GemeindeStrasse { get; set; }
    /// <summary>Postleitzahl der Gemeindeverwaltung.</summary>
    public string? GemeindePlz { get; set; }
    /// <summary>Ort der Gemeindeverwaltung.</summary>
    public string? GemeindeOrt { get; set; }
    /// <summary>Telefonnummer der Gemeindeverwaltung.</summary>
    public string? GemeindeTelefon { get; set; }
    /// <summary>E-Mail-Adresse der Gemeindeverwaltung.</summary>
    public string? GemeindeEmail { get; set; }

    /// <summary>Logo als SVG, PNG oder JPEG; erscheint im Kopf der Formulare.</summary>
    [NichtProtokollieren]
    public byte[]? Logo { get; set; }
    /// <summary>MIME-Typ des Logos, z. B. „image/png“; nötig, um es korrekt auszuliefern.</summary>
    public string? LogoInhaltstyp { get; set; }

    // Umsatzsteuer: Steuerpflichtig ist die Gemeinde als juristische Person des öffentlichen Rechts (§ 2b UStG). Die
    // Kameradschaftskassen sind Sondervermögen der Gemeinde (§ 18 FwG) und haben keine eigene Steuernummer; ihre Umsätze
    // meldet die Kämmerei unter der Steuernummer der Gemeinde. Grenzen und Steuersätze stehen als Parameter, nicht hier.

    /// <summary>Steuernummer der Gemeinde beim Finanzamt, z. B. „62/123/45678“.</summary>
    public string? Steuernummer { get; set; }

    /// <summary>Umsatzsteuer-Identifikationsnummer (§ 27a UStG), z. B. „DE123456789“; braucht die Gemeinde für Rechnungen ins EU-Ausland.</summary>
    public string? UmsatzsteuerId { get; set; }

    /// <summary>Zuständiges Finanzamt der Gemeinde.</summary>
    public string? Finanzamt { get; set; }

    /// <summary>
    /// Ab diesem Tag gilt § 2b UStG für die Gemeinde (Ende der Übergangsregelung § 27 Abs. 22a UStG; Angabe Kämmerei Adelberg:
    /// 01.01.2027). Vorher sind die Umsätze nach altem Recht (§ 2 Abs. 3 UStG a. F.) nur im Betrieb gewerblicher Art steuerbar.
    /// </summary>
    public DateOnly? UmsatzsteuerParagraph2bAb { get; set; }

    /// <summary>Die Gemeinde nutzt die Kleinunternehmerregelung (§ 19 UStG); dann weist keine Rechnung Umsatzsteuer aus.</summary>
    public bool Kleinunternehmer { get; set; }

    /// <summary>Soll- oder Istversteuerung (§§ 13, 20 UStG); bestimmt, ob die Steuer mit der Rechnung oder mit dem Zahlungseingang entsteht.</summary>
    public Versteuerungsart? Versteuerungsart { get; set; }

    /// <summary>Wie oft die Gemeinde Umsatzsteuer-Voranmeldungen abgibt; leer, solange die Kämmerei es nicht angegeben hat.</summary>
    public Voranmeldungszeitraum? Voranmeldungszeitraum { get; set; }

    /// <summary>Hinweis der Kämmerei, z. B. wer in der Verwaltung die Meldungen der Kameradschaftskasse annimmt und bis wann.</summary>
    public string? UmsatzsteuerHinweis { get; set; }

    /// <summary>Anschrift der Feuerwehr als eine Zeile; null, solange Straße oder Ort fehlen.</summary>
    public string? FeuerwehrAnschrift => Anschrift.Zeile(FeuerwehrStrasse, FeuerwehrPlz, FeuerwehrOrt);
    /// <summary>Anschrift der Gemeinde als eine Zeile; null, solange Straße oder Ort fehlen.</summary>
    public string? GemeindeAnschrift => Anschrift.Zeile(GemeindeStrasse, GemeindePlz, GemeindeOrt);
}

/// <summary>Wann die Umsatzsteuer entsteht (§ 13 Abs. 1 Nr. 1 UStG).</summary>
public enum Versteuerungsart
{
    /// <summary>Nach vereinbarten Entgelten: mit Ablauf des Monats der Leistung.</summary>
    [Display(Name = "Sollversteuerung (vereinbarte Entgelte)")] Soll,

    /// <summary>Nach vereinnahmten Entgelten (§ 20 UStG): mit dem Zahlungseingang.</summary>
    [Display(Name = "Istversteuerung (vereinnahmte Entgelte, § 20 UStG)")] Ist,
}

/// <summary>Zeitraum der Umsatzsteuer-Voranmeldung (§ 18 Abs. 2 UStG).</summary>
public enum Voranmeldungszeitraum
{
    /// <summary>Voranmeldung für jeden Kalendermonat.</summary>
    [Display(Name = "monatlich")] Monat,
    /// <summary>Voranmeldung für jedes Kalendervierteljahr.</summary>
    [Display(Name = "vierteljährlich")] Vierteljahr,
    /// <summary>Befreit von der Voranmeldung; nur die Jahreserklärung wird abgegeben.</summary>
    [Display(Name = "keine Voranmeldung, nur Jahreserklärung")] Keine,
}

/// <summary>Formatierung von Postanschriften.</summary>
public static class Anschrift
{
    /// <summary>„Straße, PLZ Ort“; null, solange Straße oder Ort fehlen.</summary>
    /// <param name="strasse">Straße und Hausnummer.</param>
    /// <param name="plz">Postleitzahl, darf fehlen.</param>
    /// <param name="ort">Ort.</param>
    /// <returns>Die Anschrift als eine Zeile oder null.</returns>
    public static string? Zeile(string? strasse, string? plz, string? ort) =>
        string.IsNullOrWhiteSpace(strasse) || string.IsNullOrWhiteSpace(ort) ? null : $"{strasse.Trim()}, {$"{plz} {ort}".Trim()}";
}
