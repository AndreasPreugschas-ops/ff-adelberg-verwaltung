using System.Globalization;
using System.Xml.Linq;
using Fevad.Domain.Entschaedigung;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// SEPA-Sammelüberweisung (pain.001.001.09) zum Hochladen ins Onlinebanking, für Entschädigungen und Auslagenerstattungen. Keine
/// Lastschrift. Die Ende-zu-Ende-Kennung jeder Zahlung trägt die Personalnummer, damit die Kämmerei (SAP) und der Bankimport die
/// Zahlung zuordnen können.
/// </summary>
public static class SepaUeberweisung
{
    /// <summary>Eine Überweisung.</summary>
    /// <param name="Name">Name des Empfängers (Kontoinhaber).</param>
    /// <param name="Iban">Normalisierte IBAN des Empfängers.</param>
    /// <param name="Betrag">Betrag in Euro, größer als 0.</param>
    /// <param name="Zweck">Verwendungszweck; wird auf den SEPA-Zeichensatz und 140 Zeichen gekürzt.</param>
    /// <param name="Referenz">Ende-zu-Ende-Kennung; wird auf den SEPA-Zeichensatz und 35 Zeichen gekürzt.</param>
    public sealed record Posten(string Name, string Iban, decimal Betrag, string Zweck, string Referenz);

    /// <summary>
    /// Ende-zu-Ende-Kennung „PN{Personalnummer}-{Schlüssel}“ aus der Personalnummer und den zufälligen letzten 16 Stellen eines
    /// UUID-v7-Schlüssels; höchstens 35 Zeichen.
    /// </summary>
    /// <param name="personalnummer">Personalnummer des Empfängers.</param>
    /// <param name="id">Schlüssel des Vorgangs.</param>
    /// <returns>Die Kennung.</returns>
    public static string Referenz(string personalnummer, Guid id) => $"{Text($"PN{personalnummer}", 18).Replace(' ', '-')}-{id.ToString("N")[16..]}";

    /// <summary>
    /// Erzeugt die Datei für alle Posten mit Betrag über 0. Wirft <see cref="InvalidOperationException"/>, wenn kein Posten übrig
    /// bleibt, der Auftraggeber fehlt oder seine IBAN ungültig ist.
    /// </summary>
    /// <param name="posten">Die Überweisungen.</param>
    /// <param name="auftraggeber">Name des Auftraggebers (Kontoinhaber des belasteten Kontos).</param>
    /// <param name="auftraggeberIban">IBAN des belasteten Kontos.</param>
    /// <param name="ausfuehrung">Gewünschter Ausführungstag.</param>
    /// <param name="jetzt">Zeitpunkt der Erstellung; ergibt die Nachrichtenkennung.</param>
    /// <returns>Die XML-Datei als Bytes (UTF-8).</returns>
    public static byte[] Erzeugen(IEnumerable<Posten> posten, string auftraggeber, string auftraggeberIban, DateOnly ausfuehrung, DateTimeOffset jetzt)
    {
        var iban = Iban.Normalisieren(auftraggeberIban) ?? throw new InvalidOperationException("Die IBAN des Auftraggebers ist ungültig.");
        if (string.IsNullOrWhiteSpace(auftraggeber))
        {
            throw new InvalidOperationException("Den Namen des Auftraggebers angeben.");
        }

        var liste = posten.Where(p => p.Betrag > 0).ToList();
        if (liste.Count == 0)
        {
            throw new InvalidOperationException("Es gibt nichts zu überweisen.");
        }

        XNamespace ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.09";
        var kennung = $"FEVAD{jetzt:yyyyMMddHHmmss}";
        string Betrag(decimal b) => b.ToString("0.00", CultureInfo.InvariantCulture);
        var summe = liste.Sum(p => p.Betrag);
        var dokument = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "CstmrCdtTrfInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", kennung),
                        new XElement(ns + "CreDtTm", jetzt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                        new XElement(ns + "NbOfTxs", liste.Count),
                        new XElement(ns + "CtrlSum", Betrag(summe)),
                        new XElement(ns + "InitgPty", new XElement(ns + "Nm", Text(auftraggeber, 70)))),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", kennung + "-1"),
                        new XElement(ns + "PmtMtd", "TRF"),
                        new XElement(ns + "NbOfTxs", liste.Count),
                        new XElement(ns + "CtrlSum", Betrag(summe)),
                        new XElement(ns + "PmtTpInf", new XElement(ns + "SvcLvl", new XElement(ns + "Cd", "SEPA"))),
                        new XElement(ns + "ReqdExctnDt", new XElement(ns + "Dt", ausfuehrung.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                        new XElement(ns + "Dbtr", new XElement(ns + "Nm", Text(auftraggeber, 70))),
                        new XElement(ns + "DbtrAcct", new XElement(ns + "Id", new XElement(ns + "IBAN", iban))),
                        new XElement(ns + "DbtrAgt", new XElement(ns + "FinInstnId", new XElement(ns + "Othr", new XElement(ns + "Id", "NOTPROVIDED")))),
                        new XElement(ns + "ChrgBr", "SLEV"),
                        liste.Select(p => new XElement(ns + "CdtTrfTxInf",
                            new XElement(ns + "PmtId", new XElement(ns + "EndToEndId", Text(p.Referenz, 35))),
                            new XElement(ns + "Amt", new XElement(ns + "InstdAmt", new XAttribute("Ccy", "EUR"), Betrag(p.Betrag))),
                            new XElement(ns + "Cdtr", new XElement(ns + "Nm", Text(p.Name, 70))),
                            new XElement(ns + "CdtrAcct", new XElement(ns + "Id", new XElement(ns + "IBAN", p.Iban))),
                            new XElement(ns + "RmtInf", new XElement(ns + "Ustrd", Text(p.Zweck, 140)))))))));
        using var strom = new MemoryStream();
        dokument.Save(strom);
        return strom.ToArray();
    }

    /// <summary>SEPA erlaubt nur einen eingeschränkten Zeichensatz; Umlaute werden umschrieben, der Rest durch Leerzeichen ersetzt.</summary>
    /// <param name="text">Der Text.</param>
    /// <param name="laenge">Größte Länge.</param>
    /// <returns>Der bereinigte, gekürzte Text.</returns>
    public static string Text(string text, int laenge)
    {
        var umschrieben = text.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");
        var erlaubt = new string(umschrieben.Select(c => char.IsAsciiLetterOrDigit(c) || "/-?:().,'+ ".Contains(c) ? c : ' ').ToArray()).Trim();
        return erlaubt.Length > laenge ? erlaubt[..laenge] : erlaubt;
    }
}
