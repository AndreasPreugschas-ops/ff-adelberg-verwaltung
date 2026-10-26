using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Entschaedigung;

/// <summary>Was eine Arbeitgeberbescheinigung belegt.</summary>
public enum BescheinigungArt
{
    /// <summary>Teilnahme an Einsätzen, Lehrgängen und Diensten in einem Zeitraum (§ 15 Abs. 1 FwG).</summary>
    [Display(Name = "Bescheinigung über Feuerwehrdienst")] Teilnahme,
    /// <summary>Vorab-Mitteilung über einen bevorstehenden Lehrgang (§ 15 Abs. 1 Satz 3 FwG).</summary>
    [Display(Name = "Mitteilung über einen Lehrgang")] Lehrgangsmitteilung,
}

/// <summary>Bearbeitungsstand einer Erstattung an den Arbeitgeber.</summary>
public enum ErstattungStand
{
    /// <summary>Antrag des Arbeitgebers eingegangen.</summary>
    [Display(Name = "eingegangen")] Eingegangen,
    /// <summary>Von der Gemeinde erstattet.</summary>
    [Display(Name = "erstattet")] Erstattet,
    /// <summary>Abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
}

/// <summary>
/// Ausgestellte Bescheinigung für den Arbeitgeber (V33, E8). Sie trägt einen Prüfcode, mit dem der Arbeitgeber die Angaben auf der
/// Prüfseite vergleichen kann, und auf Papier die Unterschrift des Kommandanten. Gespeichert werden nur die bescheinigten Zeiten,
/// keine Einsatzorte oder Stichworte.
/// </summary>
public class Arbeitgeberbescheinigung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Teilnahme oder Lehrgangsmitteilung.</summary>
    public BescheinigungArt Art { get; set; }

    /// <summary>Erster Tag des bescheinigten Zeitraums.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Letzter Tag des bescheinigten Zeitraums.</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Prüfcode in Gruppen, z. B. „K7QM-4XHT-PZ9C“; eindeutig.</summary>
    public required string Pruefcode { get; set; }

    /// <summary>Arbeitgeber zum Zeitpunkt der Ausstellung.</summary>
    public required string Arbeitgeber { get; set; }

    /// <summary>Bescheinigte Zeilen, je Zeile „Datum|Art|Beginn|Ende|Stunden|Ruhezeit bis“ (Ortszeit), damit die Prüfseite sie zeigen kann.</summary>
    public required string Zeilen { get; set; }

    /// <summary>Summe der bescheinigten Stunden ohne Ruhezeit.</summary>
    public decimal Stunden { get; set; }

    /// <summary>Angemessene Ruhezeit nach Einsätzen in Stunden, wenn der Kommandant sie bescheinigt; sonst null.</summary>
    public int? RuhezeitStunden { get; set; }

    /// <summary>Zeitpunkt der Ausstellung (UTC).</summary>
    public DateTimeOffset AusgestelltAm { get; set; }
    /// <summary>Konto, das ausgestellt hat.</summary>
    public Guid? AusgestelltVon { get; set; }

    /// <summary>Für ungültig erklärt, z. B. weil Zeiten falsch waren; die Prüfseite zeigt das an.</summary>
    public bool Widerrufen { get; set; }

    /// <summary>Zeichen des Prüfcodes ohne leicht verwechselbare (0/O, 1/I/L).</summary>
    public const string Zeichen = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>Erzeugt einen zufälligen Prüfcode aus drei Vierergruppen (31^12 Möglichkeiten).</summary>
    /// <returns>Der Prüfcode.</returns>
    public static string NeuerPruefcode()
    {
        Span<char> code = stackalloc char[14];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = i is 4 or 9 ? '-' : Zeichen[RandomNumberGenerator.GetInt32(Zeichen.Length)];
        }

        return new string(code);
    }

    /// <summary>Bringt eine Eingabe in die Form des Prüfcodes: Großbuchstaben, ohne Leerzeichen, mit Bindestrichen.</summary>
    /// <param name="eingabe">Eingabe des Arbeitgebers.</param>
    /// <returns>Der Prüfcode oder null, wenn die Eingabe keiner sein kann.</returns>
    public static string? PruefcodeNormalisieren(string? eingabe)
    {
        var roh = new string((eingabe ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return roh.Length == 12 && roh.All(c => Zeichen.Contains(c)) ? $"{roh[..4]}-{roh[4..8]}-{roh[8..]}" : null;
    }
}

/// <summary>Eine bescheinigte Zeile.</summary>
/// <param name="Datum">Tag (Ortszeit).</param>
/// <param name="Anlass">„Einsatz“, „Lehrgang“ oder „Dienst“; keine Einzelheiten.</param>
/// <param name="Beginn">Beginn (UTC).</param>
/// <param name="Ende">Ende (UTC).</param>
/// <param name="RuheBis">Ende der Ruhezeit (UTC) oder null.</param>
public sealed record Bescheinigungszeile(DateOnly Datum, string Anlass, DateTimeOffset Beginn, DateTimeOffset Ende, DateTimeOffset? RuheBis)
{
    /// <summary>Dauer in Stunden, auf zwei Stellen gerundet.</summary>
    public decimal Stunden => Math.Round((decimal)(Ende - Beginn).TotalHours, 2);
}

/// <summary>
/// Antrag eines privaten Arbeitgebers auf Erstattung des fortgezahlten Entgelts (§ 15 Abs. 2 FwG). Solange er eingegangen oder erstattet ist,
/// zahlt die Abrechnung dem Mitglied für diese Tage keinen Verdienstausfall; so wird nichts doppelt gezahlt.
/// </summary>
public class Arbeitgebererstattung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Arbeitgeber, der die Erstattung beantragt.</summary>
    public required string Arbeitgeber { get; set; }

    /// <summary>Erster Tag des Zeitraums.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Letzter Tag des Zeitraums.</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Fortgezahltes Arbeitsentgelt in Euro.</summary>
    [NichtProtokollieren]
    public decimal Entgelt { get; set; }

    /// <summary>Arbeitgeberanteile zur Sozialversicherung und sonstige Beiträge in Euro.</summary>
    [NichtProtokollieren]
    public decimal Sozialversicherung { get; set; }

    /// <summary>Prüfcode der zugrunde liegenden Bescheinigung, falls angegeben.</summary>
    public string? Pruefcode { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public ErstattungStand Stand { get; set; }
    /// <summary>Eingang des Antrags.</summary>
    public DateOnly EingegangenAm { get; set; }
    /// <summary>Tag der Erstattung oder Ablehnung.</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Vermerk, z. B. Kassenzeichen oder Ablehnungsgrund.</summary>
    public string? Vermerk { get; set; }

    /// <summary>Summe der Erstattung.</summary>
    public decimal Summe => Entgelt + Sozialversicherung;

    /// <summary>Die Erstattung schließt Verdienstausfall des Mitglieds für diese Tage aus.</summary>
    public bool SperrtVerdienstausfall => Stand != ErstattungStand.Abgelehnt;
}
