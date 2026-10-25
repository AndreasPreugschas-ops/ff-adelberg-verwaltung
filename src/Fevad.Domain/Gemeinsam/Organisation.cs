using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Anschriften der Feuerwehr und ihrer Trägerin, der Gemeinde, dazu das Logo für die Formulare. Es gibt genau einen
/// Datensatz; der Start legt ihn an. Die Gemeinde ist Verantwortliche im Sinne der DSGVO.
/// </summary>
public class Organisation : Entitaet
{
    public string FeuerwehrName { get; set; } = "Freiwillige Feuerwehr";
    public string? FeuerwehrStrasse { get; set; }
    public string? FeuerwehrPlz { get; set; }
    public string? FeuerwehrOrt { get; set; }
    public string? FeuerwehrTelefon { get; set; }
    public string? FeuerwehrEmail { get; set; }

    public string GemeindeName { get; set; } = "Gemeinde";

    /// <summary>Vertretung der Gemeinde, z. B. „vertreten durch den Bürgermeister“.</summary>
    public string? GemeindeVertretung { get; set; }
    public string? GemeindeStrasse { get; set; }
    public string? GemeindePlz { get; set; }
    public string? GemeindeOrt { get; set; }
    public string? GemeindeTelefon { get; set; }
    public string? GemeindeEmail { get; set; }

    /// <summary>Logo als SVG, PNG oder JPEG; erscheint im Kopf der Formulare.</summary>
    [NichtProtokollieren]
    public byte[]? Logo { get; set; }
    public string? LogoInhaltstyp { get; set; }

    // Umsatzsteuer: Steuerpflichtig ist die Gemeinde als juristische Person des öffentlichen Rechts (§ 2b UStG). Die
    // Kameradschaftskassen sind Sondervermögen der Gemeinde (§ 18 FwG) und haben keine eigene Steuernummer; ihre Umsätze
    // meldet die Kämmerei unter der Steuernummer der Gemeinde. Grenzen und Steuersätze stehen als Parameter, nicht hier.

    /// <summary>Steuernummer der Gemeinde beim Finanzamt, z. B. „62/123/45678“.</summary>
    public string? Steuernummer { get; set; }

    /// <summary>Umsatzsteuer-Identifikationsnummer (§ 27a UStG), z. B. „DE123456789“; braucht die Gemeinde für Rechnungen ins EU-Ausland.</summary>
    public string? UmsatzsteuerId { get; set; }

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

    public Voranmeldungszeitraum? Voranmeldungszeitraum { get; set; }

    /// <summary>Hinweis der Kämmerei, z. B. wer in der Verwaltung die Meldungen der Kameradschaftskasse annimmt und bis wann.</summary>
    public string? UmsatzsteuerHinweis { get; set; }

    public string? FeuerwehrAnschrift => Anschrift.Zeile(FeuerwehrStrasse, FeuerwehrPlz, FeuerwehrOrt);
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
    [Display(Name = "monatlich")] Monat,
    [Display(Name = "vierteljährlich")] Vierteljahr,
    [Display(Name = "keine Voranmeldung, nur Jahreserklärung")] Keine,
}

public static class Anschrift
{
    /// <summary>„Straße, PLZ Ort“; null, solange Straße oder Ort fehlen.</summary>
    public static string? Zeile(string? strasse, string? plz, string? ort) =>
        string.IsNullOrWhiteSpace(strasse) || string.IsNullOrWhiteSpace(ort) ? null : $"{strasse.Trim()}, {$"{plz} {ort}".Trim()}";
}
