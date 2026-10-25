using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kassen;

/// <summary>Format einer eingelesenen Kontoauszugsdatei.</summary>
public enum Auszugsformat
{
    /// <summary>ISO 20022 camt.053 (Tagesauszug als XML, auch gepackt als ZIP).</summary>
    [Display(Name = "CAMT.053")] Camt053,

    /// <summary>CSV-Export des Onlinebankings nach einem Bankformat aus den Stammdaten.</summary>
    [Display(Name = "CSV")] Csv,
}

/// <summary>Stand eines eingelesenen Kontoumsatzes im Abgleich.</summary>
public enum UmsatzStatus
{
    /// <summary>Noch keiner Buchung zugeordnet.</summary>
    [Display(Name = "offen")] Offen,

    /// <summary>Einer Buchung im Kassenbuch zugeordnet.</summary>
    [Display(Name = "zugeordnet")] Zugeordnet,

    /// <summary>Bewusst nicht gebucht, z. B. eine Umbuchung, die schon über das andere Konto erfasst ist.</summary>
    [Display(Name = "ignoriert")] Ignoriert,
}

/// <summary>
/// CSV-Format eines Onlinebankings (V07, Andi 25.10.2026): welche Spalte Buchungstag, Betrag, Gegenpartei und Verwendungszweck enthält.
/// Spalten werden über ihre Überschrift gefunden; Kopfzeilen vor der Überschrift lassen sich überspringen. Gepflegt in den Stammdaten.
/// </summary>
public class Bankformat : Entitaet
{
    /// <summary>Name, z. B. „Kreissparkasse Göppingen CSV-CAMT“.</summary>
    public required string Name { get; set; }

    /// <summary>Zeilen vor der Überschriftszeile, die übersprungen werden.</summary>
    public int KopfzeilenUeberspringen { get; set; }

    /// <summary>Überschrift der Spalte mit dem Buchungstag.</summary>
    public required string SpalteBuchungstag { get; set; }

    /// <summary>Überschrift der Spalte mit dem Wertstellungstag; leer, wenn es keine gibt.</summary>
    public string? SpalteValuta { get; set; }

    /// <summary>Überschrift der Spalte mit dem Betrag (Ausgänge negativ); leer, wenn Soll und Haben getrennt stehen.</summary>
    public string? SpalteBetrag { get; set; }

    /// <summary>Überschrift der Spalte mit Ausgängen (Soll), wenn getrennt.</summary>
    public string? SpalteSoll { get; set; }

    /// <summary>Überschrift der Spalte mit Eingängen (Haben), wenn getrennt.</summary>
    public string? SpalteHaben { get; set; }

    /// <summary>Überschrift der Spalte mit Kennzeichen „S“/„H“, wenn der Betrag ohne Vorzeichen steht.</summary>
    public string? SpalteSollHaben { get; set; }

    /// <summary>Überschrift der Spalte mit dem Namen der Gegenpartei.</summary>
    public string? SpalteGegenpartei { get; set; }

    /// <summary>Überschrift der Spalte mit dem Verwendungszweck.</summary>
    public string? SpalteVerwendungszweck { get; set; }

    /// <summary>Überschrift der Spalte mit der Ende-zu-Ende-Referenz.</summary>
    public string? SpalteReferenz { get; set; }

    /// <summary>Datumsformat, z. B. „dd.MM.yyyy“ oder „dd.MM.yy“.</summary>
    public string Datumsformat { get; set; } = "dd.MM.yyyy";

    /// <summary>Beträge mit Dezimalkomma (deutsch) statt Dezimalpunkt.</summary>
    public bool Dezimalkomma { get; set; } = true;
}

/// <summary>Eine eingelesene Kontoauszugsdatei mit Zeitraum und Salden.</summary>
public class Kontoauszugimport : Entitaet
{
    /// <summary>Schlüssel des Kontos.</summary>
    public Guid KontoId { get; set; }

    /// <summary>Das Konto.</summary>
    public Zahlungsmittelkonto? Konto { get; set; }

    /// <summary>Dateiname.</summary>
    public required string Dateiname { get; set; }

