using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Kassen;

/// <summary>
/// Prüfung einer Kasse durch die zwei gewählten Rechnungsprüfer (V05, Satzung § 16 Abs. 5) für einen Zeitraum, meist das Haushaltsjahr.
/// Beide Prüfer bestätigen unter ihrer eigenen Anmeldung; erst dann ist die Prüfung abgeschlossen und der Prüfbericht endgültig.
/// </summary>
public class Kassenpruefung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Erster Tag des geprüften Zeitraums.</summary>
    public DateOnly Von { get; set; }

    /// <summary>Letzter Tag des geprüften Zeitraums.</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Tag der Prüfung vor Ort.</summary>
    public DateOnly? GeprueftAm { get; set; }

    /// <summary>Erster Prüfer (legt die Prüfung an).</summary>
    public Guid Pruefer1Id { get; set; }

    /// <summary>Der erste Prüfer.</summary>
    public Mitglied? Pruefer1 { get; set; }

    /// <summary>Zeitpunkt, zu dem der erste Prüfer bestätigt hat, in UTC.</summary>
    public DateTimeOffset? Pruefer1BestaetigtAm { get; set; }

    /// <summary>Zweiter Prüfer.</summary>
    public Guid Pruefer2Id { get; set; }

    /// <summary>Der zweite Prüfer.</summary>
    public Mitglied? Pruefer2 { get; set; }

    /// <summary>Zeitpunkt, zu dem der zweite Prüfer bestätigt hat, in UTC.</summary>
    public DateTimeOffset? Pruefer2BestaetigtAm { get; set; }

    /// <summary>Ergebnis in Worten, z. B. „Kasse ordnungsgemäß geführt, Belege vollständig“.</summary>
    public string? Ergebnis { get; set; }

    /// <summary>Die Prüfer empfehlen der Hauptversammlung die Entlastung.</summary>
    public bool EntlastungEmpfohlen { get; set; }

    /// <summary>Beanstandungen mit den Antworten des Kassenwarts.</summary>
    public List<Beanstandung> Beanstandungen { get; set; } = [];

    /// <summary>Beide Prüfer haben bestätigt; die Prüfung ist nicht mehr änderbar.</summary>
    public bool Abgeschlossen => Pruefer1BestaetigtAm is not null && Pruefer2BestaetigtAm is not null;

    /// <summary>Ist das Mitglied einer der beiden Prüfer?</summary>
    /// <param name="mitgliedId">Das Mitglied des angemeldeten Benutzers.</param>
    /// <returns>true, wenn es Prüfer dieser Prüfung ist.</returns>
    public bool IstPruefer(Guid? mitgliedId) => mitgliedId is { } id && (id == Pruefer1Id || id == Pruefer2Id);
}

/// <summary>Beanstandung der Rechnungsprüfer, auf die der Kassenwart antwortet.</summary>
public class Beanstandung : Entitaet
{
    /// <summary>Schlüssel der Prüfung.</summary>
    public Guid KassenpruefungId { get; set; }

    /// <summary>Betroffene Buchung, falls die Beanstandung einen Beleg betrifft.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Die Buchung.</summary>
    public Buchung? Buchung { get; set; }

    /// <summary>Was beanstandet wird.</summary>
    public required string Text { get; set; }

    /// <summary>Antwort des Kassenwarts.</summary>
    public string? Antwort { get; set; }

    /// <summary>Zeitpunkt der Antwort in UTC.</summary>
    public DateTimeOffset? BeantwortetAm { get; set; }

    /// <summary>Die Prüfer sehen die Beanstandung als ausgeräumt an.</summary>
    public bool Erledigt { get; set; }
}

/// <summary>Regeln der Kassenprüfung.</summary>
public static class Pruefregeln
{
    /// <summary>Gründe, warum eine Prüfung so nicht angelegt werden kann.</summary>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <param name="pruefer1">Erster Prüfer.</param>
    /// <param name="pruefer2">Zweiter Prüfer.</param>
    /// <param name="amtierend">Mitglieder mit laufender Prüferfunktion.</param>
    /// <returns>Die Fehler; leer, wenn alles passt.</returns>
    public static IReadOnlyList<string> Fehler(DateOnly von, DateOnly bis, Guid pruefer1, Guid pruefer2, IReadOnlySet<Guid> amtierend)
    {
        var fehler = new List<string>();
        if (bis < von)
        {
            fehler.Add("Der Zeitraum endet vor seinem Beginn.");
        }

        if (pruefer1 == pruefer2)
        {
            fehler.Add("Die Kasse prüfen zwei verschiedene Rechnungsprüfer.");
        }

        if (!amtierend.Contains(pruefer1) || !amtierend.Contains(pruefer2))
        {
            fehler.Add("Beide Prüfer müssen gerade als Kassenprüfer im Amt sein.");
        }

        return fehler;
    }
}
