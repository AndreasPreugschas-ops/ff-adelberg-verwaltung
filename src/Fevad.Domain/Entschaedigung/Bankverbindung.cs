using System.Numerics;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Entschaedigung;

/// <summary>
/// Bankverbindung eines Mitglieds für Auszahlungen (Entschädigung, Auslagenerstattung). Freiwillig und nur bei Bedarf erfasst,
/// nie im Aufnahmeantrag. Sichtbar nur mit dem Recht für Bankdaten, sonst maskiert; die IBAN steht nicht im Änderungsprotokoll.
/// Gelöscht nach dem Austritt (Löschkonzept L37).
/// </summary>
public class Bankverbindung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Nur wenn abweichend vom Mitglied, z. B. Konto der Eltern.</summary>
    public string? Kontoinhaber { get; set; }

    /// <summary>Ohne Leerzeichen, Großbuchstaben, Prüfziffer geprüft.</summary>
    [NichtProtokollieren]
    public required string Iban { get; set; }

    /// <summary>Tag der Erfassung.</summary>
    public DateOnly ErfasstAm { get; set; }
}

/// <summary>Prüfung und Darstellung von IBANs.</summary>
public static class Iban
{
    /// <summary>Entfernt Leerzeichen und schreibt groß; prüft Länderkennung, Länge (DE: 22) und Prüfziffer (ISO 13616, Modulo 97).</summary>
    /// <param name="eingabe">Eingegebene IBAN.</param>
    /// <returns>Die bereinigte IBAN oder null, wenn sie ungültig ist.</returns>
    public static string? Normalisieren(string? eingabe)
    {
        var iban = new string((eingabe ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (iban.Length is < 15 or > 34 || !char.IsAsciiLetter(iban[0]) || !char.IsAsciiLetter(iban[1]) || !iban.All(char.IsAsciiLetterOrDigit)
            || (iban.StartsWith("DE") && iban.Length != 22))
        {
            return null;
        }

        var umgestellt = iban[4..] + iban[..4];
        var zahl = string.Concat(umgestellt.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));
        return BigInteger.Parse(zahl) % 97 == 1 ? iban : null;
    }

    /// <summary>„DE89 3704 0044 0532 0130 00“.</summary>
    /// <param name="iban">Bereinigte IBAN.</param>
    /// <returns>Die IBAN in Vierergruppen.</returns>
    public static string Formatieren(string iban) => string.Join(' ', iban.Chunk(4).Select(c => new string(c)));

    /// <summary>Für alle ohne Recht auf Bankdaten: Land und die letzten vier Stellen, z. B. „DE•• •••• •••• •••• ••30 00“.</summary>
    /// <param name="iban">Bereinigte IBAN.</param>
    /// <returns>Die maskierte IBAN in Vierergruppen.</returns>
    public static string Maskieren(string iban) =>
        Formatieren(iban[..2] + new string('•', iban.Length - 6) + iban[^4..]);
}