    /// <summary>Format der Datei.</summary>
    public Auszugsformat Format { get; set; }

    /// <summary>Erster Buchungstag.</summary>
    public DateOnly? Von { get; set; }

    /// <summary>Letzter Buchungstag.</summary>
    public DateOnly? Bis { get; set; }

    /// <summary>Saldo am Ende des Zeitraums laut Bank (nur CAMT oder wenn die CSV ihn nennt).</summary>
    public decimal? Endsaldo { get; set; }

    /// <summary>Zahl der Umsätze in der Datei.</summary>
    public int Anzahl { get; set; }

    /// <summary>Davon neu übernommen; die übrigen waren schon eingelesen.</summary>
    public int Neu { get; set; }
}

/// <summary>
/// Umsatz aus einem Kontoauszug (V07). Gespeichert werden Tag, Betrag, Name der Gegenpartei, Verwendungszweck und Referenz, keine
/// IBAN der Gegenpartei. Eingänge sind positiv, Ausgänge negativ. Der Abgleich ordnet ihn einer Buchung zu oder bucht ihn neu.
/// </summary>
public class Kontoumsatz : Entitaet
{
    /// <summary>Schlüssel des Kontos.</summary>
    public Guid KontoId { get; set; }

    /// <summary>Das Konto.</summary>
    public Zahlungsmittelkonto? Konto { get; set; }

    /// <summary>Import, mit dem der Umsatz kam.</summary>
    public Guid ImportId { get; set; }

    /// <summary>Buchungstag laut Bank.</summary>
    public DateOnly Buchungstag { get; set; }

    /// <summary>Wertstellung laut Bank.</summary>
    public DateOnly? Valuta { get; set; }

    /// <summary>Betrag in Euro; Eingang positiv, Ausgang negativ.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Name der Gegenpartei.</summary>
    public string? Gegenpartei { get; set; }

    /// <summary>Verwendungszweck.</summary>
    public string? Verwendungszweck { get; set; }

    /// <summary>Ende-zu-Ende-Referenz, z. B. aus der Überweisungsdatei von FEVAD.</summary>
    public string? Referenz { get; set; }

    /// <summary>Fingerabdruck aus Konto, Tag, Betrag, Zweck und Referenz; verhindert doppeltes Einlesen.</summary>
    public required string Fingerabdruck { get; set; }

    /// <summary>Stand im Abgleich.</summary>
    public UmsatzStatus Status { get; set; }

    /// <summary>Zugeordnete Buchung.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Die Buchung.</summary>
    public Buchung? Buchung { get; set; }
}

/// <summary>Ein Umsatz, wie ihn ein Leser aus der Datei holt, noch ohne Konto.</summary>
/// <param name="Buchungstag">Buchungstag.</param>
/// <param name="Valuta">Wertstellung.</param>
/// <param name="Betrag">Eingang positiv, Ausgang negativ.</param>
/// <param name="Gegenpartei">Name der Gegenpartei.</param>
/// <param name="Verwendungszweck">Verwendungszweck.</param>
/// <param name="Referenz">Ende-zu-Ende-Referenz; „NOTPROVIDED“ gilt als leer.</param>
public sealed record Auszugszeile(DateOnly Buchungstag, DateOnly? Valuta, decimal Betrag, string? Gegenpartei, string? Verwendungszweck, string? Referenz);

/// <summary>Was ein Leser aus einer Datei geholt hat.</summary>
/// <param name="Zeilen">Die Umsätze.</param>
/// <param name="Endsaldo">Schlusssaldo laut Bank, falls die Datei ihn enthält.</param>
/// <param name="Iban">IBAN des Kontos laut Datei, falls enthalten.</param>
public sealed record Auszug(IReadOnlyList<Auszugszeile> Zeilen, decimal? Endsaldo, string? Iban);

/// <summary>Art eines Vorschlags im Abgleich.</summary>
public enum VorschlagArt
{
    /// <summary>Eine vorhandene Buchung mit gleichem Betrag passt.</summary>
    Buchung,

