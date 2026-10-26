using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Objekt mit Brandmeldeanlage, die bei der Leitstelle aufgeschaltet ist (V24). Der Betreiber ist bei einem Fehlalarm Kostenschuldner nach
/// § 34 Abs. 1 Nr. 6 FwG; FEVAD zählt die Fehlalarme je Jahr und legt daraus die Kostenersatz-Vorgänge in Serie an.
/// </summary>
public class BmaObjekt : Entitaet
{
    /// <summary>Bezeichnung des Objekts, z. B. „Seniorenzentrum Adelberg“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Anschrift des Objekts.</summary>
    public string? Anschrift { get; set; }

    /// <summary>Nummer der Aufschaltung bei der Leitstelle bzw. beim Konzessionär.</summary>
    public string? Aufschaltnummer { get; set; }

    /// <summary>Konzessionär der Übertragungseinrichtung.</summary>
    public string? Konzessionaer { get; set; }

    /// <summary>Name des Betreibers (Person oder Firma), Kostenschuldner bei einem Fehlalarm.</summary>
    public required string BetreiberName { get; set; }

    /// <summary>Zustellanschrift des Betreibers.</summary>
    public string? BetreiberAnschrift { get; set; }

    /// <summary>Ansprechpartner beim Betreiber mit Telefon, nur für Rückfragen und den Bescheid.</summary>
    public string? Ansprechpartner { get; set; }

    /// <summary>Die Anlage ist noch aufgeschaltet; abgeschaltete Objekte bleiben für alte Einsätze erhalten.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Bemerkung, z. B. Laufkarten oder Schlüsseldepot.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>
    /// Legt für einen Fehlalarm dieses Objekts den Kostenersatz-Vorgang an: Tatbestand § 34 Abs. 1 Nr. 6 FwG, Betreiber als Schuldner,
    /// Sachverhalt mit Objekt und Aufschaltnummer. Empfehlung und Entscheidung bleiben wie bei jedem Vorgang Sache von Feuerwehr und Gemeinde.
    /// </summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="tag">Tag des Einsatzes in Ortszeit.</param>
    /// <param name="nummerImJahr">Der wievielte Fehlalarm des Objekts im Jahr es ist.</param>
    /// <returns>Der neue Vorgang mit einem Schuldner.</returns>
    public Kostenersatzvorgang SerienVorgang(Guid einsatzId, DateOnly tag, int nummerImJahr) => new()
    {
        EinsatzId = einsatzId,
        Tatbestand = Kostentatbestand.Abs1Nr6Brandmeldeanlage,
        Sachverhalt = $"Fehlalarm der Brandmeldeanlage {Bezeichnung}"
            + (string.IsNullOrWhiteSpace(Aufschaltnummer) ? "" : $" (Aufschaltnummer {Aufschaltnummer})")
            + $" am {tag:dd.MM.yyyy}, {nummerImJahr}. Fehlalarm des Objekts im Jahr {tag.Year}.",
        Schuldner =
        [
            new Kostenschuldner { Art = SchuldnerArt.Betreiber, Name = BetreiberName, Anschrift = BetreiberAnschrift, Zustellweg = Zustellweg.Brief },
        ],
    };
}
