using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kassen;

/// <summary>Was zugewendet wurde.</summary>
public enum ZuwendungArt
{
    /// <summary>Geld, bar oder überwiesen.</summary>
    [Display(Name = "Geld")] Geld,

    /// <summary>Sache, z. B. Getränke für das Grillfest oder ein Gerät; der Betrag ist dann der geschätzte Wert.</summary>
    [Display(Name = "Sache")] Sache,
}

/// <summary>
/// Eintrag im Spenden- und Sponsoringregister einer Kasse (V08). Die Kassen sind Sondervermögen der Gemeinde: Über die Annahme von
/// Spenden und Sponsoring entscheidet der Gemeinderat (§ 78 Abs. 4 GemO), die Gemeinde berichtet jährlich der Rechtsaufsicht.
/// Eine Zuwendungsbestätigung stellt die Kasse nicht aus. Sponsoring ist eine Leistung mit Gegenleistung und gehört in den
/// wirtschaftlichen Bereich. Geberdaten löscht der Löschlauf nach der Aufbewahrungsfrist (L43).
/// </summary>
public class Zuwendung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Tag des Eingangs.</summary>
    public DateOnly Eingang { get; set; }

    /// <summary>Name des Gebers, Person oder Firma.</summary>
    public required string Geber { get; set; }

    /// <summary>Anschrift des Gebers für das Dankschreiben.</summary>
    public string? Anschrift { get; set; }

    /// <summary>Geld oder Sache.</summary>
    public ZuwendungArt Art { get; set; }

    /// <summary>Betrag in Euro, bei Sachen der geschätzte Wert.</summary>
    public decimal Betrag { get; set; }

    /// <summary>Beschreibung der Sache; bei Geld leer.</summary>
    public string? Sache { get; set; }

    /// <summary>Zweck, z. B. „Jugendzeltlager“ oder „Ausstattung Aufenthaltsraum“.</summary>
    public string? Zweck { get; set; }

    /// <summary>Der Geber hat die Zuwendung an den Zweck gebunden; sie darf nur dafür ausgegeben werden.</summary>
    public bool Zweckgebunden { get; set; }

    /// <summary>Sponsoring mit Gegenleistung (z. B. Werbung auf dem Festplakat): wirtschaftlicher Bereich, keine Spende.</summary>
    public bool Sponsoring { get; set; }

    /// <summary>Buchung im Kassenbuch, bei Geld.</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Die Buchung.</summary>
    public Buchung? Buchung { get; set; }

    /// <summary>Tag, an dem der Gemeinderat (oder nach der Hauptsatzung der Bürgermeister) die Annahme beschlossen hat.</summary>
    public DateOnly? AnnahmeAm { get; set; }

    /// <summary>Verweis auf den Beschluss, z. B. „GR 14.03.2027, TOP 5“.</summary>
    public string? AnnahmeVerweis { get; set; }

    /// <summary>Tag, an dem das Dankschreiben verschickt wurde.</summary>
    public DateOnly? DankschreibenAm { get; set; }

    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Braucht die Zuwendung noch den Annahmebeschluss?</summary>
    /// <param name="abEuro">Wertgrenze aus dem Parameter; 0 heißt jede Zuwendung.</param>
    /// <returns>true, wenn der Beschluss fehlt und die Grenze erreicht ist.</returns>
    public bool AnnahmeFehlt(decimal abEuro) => AnnahmeAm is null && Betrag >= abEuro;

    /// <summary>
    /// Sponsoring gehört in den wirtschaftlichen Bereich. true, wenn die verknüpfte Buchung anders zugeordnet ist; ohne Buchung
    /// oder ohne Sponsoring false.
    /// </summary>
    public bool SteuerbereichFalsch => Sponsoring && Buchung is { } b && b.Steuerbereich != Steuerbereich.Wirtschaftlich;
}