    /// <summary>Eine vorbereitete Ausgabe passt (Referenz aus der Überweisungsdatei oder gleicher Betrag).</summary>
    Anordnung,

    /// <summary>Eine fällige wiederkehrende Buchung passt.</summary>
    Wiederkehrend,
}

/// <summary>Vorschlag, wozu ein Umsatz gehört.</summary>
/// <param name="Art">Art des Vorschlags.</param>
/// <param name="Id">Kennung der Buchung, Anordnung oder wiederkehrenden Buchung.</param>
/// <param name="Text">Beschreibung für die Anzeige.</param>
public sealed record Abgleichvorschlag(VorschlagArt Art, Guid Id, string Text);

/// <summary>Regeln des Bankabgleichs.</summary>
public static class Bankabgleich
{
    /// <summary>So viele Tage dürfen Buchungstag der Bank und Datum im Kassenbuch auseinanderliegen.</summary>
    public const int ToleranzTage = 7;

    /// <summary>Fingerabdruck eines Umsatzes; gleiche Umsätze am selben Tag unterscheidet die laufende Nummer.</summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="z">Der Umsatz.</param>
    /// <param name="nummer">Laufende Nummer gleicher Umsätze in der Datei, ab 1.</param>
    /// <returns>SHA-256 als Hex-Text.</returns>
    public static string Fingerabdruck(Guid kontoId, Auszugszeile z, int nummer)
    {
        var text = string.Join('|', kontoId.ToString("N"), z.Buchungstag.ToString("yyyy-MM-dd"), z.Betrag.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Normal(z.Verwendungszweck), Normal(z.Referenz), nummer);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Leerzeichen vereinheitlichen, damit Zeilenumbrüche der Bank den Fingerabdruck nicht ändern.</summary>
    private static string Normal(string? text) => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Ende-zu-Ende-Referenz ohne Platzhalter der Bank.</summary>
    /// <param name="referenz">Referenz aus der Datei.</param>
    /// <returns>Die Referenz oder null.</returns>
    public static string? Referenz(string? referenz) =>
        string.IsNullOrWhiteSpace(referenz) || referenz.Trim().Equals("NOTPROVIDED", StringComparison.OrdinalIgnoreCase) ? null : referenz.Trim();

    /// <summary>Die letzten 16 Hex-Stellen eines Schlüssels aus einer FEVAD-Referenz „PN…-{16 Hex}“; null bei anderen Referenzen.</summary>
    /// <param name="referenz">Referenz der Bank.</param>
    /// <returns>Die 16 Stellen in Kleinbuchstaben oder null.</returns>
    public static string? SchluesselAusReferenz(string? referenz)
    {
        if (referenz is null || !referenz.StartsWith("PN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var teil = referenz[(referenz.LastIndexOf('-') + 1)..].Trim().ToLowerInvariant();
        return teil.Length == 16 && teil.All(Uri.IsHexDigit) ? teil : null;
    }

    /// <summary>
    /// Beste vorhandene Buchung zu einem Umsatz: gleiche Wirkung auf das Konto, noch keinem Umsatz zugeordnet, Datum innerhalb der
    /// Toleranz, die nächstgelegene zuerst.
    /// </summary>
    /// <param name="u">Der Umsatz.</param>
    /// <param name="buchungen">Buchungen der Kasse.</param>
    /// <param name="vergeben">Buchungen, die schon einem Umsatz zugeordnet sind.</param>
    /// <returns>Die Buchung oder null.</returns>
    public static Buchung? PassendeBuchung(Kontoumsatz u, IEnumerable<Buchung> buchungen, IReadOnlySet<Guid> vergeben) =>
        buchungen
            .Where(b => !vergeben.Contains(b.Id) && Kassenregeln.Wirkung(b, u.KontoId) == u.Betrag
                && Math.Abs(b.Datum.DayNumber - u.Buchungstag.DayNumber) <= ToleranzTage)
            .OrderBy(b => Math.Abs(b.Datum.DayNumber - u.Buchungstag.DayNumber))
            .FirstOrDefault();
}
